// P3.2a — D-231 (Completion Programme §5.2, broadcast Option A): the
// session broadcast lifecycle (Scheduled → Held → Recorded → Published)
// driven by the Scientific Committee via Sessions.Publish. Mirrors
// AdminSessionsTests' setup; asserts the legal transitions, the illegal
// jump guard, the PublishedAt stamp, and the public read.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionLifecycleTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionLifecycleTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task New_session_starts_scheduled()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);
        Assert.Equal(SessionStatus.Scheduled, session.Status);
        Assert.Null(session.PublishedAt);
    }

    [Fact]
    public async Task Lifecycle_advances_to_published_and_stamps_publishedAt()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);

        var held = await SetStatusAsync(session.Id, SessionStatus.Held, token);
        Assert.Equal(SessionStatus.Held, await ReadStatusAsync(held));

        var recorded = await SetStatusAsync(session.Id, SessionStatus.Recorded, token);
        Assert.Equal(SessionStatus.Recorded, await ReadStatusAsync(recorded));

        var publishResponse = await SetStatusAsync(session.Id, SessionStatus.Published, token);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        var published = (await publishResponse.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionStatus.Published, published.Status);
        Assert.NotNull(published.PublishedAt);

        // The public read reflects the published state.
        var pub = await _client.GetAsync($"/api/v1/app/programme/sessions/{session.Id}");
        Assert.Equal(HttpStatusCode.OK, pub.StatusCode);
        var detail = (await pub.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;
        Assert.Equal(SessionStatus.Published, detail.Status);
        Assert.NotNull(detail.PublishedAt);
    }

    [Fact]
    public async Task Skipping_a_step_is_400_SESSION_STATUS_TRANSITION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);

        var response = await SetStatusAsync(session.Id, SessionStatus.Published, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionStatusTransitionInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Unpublishing_clears_publishedAt()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);
        await SetStatusAsync(session.Id, SessionStatus.Held, token);
        await SetStatusAsync(session.Id, SessionStatus.Recorded, token);
        await SetStatusAsync(session.Id, SessionStatus.Published, token);

        var unpublishResponse = await SetStatusAsync(session.Id, SessionStatus.Recorded, token);
        Assert.Equal(HttpStatusCode.OK, unpublishResponse.StatusCode);
        var detail = (await unpublishResponse.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionStatus.Recorded, detail.Status);
        Assert.Null(detail.PublishedAt);
    }

    [Fact]
    public async Task Setting_the_same_status_is_an_idempotent_noop()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);

        var response = await SetStatusAsync(session.Id, SessionStatus.Scheduled, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionStatus.Scheduled, detail.Status);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_set_status()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(adminToken);

        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await SetStatusAsync(session.Id, SessionStatus.Held, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<SessionStatus> ReadStatusAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        return detail.Status;
    }

    private Task<HttpResponseMessage> SetStatusAsync(Guid id, SessionStatus status, string token) =>
        PutAuthAsync($"/api/v1/admin/sessions/{id}/status",
            new SetSessionStatusRequest { Status = status }, token);

    private async Task<AdminSessionDetail> CreateSessionAsync(string token)
    {
        var hall = await SeedHallAsync(capacity: 100);
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Title = "Lifecycle session",
                TitleArabic = "جلسة دورة الحياة",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
    }

    private async Task<Hall> SeedHallAsync(int capacity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Lifecycle Hall",
            NameArabic = "قاعة دورة الحياة",
            Capacity = capacity,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"lifecycle-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Lifecycle Admin",
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
                Email = email,
                Password = AuthFlow.Password,
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

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
