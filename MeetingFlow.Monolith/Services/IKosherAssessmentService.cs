using MeetingFlow.Monolith.Models;

namespace MeetingFlow.Monolith.Services;

public interface IKosherAssessmentService
{
    Task<DishAssessmentBatch> AssessAsync(
        IReadOnlyList<DishCheckEntry> dishes,
        CancellationToken cancellationToken = default);
}
