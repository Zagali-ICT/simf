// DEF-MOD-005 — shared seeding for the per-session moderator grant tests.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Api.Tests;

/// <summary>
/// DEF-MOD-005 — an admin may only grant a moderation desk to an account that is
/// ELIGIBLE to moderate: approved (Identity DB) AND carrying a partner profile
/// type whose <see cref="MobileAppRole"/> is <see cref="MobileAppRole.Moderator"/>
/// (App DB). Both facts are seeded here so the assign tests exercise the real
/// path instead of a bare approved visitor.
/// </summary>
internal static class SessionModeratorSeed
{
    /// <summary>Creates an approved account that IS eligible to moderate.</summary>
    public static Task<Guid> CreateEligibleModeratorAsync(SimfApiFactory factory) =>
        CreateAccountAsync(factory, eligible: true);

    /// <summary>Creates an approved account that is NOT eligible (a plain
    /// visitor with no operational profile type) — the exact account the old
    /// free-text GUID box could hand a moderation desk to by a typo.</summary>
    public static Task<Guid> CreatePlainVisitorAsync(SimfApiFactory factory) =>
        CreateAccountAsync(factory, eligible: false);

    private static async Task<Guid> CreateAccountAsync(
        SimfApiFactory factory, bool eligible)
    {
        var email = $"mod-{Guid.NewGuid():N}@simf.test";
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = "Mod",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        if (!eligible)
        {
            return user.Id;
        }

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // The IdentitySeeder ships the canonical "Moderator" partner type
        // (isVisitor=false, MobileAppRole.Moderator) — reuse it rather than
        // inventing a second one.
        var moderatorType = await appDb.ProfileTypes
            .SingleOrDefaultAsync(p => p.MobileAppRole == MobileAppRole.Moderator
                && !p.IsForVisitor);
        if (moderatorType is null)
        {
            moderatorType = new UserProfileType
            {
                Id = Guid.NewGuid(),
                Name = "Moderator", NameArabic = "منسّق",
                PageColor = "#6366F1",
                IsForVisitor = false,
                MobileAppRole = MobileAppRole.Moderator,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            appDb.ProfileTypes.Add(moderatorType);
        }

        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Mod", NameArabic = "منسّق",
            ProfileTypeId = moderatorType.Id,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
        return user.Id;
    }
}
