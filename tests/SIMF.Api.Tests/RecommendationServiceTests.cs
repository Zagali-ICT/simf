// D-170 (gap doc G9, PDF §2.8) — "Meet People Like You" ranker.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Recommendations;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class RecommendationServiceTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RecommendationServiceTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Caller_with_no_interests_returns_empty()
    {
        var (callerToken, _) = await SeedApprovedVisitorWithInterestsAsync(0);
        var response = await GetAuthAsync(
            "/api/v1/account/recommendations/meet-like-you", callerToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<RecommendationsResponse>>())!.Data!;
        Assert.Empty(body.Matches);
    }

    [Fact]
    public async Task Two_users_share_one_interest_ranks_first_over_zero_overlap()
    {
        var interestA = await SeedInterestAsync("Maritime Security");
        var interestB = await SeedInterestAsync("AI / ML");
        var (callerToken, _) = await SeedApprovedVisitorWithSpecificInterestsAsync(
            new[] { interestA.Id });
        await SeedApprovedVisitorWithSpecificInterestsAsync(
            new[] { interestA.Id }); // shares 1
        await SeedApprovedVisitorWithSpecificInterestsAsync(
            new[] { interestB.Id }); // shares 0 — should be excluded

        var response = await GetAuthAsync(
            "/api/v1/account/recommendations/meet-like-you", callerToken);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<RecommendationsResponse>>())!.Data!;
        Assert.Single(body.Matches);
        Assert.Equal(1, body.Matches[0].SharedInterestCount);
    }

    [Fact]
    public async Task Higher_overlap_ranks_above_lower_overlap()
    {
        var interestA = await SeedInterestAsync("Naval Engineering");
        var interestB = await SeedInterestAsync("Logistics");
        var (callerToken, _) = await SeedApprovedVisitorWithSpecificInterestsAsync(
            new[] { interestA.Id, interestB.Id });
        var (_, twoSharedUserId) = await SeedApprovedVisitorWithSpecificInterestsAsync(
            new[] { interestA.Id, interestB.Id });
        var (_, oneSharedUserId) = await SeedApprovedVisitorWithSpecificInterestsAsync(
            new[] { interestA.Id });

        var response = await GetAuthAsync(
            "/api/v1/account/recommendations/meet-like-you", callerToken);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<RecommendationsResponse>>())!.Data!;
        Assert.True(body.Matches.Count >= 2);
        Assert.True(body.Matches[0].SharedInterestCount
            >= body.Matches[1].SharedInterestCount);
    }

    [Fact]
    public async Task Caller_is_excluded_from_their_own_recommendations()
    {
        var interest = await SeedInterestAsync("Cybersecurity");
        var (callerToken, callerUserId) = await SeedApprovedVisitorWithSpecificInterestsAsync(
            new[] { interest.Id });
        // Seed a second user with the same interest so the caller's row is the only
        // potential collision.
        await SeedApprovedVisitorWithSpecificInterestsAsync(new[] { interest.Id });

        var response = await GetAuthAsync(
            "/api/v1/account/recommendations/meet-like-you", callerToken);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<RecommendationsResponse>>())!.Data!;
        Assert.DoesNotContain(body.Matches,
            m => m.UserProfileId == GetProfileIdForUser(callerUserId));
    }

    [Fact]
    public async Task Non_approved_users_are_excluded()
    {
        var interest = await SeedInterestAsync("Ship Design");
        var (callerToken, _) = await SeedApprovedVisitorWithSpecificInterestsAsync(
            new[] { interest.Id });
        // Seed a Pending user with the same interest — should NOT appear.
        await SeedNonApprovedVisitorWithSpecificInterestsAsync(new[] { interest.Id });

        var response = await GetAuthAsync(
            "/api/v1/account/recommendations/meet-like-you", callerToken);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<RecommendationsResponse>>())!.Data!;
        Assert.Empty(body.Matches);
    }

    [Fact]
    public async Task Take_parameter_clamps_the_result_count()
    {
        var interest = await SeedInterestAsync("Sustainability");
        var (callerToken, _) = await SeedApprovedVisitorWithSpecificInterestsAsync(
            new[] { interest.Id });
        for (var i = 0; i < 5; i++)
        {
            await SeedApprovedVisitorWithSpecificInterestsAsync(new[] { interest.Id });
        }

        var response = await GetAuthAsync(
            "/api/v1/account/recommendations/meet-like-you?take=3", callerToken);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<RecommendationsResponse>>())!.Data!;
        Assert.True(body.Matches.Count <= 3);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Interest> SeedInterestAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var interest = new Interest
        {
            Id = Guid.NewGuid(),
            Name = name + " " + Guid.NewGuid().ToString("N")[..6],
            NameArabic = "اهتمام",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Interests.Add(interest);
        await db.SaveChangesAsync();
        return interest;
    }

    private async Task<(string accessToken, Guid userId)>
        SeedApprovedVisitorWithInterestsAsync(int interestCount)
    {
        var interestIds = new List<Guid>();
        for (var i = 0; i < interestCount; i++)
        {
            interestIds.Add((await SeedInterestAsync($"Interest {i}")).Id);
        }
        return await SeedApprovedVisitorWithSpecificInterestsAsync(interestIds);
    }

    private async Task<(string accessToken, Guid userId)>
        SeedApprovedVisitorWithSpecificInterestsAsync(IReadOnlyList<Guid> interestIds)
    {
        var email = $"rec-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Rec User",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EnglishName = $"User {Guid.NewGuid():N}".Substring(0, 16),
                ArabicName = "مستخدم",
                PlaceOfBirth = "Riyadh",
                IsSaudi = true,
                NationalId = "1234567890",
                NationalityId = 0,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            if (interestIds.Count > 0)
            {
                var interests = await appDb.Interests
                    .Where(i => interestIds.Contains(i.Id))
                    .ToListAsync();
                foreach (var interest in interests)
                {
                    profile.Interests.Add(interest);
                }
            }
            appDb.UserProfiles.Add(profile);
            await appDb.SaveChangesAsync();
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var envelope = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (envelope.Data!.Tokens!.AccessToken, userId);
    }

    private async Task<(string accessToken, Guid userId)>
        SeedNonApprovedVisitorWithSpecificInterestsAsync(IReadOnlyList<Guid> interestIds)
    {
        var email = $"rec-pending-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Pending User",
                AccountState = AccountState.PendingApproval,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EnglishName = "Pending",
                ArabicName = "بانتظار",
                PlaceOfBirth = "Riyadh",
                IsSaudi = true,
                NationalId = "1234567890",
                NationalityId = 0,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var interests = await appDb.Interests
                .Where(i => interestIds.Contains(i.Id))
                .ToListAsync();
            foreach (var interest in interests)
            {
                profile.Interests.Add(interest);
            }
            appDb.UserProfiles.Add(profile);
            await appDb.SaveChangesAsync();
        }
        return (string.Empty, userId);
    }

    private Guid GetProfileIdForUser(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return appDb.UserProfiles.Single(p => p.UserId == userId).Id;
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
