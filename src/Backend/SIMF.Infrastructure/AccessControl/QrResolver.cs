// Tests: SIMF.Api.Tests/GateScanTests.cs, SIMF.Api.Tests/OfflineBadgeUploadTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Common.Badges;
using SIMF.Common.Options;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>Implements <see cref="IQrResolver"/> against
/// <c>UserProfile</c> + <c>SimfUser</c> + <c>ProfileType</c>. Because
/// UserProfile + ProfileType live on SimfAppDbContext, this is two
/// round-trips: App-DB lookup by QR id, then Identity-DB lookup by user
/// id. Both queries are PK / unique-index hits so total latency stays at
/// sub-millisecond.</summary>
internal sealed class QrResolver(
    SimfIdentityDbContext identityDbContext,
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider,
    IOptionsMonitor<WalkInModeOptions> walkInMode) : IQrResolver
{
    public async Task<QrResolution?> ResolveAsync(
        string qrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrId)) { return null; }
        var normalised = QrId.Normalise(qrId);
        var now = timeProvider.SimfNow();

        // An encrypted offline badge is not a QR id, so translate it to
        // one before the lookup. Branching on LENGTH rather than trying the
        // database first keeps the online path at exactly one query and byte
        // identical to before: every id the system mints is QrIdLength, and a
        // badge blob is about 54 characters.
        if (normalised.Length != OfflineBadgeId.QrIdLength
            && !TryTranslateEventBadge(normalised, now, out normalised))
        {
            return null;
        }

        var profileRow = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.QrId == normalised)
            .Select(profile => new
            {
                profile.Id,
                profile.UserId,
                profile.AdmissionState,
                profile.ProfileTypeId,
                profileTypeActive = profile.ProfileType != null && profile.ProfileType.IsActive,
                profileTypeName = profile.ProfileType != null ? profile.ProfileType.Name : null,
                profileTypeNameAr = profile.ProfileType != null ? profile.ProfileType.NameArabic : null,
                profileTypePageColor = profile.ProfileType != null ? profile.ProfileType.PageColor : null,
                profile.Name,
                profile.NameArabic,
                profile.BadgeBatchId,
                profile.EditionYear,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (profileRow is null) { return null; }

        // The Identity row is OPTIONAL and is read only for the two things it
        // alone knows: the lockout flag and the account's display name. Most
        // holders at a gate have no account — a walk-in registration and a
        // pre-generated badge both produce a profile without one — so a missing
        // user is the ordinary case, NOT a failed resolution.
        //
        // This used to query unconditionally and return null when it found
        // nothing, which turned a valid approved badge into a QR_UNKNOWN denial
        // before the approval checks ever ran: the holder was told their badge
        // was not recognised, and no amount of approving them would have fixed
        // it. Admission is read from the profile above, which is the row that
        // exists for every attendee.
        var userRow = profileRow.UserId is null
            ? null
            : await identityDbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == profileRow.UserId)
                .Select(user => new
                {
                    user.LockoutEnd,
                    user.DisplayName,
                })
                .SingleOrDefaultAsync(cancellationToken);

        return new QrResolution(
            profileRow.Id,
            profileRow.UserId,
            profileRow.AdmissionState,
            userRow?.LockoutEnd != null && userRow.LockoutEnd > now,
            profileRow.ProfileTypeId,
            profileRow.profileTypeActive,
            profileRow.profileTypeName,
            profileRow.profileTypeNameAr,
            profileRow.profileTypePageColor,
            // Falls back to the profile's own name, which is what a badge is
            // printed from and what an operator sees on the paper in front of
            // them; the account display name is only richer when there is one.
            userRow?.DisplayName ?? profileRow.Name,
            profileRow.NameArabic,
            profileRow.BadgeBatchId,
            profileRow.EditionYear);
    }

    public string ToStoredQrId(string scanned)
    {
        var normalised = QrId.Normalise(scanned ?? string.Empty);
        if (normalised.Length == OfflineBadgeId.QrIdLength) { return normalised; }
        return TryTranslateEventBadge(normalised, timeProvider.SimfNow(), out var translated)
            ? translated
            : normalised;
    }

    /// <summary>
    /// Decrypts an offline badge and returns the QR id it stands for.
    /// False for anything that is not a badge this server can open, which the
    /// caller turns into the same <c>QR_UNKNOWN</c> denial an unrecognised code
    /// has always produced: a scan is never an oracle for which keys are loaded.
    ///
    /// <para>The payload's profile-type code is deliberately IGNORED here. It is
    /// there for the scanner's offline allowed-at-this-gate decision; online, the
    /// ProfileType on the holder's record is authoritative and is what the
    /// constraint engine checks, so a badge printed with a stale code cannot
    /// widen access.</para>
    /// </summary>
    private bool TryTranslateEventBadge(
        string encoded, DateTime now, out string qrId)
    {
        qrId = string.Empty;
        var options = walkInMode.CurrentValue;
        if (!options.AcceptOfflineBadgesActive(now)) { return false; }
        if (encoded.Length > EventBadgeCodec.MaxEncodedLength) { return false; }
        if (!EventBadgeCodec.TryReadKeyVersion(encoded, out var keyVersion)) { return false; }
        if (options.KeyForVersion(keyVersion) is not { } key) { return false; }
        if (!EventBadgeCodec.TryDecode(encoded, key, out var payload)) { return false; }
        return OfflineBadgeId.TryFormat(payload.Sequence, out qrId);
    }
}

/// <summary>Canonical form of a QR id. Trim + upper-case; the QR is
/// case-insensitive on every scan path.</summary>
internal static class QrId
{
    public static string Normalise(string raw) =>
        (raw ?? string.Empty).Trim().ToUpperInvariant();
}
