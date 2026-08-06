using System.Globalization;
using SIMF.Common.Badges;

namespace SIMF.Common.Options;

/// <summary>
/// The standby "walk-in" capability, for an event where a large crowd
/// arrives who never installed the app, never registered online, hold no badge
/// and never booked a session.
///
/// <para>Every switch defaults to OFF, so with no configuration the system
/// behaves exactly as it does today: walk-ins queue for approval, the full desk
/// field set is required, and a session hall admits only registered attendees.
/// The capability ships built and dormant and is ARMED by editing
/// <c>appsettings</c> / <c>set-env-*</c> when it is actually needed.</para>
///
/// <para>FAIL-CLOSED, deliberately. A missing, blank or unparseable value
/// resolves to off, so a configuration mistake can only ever leave the system
/// STRICTER than today, never looser. Note this is the opposite polarity to
/// <c>AppVersionPolicyService</c>, which fails open because there "off" means
/// "no restriction". Here "off" means "the normal control still applies".</para>
///
/// <para>It lives in configuration rather than a Control Panel screen on
/// purpose: arming it requires server access, which is the strongest access
/// control available for a capability that relaxes an approval gate.</para>
/// </summary>
public sealed class WalkInModeOptions
{
    public const string SectionName = "WalkInMode";

    /// <summary>The master switch. Every other switch here is inert while this
    /// is false, so one edit disarms the whole capability.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Optional hard stop, evaluated on read. Once passed, every rule reports
    /// off even if <see cref="Enabled"/> is still true, so a mode left armed
    /// after the event closes itself rather than lingering.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Accept the reduced desk field set (one name, profile type, one
    /// mobile, one ID number). The ID number is deliberately still required:
    /// it is the only thing preventing one person collecting several badges,
    /// and the encrypted columns cannot be reconstructed after the fact.</summary>
    public bool QuickRegister { get; set; }

    /// <summary>
    /// Whether quick register still demands one identity document (national ID,
    /// Iqama or passport). Defaults to TRUE, and should stay that way: the number
    /// is the only thing preventing one person collecting several badges, because
    /// the duplicate-identity guard and its three filtered unique indexes key off
    /// a blind index of it. The plaintext columns are AES-GCM encrypted with a
    /// random nonce, so an id not captured at the desk can never be reconstructed
    /// afterwards. Turning this off buys a few seconds per visitor and gives up
    /// duplicate detection entirely.
    /// </summary>
    public bool QuickRegisterRequiresIdentityDocument { get; set; } = true;

    /// <summary>Approve an on-site registered visitor immediately, which is what
    /// mints their QR. Audience visitors only — never the partner desk, whose
    /// profile types can carry an operational mobile-app role.</summary>
    public bool AutoApprove { get; set; }

    /// <summary>Admit an attendee to a session hall who is not registered for
    /// that session. This is the ONLY one of the three gate rules the walk-in
    /// mode relaxes; approved and profile-type-allowed always hold.</summary>
    public bool SessionWalkIn { get; set; }

    /// <summary>
    /// How far either side of a session's scheduled window arrivals are
    /// accepted. Defaults to the long-standing 15 minutes; widen it when a
    /// queue forms well before a keynote. Clamped to 0..240 on read.
    /// </summary>
    public int ArrivalGraceMinutes { get; set; } = DefaultArrivalGraceMinutes;

    // D-821 review: AdvisoryHallCapacity was declared here, documented in D-819
    // and shipped in set-env-api.template.ps1 — with NO implementation anywhere.
    // Arming it did nothing, which is worse than not offering it: an operator
    // would have believed capacity was relaxed. Removed rather than implemented,
    // because the gate-door arrival path already passes enforceCapacity: false
    // unconditionally (HallAttendanceService), so there was nothing for it to do.

    /// <summary>Accept badges minted offline by the desk tool.</summary>
    public bool AcceptOfflineBadges { get; set; }

    /// <summary>Enable the offline batch upload endpoint.</summary>
    public bool OfflineUpload { get; set; }

    /// <summary>
    /// Allow a walk-in-created account to be claimed as a full app account via
    /// badge activation. Defaults to FALSE: badge activation accepts a
    /// caller-supplied email for accounts with a synthesized placeholder
    /// address, so with live walk-in badges in circulation anyone who
    /// photographs one could claim it. Off, a walk-in badge grants physical
    /// access only, and a staffed desk can upgrade the person after an ID check.
    /// </summary>
    public bool AllowBadgeActivation { get; set; }

    /// <summary>Base64 AES-256 key shared by the desk generator, the scanners
    /// and the server. Blank disables offline badge handling entirely.</summary>
    public string BadgeKey { get; set; } = string.Empty;

    /// <summary>The version stamped into badges minted now (0..31).</summary>
    public int BadgeKeyVersion { get; set; }

    /// <summary>Base64 AES-256 key for the PREVIOUS version, accepted but never
    /// used to mint, so a key can be rotated without invalidating badges
    /// already in circulation. Blank when no rotation is in progress.</summary>
    public string PreviousBadgeKey { get; set; } = string.Empty;

    /// <summary>The version <see cref="PreviousBadgeKey"/> corresponds to.</summary>
    public int PreviousBadgeKeyVersion { get; set; }

    /// <summary>The arrival grace the system has always used.</summary>
    public const int DefaultArrivalGraceMinutes = 15;

    /// <summary>The widest arrival grace any layer may set. Public since D-839,
    /// which gave the same knob a per-hall and per-session home: the bound has to
    /// be one number, or the DB check constraint, the request validators and this
    /// clamp could disagree about what is legal.</summary>
    public const int MaxArrivalGraceMinutes = 240;

    /// <summary>
    /// True when the capability is armed at <paramref name="now"/>: the master
    /// switch is on and any expiry has not passed. Every per-rule accessor
    /// below is gated on this, so a single edit disarms everything.
    /// </summary>
    public bool IsArmed(DateTime now) =>
        Enabled && (ExpiresAt is not { } expiry || now < expiry);

    public bool QuickRegisterActive(DateTime now) => IsArmed(now) && QuickRegister;

    public bool AutoApproveActive(DateTime now) => IsArmed(now) && AutoApprove;

    public bool SessionWalkInActive(DateTime now) => IsArmed(now) && SessionWalkIn;

    public bool AcceptOfflineBadgesActive(DateTime now) =>
        IsArmed(now) && AcceptOfflineBadges && ResolveBadgeKey(BadgeKey) is not null;

    public bool OfflineUploadActive(DateTime now) => IsArmed(now) && OfflineUpload;

    /// <summary>
    /// Badge activation for walk-in accounts. NOT gated on <see cref="IsArmed"/>:
    /// badges minted while the mode was armed keep circulating after it is
    /// disarmed, so the block has to outlive the mode that created them.
    /// </summary>
    public bool BadgeActivationAllowedForWalkIns => AllowBadgeActivation;

    /// <summary>The effective GLOBAL arrival grace in whole minutes, clamped,
    /// falling back to the historical 15 whenever the mode is not armed. Minutes
    /// is the stored unit, so this is the primitive and the
    /// <see cref="TimeSpan"/> accessor derives from it — not the other way round,
    /// which had callers unwrapping a TimeSpan with a truncating cast to recover
    /// the int it was built from.</summary>
    public int ResolveArrivalGraceMinutes(DateTime now) =>
        IsArmed(now)
            ? Math.Clamp(ArrivalGraceMinutes, 0, MaxArrivalGraceMinutes)
            : DefaultArrivalGraceMinutes;

    /// <summary>The effective arrival grace, clamped, falling back to the
    /// historical 15 minutes whenever the mode is not armed.</summary>
    public TimeSpan ResolveArrivalGrace(DateTime now) =>
        TimeSpan.FromMinutes(ResolveArrivalGraceMinutes(now));

    /// <summary>
    /// The arrival-grace precedence, in whole minutes:
    /// <c>session override → hall → the global value</c>. Null at a layer means
    /// "inherit the one below"; a stored <c>0</c> is a real zero and wins.
    ///
    /// <para>Static and free of any option state so the SAME rule serves the
    /// hall door, the admin list projection and the detail read. It previously
    /// existed as three hand-written copies that already disagreed — only the
    /// door clamped — which is the exact class of divergence D-839 was written
    /// to end. Clamping here rather than trusting the check constraints keeps it
    /// true for a row that reaches the database another way (a restore with
    /// <c>NOCHECK</c>, a direct UPDATE).</para>
    /// </summary>
    public static int ResolveArrivalGraceMinutes(
        int? sessionOverrideMinutes, int? hallMinutes, int globalMinutes) =>
        (sessionOverrideMinutes ?? hallMinutes) is { } minutes
            ? Math.Clamp(minutes, 0, MaxArrivalGraceMinutes)
            : globalMinutes;

    /// <summary>Null means "inherit the layer above"; any other value must
    /// sit inside the one shared bound, the same bound the DB check constraints
    /// enforce.</summary>
    public static bool IsValidArrivalGrace(int? minutes) =>
        minutes is not { } value || value is >= 0 and <= MaxArrivalGraceMinutes;

    /// <summary>
    /// Parses a blank-or-bounded arrival-grace input. Blank returns true
    /// with <paramref name="minutes"/> null, which is the real "inherit" value —
    /// NOT zero, which would slam a hall's doors shut the instant a session ends.
    ///
    /// <para>Shared by both Excel importers and both Control Panel forms, which
    /// each carried a copy. Same reasoning as <see cref="LiveStreamUrlPolicy"/>,
    /// which the sibling field on those very forms already uses: the rule lives
    /// in exactly one place so the client and the server cannot disagree.</para>
    /// </summary>
    public static bool TryParseArrivalGrace(string? raw, out int? minutes)
    {
        minutes = null;
        var trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return true;
        }
        if (!int.TryParse(
                trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || !IsValidArrivalGrace(value))
        {
            return false;
        }
        minutes = value;
        return true;
    }

    /// <summary>
    /// The key for a given badge version, or null when that version is not
    /// configured. Lets a scanner or the server accept both the current and the
    /// previous key during a rotation window.
    /// </summary>
    public byte[]? KeyForVersion(int keyVersion)
    {
        if (keyVersion == BadgeKeyVersion && ResolveBadgeKey(BadgeKey) is { } current)
        {
            return current;
        }
        if (keyVersion == PreviousBadgeKeyVersion
            && ResolveBadgeKey(PreviousBadgeKey) is { } previous)
        {
            return previous;
        }
        return null;
    }

    /// <summary>Decodes a configured key, returning null for anything that is
    /// not a usable AES-256 key. Fail-closed: a malformed key disables badge
    /// handling rather than throwing at request time.</summary>
    private static byte[]? ResolveBadgeKey(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) { return null; }
        Span<byte> buffer = stackalloc byte[EventBadgeCodec.KeyBytes];
        return Convert.TryFromBase64String(configured, buffer, out var written)
            && written == EventBadgeCodec.KeyBytes
                ? buffer.ToArray()
                : null;
    }
}
