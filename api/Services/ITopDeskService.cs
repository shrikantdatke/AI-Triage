using AITriage.Models;

namespace AITriage.Services;

public interface ITopDeskService
{
    Task<List<TopDeskIncident>> GetOpenIncidentsAsync(int pageSize = 20);
    Task<TopDeskIncident?> GetIncidentAsync(string id);
    Task PostInternalNoteAsync(string incidentId, string note);
}
