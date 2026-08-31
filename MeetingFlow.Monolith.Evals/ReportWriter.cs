using System.Globalization;
using System.Text;

namespace MeetingFlow.Monolith.Evals;

public static class ReportWriter
{
    public static string Build(
        IReadOnlyList<CaseOutcome> outcomes,
        string evaluatedModel,
        string judgeModel,
        DateTimeOffset runDate)
    {
        var totalCases = outcomes.Count;
        var passingCases = outcomes.Count(o => o.OverallPassed);
        var averageScore = outcomes.Count == 0 ? 0 : outcomes.Average(o => (double)o.Judge.Score);

        var sb = new StringBuilder();
        sb.AppendLine("# Kosher Dish Assessment Eval Report");
        sb.AppendLine();
        sb.AppendLine($"- **Run date:** {runDate.ToString("u", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"- **Evaluated model:** {evaluatedModel}");
        sb.AppendLine($"- **Judge model:** {judgeModel}");
        sb.AppendLine($"- **Passing cases:** {passingCases} / {totalCases}");
        sb.AppendLine($"- **Average judge score:** {averageScore.ToString("F2", CultureInfo.InvariantCulture)} / 5");
        sb.AppendLine();

        sb.AppendLine("## Case Results");
        sb.AppendLine();
        sb.AppendLine("| Case | Category | Deterministic Check | Statuses Returned | Score | Passed | Judge Reasons |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var outcome in outcomes)
        {
            var statuses = outcome.Submissions.Count == 0
                ? "-"
                : string.Join("; ", outcome.Submissions.Select(s => s.Status));
            var reasons = string.Join("<br>", outcome.Judge.Reasons.Select(EscapePipes));
            sb.AppendLine(
                $"| {outcome.Case.Id} | {outcome.Case.Category} | " +
                $"{(outcome.DeterministicPassed ? "PASS" : "FAIL")} — {EscapePipes(outcome.DeterministicNote)} | " +
                $"{EscapePipes(statuses)} | {outcome.Judge.Score}/{outcome.Judge.MaxScore} | " +
                $"{(outcome.Judge.Passed ? "yes" : "no")} | {reasons} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Conclusion");
        sb.AppendLine();
        sb.AppendLine(BuildConclusion(outcomes, evaluatedModel, totalCases, passingCases, averageScore));

        return sb.ToString();
    }

    private static string BuildConclusion(
        IReadOnlyList<CaseOutcome> outcomes,
        string evaluatedModel,
        int totalCases,
        int passingCases,
        double averageScore)
    {
        var failingCategories = outcomes
            .Where(o => !o.OverallPassed)
            .Select(o => o.Case.Category)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        var passingCategories = outcomes
            .Where(o => o.OverallPassed)
            .Select(o => o.Case.Category)
            .Distinct()
            .Except(failingCategories)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Across {totalCases} cases, {evaluatedModel} passed {passingCases} ");
        sb.Append(CultureInfo.InvariantCulture, $"(average judge score {averageScore.ToString("F2", CultureInfo.InvariantCulture)}/5). ");

        sb.Append(passingCategories.Count > 0
            ? $"It was consistently correct on: {string.Join(", ", passingCategories)}. "
            : "No category passed every case in this run. ");

        sb.Append(failingCategories.Count > 0
            ? $"It struggled most with: {string.Join(", ", failingCategories)}."
            : "No category showed a systematic weakness in this run.");

        return sb.ToString();
    }

    private static string EscapePipes(string value) => value.Replace("|", "\\|").Replace("\n", " ");
}
