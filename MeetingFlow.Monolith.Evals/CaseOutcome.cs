namespace MeetingFlow.Monolith.Evals;

public sealed record DishSubmission(string Description, string Status, string Explanation);

public sealed record CaseOutcome(
    EvalCase Case,
    bool DeterministicPassed,
    string DeterministicNote,
    IReadOnlyList<DishSubmission> Submissions,
    JudgeVerdict Judge)
{
    public bool OverallPassed => DeterministicPassed && Judge.Passed;
}
