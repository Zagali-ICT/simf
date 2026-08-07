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
}
