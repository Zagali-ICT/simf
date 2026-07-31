// DEF-MOD-005 — the session-moderator assignment used to take two free-text
// GUIDs and only check the target account was Approved, so ANY visitor could be
// handed a moderation desk by a typo (or on purpose) and there was no way for an
// admin to find the right person. These tests pin the server-side eligibility
// rule and the picker lookup that replaced the GUID boxes.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionModeratorEligibilityTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionModeratorEligibilityTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Assigning_a_plain_visitor_is_rejected_as_not_eligible()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync();
        // Approved, but no operational profile type — the account a typo in the
        // old free-text GUID box would land on.
        var visitor = await SessionModeratorSeed.CreatePlainVisitorAsync(_factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/session-moderators",
            new AssignSessionModeratorRequest
            {
                SessionId = session.Id, UserId = visitor,
            },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionModeratorNotEligible, body.Error!.Code);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.False(await db.SessionModerators
            .AnyAsync(m => m.SessionId == session.Id && m.UserId == visitor));
    }

    [Fact]
    public async Task Assigning_an_eligible_moderator_succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync();
        var moderator =
            await SessionModeratorSeed.CreateEligibleModeratorAsync(_factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/session-moderators",
            new AssignSessionModeratorRequest
            {
                SessionId = session.Id, UserId = moderator,
            },
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionModeratorRow>>())!.Data!;
        Assert.Equal(moderator, row.UserId);
    }

    [Fact]
    public async Task Assign_options_offer_the_active_session_and_only_eligible_accounts()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync();
        var moderator =
            await SessionModeratorSeed.CreateEligibleModeratorAsync(_factory);
        var visitor = await SessionModeratorSeed.CreatePlainVisitorAsync(_factory);

        var response = await GetAuthAsync(
            "/api/v1/admin/session-moderators/assign-options", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionModeratorAssignOptions>>())!.Data!;

        Assert.Contains(options.Sessions, s => s.Id == session.Id);
        var candidate = Assert.Single(
            options.Candidates, c => c.UserId == moderator);
        Assert.False(string.IsNullOrEmpty(candidate.ProfileTypeName));
        // The plain visitor is never offered as a moderator.
        Assert.DoesNotContain(options.Candidates, c => c.UserId == visitor);
    }

    [Fact]
    public async Task Assign_options_are_forbidden_without_the_assign_permission()
    {
        // The lookup feeds the assign dialog, so it carries the same gate as the
        // write it feeds — an ordinary signed-in visitor cannot enumerate the
        // eligible moderator list.
        var visitor = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await GetAuthAsync(
            "/api/v1/admin/session-moderators/assign-options", visitor.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_second_moderator_profile_type_does_not_break_the_eligible_seed()
    {
        // M5 — SessionModeratorSeed looked the canonical partner type up with
        // SingleOrDefault on (MobileAppRole.Moderator && !IsForVisitor), so the
        // whole eligibility suite would start throwing the day the seeder — or
        // another test — shipped a SECOND partner type carrying the moderator app
        // role. The lookup is a deterministic first match instead.
        // The first call guarantees the canonical partner type exists …
        await SessionModeratorSeed.CreateEligibleModeratorAsync(_factory);
        // … and this is the second one that used to break the lookup.
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.ProfileTypes.Add(new UserProfileType
            {
                Id = Guid.NewGuid(),
                Name = "Co-moderator", NameArabic = "منسّق مشارك",
                PageColor = "#0EA5E9",
                IsForVisitor = false,
                MobileAppRole = MobileAppRole.Moderator,
                IsActive = true,
                // Later than the canonical type, so the ordering is unambiguous.
                CreatedAt = SimfClock.Now.AddYears(1),
            });
            await appDb.SaveChangesAsync();
        }

        var token = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync();
        var moderator =
            await SessionModeratorSeed.CreateEligibleModeratorAsync(_factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/session-moderators",
            new AssignSessionModeratorRequest
            {
                SessionId = session.Id, UserId = moderator,
            },
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Session> SeedSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall E", NameArabic = "قاعة هـ",
            Capacity = 100, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Eligibility", TitleArabic = "الأهلية",
            HallId = hall.Id,
            Start = SimfClock.Now.AddHours(1),
            End = SimfClock.Now.AddHours(2),
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sme-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "SME Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
