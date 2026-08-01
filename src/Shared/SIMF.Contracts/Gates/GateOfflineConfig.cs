namespace SIMF.Contracts.Gates;

/// <summary>
/// D-811 — everything a scanner needs to judge a badge with **no network**,
/// fetched while online and cached on the device.
///
/// <para>Without this a scanner that loses the link can only queue scans and
/// show the operator nothing, which is the moment a gate stops being a gate.
/// With it the device decrypts the badge, checks the profile type against its
/// own gate's allow-list, shows a verdict, and still queues the scan so the
/// server records the authoritative one later.</para>
///
/// <para>The device's answer is ADVISORY. The server re-decides every queued
/// scan when it arrives, so a stale cache can let someone through who should
/// not have been — that is the accepted cost of a gate that keeps working, and
/// it is why the queued scan is still uploaded and reconciled.</para>
/// </summary>
/// <param name="BadgeKey">Base64 AES-256 badge key, or null when offline badges
/// are not armed. Null means the device cannot verify anything offline and falls
/// back to queueing without a verdict.</param>
/// <param name="BadgeKeyVersion">The version the key corresponds to. A badge
/// stamped with a different version is not one this device can open.</param>
/// <param name="PreviousBadgeKey">The previous key during a rotation window;
/// null outside one.</param>
/// <param name="PreviousBadgeKeyVersion">The version
/// <paramref name="PreviousBadgeKey"/> corresponds to.</param>
/// <param name="SessionWalkIn">True when the session-registration rule is
/// relaxed, so a hall door admits an attendee the device has no booking record
/// for.</param>
/// <param name="IssuedAt">When this snapshot was taken, so a device can show the
/// operator how stale its cache is.</param>
/// <param name="Gates">One entry per gate this operator is assigned to.</param>
public sealed record GateOfflineConfig(
    string? BadgeKey,
    int BadgeKeyVersion,
    string? PreviousBadgeKey,
    int PreviousBadgeKeyVersion,
    bool SessionWalkIn,
    DateTimeOffset IssuedAt,
    IReadOnlyList<GateOfflineRule> Gates);

/// <summary>D-811 — the offline rule for one gate.</summary>
/// <param name="GateId">The gate this rule applies to.</param>
/// <param name="Code">The gate's short code, for the operator's display.</param>
/// <param name="AllowedProfileTypeCodes">Profile-type CODES (the small number
/// the badge carries), not Guids — an offline device reads the code straight out
/// of the decrypted payload and has no way to resolve a Guid. An EMPTY list
/// means the gate admits every type, matching the server's rule.</param>
/// <param name="IsHallDoor">True when this gate is a hall door. A hall door
/// normally requires a booking, which an offline device cannot confirm.</param>
/// <param name="IsActive">False when the gate was switched off as of this
/// snapshot.</param>
public sealed record GateOfflineRule(
    Guid GateId,
    string Code,
    IReadOnlyList<short> AllowedProfileTypeCodes,
    bool IsHallDoor,
    bool IsActive);
