// Tests: SIMF.Infrastructure/Identity/RetentionPurgeService.cs
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.AccessControl;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests;

/// <summary>
/// A4 (D-592) — the retention purge removes security artifacts past their
/// retention window (dead) and leaves live ones untouched, across both the
/// Identity DB (refresh tokens, 2FA tickets, account codes) and the App DB
/// (scan-idempotency records).
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class RetentionPurgeServiceTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public RetentionPurgeServiceTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task PurgeExpired_removes_dead_artifacts_and_keeps_live_ones()
    {
        var now = SimfClock.Now;
        var gateId = Guid.NewGuid();
        // Unique markers so a shared fixture DB / parallel classes don't interfere.
        var rtDead = $"rt-dead-{Guid.NewGuid():N}";
        var rtLive = $"rt-live-{Guid.NewGuid():N}";
        var sfDead = $"sf-dead-{Guid.NewGuid():N}";
        var sfLive = $"sf-live-{Guid.NewGuid():N}";
        // AccountCode.Code stores a fixed-length hash (max 16 chars) — keep the
        // markers within that, distinguishable by the leading "ad"/"al".
        var acDead = $"ad{Guid.NewGuid():N}"[..16];
        var acLive = $"al{Guid.NewGuid():N}"[..16];
        var scanDead = $"scan-dead-{Guid.NewGuid():N}";
        var scanLive = $"scan-live-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var userId = identity.Users.OrderBy(u => u.Email).First().Id;

            // Refresh tokens: expired > 7d (dead) vs expiring in 24h (live).
            identity.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(), UserId = userId, TokenHash = rtDead,
                CreatedAt = now.AddDays(-30), ExpiresAt = now.AddDays(-8),
            });
            identity.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(), UserId = userId, TokenHash = rtLive,
                CreatedAt = now, ExpiresAt = now.AddHours(24),
            });

            // 2FA tickets: expired > 1d (dead) vs live (5-min window).
            identity.SecondFactorTokens.Add(new SecondFactorToken
            {
                Id = Guid.NewGuid(), UserId = userId, TokenHash = sfDead,
                Kind = SecondFactorKind.EmailOtp,
                CreatedAt = now.AddDays(-2), ExpiresAt = now.AddDays(-2).AddMinutes(5),
            });
            identity.SecondFactorTokens.Add(new SecondFactorToken
            {
                Id = Guid.NewGuid(), UserId = userId, TokenHash = sfLive,
                Kind = SecondFactorKind.EmailOtp,
                CreatedAt = now, ExpiresAt = now.AddMinutes(5),
            });

            // Account codes: expired > 1d (dead) vs live (10-min window).
            identity.AccountCodes.Add(new AccountCode
            {
                Id = Guid.NewGuid(), UserId = userId, Purpose = AccountCodePurpose.SignInOtp,
                Code = acDead, CreatedAt = now.AddDays(-2),
                ExpiresAt = now.AddDays(-2).AddMinutes(10),
            });
            identity.AccountCodes.Add(new AccountCode
            {
                Id = Guid.NewGuid(), UserId = userId, Purpose = AccountCodePurpose.SignInOtp,
                Code = acLive, CreatedAt = now, ExpiresAt = now.AddMinutes(10),
            });
            await identity.SaveChangesAsync();

            // Scan idempotency: stored > 2d (dead) vs fresh (live).
            app.ScanIdempotencies.Add(new ScanIdempotency
            {
                Key = scanDead, GateId = gateId, RequestHash = "rh",
                StoredAt = now.AddDays(-3),
            });
            app.ScanIdempotencies.Add(new ScanIdempotency
            {
                Key = scanLive, GateId = gateId, RequestHash = "rh",
                StoredAt = now,
            });
            await app.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IRetentionPurgeService>();
            var result = await service.PurgeExpiredAsync();

            // At least our four dead rows were removed (a shared DB may add more).
            Assert.True(result.RefreshTokens >= 1);
            Assert.True(result.SecondFactorTokens >= 1);
            Assert.True(result.AccountCodes >= 1);
            Assert.True(result.ScanIdempotencies >= 1);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            // Dead rows are gone; live rows survive.
            Assert.DoesNotContain(identity.RefreshTokens, t => t.TokenHash == rtDead);
            Assert.Contains(identity.RefreshTokens, t => t.TokenHash == rtLive);
            Assert.DoesNotContain(identity.SecondFactorTokens, t => t.TokenHash == sfDead);
            Assert.Contains(identity.SecondFactorTokens, t => t.TokenHash == sfLive);
            Assert.DoesNotContain(identity.AccountCodes, c => c.Code == acDead);
            Assert.Contains(identity.AccountCodes, c => c.Code == acLive);
            Assert.DoesNotContain(app.ScanIdempotencies, r => r.Key == scanDead);
            Assert.Contains(app.ScanIdempotencies, r => r.Key == scanLive);
        }
    }
}
