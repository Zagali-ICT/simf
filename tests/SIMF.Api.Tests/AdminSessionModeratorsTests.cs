// D-169 (gap doc G6) — admin assign/revoke per-session moderator grant.
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
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminSessionModeratorsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminSessionModeratorsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Assign_returns_row_with_session_and_moderator_projection()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync();
        var moderator = await CreateApprovedUserAsync();

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
        Assert.Equal(session.Id, row.SessionId);
        Assert.Equal(moderator, row.UserId);
        Assert.False(string.IsNullOrEmpty(row.SessionCode));
    }

    [Fact]
    public async Task Assign_with_unknown_session_is_SESSION_NOT_FOUND()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var moderator = await CreateApprovedUserAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/session-moderators",
            new AssignSessionModeratorRequest
            {
                SessionId = Guid.NewGuid(), UserId = moderator,
            },
            token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Assign_duplicate_returns_SESSION_MODERATOR_ALREADY_ASSIGNED()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync();
        var moderator = await CreateApprovedUserAsync();

        var first = await PostAuthAsync(
            "/api/v1/admin/session-moderators",
            new AssignSessionModeratorRequest { SessionId = session.Id, UserId = moderator },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/session-moderators",
            new AssignSessionModeratorRequest { SessionId = session.Id, UserId = moderator },
            token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionModeratorAlreadyAssigned, body.Error!.Code);
    }

    [Fact]
    public async Task Revoke_removes_grant_and_is_idempotent()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync();
        var moderator = await CreateApprovedUserAsync();
        await PostAuthAsync(
            "/api/v1/admin/session-moderators",
            new AssignSessionModeratorRequest { SessionId = session.Id, UserId = moderator },
            token);

        var first = await DeleteAuthAsync(
            $"/api/v1/admin/session-moderators/{session.Id}/{moderator}", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await DeleteAuthAsync(
            $"/api/v1/admin/session-moderators/{session.Id}/{moderator}", token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var remaining = await db.SessionModerators
            .AnyAsync(m => m.SessionId == session.Id && m.UserId == moderator);
        Assert.False(remaining);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_assignment()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var session = await SeedSessionAsync();
        var moderator = await CreateApprovedUserAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/session-moderators",
            new AssignSessionModeratorRequest { SessionId = session.Id, UserId = moderator },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
            Name = "Hall X", NameArabic = "قاعة س",
            Capacity = 100, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "S", TitleArabic = "ج",
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

    /// <summary>An approved account that is ELIGIBLE to moderate (DEF-MOD-005):
    /// it carries a partner profile type whose mobile app role is Moderator —
    /// the same fact the JWT's app role is minted from.</summary>
    private Task<Guid> CreateApprovedUserAsync() =>
        SessionModeratorSeed.CreateEligibleModeratorAsync(_factory);

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sm-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "SM Admin",
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
