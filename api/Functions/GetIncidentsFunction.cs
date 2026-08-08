using System.Text.Json;
using AITriage.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AITriage.Functions;

public class GetIncidentsFunction(ITopDeskService topDesk, ILogger<GetIncidentsFunction> logger)
{
    [Function("GetIncidents")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "incidents")] HttpRequestData req)
    {
        logger.LogInformation("GET /api/incidents");

        var pageSize = 20;
        if (req.Query["pageSize"] is string ps && int.TryParse(ps, out var n)) pageSize = n;

        var incidents = await topDesk.GetOpenIncidentsAsync(pageSize);

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(incidents, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        return response;
    }
}
