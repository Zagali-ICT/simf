namespace SIMF.Common.Options;

/// <summary>
/// JWT access-token settings, bound from the <c>Jwt</c> configuration section.
/// The signing key is supplied through the environment / <c>set-env</c> scripts
/// and is never committed.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SIMF";

    public string Audience { get; set; } = "SIMF";

    /// <summary>The HMAC-SHA256 signing key — at least 32 bytes.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>The access-token lifetime, in minutes. D-443 (NCA finding):
    /// capped at 5 minutes — short-lived and silently rotated in the
    /// background before it lapses, so an active user is never interrupted.</summary>
    public int AccessTokenMinutes { get; set; } = 5;

    /// <summary>D-443 (NCA finding): the absolute session lifetime, in hours.
    /// The refresh token issued at sign-in expires at <c>sign-in + this</c>,
    /// and rotation carries that same deadline forward (it does NOT slide a
    /// fresh window), so a session can never outlive the cap — re-login is
    /// required after it even for an active user. Capped at 24 hours (1 day).</summary>
    public int SessionLifetimeHours { get; set; } = 24;

    /// <summary>P3.2b — D-232 (D-213): the distinct audience for short-lived
    /// session-recording streaming tokens. A token minted for streaming
    /// (<c>aud = simf-stream</c>) is rejected by the main user scheme
    /// (<c>aud = SIMF</c>) and vice-versa, so a stream token can never be
    /// replayed as a full session token.</summary>
    public string StreamAudience { get; set; } = "simf-stream";

    /// <summary>P3.2b — D-232: the recording-stream token lifetime, in minutes.
    /// Long enough to watch one recording in a sitting, still bounded/expiring
    /// and scoped to a single recording (not a permanent URL). The deferred
    /// upgrade path (per-segment signed URLs / client token refresh) is noted
    /// in D-213.</summary>
    public int StreamTokenMinutes { get; set; } = 180;
}
