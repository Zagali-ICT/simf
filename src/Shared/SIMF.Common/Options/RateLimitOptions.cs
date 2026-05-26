namespace SIMF.Common.Options;

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

    /// <summary>
    /// H29 — D-088: per-IP cap applied to EVERY request (not just the
    /// "auth" routes). Closes the post-R3 reviewer's Security SEV-2.1
    /// main finding: the pre-H29 rate limiter only covered <c>/auth/*</c>;
    /// every bearer-protected route had no per-IP rate cap, so a
    /// malformed-bearer flood could pin a CPU core with token-validation
    /// work. The global cap is set high enough that real clients never
    /// see it (600 / minute / IP = 10 req/s — orders of magnitude above
    /// normal traffic, well below abuse traffic).
    /// </summary>
    public int GlobalPermitLimit { get; set; } = 600;

    /// <summary>The global window length, in seconds.</summary>
    public int GlobalWindowSeconds { get; set; } = 60;
}
