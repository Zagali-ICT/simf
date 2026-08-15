// CHAIN-3 (H-1) — proves the filtered UNIQUE indexes on the UserProfile
// blind-index hash columns (NationalIdHash / IqamaNumberHash / PassportNumberHash)
// are the hard backstop behind the walk-in duplicate-identity guard: two profiles
// sharing a hash collide, while null-hash rows coexist freely.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests;

public sealed class UserProfileIdentifierUniquenessTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public UserProfileIdentifierUniquenessTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Two_profiles_with_the_same_national_id_hash_violate_the_unique_index()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hash = "nid-" + Guid.NewGuid().ToString("N");

        db.UserProfiles.Add(NewProfile(nationalIdHash: hash));
        await db.SaveChangesAsync();

        db.UserProfiles.Add(NewProfile(nationalIdHash: hash));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_profiles_with_the_same_passport_hash_violate_the_unique_index()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hash = "pp-" + Guid.NewGuid().ToString("N");

        db.UserProfiles.Add(NewProfile(passportNumberHash: hash));
        await db.SaveChangesAsync();

        db.UserProfiles.Add(NewProfile(passportNumberHash: hash));
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
    public async Task Two_profiles_with_null_hashes_coexist()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        db.UserProfiles.Add(NewProfile());
        db.UserProfiles.Add(NewProfile());
        // No throw — the filtered indexes exclude NULL-hash rows.
        await db.SaveChangesAsync();
    }

    private static UserProfile NewProfile(
        string? nationalIdHash = null,
        string? iqamaNumberHash = null,
        string? passportNumberHash = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Uniqueness Test",
            NameArabic = "اختبار",
            NationalityId = 0,
            IsSaudi = true,
            NationalIdHash = nationalIdHash,
            IqamaNumberHash = iqamaNumberHash,
            PassportNumberHash = passportNumberHash,
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
