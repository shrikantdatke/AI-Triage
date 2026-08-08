namespace AITriage.Models;

public class TopDeskIncident
{
    public string Id { get; set; } = "";
    public string Number { get; set; } = "";
    public string BriefDescription { get; set; } = "";
    public string? Request { get; set; }
    public TopDeskCaller? Caller { get; set; }
    public TopDeskCategory? Category { get; set; }
    public TopDeskCategory? Subcategory { get; set; }
    public TopDeskNamedItem? Priority { get; set; }
    public string? Status { get; set; }
    public string? CreationDate { get; set; }
    public bool Completed { get; set; }
}

public class TopDeskCaller
{
    public string? DynamicName { get; set; }
    public string? Email { get; set; }
}

public class TopDeskCategory
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public class TopDeskNamedItem
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}
