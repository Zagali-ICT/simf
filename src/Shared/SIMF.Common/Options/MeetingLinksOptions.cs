namespace SIMF.Common.Options;

/// <summary>
/// Configuration for the speaker
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
    /// the link is built). Defaulted to the Website's local origin in
    /// <c>appsettings.Development.json</c>; in QA / Production it MUST be set via
    /// <c>SIMF_MeetingLinks__PublicWebBaseUrl</c>. When it is empty the
    /// speaker approve / resend paths FAIL LOUDLY
    /// (<c>MEETING_LINKS_NOT_CONFIGURED</c>) instead of minting tokens and skipping
    /// the email with a log line, which parked the request in <c>AwaitingSpeaker</c>
    /// with no way out.</summary>
    public string PublicWebBaseUrl { get; set; } = string.Empty;

    /// <summary>Token time-to-live in hours (default 72).</summary>
    public int TokenTtlHours { get; set; } = 72;
}
