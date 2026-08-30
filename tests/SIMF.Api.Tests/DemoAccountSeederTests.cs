using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Files.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Covers <see cref="DemoAccountSeeder"/>, the demo <c>@simf.local</c> matrix the
/// integration suite creates for itself.
///
/// <para>These assertions used to live in <c>IdentitySeederTests</c>, because the
/// matrix used to be seeded by <c>IdentitySeeder</c> behind an environment gate.
/// It is not production code any more - a fixture has no business running inside
/// production startup - but it is still load-bearing for the suite: the gate
/// scanner, the moderation desk and the exhibitor lead-capture tests all sign in
/// as one of these accounts, and every one of them needs a COMPLETE profile (an
/// interest, an ID document, a face photo) before the app lets it past the
/// "complete your profile" wall. A silent regression here surfaces as a dozen
/// unrelated failures somewhere else, which is exactly what these pin.</para>
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class DemoAccountSeederTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public DemoAccountSeederTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    /// <summary>Re-runs the fixture's demo seed (idempotent), so each test
    /// asserts against a seed it performed rather than trusting the one the
    /// fixture ran in its constructor.</summary>
    private static Task SeedAsync(IServiceProvider services) =>
        DemoAccountSeeder.SeedAsync(services, SimfApiFactory.DemoAccountPassword);

    [Fact]
    public async Task SeedAsync_seeds_the_full_demo_account_matrix()
    {
        // One demo account per user type / profile type so every role is
        // testable from a fresh DB. Admin → Administrator role, no profile;
        // visitor/partner → an Approved profile with a minted QR badge. The
        // second run proves idempotency (no duplicate accounts / profiles).
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        await SeedAsync(scope.ServiceProvider);

        // The extra CP admin — Administrator role, no visitor profile.
        var admin = await users.FindByEmailAsync("admin@simf.local");
        Assert.NotNull(admin);
        Assert.Equal(UserType.Admin, admin!.UserType);
        Assert.True(await users.IsInRoleAsync(admin, "Administrator"));

        // A visitor — Approved, with an Approved profile carrying a QR badge
        // under the "Normal" profile type.
        var visitor = await users.FindByEmailAsync("visitor@simf.local");
        Assert.NotNull(visitor);
        Assert.Equal(UserType.Visitor, visitor!.UserType);
        Assert.Equal(AccountState.Approved, visitor.AccountState);
        var visitorProfile = database.UserProfiles.SingleOrDefault(p => p.UserId == visitor.Id);
        Assert.NotNull(visitorProfile);
        Assert.False(
            string.IsNullOrEmpty(visitorProfile!.QrId),
            "an Approved demo profile carries a QR badge");
        var normalType = database.ProfileTypes.Single(t => t.Id == visitorProfile.ProfileTypeId);
        Assert.Equal("Normal", normalType.Name);

        // A partner staff account resolves to the Staff app role via its profile type.
        var staff = await users.FindByEmailAsync("staff@simf.local");
        Assert.NotNull(staff);
        var staffProfile = database.UserProfiles.Single(p => p.UserId == staff!.Id);
        var staffType = database.ProfileTypes.Single(t => t.Id == staffProfile.ProfileTypeId);
        Assert.Equal(MobileAppRole.Staff, staffType.MobileAppRole);

        // All nine demo emails exist.
        foreach (var email in new[]
        {
            "admin@simf.local", "vvip@simf.local", "vip@simf.local", "visitor@simf.local",
            "staff@simf.local", "moderator@simf.local", "exhibitor@simf.local",
            "media@simf.local", "sponsor@simf.local",
        })
        {
            Assert.NotNull(await users.FindByEmailAsync(email));
        }

        // Idempotent — a second seed adds no duplicate demo profiles.
        //
        // Counted over the demo accounts' own user ids. The predicate here used to
        // be `p.NationalId.StartsWith("100000000")`, which asserted nothing at all:
        // NationalId was encrypted at rest, so EF translated the StartsWith into a
        // LIKE against ciphertext, matched no row either side of the re-seed, and
        // compared 0 to 0.
        var demoUserIds = await DemoProfileUserIdsAsync(users);
        var demoProfileCount = await database.UserProfiles
            .CountAsync(p => p.UserId != null && demoUserIds.Contains(p.UserId.Value));
        Assert.Equal(8, demoProfileCount);
        await SeedAsync(scope.ServiceProvider);
        Assert.Equal(
            demoProfileCount,
            await database.UserProfiles
                .CountAsync(p => p.UserId != null && demoUserIds.Contains(p.UserId.Value)));
    }

    [Fact]
    public async Task SeedAsync_writes_each_demo_national_id_as_a_document_row_with_a_digest()
    {
        // The old production seeder wrote the demo national id straight onto
        // UserProfile.NationalId, with no blind-index digest and no child row —
        // which would have silently dropped the number altogether once the column
        // went. It now writes through ProfileIdentityStorage, the same helper the
        // self-service upsert and the walk-in desk use.
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        await SeedAsync(scope.ServiceProvider);

        var demoUserIds = await DemoProfileUserIdsAsync(users);
        var documents = await database.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId != null && demoUserIds.Contains(p.UserId.Value))
            .SelectMany(p => p.IdentityDocuments)
            .ToListAsync();

        Assert.Equal(demoUserIds.Count, documents.Count);
        Assert.All(documents, document =>
        {
            Assert.Equal(IdentityDocumentKind.NationalId, document.Kind);
            // The digest the unique index and the guard both key off. A row
            // without one is in the table and invisible to both.
            Assert.Equal(64, document.NumberHash.Length);
            // Read back through the value converter, so the seeded number is the
            // plaintext the demo matrix declares.
            Assert.StartsWith("100000000", document.Number, StringComparison.Ordinal);
        });
    }

    /// <summary>The Identity user ids of the eight profile-carrying demo accounts
    /// (everything but the CP-only admin). Resolved by email because the profile
    /// row no longer carries a column a test can pattern-match on.</summary>
    private static async Task<List<Guid>> DemoProfileUserIdsAsync(UserManager<SimfUser> users)
    {
        string[] emails =
        [
            "vvip@simf.local", "vip@simf.local", "visitor@simf.local", "staff@simf.local",
            "moderator@simf.local", "exhibitor@simf.local", "media@simf.local",
            "sponsor@simf.local",
        ];
        var ids = new List<Guid>(emails.Length);
        foreach (var email in emails)
        {
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            ids.Add(user!.Id);
        }
        return ids;
    }

    [Fact]
    public async Task SeedAsync_makes_every_demo_account_profile_complete()
    {
        // BUG-022 regression — the Moderator and Exhibitor demo accounts could NEVER
        // be used in the app: the interest pass only linked interests
        // for visitor@ / vip@ / vvip@ / staff@, and IsProfileCompleteAsync demands
        // >= 1 interest, so those accounts stayed profileComplete=false no matter
        // what the tester uploaded. The same pass now also seeds the ID document and
        // the face photo (avatar) every demo profile needs, so ALL eight
        // profile-carrying demo accounts are usable straight after a fresh seed.
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profiles = scope.ServiceProvider
            .GetRequiredService<SIMF.Application.IdentityAccess.IUserProfileService>();

        await SeedAsync(scope.ServiceProvider);

        string[] demoProfileEmails =
        [
            "vvip@simf.local", "vip@simf.local", "visitor@simf.local", "staff@simf.local",
            "moderator@simf.local", "exhibitor@simf.local", "media@simf.local",
            "sponsor@simf.local",
        ];

        foreach (var email in demoProfileEmails)
        {
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.False(
                user!.AvatarFileId is null,
                $"{email} must carry a seeded face photo");

            var profile = await database.UserProfiles
                .Include(p => p.Interests)
                .SingleAsync(p => p.UserId == user.Id);
            Assert.False(
                profile.IdImageFileId is null,
                $"{email} must carry a seeded ID document");
            Assert.NotEmpty(profile.Interests);

            Assert.True(
                await profiles.IsProfileCompleteAsync(user.Id),
                $"{email} must be profileComplete out of the box");
        }

        // Idempotent — a re-seed uploads nothing new (the pointers stay put).
        // Selected by demo user id for the same reason as above: the old
        // NationalId LIKE predicate matched ciphertext and returned two empty
        // lists, which are trivially equal.
        var demoUserIds = await DemoProfileUserIdsAsync(users);
        var pointersBefore = await database.UserProfiles
            .Where(p => p.UserId != null && demoUserIds.Contains(p.UserId.Value))
            .OrderBy(p => p.Id)
            .Select(p => p.IdImageFileId)
            .ToListAsync();
        Assert.Equal(demoUserIds.Count, pointersBefore.Count);
        await SeedAsync(scope.ServiceProvider);
        var pointersAfter = await database.UserProfiles
            .Where(p => p.UserId != null && demoUserIds.Contains(p.UserId.Value))
            .OrderBy(p => p.Id)
            .Select(p => p.IdImageFileId)
            .ToListAsync();
        Assert.Equal(pointersBefore, pointersAfter);
    }

    [Fact]
    public async Task SeedAsync_repairs_a_demo_image_whose_bytes_have_gone()
    {
        // Regression — the image pass was documented "self-healing" but
        // skipped any account whose pointer was merely non-empty. A non-empty pointer
        // proves something was uploaded ONCE, not that it is still there, so the two
        // ways a store loses bytes underneath a healthy-looking pointer — the storage
        // root moves / the working folder is cleaned (bytes gone, row intact), and a
        // database is restored past its file store (row gone too) — both produced a
        // pointer no re-seed could ever repair. That is the "can't connect store/file"
        // 404 seen after a deployment reset.
        //
        // The second shape is now unreachable for the ID document, because its
        // pointer became a real foreign key into StoredFiles. This test asserts
        // that too, so the guarantee is pinned rather than assumed.
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<IFileService>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageProvider>();

        await SeedAsync(scope.ServiceProvider);

        var user = await users.FindByEmailAsync("visitor@simf.local");
        Assert.NotNull(user);
        var profile = await database.UserProfiles.SingleAsync(p => p.UserId == user!.Id);

        var avatarBefore = user!.AvatarFileId;
        var idDocumentBefore = profile.IdImageFileId;
        Assert.NotNull(avatarBefore);
        Assert.NotNull(idDocumentBefore);

        // Shape 1, for both pointers — the bytes vanish and the rows survive (a
        // moved storage root, a cleaned working folder).
        foreach (var fileId in new[] { avatarBefore!.Value, idDocumentBefore!.Value })
        {
            var storageKey = await database.StoredFiles.AsNoTracking()
                .Where(f => f.Id == fileId)
                .Select(f => f.StorageKey)
                .SingleAsync();
            await storage.DeleteAsync(storageKey!);
        }

        // Shape 2 — the row vanishing under a healthy-looking pointer — is no
        // longer reachable for the ID document, and that is a change worth
        // asserting rather than quietly dropping. UserProfiles.IdImageFileId is
        // now a real foreign key into StoredFiles, so the database refuses to
        // hold a pointer to a row that is not there. The seeder's repair still
        // has to handle shape 1; shape 2 has stopped being possible.
        profile.IdImageFileId = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        database.ChangeTracker.Clear();

        // The pre-condition the old guard could not see: both pointers still look
        // perfectly healthy — set, well-formed — and both resolve to nothing.
        Assert.False(await files.ContentExistsAsync(avatarBefore.Value));
        Assert.False(await files.ContentExistsAsync(idDocumentBefore.Value));

        await SeedAsync(scope.ServiceProvider);

        // Both are re-uploaded and re-pointed, and the new content is really there.
        var repairedUser = await users.FindByEmailAsync("visitor@simf.local");
        var repairedProfile = await database.UserProfiles
            .AsNoTracking().SingleAsync(p => p.UserId == user.Id);

        Assert.NotEqual(avatarBefore, repairedUser!.AvatarFileId);
        Assert.NotEqual(idDocumentBefore, repairedProfile.IdImageFileId);
        Assert.True(
            await files.ContentExistsAsync(repairedUser.AvatarFileId!.Value),
            "the re-seeded avatar must resolve to bytes that exist");
        Assert.True(
            await files.ContentExistsAsync(repairedProfile.IdImageFileId!.Value),
            "the re-seeded ID document must resolve to bytes that exist");

        // Still idempotent: a healthy pointer is left alone, so repair never becomes
        // a re-upload on every restart.
        await SeedAsync(scope.ServiceProvider);
        var afterThirdSeed = await database.UserProfiles
            .AsNoTracking().SingleAsync(p => p.UserId == user.Id);
        Assert.Equal(repairedProfile.IdImageFileId, afterThirdSeed.IdImageFileId);
    }
}
