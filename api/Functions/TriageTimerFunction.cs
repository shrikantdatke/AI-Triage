using AITriage.Models;
using AITriage.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AITriage.Functions;

public class TriageTimerFunction(
    ITopDeskService topDesk,
    IAITriageService aiTriage,
    ITriageStateService state,
    IConfiguration config,
    ILogger<TriageTimerFunction> logger)
{
    [Function("TriageTimer")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        if (!bool.TryParse(config["AI_TRIAGE_ENABLED"], out var enabled) || !enabled)
        {
            logger.LogInformation("AI Triage disabled. Set AI_TRIAGE_ENABLED=true to enable.");
            return;
        }

        logger.LogInformation("AI Triage timer fired at {Time}", DateTime.UtcNow);
        var incidents = await topDesk.GetOpenIncidentsAsync(20);

        foreach (var incident in incidents)
        {
            if (await state.IsProcessedAsync(incident.Id))
                continue;

            try
            {
                var result = await aiTriage.TriageIncidentAsync(incident);
                await topDesk.PostInternalNoteAsync(incident.Id, FormatNote(result));
                await state.MarkProcessedAsync(incident.Id);
                logger.LogInformation("Noted {Number}: {Priority} ({Confidence:P0})",
                    result.IncidentNumber, result.RecommendedPriority, result.Confidence);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to triage incident {Id}", incident.Id);
            }
        }
    }

    private static string FormatNote(TriageResult r) => $"""
        AI Triage Recommendation
        ========================
        Priority:   {r.RecommendedPriority}
        Category:   {r.RecommendedCategory}
        Confidence: {r.Confidence:P0}

        Suggested Action:
        {r.SuggestedAction}

        Reasoning:
        {r.Reasoning}

        -- Generated automatically by AI Triage
        """;
}
