namespace SIMF.Common.Enums;

/// <summary>
/// D-148 — outcome of a single <c>GateScan</c>. Both values are recorded;
/// a denial does NOT raise an HTTP error — the request succeeded (the system
/// did what it was asked: scan + record). The denial reason rides in
/// <see cref="DenialReasonCode"/> on the response body.
/// </summary>
public enum ScanOutcome
{
    Allowed = 0,
    Denied = 1,
}
