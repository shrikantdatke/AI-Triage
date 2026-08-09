namespace AITriage.Services;

public class BranchAssignment
{
    public string BranchId { get; set; } = "";
    public string? CategoryId { get; set; }
    public string? SubcategoryId { get; set; }
    public string? PriorityId { get; set; }
    public string? OperatorGroupId { get; set; }
}

public interface IBranchAssignmentService
{
    Task<BranchAssignment?> GetAssignmentAsync(string branchId);
}
