using MeetingFlow.Monolith.Models;
using MeetingFlow.Monolith.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MeetingFlow.Monolith.Pages;

public sealed class KosherCheckModel(
    IKosherAssessmentService assessmentService,
    ILogger<KosherCheckModel> logger) : PageModel
{
    private const string UnavailableMessage =
        "Kosher checking is currently unavailable. Please try again later.";

    [BindProperty]
    public List<string> Dishes { get; set; } = [string.Empty];

    public async Task<IActionResult> OnPostCheckAsync(CancellationToken cancellationToken)
    {
        var validation = KosherInputValidator.Validate(Dishes);
        if (!validation.IsValid)
        {
            return new JsonResult(new { errors = validation.Errors })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var entries = validation.Dishes
            .Select((dish, index) => new DishCheckEntry($"dish-{index + 1}", dish))
            .ToList();

        try
        {
            var assessment = await assessmentService.AssessAsync(entries, cancellationToken);
            var assessmentsById = assessment.Items.ToDictionary(item => item.DishId, StringComparer.Ordinal);
            var results = entries.Select(entry =>
            {
                var item = assessmentsById[entry.Id];
                return new
                {
                    dish = entry.Description,
                    status = ToWireValue(item.Status),
                    explanation = item.Explanation
                };
            });

            return new JsonResult(new { results })
            {
                StatusCode = StatusCodes.Status200OK
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Kosher checking failed.");
            return new JsonResult(new { error = UnavailableMessage })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
        }
    }

    private static string ToWireValue(DishAssessmentStatus status) => status switch
    {
        DishAssessmentStatus.Kosher => "KOSHER",
        DishAssessmentStatus.NotKosher => "NOT_KOSHER",
        DishAssessmentStatus.Conditional => "CONDITIONAL",
        DishAssessmentStatus.InvalidInput => "INVALID_INPUT",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown kosher assessment status.")
    };
}
