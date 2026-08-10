namespace AITriage.Services;

public class PriorityMapping
{
    public string AIPriority { get; set; } = "";  // P1, P2, P3, P4
    public string TopDeskPriorityId { get; set; } = "";
    public string DurationId { get; set; } = "";  // SLA duration
}

public interface IPriorityMapperService
{
    PriorityMapping? MapAIPriority(string aiPriority);
}

public class PriorityMapperService : IPriorityMapperService
{
    private readonly List<PriorityMapping> _mappings = new()
    {
        // P1 (Urgent) → High priority, 4 hour SLA
        new() { AIPriority = "P1", TopDeskPriorityId = "23d03f34-44cf-469e-8471-5b9399f14a22", DurationId = "50cc3ceb-dea9-5e49-9c8c-45713ebb7336" },

        // P2 (High) → High priority, 4 hour SLA
        new() { AIPriority = "P2", TopDeskPriorityId = "23d03f34-44cf-469e-8471-5b9399f14a22", DurationId = "50cc3ceb-dea9-5e49-9c8c-45713ebb7336" },

        // P3 (Medium) → Medium priority
        new() { AIPriority = "P3", TopDeskPriorityId = "b6b6b3f2-6e06-4200-abc1-dd52dbdf30c7", DurationId = null },

        // P4 (Low) → Low priority
        new() { AIPriority = "P4", TopDeskPriorityId = "e82ec275-6203-42f6-8b8a-22ee8219eec2", DurationId = null },
    };

    public PriorityMapping? MapAIPriority(string aiPriority)
    {
        return _mappings.FirstOrDefault(m => m.AIPriority == aiPriority);
    }
}
