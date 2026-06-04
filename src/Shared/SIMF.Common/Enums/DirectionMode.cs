namespace SIMF.Common.Enums;

/// <summary>
/// D-148 — direction policy for a <c>Gate</c> (SIMF-FDS-003 §5.6 step 10).
/// Drives the operator console UI (fixed-direction button vs single Scan
/// with server-side inference) and the constraint engine's direction step.
/// </summary>
public enum DirectionMode
{
    /// <summary>The gate only accepts entry scans. Every recorded scan
    /// has <c>Direction = CheckIn</c>.</summary>
    In = 0,

    /// <summary>The gate only accepts exit scans. Every recorded scan
    /// has <c>Direction = CheckOut</c>.</summary>
    Out = 1,

    /// <summary>Both directions; the engine infers direction from the
    /// visitor's last allowed scan at this gate (cold start = CheckIn,
    /// alternation thereafter).</summary>
    Both = 2,
}
