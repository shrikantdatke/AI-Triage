using System.Text.Json;
using AITriage.Models;
using AITriage.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AITriage.Functions;

public class TriageIncidentFunction(ITopDeskService topDesk, IAITriageService aiTriage, ILogger<TriageIncidentFunction> logger)
{
    [Function("TriageIncident")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "incidents/triage")] HttpRequestData req)
    {
        logger.LogInformation("POST /api/incidents/triage");

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
            results = [await aiTriage.TriageIncidentAsync(incident)];
        }
        else
        {
            // Triage all open incidents
            var incidents = await topDesk.GetOpenIncidentsAsync(10);
            var tasks = incidents.Select(i => aiTriage.TriageIncidentAsync(i));
            results = [.. await Task.WhenAll(tasks)];
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(results, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        return response;
    }
}
