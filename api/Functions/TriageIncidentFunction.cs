using System.Text.Json;
using AITriage.Models;
using AITriage.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AITriage.Functions;

public class TriageIncidentFunction(
    ITopDeskService topDesk,
    IAITriageService aiTriage,
    ITriageStateService triagedState,
    IBranchAssignmentService branchAssignments,
    ICategoryMapperService categoryMapper,
    IPriorityMapperService priorityMapper,
    IConfiguration config,
    ILogger<TriageIncidentFunction> logger)
{
    [Function("TriageIncident")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "incidents/triage")] HttpRequestData req)
    {
        logger.LogInformation("POST /api/incidents/triage");

        bool.TryParse(config["AI_TRIAGE_ENABLED"], out var postNotes);

        var body = await req.ReadAsStringAsync();
        TriageRequest? triageReq = null;
        if (!string.IsNullOrEmpty(body))
            triageReq = JsonSerializer.Deserialize<TriageRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        List<TriageResult> results;

        if (triageReq?.IncidentId is { Length: > 0 } id)
        {
            var incident = await topDesk.GetIncidentAsync(id);
            if (incident is null)
            {
                var notFound = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"Incident {id} not found");
                return notFound;
            }

            // Check if already triaged
            var alreadyProcessed = await triagedState.IsProcessedAsync(incident.Id);
            if (alreadyProcessed)
            {
                var skipResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                skipResponse.Headers.Add("Content-Type", "application/json");
                await skipResponse.WriteStringAsync(JsonSerializer.Serialize(
                    new { message = $"Incident {incident.Number} already triaged", skipped = true },
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                return skipResponse;
            }

            // Apply branch-based assignments if not set
            if (incident.Category == null && incident.CallerBranch?.Id != null)
            {
                var assignment = await branchAssignments.GetAssignmentAsync(incident.CallerBranch.Id);
                if (assignment != null)
                {
                    await topDesk.UpdateIncidentAssignmentsAsync(
                        incident.Id,
                        assignment.CategoryId,
                        assignment.SubcategoryId,
                        assignment.PriorityId,
                        assignment.OperatorGroupId);
                    logger.LogInformation("Assigned {Number} via branch", incident.Number);
                }
            }
            var result = await aiTriage.TriageIncidentAsync(incident);

            // Auto-assign category/subcategory based on description keywords
            var categoryMapping = categoryMapper.MapDescription(incident.BriefDescription);
            if (categoryMapping != null && incident.Category == null)
            {
                await topDesk.UpdateIncidentAssignmentsAsync(
                    incident.Id,
                    categoryMapping.CategoryId,
                    categoryMapping.SubcategoryId);
                logger.LogInformation("Auto-assigned category for {Number}", incident.Number);
            }

            // Assign priority based on AI recommendation
            var priorityMapping = priorityMapper.MapAIPriority(result.RecommendedPriority);
            if (priorityMapping != null)
            {
                await topDesk.UpdateIncidentAssignmentsAsync(
                    incident.Id,
                    null,
                    null,
                    priorityMapping.TopDeskPriorityId,
                    "bdfc2c34-85e2-412a-9cf1-4869efebd7c4");  // Default operator group: 24/7 Team - Monitoring
                logger.LogInformation("Assigned priority {Priority} + default operator for {Number}", result.RecommendedPriority, incident.Number);
            }

            if (postNotes)
                await topDesk.PostInternalNoteAsync(incident.Id, FormatNote(result));

            // Mark as processed
            await triagedState.MarkProcessedAsync(incident.Id);

            results = [result];
        }
        else
        {
            var incidents = await topDesk.GetOpenIncidentsAsync(10);

            // Filter out already-triaged incidents
            var unprocessedIncidents = new List<TopDeskIncident>();
            foreach (var inc in incidents)
            {
                if (!await triagedState.IsProcessedAsync(inc.Id))
                    unprocessedIncidents.Add(inc);
            }

            if (unprocessedIncidents.Count == 0)
            {
                var skipResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                skipResponse.Headers.Add("Content-Type", "application/json");
                await skipResponse.WriteStringAsync(JsonSerializer.Serialize(
                    new { message = "No new incidents to triage", skipped = true },
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                return skipResponse;
            }

            // Apply branch assignments to any without category
            foreach (var inc in unprocessedIncidents.Where(i => i.Category == null && i.CallerBranch?.Id != null))
            {
                var assignment = await branchAssignments.GetAssignmentAsync(inc.CallerBranch.Id);
                if (assignment != null)
                {
                    await topDesk.UpdateIncidentAssignmentsAsync(
                        inc.Id,
                        assignment.CategoryId,
                        assignment.SubcategoryId,
                        assignment.PriorityId,
                        assignment.OperatorGroupId);
                    logger.LogInformation("Assigned {Number} via branch", inc.Number);
                }
            }
            var triaged = await Task.WhenAll(unprocessedIncidents.Select(i => aiTriage.TriageIncidentAsync(i)));

            // Auto-assign category based on description keywords
            foreach (var (incident, result) in unprocessedIncidents.Zip(triaged))
            {
                var categoryMapping = categoryMapper.MapDescription(incident.BriefDescription);
                if (categoryMapping != null && incident.Category == null)
                {
                    await topDesk.UpdateIncidentAssignmentsAsync(
                        incident.Id,
                        categoryMapping.CategoryId,
                        categoryMapping.SubcategoryId);
                    logger.LogInformation("Auto-assigned category for {Number}", incident.Number);
                }

                // Assign priority based on AI recommendation + default operator
                var priorityMapping = priorityMapper.MapAIPriority(result.RecommendedPriority);
                if (priorityMapping != null)
                {
                    await topDesk.UpdateIncidentAssignmentsAsync(
                        incident.Id,
                        null,
                        null,
                        priorityMapping.TopDeskPriorityId,
                        "bdfc2c34-85e2-412a-9cf1-4869efebd7c4");  // 24/7 Team - Monitoring
                    logger.LogInformation("Assigned priority {Priority} for {Number}", result.RecommendedPriority, incident.Number);
                }

                // Mark as processed
                await triagedState.MarkProcessedAsync(incident.Id);
            }

            if (postNotes)
                await Task.WhenAll(triaged.Select(r => topDesk.PostInternalNoteAsync(r.IncidentId, FormatNote(r))));
            results = [.. triaged];
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(results, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        return response;
    }

    private static string FormatNote(TriageResult r) => $"""
        AI Triage Recommendation
        ========================
        Priority:   {r.RecommendedPriority}
        Category:   {r.RecommendedCategory}
        Confidence: {r.Confidence:P0}

        Suggested Action:
        {r.SuggestedAction}

        Reasoning:
        {r.Reasoning}

        -- Generated automatically by AI Triage
        """;
}
