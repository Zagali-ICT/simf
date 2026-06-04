using SIMF.Common.Enums;

namespace SIMF.Application.AccessControl.Abstractions;

/// <summary>
/// D-148 — resolves a 12-char QR id to the gate engine's view of the holder
/// (SIMF-FDS-003 §5.6 step 3; plan §11.1 reserved seam). The constraint
/// engine consumes <see cref="QrResolution"/>; the API resolver implementation
/// queries the existing <c>UserProfile</c> table.
///
/// Returns <c>null</c> when the QR resolves to nothing — that null drives
/// the engine to record a <c>QR_UNKNOWN</c> denial.
/// </summary>
public interface IQrResolver
{
    Task<QrResolution?> ResolveAsync(string qrId, CancellationToken cancellationToken = default);
}

/// <summary>D-148 — domain view of the visitor a QR resolved to. Carries
/// every field the constraint engine needs to walk steps 6–11.</summary>
public sealed record QrResolution(
    Guid UserProfileId,
    Guid UserId,
    AccountState AccountState,
    bool IsLockedOut,
    Guid? ProfileTypeId,
    bool ProfileTypeActive,
    string? ProfileTypeName,
    string? ProfileTypeNameArabic,
    string? ProfileTypePageColor,
    string DisplayName,
    string DisplayNameArabic);
