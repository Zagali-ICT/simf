// Shared test helper for the id-space every attendee record is keyed by.
//
// HallAttendance, SeatReservation and ExhibitorVisitorScan are keyed by
// UserProfile.Id, not by the Identity account id, and each carries a REAL foreign
// key to it. A fixture can therefore no longer invent an attendee out of a random
// Guid: that is the point of the key, because production cannot invent one either.
//
// Two shapes are needed, and they are the two the system really produces:
//   - an attendee who signed up, so an account exists and a profile hangs off it
//     (EnsureForAccountAsync — most tests, whose helpers create the account only);
//   - an attendee with NO account at all: the walk-in admitted at the door and the
//     badge minted into a bulk order (CreateAccountlessAsync). Never give one of
//     these a Guid.Empty account id — null is the marker, and Guid.Empty is a
//     matches-nobody sentinel that would collide every such row with every other.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Api.Tests;

internal static class TestAttendeeProfiles
{
    /// <summary>The attendee-profile id for an existing account, creating the
    /// profile if the account's own test helper did not (most only create the
    /// Identity row). Idempotent, so a fixture may call it more than once.
    ///
    /// <para>It reads the STORE, so a profile the caller has Added but not yet
    /// saved is invisible here and this would add a second one — which the
    /// filtered unique index over UserId then rejects. A fixture that builds its
    /// own profile should keep that instance and use its <c>Id</c> instead of
    /// asking for it again.</para></summary>
    internal static async Task<Guid> EnsureForAccountAsync(
        SimfAppDbContext db, Guid userId, string? qrId = null)
    {
        var existing = await db.UserProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync();
        if (existing is { } found)
        {
            return found;
        }
        return await AddAsync(db, userId, qrId);
    }

    /// <summary>Null in, null out — for the seeders whose holder is optional,
    /// where null means an ADMIN BLOCK rather than a missing attendee. Keeping the
    /// null distinct is what stops a blocked seat being handed an attendee.</summary>
    internal static async Task<Guid?> EnsureForOptionalAccountAsync(
        SimfAppDbContext db, Guid? userId) =>
        userId is { } id ? await EnsureForAccountAsync(db, id) : null;

    /// <summary>Opens its own scope — for the many fixtures that hold only the
    /// factory at the point they need the profile id.</summary>
    internal static async Task<Guid> EnsureForAccountAsync(
        SimfApiFactory factory, Guid userId, string? qrId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await EnsureForAccountAsync(db, userId, qrId);
    }

    /// <summary>An attendee with NO Identity account — the walk-in and the
    /// bulk-minted badge. Approved, so the gate and the hall door admit them, and
    /// given a badge QR so the scan paths can resolve them.</summary>
    internal static Task<Guid> CreateAccountlessAsync(
        SimfAppDbContext db, string? qrId = null) =>
        AddAsync(db, userId: null, qrId);

    /// <summary>Scope-opening twin, for a fixture that holds only the factory.</summary>
    internal static async Task<Guid> CreateAccountlessAsync(
        SimfApiFactory factory, string? qrId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await CreateAccountlessAsync(db, qrId);
    }

    /// <summary>A badge QR id in the real minted shape: 12 characters of the
    /// Crockford base32 alphabet, which excludes 0, O, 1, I, L and U.</summary>
    internal static string NewQrId() =>
        string.Concat(Guid.NewGuid().ToString("N")
            .Where(c => "23456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(char.ToUpperInvariant(c)))
            .Select(char.ToUpperInvariant)
            .Take(12))
            .PadRight(12, 'Z');

    private static async Task<Guid> AddAsync(SimfAppDbContext db, Guid? userId, string? qrId)
    {
        var now = SimfClock.Now;
        var profileType = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Visitor " + Guid.NewGuid().ToString("N")[..8],
            NameArabic = "زائر",
            PageColor = "#0EA5E9",
            IsForVisitor = true,
            MobileAppRole = MobileAppRole.None,
            IsActive = true,
            CreatedAt = now,
        };
        db.ProfileTypes.Add(profileType);

        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProfileTypeId = profileType.Id,
            Name = "Attendee",
            NameArabic = "حاضر",
            NationalityId = 682,
            // Admission lives on the profile, and the door reads it here rather
            // than on any account — which is what lets an accountless attendee in.
            AdmissionState = AccountState.Approved,
            QrId = qrId,
            IsActive = true,
            CreatedAt = now,
        };
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile.Id;
    }
}
