using System.ClientModel;
using AITriage.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace AITriage.Services;

public class AITriageService : IAITriageService
{
    private readonly ChatClient _chat;
    private readonly ILogger<AITriageService> _logger;

    public AITriageService(IConfiguration config, ILogger<AITriageService> logger)
    {
        _logger = logger;
        var key = config["OPENAI_API_KEY"] ?? throw new InvalidOperationException("OPENAI_API_KEY not configured");
        var model = config["OPENAI_MODEL"] ?? "gpt-4o";

        _chat = new OpenAIClient(new ApiKeyCredential(key)).GetChatClient(model);
    }

    public async Task<TriageResult> TriageIncidentAsync(TopDeskIncident incident)
    {
        var prompt = $$"""
            Analyze this IT support ticket and provide triage recommendations.

            Ticket: {{incident.BriefDescription}}
            Details: {{incident.Request ?? "No details provided"}}
            Current Category: {{incident.Category?.Name ?? "Unknown"}}
            Current Priority: {{incident.Priority?.Name ?? "Unknown"}}

            Respond in this exact JSON format:
            {
              "recommendedPriority": "P1|P2|P3|P4",
              "recommendedCategory": "category name",
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
            // Strip markdown code fences if present
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
}
