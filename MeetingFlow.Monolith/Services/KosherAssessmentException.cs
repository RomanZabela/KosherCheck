namespace MeetingFlow.Monolith.Services;

public sealed class KosherAssessmentException : Exception
{
    public KosherAssessmentException(string message)
        : base(message)
    {
    }

    public KosherAssessmentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
