using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace AITriage.Services;

public class TriageStateService : ITriageStateService
{
    private readonly TableClient _table;

    public TriageStateService(IConfiguration config)
    {
        var conn = config["AzureWebJobsStorage"] ?? throw new InvalidOperationException("AzureWebJobsStorage not configured");
        _table = new TableClient(conn, "TriagedIncidents");
        _table.CreateIfNotExists();
    }

    public async Task<bool> IsProcessedAsync(string incidentId)
    {
        try
        {
            await _table.GetEntityAsync<TableEntity>("triage", incidentId);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task MarkProcessedAsync(string incidentId) =>
        await _table.UpsertEntityAsync(new TableEntity("triage", incidentId)
        {
            ["ProcessedAt"] = DateTimeOffset.UtcNow
        });
}
