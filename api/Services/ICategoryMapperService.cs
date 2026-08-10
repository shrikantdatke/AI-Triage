namespace AITriage.Services;

public class CategoryMapping
{
    public string KeywordPattern { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public string? SubcategoryId { get; set; }
}

public interface ICategoryMapperService
{
    CategoryMapping? MapDescription(string description);
}

public class CategoryMapperService : ICategoryMapperService
{
    private readonly List<CategoryMapping> _mappings = new()
    {
        new() { KeywordPattern = "outlook", CategoryId = "887705f8-8184-40c1-bc39-668f72d18922", SubcategoryId = "94d788d8-5827-43d2-8fe7-87802972464f" },
        new() { KeywordPattern = "teams", CategoryId = "887705f8-8184-40c1-bc39-668f72d18922", SubcategoryId = "94d788d8-5827-43d2-8fe7-87802972464f" },
        new() { KeywordPattern = "exchange", CategoryId = "887705f8-8184-40c1-bc39-668f72d18922", SubcategoryId = "94d788d8-5827-43d2-8fe7-87802972464f" },
    };

    public CategoryMapping? MapDescription(string description)
    {
        var lower = description.ToLowerInvariant();
        return _mappings.FirstOrDefault(m => lower.Contains(m.KeywordPattern));
    }
}
