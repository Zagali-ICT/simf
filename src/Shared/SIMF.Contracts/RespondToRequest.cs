using SIMF.Common.Enums;

namespace SIMF.Contracts;

/// <summary>
/// The shared body of every admin "respond to a Pending request" action — move
/// the row to <see cref="MeetingRequestStatus.Accepted"/> / Rejected with an
/// optional note. Each feature's <c>RespondTo…Request</c> inherits this so the
/// route-binding endpoint can still add its own <c>Id</c> (the D-168 pattern)
/// while the <c>Status</c> / <c>ResponseNote</c> shape is declared exactly once.
/// Admin-only (CP + admin API); not part of the mobile wire contract.
/// </summary>
public class RespondToRequest
{
    public MeetingRequestStatus Status { get; set; }
    public string? ResponseNote { get; set; }
}
