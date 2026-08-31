using MeetingFlow.Monolith.Models;

namespace MeetingFlow.Monolith.Evals;

/// <summary>
/// Code-only checks on the shape of a response: no language model involved.
/// </summary>
public static class DeterministicCheck
{
    public static (bool Passed, string Note) Evaluate(
        IReadOnlyList<DishCheckEntry> requested,
        DishAssessmentBatch batch)
    {
        if (batch.Items.Count != requested.Count)
        {
            return (false, $"Expected {requested.Count} result(s), got {batch.Items.Count}.");
        }

        var requestedIds = requested.Select(dish => dish.Id).ToHashSet(StringComparer.Ordinal);
        var returnedIds = batch.Items.Select(item => item.DishId).ToList();

        if (returnedIds.Distinct(StringComparer.Ordinal).Count() != returnedIds.Count)
        {
            return (false, "Duplicate dish identifiers in the response.");
        }

        if (!returnedIds.ToHashSet(StringComparer.Ordinal).SetEquals(requestedIds))
        {
            return (false, "Response dish identifiers do not match the request.");
        }

        var allowedStatuses = Enum.GetValues<DishAssessmentStatus>().ToHashSet();
        foreach (var item in batch.Items)
        {
            if (!allowedStatuses.Contains(item.Status))
            {
                return (false, $"Status for {item.DishId} is not one of the allowed values.");
            }

            if (string.IsNullOrWhiteSpace(item.Explanation))
            {
                return (false, $"Explanation for {item.DishId} is empty.");
            }

            if (item.Explanation.Length > 1000)
            {
                return (false, $"Explanation for {item.DishId} exceeds 1000 characters.");
            }
        }

        return (true, "Result count, dish identifiers, allowed statuses, and explanations are all valid.");
    }
}
