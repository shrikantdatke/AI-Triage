using System.Text.Json;
using AITriage.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AITriage.Functions;

public class TriageTimerFunction(ITopDeskService topDesk, IAITriageService aiTriage, ILogger<TriageTimerFunction> logger)
{
    // Runs every 5 minutes
    [Function("TriageTimer")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("Triage timer fired at {Time}", DateTime.UtcNow);

        var incidents = await topDesk.GetOpenIncidentsAsync(10);
        logger.LogInformation("Processing {Count} open incidents", incidents.Count);

        foreach (var incident in incidents)
        {
            try
            {
                var result = await aiTriage.TriageIncidentAsync(incident);
                logger.LogInformation(
                    "Triage {Number}: priority={Priority}, category={Category}, confidence={Confidence:P0}",
                    result.IncidentNumber, result.RecommendedPriority, result.RecommendedCategory, result.Confidence);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to triage incident {Id}", incident.Id);
            }
        }
    }
}
