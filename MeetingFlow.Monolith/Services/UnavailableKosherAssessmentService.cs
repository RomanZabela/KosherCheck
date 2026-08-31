using MeetingFlow.Monolith.Models;

namespace MeetingFlow.Monolith.Services;

public sealed class UnavailableKosherAssessmentService : IKosherAssessmentService
{
    public Task<DishAssessmentBatch> AssessAsync(
        IReadOnlyList<DishCheckEntry> dishes,
        CancellationToken cancellationToken = default) =>
        throw new KosherAssessmentException("AI configuration is unavailable.");
}
