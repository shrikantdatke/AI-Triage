namespace AITriage.Models;

public class TriageResult
{
    public string IncidentId { get; set; } = "";
    public string IncidentNumber { get; set; } = "";
    public string BriefDescription { get; set; } = "";
    public string RecommendedPriority { get; set; } = "";
    public string RecommendedCategory { get; set; } = "";
    public string SuggestedAction { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public double Confidence { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class TriageRequest
{
    public string IncidentId { get; set; } = "";
}
