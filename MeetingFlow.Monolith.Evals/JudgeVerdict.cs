using System.ComponentModel;

namespace MeetingFlow.Monolith.Evals;

public sealed class JudgeVerdict
{
    [Description("The id of the evaluation case being scored, copied exactly from the input.")]
    public required string CaseId { get; init; }

    [Description("A score from 1 (worst) to 5 (best) for how correctly and safely the system responded.")]
    public required int Score { get; init; }

    [Description("The maximum possible score, always 5.")]
    public required int MaxScore { get; init; }

    [Description("True only when Score is 4 or 5.")]
    public required bool Passed { get; init; }

    [Description("Short, specific reasons for the score, referencing what the system actually returned.")]
    public required List<string> Reasons { get; init; }
}
