namespace MeetingFlow.Monolith.Evals;

public sealed class EvalCase
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required List<string> Dishes { get; init; }
    public required string Notes { get; init; }
}
