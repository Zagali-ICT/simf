namespace SIMF.Common.Enums;

/// <summary>
/// Recorded direction of a single <c>GateScan</c>. Distinct from
/// <see cref="DirectionMode"/> (which is the gate-level policy) because a
/// Both-mode gate emits both values across the day.
/// </summary>
public enum ScanDirection
{
    /// <summary>Visitor entering the area controlled by this gate.</summary>
    CheckIn = 0,

    /// <summary>Visitor leaving the area controlled by this gate.</summary>
    CheckOut = 1,
}
