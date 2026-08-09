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
            var result = await aiTriage.TriageIncidentAsync(incident);
            if (postNotes)
                await topDesk.PostInternalNoteAsync(incident.Id, FormatNote(result));
            results = [result];
        }
        else
        {
            var incidents = await topDesk.GetOpenIncidentsAsync(10);
            var triaged = await Task.WhenAll(incidents.Select(i => aiTriage.TriageIncidentAsync(i)));
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
