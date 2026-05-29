namespace SIMF.Common.Enums;

/// <summary>
/// D-148 — the stable denial reason emitted by the gate constraint engine
/// (SIMF-FDS-003 §5.6.1, SIMF-API-GATES-001 §8.2). Each value is wire-stable
/// and once published does not change meaning. Localised display strings
/// live in <c>Strings.resx</c> / <c>Strings.ar.resx</c> and are picked by the
/// request's <c>Accept-Language</c>.
/// </summary>
public enum DenialReasonCode
{
    /// <summary>The QR resolved to no <c>UserProfile</c>.</summary>
    QrUnknown = 0,

    /// <summary>The gate was deactivated between request and engine
    /// validation. Recorded for forensic completeness.</summary>
    GateInactiveAtScan = 1,

    /// <summary>The visitor's account is not in <c>Approved</c> state.</summary>
    HolderNotApproved = 2,

    /// <summary>The visitor's account is <c>Disabled</c>.</summary>
    HolderDisabled = 3,

    /// <summary>The visitor's account is <c>Locked</c>.</summary>
    HolderLocked = 4,

    /// <summary>The visitor's <c>ProfileType</c> is <c>IsActive = false</c>.</summary>
    ProfileTypeInactive = 5,

    /// <summary>Reserved (engine step 9.5) — emitted only when the
    /// time-window feature ships in a later increment.</summary>
    OutsideTimeWindow = 6,

    /// <summary>The visitor's <c>ProfileType</c> is not in this gate's
    /// allow-list (or the allow-list filtered empty per L-15).</summary>
    ProfileTypeNotAllowed = 7,

    /// <summary>Reserved (engine step 11.5) — emitted only when the
    /// booking-required feature ships in a later increment.</summary>
    BookingRequiredMissing = 8,
}
