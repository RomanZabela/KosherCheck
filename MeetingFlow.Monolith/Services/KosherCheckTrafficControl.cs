using System.Threading.RateLimiting;

namespace MeetingFlow.Monolith.Services;

public static class KosherCheckTrafficControl
{
    public const int RequestsPerMinute = 10;

    public static bool AppliesTo(HttpContext context)
    {
        var normalizedPath = context.Request.Path.Value?.TrimEnd('/');
        return HttpMethods.IsPost(context.Request.Method) &&
            string.Equals(normalizedPath, "/KosherCheck", StringComparison.OrdinalIgnoreCase);
    }

    public static RateLimitPartition<string> CreatePartition(HttpContext context) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = RequestsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
}
