namespace SIMF.Api.RateLimiting;

/// <summary>
/// Rate-limit settings for the authentication endpoints, bound from the
/// <c>RateLimit</c> configuration section.
///
/// <para>Two independent partitions guard the auth surface (H7 — D-062):
/// the per-IP partition (<see cref="PermitLimit"/> / <see cref="WindowSeconds"/>)
/// covers the whole "auth" policy; the per-email partition
/// (<see cref="EmailPermitLimit"/> / <see cref="EmailWindowSeconds"/>) is
/// applied additionally to the credential-touching endpoints
/// (sign-in, forgot-password, reset-password) so an attacker who rotates
/// source IPs against a single account is bounded by the per-email cap
/// before tripping the per-account lockout (which would DoS the legitimate
/// user). Both partitions must pass for a request to proceed.</para>
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>Requests permitted per window, per client IP.</summary>
    public int PermitLimit { get; set; } = 20;

    /// <summary>The per-IP window length, in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Requests permitted per window against ONE email address, across all
    /// source IPs. Caps credential-stuffing attacks that rotate proxies
    /// against a single victim account.
    /// </summary>
    public int EmailPermitLimit { get; set; } = 5;

    /// <summary>The per-email window length, in seconds.</summary>
    public int EmailWindowSeconds { get; set; } = 60;
}
