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

    /// <summary>
    /// D-811 — canonicalises a SCANNED code to the <c>UserProfile.QrId</c> the
    /// database actually stores.
    ///
    /// <para>A minted serial is returned normalised and unchanged. An encrypted
    /// offline badge (D-810) is decrypted and mapped to its derived <c>W</c> id.
    /// Anything that cannot be translated comes back normalised, so the caller's
    /// existing lookup misses and produces its usual not-found.</para>
    ///
    /// <para>For the surfaces that query <c>QrId</c> directly rather than going
    /// through <see cref="ResolveAsync"/> — the staff seating desk, the exhibitor
    /// lead scan and the Control Panel badge lookup. Without it those three see a
    /// ~61-character encrypted badge as an unknown code, and a walk-in standing
    /// at the seating desk cannot be found even though the walk-in seat hold was
    /// written for exactly that moment.</para>
    /// </summary>
    string ToStoredQrId(string scanned);
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
