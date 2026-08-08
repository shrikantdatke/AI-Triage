using AITriage.Models;

namespace AITriage.Services;

public interface IAITriageService
{
    Task<TriageResult> TriageIncidentAsync(TopDeskIncident incident);
}
