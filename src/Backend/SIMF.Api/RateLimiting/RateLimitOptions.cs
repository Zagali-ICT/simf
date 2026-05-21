namespace SIMF.Api.RateLimiting;

/// <summary>
/// Rate-limit settings for the authentication endpoints, bound from the
/// <c>RateLimit</c> configuration section.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>Requests permitted per window, per client IP.</summary>
    public int PermitLimit { get; set; } = 20;

    /// <summary>The window length, in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
}
