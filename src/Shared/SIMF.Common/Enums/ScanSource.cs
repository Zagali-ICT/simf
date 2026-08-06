namespace SIMF.Common.Enums;

/// <summary>
/// Origin of a single <c>GateScan</c>. Drives both the audit trail
/// and the rate-limit posture (Simulator is dev-only and gets relaxed
/// limits; Kiosk authenticates via the future device API-key path).
/// </summary>
public enum ScanSource
{
    /// <summary>The Control Panel scan simulator (dev / staging only).</summary>
    Simulator = 0,

    /// <summary>The Flutter mobile app operator console.</summary>
    MobileApp = 1,

    /// <summary>A fixed kiosk device (future GateDevice path).</summary>
    Kiosk = 2,
}
