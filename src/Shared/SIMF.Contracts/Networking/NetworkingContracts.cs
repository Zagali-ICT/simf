using SIMF.Common.Enums;

namespace SIMF.Contracts.Networking;

/// <summary>Body for <c>POST /api/v1/app/account/connections</c> —
/// the caller asks to connect with <see cref="TargetUserId"/>.</summary>
public sealed class SendConnectionRequest
{
    public Guid TargetUserId { get; set; }
}

/// <summary>The result of a request / accept action.</summary>
public sealed record ConnectionResult(
    Guid Id,
    Guid RequesterUserId,
    Guid TargetUserId,
    ConnectionState State,
    DateTime CreatedAt);

/// <summary>One row in the caller's connection list. Carries the
/// OTHER party (never the caller), their display name (resolved cross-DB), and
/// <see cref="IsIncoming"/> = the request was sent TO the caller.</summary>
public sealed record ConnectionRow(
    Guid Id,
    Guid OtherUserId,
    string OtherDisplayName,
    ConnectionState State,
    bool IsIncoming,
    DateTime CreatedAt);
