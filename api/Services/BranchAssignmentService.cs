using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace AITriage.Services;

public class BranchAssignmentService : IBranchAssignmentService
{
    private readonly TableClient _table;

    public BranchAssignmentService(IConfiguration config)
    {
        var conn = config["AzureWebJobsStorage"] ?? throw new InvalidOperationException("AzureWebJobsStorage not configured");
        _table = new TableClient(conn, "BranchAssignments");
        _table.CreateIfNotExists();
    }

    public async Task<BranchAssignment?> GetAssignmentAsync(string branchId)
    {
        try
        {
            var response = await _table.GetEntityAsync<TableEntity>("branch", branchId);
            var entity = response.Value;
            return new BranchAssignment
            {
                BranchId = branchId,
                CategoryId = entity.TryGetValue("CategoryId", out var catId) ? catId?.ToString() : null,
                SubcategoryId = entity.TryGetValue("SubcategoryId", out var subCatId) ? subCatId?.ToString() : null,
                PriorityId = entity.TryGetValue("PriorityId", out var priId) ? priId?.ToString() : null,
                OperatorGroupId = entity.TryGetValue("OperatorGroupId", out var opId) ? opId?.ToString() : null
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
