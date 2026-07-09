namespace SIMF.Common.Options;

/// <summary>
/// D-717 (item 7, FDS-013 §15 GAP-3) — configuration for the speaker
/// double-opt-in email links. <see cref="PublicWebBaseUrl"/> is the public
/// Website origin the Approve/Reject links point at (the landing page lives at
/// <c>{PublicWebBaseUrl}/meeting/confirm?token=…</c>). Bound from the
/// <c>MeetingLinks</c> section; the value is mirrored between appsettings and the
/// <c>SIMF_MeetingLinks__PublicWebBaseUrl</c> environment override so the two
/// never drift.
/// </summary>
public sealed class MeetingLinksOptions
{
    public const string SectionName = "MeetingLinks";

    /// <summary>The public Website origin (no trailing slash needed; trimmed when
    /// the link is built). Empty when unconfigured — the links email is then
    /// skipped and only logged, never sent with a broken URL.</summary>
    public string PublicWebBaseUrl { get; set; } = string.Empty;

    /// <summary>Token time-to-live in hours (§15.7 / OI-I — default 72).</summary>
    public int TokenTtlHours { get; set; } = 72;
}
