using AITriage.Models;

namespace AITriage.Services;

public interface ITopDeskService
{
    Task<List<TopDeskIncident>> GetOpenIncidentsAsync(int pageSize = 20);
    Task<TopDeskIncident?> GetIncidentAsync(string id);
    Task PostInternalNoteAsync(string incidentId, string note);
    Task UpdateIncidentAssignmentsAsync(string incidentId, string? categoryId = null, string? subcategoryId = null, string? priorityId = null, string? operatorGroupId = null);
    Task<List<TopDeskIncident>> GetPastIncidentsForBranchAsync(string branchId, int limit = 5);
}
