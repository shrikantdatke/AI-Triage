namespace AITriage.Services;

public interface ITriageStateService
{
    Task<bool> IsProcessedAsync(string incidentId);
    Task MarkProcessedAsync(string incidentId);
}
