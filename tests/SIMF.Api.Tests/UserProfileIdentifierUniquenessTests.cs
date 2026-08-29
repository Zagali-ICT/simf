// Identity documents no longer collide ACROSS profiles. The unique digest index
// on ProfileIdentityDocument.NumberHash - and the soft read-then-insert guard in
// front of it on both write paths - were removed on owner instruction
// (2026-08-29): a visitor whose number was already on some earlier profile could
// not register at all, and the desk had no way to release it.
//
// What survives, and is pinned below, is the OTHER unique index on the same
// entity: (ProfileId, Kind). One person still holds at most one national ID, one
// Iqama and one passport - without it the child table would happily hold two
// passports for one profile and the read path would have to pick one.
//
// This file previously proved the opposite of its first two tests. They are
// inverted rather than deleted: a removed constraint needs a regression test
// saying it is gone, or the next reader restores it as an obvious omission.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Two_profiles_with_the_same_document_digest_now_coexist()
    {
        // The same-kind repeat that used to be rejected outright.
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
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Set<ProfileIdentityDocument>()
            .CountAsync(document => document.NumberHash == hash));
    }

    [Fact]
    public async Task Two_profiles_presenting_one_number_under_DIFFERENT_kinds_now_coexist()
    {
        // The cross-kind case: someone registers on a passport and returns with an
        // Iqama carrying the same number. Driven at the DbContext because the
        // validator's document shapes are pairwise disjoint, so no single value can
        // be POSTed as two different kinds.
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
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Set<ProfileIdentityDocument>()
            .CountAsync(document => document.NumberHash == sharedHash));
    }

    [Fact]
    public async Task One_profile_still_cannot_hold_two_documents_of_the_same_kind()
    {
        // The surviving constraint. This one was never the registration blocker:
        // it bounds a SINGLE profile, and it is what keeps the read path from
        // having to choose between two passports.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var profile = NewProfile();
        profile.IdentityDocuments.Add(NewDocument(
            IdentityDocumentKind.Passport, "pp-" + Guid.NewGuid().ToString("N")));
        profile.IdentityDocuments.Add(NewDocument(
            IdentityDocumentKind.Passport, "pp-" + Guid.NewGuid().ToString("N")));
        db.UserProfiles.Add(profile);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_profiles_with_different_document_digests_coexist()
    {
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
    public async Task Two_profiles_holding_no_document_at_all_coexist()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        db.UserProfiles.Add(NewProfile());
        db.UserProfiles.Add(NewProfile());
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
