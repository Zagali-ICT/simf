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

    /// <summary>
    /// Ops override for the access-token lifetime. The
    /// <c>Session:TimeoutHours</c> configuration key (env
    /// <c>SIMF_Session__TimeoutHours</c>) lets an operator lengthen the
    /// short-lived access token beyond the NCA-default 5 minutes at runtime
    /// (e.g. a longer idle window on a controlled network), WITHOUT baking a
    /// weakened value into the committed deploy template. When the override is
    /// absent or non-positive the NCA default (<see cref="AccessTokenMinutes"/>)
    /// stands. When set it is CLAMPED to the absolute session cap
    /// (<see cref="SessionLifetimeHours"/>) so it can never let a token outlive
    /// the D-443 24-hour cap — the NCA absolute ceiling always wins.
    /// </summary>
    /// <param name="defaultMinutes">The bound <see cref="AccessTokenMinutes"/> (NCA default 5).</param>
    /// <param name="overrideHours">The <c>Session:TimeoutHours</c> value (0/absent = no override).</param>
    /// <param name="sessionLifetimeHours">The bound <see cref="SessionLifetimeHours"/> (NCA cap 24).</param>
    /// <returns>The effective access-token lifetime in minutes.</returns>
    public static int ResolveAccessTokenMinutes(
        int defaultMinutes, int overrideHours, int sessionLifetimeHours)
    {
        if (overrideHours <= 0)
        {
            return defaultMinutes;
        }

        var requestedMinutes = overrideHours * 60;
        var capMinutes = sessionLifetimeHours * 60;
        return Math.Min(requestedMinutes, capMinutes);
    }
}
