// P3.4 — D-235 (Completion Programme §5.4): the recorded Q&A archive — a
// published session's Committee-approved questions, attributed to the asker.
// Covers the published-only gate, the Approved-only filter, attribution, and
// the anonymous rejection.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Programme;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class RecordedQuestionsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RecordedQuestionsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Published_session_lists_approved_questions_attributed_to_the_asker()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        var (visitor, displayName) = await SeedApprovedVisitorAsync();
        var qid = await SubmitQuestionAsync(session.Id, visitor);

        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);
        await PublishAsync(session.Id, admin);

        var rows = await ReadRecordedAsync(session.Id, admin);
        var row = Assert.Single(rows);
        Assert.Equal(qid, row.Id);
        Assert.Equal(displayName, row.AskedByDisplayName);
    }

    [Fact]
    public async Task Unpublished_session_returns_an_empty_archive()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        var (visitor, _) = await SeedApprovedVisitorAsync();
        var qid = await SubmitQuestionAsync(session.Id, visitor);
        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);
        // Not published.

        var rows = await ReadRecordedAsync(session.Id, admin);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Only_approved_questions_appear_pending_and_hidden_are_excluded()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        var (visitor, _) = await SeedApprovedVisitorAsync();

        var approved = await SubmitQuestionAsync(session.Id, visitor);
        var hidden = await SubmitQuestionAsync(session.Id, visitor);
        var pending = await SubmitQuestionAsync(session.Id, visitor);

        await PutAuthAsync($"/api/v1/admin/questions/{approved}/approve", new { }, admin);
        await PutAuthAsync($"/api/v1/admin/questions/{hidden}/hide", new { }, admin);
        // `pending` is left untouched.
        await PublishAsync(session.Id, admin);

        var rows = await ReadRecordedAsync(session.Id, admin);
        Assert.Contains(rows, r => r.Id == approved);
        Assert.DoesNotContain(rows, r => r.Id == hidden);
        Assert.DoesNotContain(rows, r => r.Id == pending);
    }

    [Fact]
    public async Task Anonymous_caller_is_401()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        await PublishAsync(session.Id, admin);

        var response = await _client.GetAsync(
            $"/api/v1/programme/sessions/{session.Id}/recorded-questions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<IReadOnlyList<PublicRecordedQuestion>> ReadRecordedAsync(
        Guid sessionId, string token)
    {
        var response = await GetAuthAsync(
            $"/api/v1/programme/sessions/{sessionId}/recorded-questions", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicRecordedQuestion>>>())!.Data!;
    }

    private async Task PublishAsync(Guid id, string token)
    {
        await PutAuthAsync($"/api/v1/admin/sessions/{id}/status",
            new SetSessionStatusRequest { Status = SessionStatus.Held }, token);
        await PutAuthAsync($"/api/v1/admin/sessions/{id}/status",
            new SetSessionStatusRequest { Status = SessionStatus.Recorded }, token);
        await PutAuthAsync($"/api/v1/admin/sessions/{id}/status",
            new SetSessionStatusRequest { Status = SessionStatus.Published }, token);
    }

    private async Task<Guid> SubmitQuestionAsync(Guid sessionId, string visitorToken)
    {
        var response = await PostAuthAsync(
            $"/api/v1/sessions/{sessionId}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Archived question?", IsAtVenue = true },
            visitorToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;
    }

    private async Task<(string Token, string DisplayName)> SeedApprovedVisitorAsync()
    {
        var email = $"asker-{Guid.NewGuid():N}@simf.test";
        var displayName = "Asker " + Guid.NewGuid().ToString("N")[..4];
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = displayName,
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, displayName);
    }

    private async Task<Session> SeedLiveSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Archive Hall", NameArabic = "قاعة الأرشيف",
            Capacity = 100, IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Archive Session", TitleArabic = "جلسة الأرشيف",
            HallId = hall.Id,
            StartUtc = DateTimeOffset.UtcNow.AddMinutes(-15),
            EndUtc = DateTimeOffset.UtcNow.AddMinutes(45),
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"archive-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Archive Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp,
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
