using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// H16 — D-071: prove the H2 (D-057) transaction actually rolls back.
/// `UserProfileService.UpsertMineAsync` wraps three writes in
/// `ITransactionRunner.ExecuteAsync` — profile save, `AccountState`
/// flip, refresh-token revoke. If the third write throws, the first
/// two must roll back. The H2 happy-path test only proves the all-
/// succeed case; this test injects a failure on the refresh-token
/// revoke and asserts the AccountState stayed at EmailVerified and
/// no UserProfile row was persisted.
/// </summary>
public sealed class ThrowingRefreshTokenApiFactory : SimfApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            // Replace ONLY the repo seen by UserProfileService's transactional
            // path — RevokeAllForUserAsync is what we want to fail. The other
            // methods (sign-in, refresh) need to keep working so the
            // surrounding test machinery (sign-in to get a token) succeeds.
            services.AddScoped<IRefreshTokenRepository>(serviceProvider =>
            {
                var inner = ActivatorUtilities.CreateInstance<RealRepo>(serviceProvider);
                return new RevokeAllThrowingRefreshTokenRepository(inner);
            });
        });
    }

    private sealed class RealRepo : IRefreshTokenRepository
    {
        // A passthrough into the actual repo by reaching for the registered
        // concrete type wouldn't work — the registration above replaces the
        // interface. Instead inline the same DbContext-backed shape used by
        // the production RefreshTokenRepository for the methods we need to
        // keep working.
        private readonly SimfIdentityDbContext _db;
        public RealRepo(SimfIdentityDbContext db) { _db = db; }

        public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        {
            _db.RefreshTokens.Add(token);
            await _db.SaveChangesAsync(ct);
        }
        public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
            _db.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);
        public async Task UpdateAsync(RefreshToken token, CancellationToken ct = default)
        {
            _db.RefreshTokens.Update(token);
            await _db.SaveChangesAsync(ct);
        }
        public Task RevokeAllForUserAsync(Guid userId, DateTime revokedAt, CancellationToken ct = default) =>
            _db.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.RevokedAt, revokedAt), ct);
        public Task<int> RevokeIfActiveAsync(Guid tokenId, DateTime revokedAt, CancellationToken ct = default) =>
            _db.RefreshTokens
                .Where(rt => rt.Id == tokenId && rt.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.RevokedAt, revokedAt), ct);
    }

    private sealed class RevokeAllThrowingRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly IRefreshTokenRepository _inner;
        public RevokeAllThrowingRefreshTokenRepository(IRefreshTokenRepository inner) { _inner = inner; }

        public Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
            _inner.AddAsync(token, ct);
        public Task<RefreshToken?> GetByTokenHashAsync(string hash, CancellationToken ct = default) =>
            _inner.GetByTokenHashAsync(hash, ct);
        public Task UpdateAsync(RefreshToken token, CancellationToken ct = default) =>
            _inner.UpdateAsync(token, ct);
        public Task RevokeAllForUserAsync(Guid userId, DateTime revokedAt, CancellationToken ct = default) =>
            throw new InvalidOperationException("Test: refresh-token revoke fails");
        public Task<int> RevokeIfActiveAsync(Guid tokenId, DateTime revokedAt, CancellationToken ct = default) =>
            _inner.RevokeIfActiveAsync(tokenId, revokedAt, ct);
    }
}

[Trait(TestAreas.TraitName, TestAreas.Profiles)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class UserProfileRollbackTests : IClassFixture<ThrowingRefreshTokenApiFactory>
{
    private readonly ThrowingRefreshTokenApiFactory _factory;
    private readonly HttpClient _client;

    public UserProfileRollbackTests(ThrowingRefreshTokenApiFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task First_save_transaction_rolls_back_when_the_refresh_token_revoke_throws()
    {
        // Create an EmailVerified visitor, sign in, get a token.
        var email = $"rb-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Rollback Test",
                AccountState = AccountState.EmailVerified,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, "Zx9#mKp2!");
            userId = user.Id;
        }
        var signIn = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = "Zx9#mKp2!",
                Audience = SignInAudience.Web,
            });
        var signInBody = (await signIn.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        var token = signInBody.Data!.Tokens!.AccessToken;

        // Seed one Interest + one Organisation so the upsert validator passes
        // (B3 — D-221 made organisation required) and the fault under test
        // (refresh-token revoke) is the only thing that throws.
        Guid interestId;
        Guid organisationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var interest = new UserInterest
            {
                Id = Guid.NewGuid(),
                Name = $"Rollback {Guid.NewGuid():N}",
                NameArabic = "اختبار",
                DisplayOrder = 0,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            appDb.Interests.Add(interest);
            var organisation = new SIMF.Domain.Organisations.Organisation
            {
                Id = Guid.NewGuid(),
                NameArabic = "جهة اختبار",
                Name = $"Rollback Org {Guid.NewGuid():N}",
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            appDb.Organisations.Add(organisation);
            await db.SaveChangesAsync();
            await appDb.SaveChangesAsync();
            interestId = interest.Id;
            organisationId = organisation.Id;
        }

        // Upsert profile — the EmailVerified → PendingApproval auto-
        // transition will fire, which tries to revoke refresh tokens,
        // which the stubbed repo throws on. The transaction must roll
        // back: no UserProfile row, AccountState stays EmailVerified.
        var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/app/account/user-profile")
        {
            Content = JsonContent.Create(new UpsertUserProfileRequest
            {
                InterestIds = new List<Guid> { interestId },
                ArabicName = "محمد عبدالله أحمد الزهراني",
                EnglishName = "Rollback Test User Account",
                NationalityCode = "SA",
                DateOfBirth = new DateOnly(1990, 1, 1),
                PlaceOfBirth = "Riyadh",
                IsSaudi = true,
                NationalId = "1101798278",
                OrganisationId = organisationId,
                // DEF-PHN-004 — the mobile is required on the upsert now.
                SaudiMobile = "0501234567",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        // The endpoint surfaces a 500 (the global error handler wraps the
        // InvalidOperationException). The contract under test is the
        // rollback, NOT the response shape.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            // AccountState stayed at EmailVerified — the flip rolled back.
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            Assert.Equal(AccountState.EmailVerified, user.AccountState);
            Assert.Null(user.StateChangedAt);

            // No UserProfile row was persisted — the save rolled back too.
            var profile = await appDb.UserProfiles
                .SingleOrDefaultAsync(p => p.UserId == userId);
            Assert.Null(profile);
        }
    }
}
