// CHAIN-3 (H-1) — proves the unique digest index on ProfileIdentityDocument is the
// hard backstop behind the walk-in duplicate-identity guard: two profiles sharing a
// document digest collide, while profiles holding no document coexist freely.
//
// This used to test three filtered UNIQUE indexes on UserProfile's NationalIdHash /
// IqamaNumberHash / PassportNumberHash columns. Those are gone, and the single
// digest index that replaced them is strictly stronger: the three could only ever
// compare like with like, so the cross-kind case below passed all of them.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Profiles)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class UserProfileIdentifierUniquenessTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public UserProfileIdentifierUniquenessTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Two_profiles_with_the_same_document_digest_violate_the_unique_index()
    {
        // The same-kind repeat the three per-kind indexes used to catch. It is
        // still caught, by the one index that replaced them.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hash = "nid-" + Guid.NewGuid().ToString("N");

        var first = NewProfile();
        first.IdentityDocuments.Add(NewDocument(IdentityDocumentKind.NationalId, hash));
        db.UserProfiles.Add(first);
        await db.SaveChangesAsync();

        var second = NewProfile();
        second.IdentityDocuments.Add(NewDocument(IdentityDocumentKind.NationalId, hash));
        db.UserProfiles.Add(second);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_profiles_presenting_one_number_under_DIFFERENT_kinds_violate_the_digest_index()
    {
        // THE case the three per-kind indexes on UserProfile cannot see, and the
        // reason the documents moved to a child table with ONE index over every
        // digest: a person registers on a passport and comes back with an Iqama
        // carrying the same number. The two digests land in different columns up
        // there, so neither index ever compares them; here they land in the same
        // column and collide.
        //
        // Driven at the DbContext rather than through the API on purpose. The
        // validator's document shapes are pairwise disjoint (national id 10 digits
        // from 1, Iqama 10 digits from 2, passport 6-9 alphanumerics), so no single
        // value can be POSTed as two different kinds — the collision is reachable
        // only below the validator, which is exactly where the index has to hold.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var sharedHash = "xkind-" + Guid.NewGuid().ToString("N");

        var onPassport = NewProfile();
        onPassport.IdentityDocuments.Add(NewDocument(IdentityDocumentKind.Passport, sharedHash));
        db.UserProfiles.Add(onPassport);
        await db.SaveChangesAsync();

        var onIqama = NewProfile();
        onIqama.IdentityDocuments.Add(NewDocument(IdentityDocumentKind.Iqama, sharedHash));
        db.UserProfiles.Add(onIqama);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_profiles_with_different_document_digests_coexist()
    {
        // The other half of the rule: the index must not reject two people who
        // simply hold documents of the same kind with different numbers.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var first = NewProfile();
        first.IdentityDocuments.Add(NewDocument(
            IdentityDocumentKind.Passport, "pp-" + Guid.NewGuid().ToString("N")));
        var second = NewProfile();
        second.IdentityDocuments.Add(NewDocument(
            IdentityDocumentKind.Passport, "pp-" + Guid.NewGuid().ToString("N")));

        db.UserProfiles.Add(first);
        db.UserProfiles.Add(second);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_index_violation_is_translated_into_a_409_and_not_left_as_a_500()
    {
        // The soft duplicate-identity guard is a non-atomic read-then-insert, so
        // the unique digest index is the real constraint and the catch filter in
        // UserProfileRepository.SaveProfileIdentityChangesAsync is what turns its
        // violation into the same 409 every other path answers with.
        //
        // That filter matches index names AS STRINGS. It used to list the three
        // per-kind IX_UserProfiles_*Hash names beside the child one; those indexes
        // are gone, and if the surviving name had gone with them a duplicate would
        // now surface as an uncaught 500. Driven at the repository rather than
        // through the API because the soft guard is cross-kind and catches every
        // duplicate a request can express — only a lost race reaches the index,
        // and this is that race made deterministic.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // Same scope, so the repository holds THIS context instance and saves the
        // rows added below.
        var repository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var hash = "race-" + Guid.NewGuid().ToString("N");

        var first = NewProfile();
        first.IdentityDocuments.Add(NewDocument(IdentityDocumentKind.NationalId, hash));
        db.UserProfiles.Add(first);
        await db.SaveChangesAsync();

        var loser = NewProfile();
        loser.IdentityDocuments.Add(NewDocument(IdentityDocumentKind.Iqama, hash));
        db.UserProfiles.Add(loser);

        var thrown = await Assert.ThrowsAsync<ApiException>(
            () => repository.SaveProfileIdentityChangesAsync());
        Assert.Equal(409, thrown.StatusCode);
        Assert.Equal(ErrorCodes.DuplicateIdentity, thrown.Code);
    }

    [Fact]
    public async Task Two_profiles_holding_no_document_at_all_coexist()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        db.UserProfiles.Add(NewProfile());
        db.UserProfiles.Add(NewProfile());
        // No throw. The digest index is unfiltered, but it indexes a table these
        // two profiles have no row in — which is what replaced the filtered
        // indexes' job of exempting the many null-hash profile rows.
        await db.SaveChangesAsync();
    }

    private static UserProfile NewProfile() =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Uniqueness Test",
            NameArabic = "اختبار",
            NationalityId = 0,
            IsSaudi = true,
            CreatedAt = SimfClock.Now,
        };

    /// <summary>A child document row. Id and ProfileId are left UNSET
    /// deliberately: a populated key reads to EF's change tracker as "this row
    /// already exists", which turns the insert into an UPDATE that matches nothing
    /// and kills the save with a concurrency exception. Relationship fixup fills
    /// ProfileId from the parent.</summary>
    private static ProfileIdentityDocument NewDocument(
        IdentityDocumentKind kind, string numberHash) =>
        new()
        {
            Kind = kind,
            Number = "number-" + Guid.NewGuid().ToString("N"),
            NumberHash = numberHash,
            CreatedAt = SimfClock.Now,
        };
}
