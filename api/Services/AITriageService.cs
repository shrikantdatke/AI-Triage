using System.ClientModel;
using AITriage.Models;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace AITriage.Services;

public class AITriageService : IAITriageService
{
    private readonly ChatClient _chat;
    private readonly ITopDeskService _topDesk;
    private readonly ILogger<AITriageService> _logger;

    public AITriageService(IConfiguration config, ITopDeskService topDesk, ILogger<AITriageService> logger)
    {
        _logger = logger;
        _topDesk = topDesk;
        var endpoint = config["AZURE_OPENAI_ENDPOINT"] ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT not configured");
        var key = config["AZURE_OPENAI_API_KEY"] ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY not configured");
        var deployment = config["AZURE_OPENAI_DEPLOYMENT"] ?? "gpt-4o-mini";

        _chat = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key)).GetChatClient(deployment);
    }

    public async Task<TriageResult> TriageIncidentAsync(TopDeskIncident incident)
    {
        // Fetch customer history in parallel
        var pastIncidents = incident.CallerBranch?.Id != null
            ? await _topDesk.GetPastIncidentsForBranchAsync(incident.CallerBranch.Id, 3)
            : new List<TopDeskIncident>();

        var historyContext = FormatPastIncidents(pastIncidents);

        var prompt = $$"""
            Analyze this IT support ticket and provide triage recommendations.

            {{historyContext}}

            Current Ticket: {{incident.BriefDescription}}
            Details: {{incident.Request ?? "No details provided"}}
            Current Category: {{incident.Category?.Name ?? "Unknown"}}
            Current Priority: {{incident.Priority?.Name ?? "Unknown"}}

            Respond in this exact JSON format:
            {
              "recommendedPriority": "P1|P2|P3|P4",
              "recommendedCategory": "category name",
              "recommendedSubcategory": "subcategory name (e.g. Outlook, Teams, Exchange)",
              "suggestedAction": "brief action description",
              "reasoning": "brief explanation",
              "confidence": 0.0 to 1.0
            }
            """;

        _logger.LogInformation("Triaging incident {Number}", incident.Number);

        var response = await _chat.CompleteChatAsync(
        [
            new SystemChatMessage("You are an IT support triage AI. Analyze tickets and provide structured recommendations. Always respond with valid JSON only."),
            new UserChatMessage(prompt)
        ]);

        var content = response.Value.Content[0].Text;

        try
        {
            var json = content.Trim();
            if (json.StartsWith("```")) json = string.Join('\n', json.Split('\n')[1..^1]);

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new TriageResult
            {
                IncidentId = incident.Id,
                IncidentNumber = incident.Number,
                BriefDescription = incident.BriefDescription,
                RecommendedPriority = root.GetProperty("recommendedPriority").GetString() ?? "",
                RecommendedCategory = root.GetProperty("recommendedCategory").GetString() ?? "",
                SuggestedAction = root.GetProperty("suggestedAction").GetString() ?? "",
                Reasoning = root.GetProperty("reasoning").GetString() ?? "",
                Confidence = root.GetProperty("confidence").GetDouble()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse AI response: {Content}", content);
            return new TriageResult
            {
                IncidentId = incident.Id,
                IncidentNumber = incident.Number,
                BriefDescription = incident.BriefDescription,
                RecommendedPriority = "P3",
                RecommendedCategory = "Unknown",
                SuggestedAction = "Manual review required",
                Reasoning = "AI parsing failed",
                Confidence = 0
            };
        }
    }

    private static string FormatPastIncidents(List<TopDeskIncident> past)
    {
        if (past.Count == 0) return "";

        var lines = new List<string>
        {
            "Customer's recent similar tickets & resolutions:",
            ""
        };

        foreach (var inc in past)
        {
            lines.Add($"Ticket {inc.Number}:");
            lines.Add($"  Issue: {inc.BriefDescription}");
            lines.Add($"  Category: {inc.Category?.Name}");

            // Extract resolution/discussion from Request field (contains history)
            if (!string.IsNullOrEmpty(inc.Request))
            {
                var request = inc.Request;
                // Extract last meaningful line from request (usually contains resolution)
                var resolutionHint = ExtractResolutionHint(request);
                if (!string.IsNullOrEmpty(resolutionHint))
                {
                    lines.Add($"  Resolution applied: {resolutionHint}");
                }
            }
            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    private static string ExtractResolutionHint(string request)
    {
        // Extract last comment/action from request history
        var lines = request.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return "";

        // Skip header lines, look for actionable content
        var actionLines = lines
            .Where(l => !l.Contains("GMT") && l.Length > 10)
            .TakeLast(3)
            .ToList();

        return actionLines.Count > 0 ? actionLines[0][..Math.Min(120, actionLines[0].Length)] : "";
    }
}
