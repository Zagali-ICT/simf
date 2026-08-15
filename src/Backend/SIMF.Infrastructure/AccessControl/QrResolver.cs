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

        // An encrypted badge decrypts straight to the attendee it belongs to, so
        // it is a PRIMARY KEY seek rather than an index probe on the printed
        // serial. It cannot be a lookup by value: the codec draws a fresh nonce
        // per call, deliberately, so two encodings of one payload differ and no
        // stored ciphertext would ever match a scan.
        //
        // Branching on LENGTH keeps the plain-serial path at exactly one query:
        // every serial the system mints is QrIdLength, and a badge blob is 78
        // characters.
        Guid? badgeProfileId = null;
        if (normalised.Length != OfflineBadgeId.QrIdLength)
        {
            if (!TryReadEventBadge(normalised, now, out var decoded)) { return null; }
            badgeProfileId = decoded.ProfileId;
        }

        var profileRow = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => badgeProfileId != null
                ? profile.Id == badgeProfileId
                : profile.QrId == normalised)
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
            // The profile name wins. It is what the badge is printed from and
            // what the operator sees on the paper in front of them, and the
            // profile is the attendee record — the account may not exist
            // at all for a walk-in. SimfUser.DisplayName serves the greeting and
            // nothing else, and can still hold a sign-up placeholder.
            profileRow.Name,
            profileRow.NameArabic,
            // Kept from main, and not optional: BadgeBatchId is what the badge
            // self-claim guard reads to tell a bulk-order badge from a walk-in's,
            // and EditionYear is the only expiry a minted QR has.
            profileRow.BadgeBatchId,
            profileRow.EditionYear);
    }

    public string ToStoredQrId(string scanned)
    {
        var normalised = QrId.Normalise(scanned ?? string.Empty);
        if (normalised.Length == OfflineBadgeId.QrIdLength) { return normalised; }
        if (!TryReadEventBadge(normalised, timeProvider.SimfNow(), out var decoded))
        {
            // Not translatable, so it comes back normalised and the caller's own
            // lookup misses in its usual way.
            return normalised;
        }
        // The surfaces that query QrId directly want the stored serial, which
        // only the attendee row knows. A synchronous lookup would need a second
        // round-trip here, so return the id in its canonical text form and let
        // those callers match on the profile instead.
        return appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == decoded.ProfileId)
            .Select(profile => profile.QrId)
            .SingleOrDefault() ?? normalised;
    }

    /// <summary>
    /// Decrypts a badge to the attendee it names. False for anything that is not
    /// a badge this server can open, which the caller turns into the same
    /// <c>QR_UNKNOWN</c> denial an unrecognised code has always produced: a scan
    /// is never an oracle for which keys are loaded.
    ///
    /// <para>The payload's profile-type code and edition year are deliberately
    /// IGNORED here. They are there for the SCANNER's offline decision; online,
    /// the attendee's own record is authoritative and is what the constraint
    /// engine checks, so a badge printed with a stale code or year cannot widen
    /// access — it can only be refused by the live check.</para>
    /// </summary>
    private bool TryReadEventBadge(
        string encoded, DateTime now, out EventBadgePayload payload)
    {
        payload = default;
        var options = walkInMode.CurrentValue;
        if (!options.AcceptOfflineBadgesActive(now)) { return false; }
        if (encoded.Length > EventBadgeCodec.MaxEncodedLength) { return false; }
        if (!EventBadgeCodec.TryReadKeyVersion(encoded, out var keyVersion)) { return false; }
        if (options.KeyForVersion(keyVersion) is not { } key) { return false; }
        return EventBadgeCodec.TryDecode(encoded, key, out payload);
    }
}

/// <summary>Canonical form of a QR id. Trim + upper-case; the QR is
/// case-insensitive on every scan path.</summary>
internal static class QrId
{
    public static string Normalise(string raw) =>
        (raw ?? string.Empty).Trim().ToUpperInvariant();
}
