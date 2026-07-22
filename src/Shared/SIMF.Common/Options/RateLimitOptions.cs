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

    /// <summary>D-179 (gap doc G12 hardening) — per-admin cap on the AI
    /// prompt dry-run endpoint (<c>POST /admin/ai/prompts/{id}/test</c>).
    /// The existing per-IP "auth" window does not protect against an
    /// office shared by multiple admins, or a stolen-credential botnet
    /// rotating IPs. Partitioned on the JWT <c>sub</c> claim so each
    /// admin gets their own bucket regardless of source IP.</summary>
    public int AiTestPermitLimit { get; set; } = 20;

    /// <summary>The per-admin AI-test window length, in seconds. Default
    /// 3600 (1 hour) so 20 permits = 20 dry-runs per admin per hour.</summary>
    public int AiTestWindowSeconds { get; set; } = 3600;

    /// <summary>Per-operator cap on the Control Panel help assistant
    /// (<c>POST /admin/ai/assistant</c>), whose every call hits the AI
    /// provider. Same per-<c>sub</c> rationale as <see cref="AiTestPermitLimit"/>
    /// — the per-IP "auth" window cannot bound a shared office or an
    /// IP-rotating botnet — but a little higher because the assistant is used
    /// interactively across pages. Default 40.</summary>
    public int AiAssistantPermitLimit { get; set; } = 40;

    /// <summary>The per-operator assistant window length, in seconds. Default
    /// 3600 (1 hour) so 40 permits = 40 questions per operator per hour.</summary>
    public int AiAssistantWindowSeconds { get; set; } = 3600;
}
