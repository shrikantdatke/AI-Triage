using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AITriage.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AITriage.Services;

public class TopDeskService : ITopDeskService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _baseUrl;
    private readonly string _credentials;
    private readonly ILogger<TopDeskService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TopDeskService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<TopDeskService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _baseUrl = config["TOPDESK_URL"]?.TrimEnd('/') ?? throw new InvalidOperationException("TOPDESK_URL not configured");

        var username = config["TOPDESK_USERNAME"] ?? throw new InvalidOperationException("TOPDESK_USERNAME not configured");
        var password = config["TOPDESK_PASSWORD"] ?? throw new InvalidOperationException("TOPDESK_PASSWORD not configured");
        _credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
    }

    private HttpClient CreateClient()
    {
        var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _credentials);
        return client;
    }

    public async Task<List<TopDeskIncident>> GetOpenIncidentsAsync(int pageSize = 20)
    {
        var url = $"{_baseUrl}/tas/api/incidents?pageSize={pageSize}&pageStart=0&status=firstLine";
        _logger.LogInformation("Fetching incidents from TopDesk: {Url}", url);

        var response = await CreateClient().GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<TopDeskIncident>>(json, _json) ?? [];
    }

    public async Task<TopDeskIncident?> GetIncidentAsync(string id)
    {
        var response = await CreateClient().GetAsync($"{_baseUrl}/tas/api/incidents/id/{id}");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TopDeskIncident>(json, _json);
    }

    public async Task PostInternalNoteAsync(string incidentId, string note)
    {
        var body = JsonSerializer.Serialize(new { action = note, actionInvisibleForCaller = true });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await CreateClient().PatchAsync($"{_baseUrl}/tas/api/incidents/id/{incidentId}", content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to post note to {Id}: {Status} {Error}", incidentId, response.StatusCode, error);
        }
    }

    public async Task UpdateIncidentAssignmentsAsync(string incidentId, string? categoryId = null, string? subcategoryId = null, string? priorityId = null, string? operatorGroupId = null)
    {
        var update = new Dictionary<string, object>();
        if (categoryId != null) update["category"] = new { id = categoryId };
        if (subcategoryId != null) update["subcategory"] = new { id = subcategoryId };
        if (priorityId != null) update["priority"] = new { id = priorityId };
        if (operatorGroupId != null) update["operatorGroup"] = new { id = operatorGroupId };

        if (update.Count == 0) return;

        var body = JsonSerializer.Serialize(update);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await CreateClient().PatchAsync($"{_baseUrl}/tas/api/incidents/id/{incidentId}", content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to update assignments for {Id}: {Status} {Error}", incidentId, response.StatusCode, error);
        }
    }

    public async Task<List<TopDeskIncident>> GetPastIncidentsForBranchAsync(string branchId, int limit = 5)
    {
        var url = $"{_baseUrl}/tas/api/incidents?pageSize={limit}&pageStart=0&completed=true&sortField=modificationDate&sortOrder=desc";
        try
        {
            var response = await CreateClient().GetAsync(url);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync();
            var incidents = JsonSerializer.Deserialize<List<TopDeskIncident>>(json, _json) ?? [];

            // Filter for same branch
            return incidents.Where(i => i.CallerBranch?.Id == branchId).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch past incidents for branch {Branch}", branchId);
            return [];
        }
    }
}
