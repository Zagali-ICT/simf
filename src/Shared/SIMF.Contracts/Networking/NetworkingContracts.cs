using SIMF.Common.Enums;

namespace SIMF.Contracts.Networking;

/// <summary>B6 — D-224: body for <c>POST /api/v1/account/connections</c> —
/// the caller asks to connect with <see cref="TargetUserId"/>.</summary>
public sealed class SendConnectionRequest
{
    public Guid TargetUserId { get; set; }
}

/// <summary>B6 — D-224: the result of a request / accept action.</summary>
public sealed record ConnectionResult(
    Guid Id,
    Guid RequesterUserId,
    Guid TargetUserId,
    ConnectionState State,
    DateTimeOffset CreatedAt);

/// <summary>B6 — D-224: one row in the caller's connection list. Carries the
/// OTHER party (never the caller), their display name (resolved cross-DB), and
/// <see cref="IsIncoming"/> = the request was sent TO the caller.</summary>
public sealed record ConnectionRow(
    Guid Id,
    Guid OtherUserId,
    string OtherDisplayName,
    ConnectionState State,
    bool IsIncoming,
    DateTimeOffset CreatedAt);
