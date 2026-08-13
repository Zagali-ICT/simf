using SIMF.Common.Enums;

namespace SIMF.Application.AccessControl.Abstractions;

/// <summary>
/// Resolves a 12-char QR id to the gate engine's view of the holder.
/// The constraint
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
    /// Canonicalises a SCANNED code to the <c>UserProfile.QrId</c> the
    /// database actually stores.
    ///
    /// <para>A minted serial is returned normalised and unchanged. An encrypted
    /// offline badge is decrypted and mapped to its derived <c>W</c> id.
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

/// <summary>Domain view of the visitor a QR resolved to. Carries
/// every field the constraint engine needs to walk steps 6–11.</summary>
/// <param name="UserId">The holder's Identity account, or null when they have
/// none. Most attendees at a gate have none — a walk-in registration and a
/// pre-generated badge both produce a profile with no account — so this being
/// null is the ordinary case and never a reason to refuse entry.</param>
/// <param name="AccountState">Admission state, read from the PROFILE. It is the
/// profile that decides whether a person may enter, so this is populated for a
/// holder with no account exactly as it is for one with an account.</param>
/// <param name="IsLockedOut">Identity lockout, which can only be true when
/// <paramref name="UserId"/> is set: lockout is a sign-in control, so a holder
/// with no account cannot be locked out of a gate they never sign in to.</param>
public sealed record QrResolution(
    Guid UserProfileId,
    Guid? UserId,
    AccountState AccountState,
    bool IsLockedOut,
    Guid? ProfileTypeId,
    bool ProfileTypeActive,
    string? ProfileTypeName,
    string? ProfileTypeNameArabic,
    string? ProfileTypePageColor,
    string DisplayName,
    string DisplayNameArabic);
