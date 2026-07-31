namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Attendees roster (D-134 Sprint A —
/// read-only view over the existing <c>UserProfile</c> + <c>SimfUser</c>
/// + <c>ProfileType</c> tables; no schema change). Combined view across
/// every Approved Visitor + Other; admins are excluded.</summary>
public sealed record AdminAttendeeSummary(
    Guid UserId,
    string Email,
    string DisplayName,
    string UserType,
    Guid? ProfileTypeId,
    string? ProfileTypeName,
    string? ProfileTypeNameArabic,
    string AccountState,
    string? QrId,
    DateTime CreatedAt);
