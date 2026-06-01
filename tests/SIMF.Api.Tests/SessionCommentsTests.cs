// D-199 (Mockup page 28 — "Audience comments") — public submission +
// approved feed + admin moderation integration tests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Domain.SessionComments;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionCommentsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionCommentsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submit_lands_approved_by_default_stub_filter()
    {
        await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments",
            new SubmitSessionCommentRequest { Body = "Great session on maritime AI!" },
            visitor.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionCommentSubmitted>>())!.Data!;
        Assert.Equal(session.Id, result.SessionId);
        // The shipped stub filter auto-approves (AutoApprove default true).
        Assert.Equal(SessionCommentStatus.Approved, result.Status);
    }

    [Fact]
    public async Task Submit_with_empty_body_is_SESSION_COMMENT_INVALID()
    {
        await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments",
            new SubmitSessionCommentRequest { Body = "   " },
            visitor.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionCommentInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Submit_on_inactive_session_is_SESSION_NOT_OPEN_FOR_COMMENTS()
    {
        await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: false);
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments",
            new SubmitSessionCommentRequest { Body = "Is this on?" },
            visitor.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionNotOpenForComments, body.Error!.Code);
    }

    [Fact]
    public async Task Submit_on_missing_session_is_SESSION_NOT_FOUND()
    {
        await CreateAdministratorAndSignInAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{Guid.NewGuid()}/comments",
            new SubmitSessionCommentRequest { Body = "Ghost session" },
            visitor.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Public_feed_returns_only_approved_active_comments_newest_first()
    {
        await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();

        // First approved comment (stub auto-approves).
        await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments",
            new SubmitSessionCommentRequest { Body = "First comment" },
            visitor.AccessToken);
        // Advance the (fake) clock so the second comment is genuinely newer —
        // both share a CreatedAt otherwise and "newest first" is ambiguous.
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        // Second approved comment.
        var second = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments",
            new SubmitSessionCommentRequest { Body = "Second comment" },
            visitor.AccessToken);
        var secondId = (await second.Content
            .ReadFromJsonAsync<ApiResult<SessionCommentSubmitted>>())!.Data!.Id;

        // Force one extra row to Pending directly in the DB so it must be excluded.
        await SeedRawCommentAsync(session.Id, Guid.Parse(VisitorIdFromToken(visitor.AccessToken)),
            "Pending hidden body", SessionCommentStatus.Pending, isActive: true);

        var feed = await GetAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments", visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
        var rows = (await feed.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionCommentFeedRow>>>())!.Data!;

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.Body == "Pending hidden body");
        // Newest first — the second submitted comment leads.
        Assert.Equal(secondId, rows[0].Id);
        Assert.False(string.IsNullOrEmpty(rows[0].AuthorDisplayName));
    }

    [Fact]
    public async Task Admin_moderation_list_can_filter_by_status()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();
        var visitorId = Guid.Parse(VisitorIdFromToken(visitor.AccessToken));

        await SeedRawCommentAsync(session.Id, visitorId, "approved one", SessionCommentStatus.Approved, true);
        await SeedRawCommentAsync(session.Id, visitorId, "pending one", SessionCommentStatus.Pending, true);

        var pendingOnly = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/comments/list",
            new { Status = SessionCommentStatus.Pending, Skip = 0, Top = 25 },
            admin);
        Assert.Equal(HttpStatusCode.OK, pendingOnly.StatusCode);
        var page = (await pendingOnly.Content
            .ReadFromJsonAsync<ApiResult<GridPage<SessionCommentModerationRow>>>())!.Data!;
        Assert.All(page.Items, r => Assert.Equal(SessionCommentStatus.Pending, r.Status));
        Assert.Contains(page.Items, r => r.Body == "pending one");
        Assert.DoesNotContain(page.Items, r => r.Body == "approved one");
        // Admin list exposes submitter email (PII — admin surface only).
        Assert.All(page.Items, r => Assert.False(string.IsNullOrEmpty(r.AuthorEmail)));
    }

    [Fact]
    public async Task Admin_set_status_to_hidden_removes_from_public_feed_and_is_idempotent()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();

        var submit = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments",
            new SubmitSessionCommentRequest { Body = "To be hidden" },
            visitor.AccessToken);
        var commentId = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionCommentSubmitted>>())!.Data!.Id;

        var hide = await PutAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/comments/{commentId}/status",
            new SetSessionCommentStatusRequest { Status = SessionCommentStatus.Hidden },
            admin);
        Assert.Equal(HttpStatusCode.OK, hide.StatusCode);
        var hidden = (await hide.Content
            .ReadFromJsonAsync<ApiResult<SessionCommentModerationRow>>())!.Data!;
        Assert.Equal(SessionCommentStatus.Hidden, hidden.Status);
        Assert.NotNull(hidden.ModeratedAt);

        // Gone from the public feed.
        var feed = await GetAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments", visitor.AccessToken);
        var rows = (await feed.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionCommentFeedRow>>>())!.Data!;
        Assert.DoesNotContain(rows, r => r.Id == commentId);

        // Idempotent re-hide.
        var hideAgain = await PutAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/comments/{commentId}/status",
            new SetSessionCommentStatusRequest { Status = SessionCommentStatus.Hidden },
            admin);
        Assert.Equal(HttpStatusCode.OK, hideAgain.StatusCode);
    }

    [Fact]
    public async Task Admin_soft_delete_removes_comment_from_feed_and_moderation_list()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();

        var submit = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments",
            new SubmitSessionCommentRequest { Body = "Delete me" },
            visitor.AccessToken);
        var commentId = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionCommentSubmitted>>())!.Data!.Id;

        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/comments/{commentId}", admin);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var feed = await GetAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments", visitor.AccessToken);
        var feedRows = (await feed.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionCommentFeedRow>>>())!.Data!;
        Assert.DoesNotContain(feedRows, r => r.Id == commentId);

        var list = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/comments/list",
            new { Skip = 0, Top = 25 }, admin);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<SessionCommentModerationRow>>>())!.Data!;
        Assert.DoesNotContain(page.Items, r => r.Id == commentId);
    }

    [Fact]
    public async Task Set_status_on_missing_comment_is_SESSION_COMMENT_NOT_FOUND()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);

        var response = await PutAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/comments/{Guid.NewGuid()}/status",
            new SetSessionCommentStatusRequest { Status = SessionCommentStatus.Approved },
            admin);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionCommentNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_moderation_list()
    {
        await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/comments/list",
            new { Skip = 0, Top = 25 }, visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- B5 — D-223: likes ----------------------------------------------------

    [Fact]
    public async Task Like_increments_count_sets_LikedByMe_and_is_idempotent()
    {
        await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();
        var commentId = await SubmitCommentAsync(session.Id, "Like me", visitor.AccessToken);

        var like = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments/{commentId}/like",
            new { }, visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, like.StatusCode);
        var result = (await like.Content
            .ReadFromJsonAsync<ApiResult<SessionCommentLikeResult>>())!.Data!;
        Assert.Equal(1, result.LikeCount);
        Assert.True(result.LikedByMe);

        // Liking again is a no-op — the count stays at 1.
        var again = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments/{commentId}/like",
            new { }, visitor.AccessToken);
        var againResult = (await again.Content
            .ReadFromJsonAsync<ApiResult<SessionCommentLikeResult>>())!.Data!;
        Assert.Equal(1, againResult.LikeCount);

        // The feed reflects the like for the requester.
        var feed = await GetAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments", visitor.AccessToken);
        var rows = (await feed.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionCommentFeedRow>>>())!.Data!;
        var row = Assert.Single(rows, r => r.Id == commentId);
        Assert.Equal(1, row.LikeCount);
        Assert.True(row.LikedByMe);
    }

    [Fact]
    public async Task Unlike_decrements_count_and_is_idempotent()
    {
        await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();
        var commentId = await SubmitCommentAsync(session.Id, "Toggle me", visitor.AccessToken);

        await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments/{commentId}/like",
            new { }, visitor.AccessToken);

        var unlike = await DeleteAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments/{commentId}/like", visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, unlike.StatusCode);
        var result = (await unlike.Content
            .ReadFromJsonAsync<ApiResult<SessionCommentLikeResult>>())!.Data!;
        Assert.Equal(0, result.LikeCount);
        Assert.False(result.LikedByMe);

        // Unliking again is a no-op — the count stays clamped at 0.
        var again = await DeleteAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments/{commentId}/like", visitor.AccessToken);
        var againResult = (await again.Content
            .ReadFromJsonAsync<ApiResult<SessionCommentLikeResult>>())!.Data!;
        Assert.Equal(0, againResult.LikeCount);
        Assert.False(againResult.LikedByMe);
    }

    [Fact]
    public async Task LikedByMe_is_per_requester()
    {
        await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var alice = await SignInApprovedVisitorAsync();
        var bob = await SignInApprovedVisitorAsync();
        var commentId = await SubmitCommentAsync(session.Id, "Shared", alice.AccessToken);

        await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments/{commentId}/like",
            new { }, alice.AccessToken);

        // Bob sees the count but not his own like.
        var feed = await GetAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments", bob.AccessToken);
        var rows = (await feed.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionCommentFeedRow>>>())!.Data!;
        var row = Assert.Single(rows, r => r.Id == commentId);
        Assert.Equal(1, row.LikeCount);
        Assert.False(row.LikedByMe);
    }

    [Fact]
    public async Task Like_on_missing_comment_is_SESSION_COMMENT_NOT_FOUND()
    {
        await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments/{Guid.NewGuid()}/like",
            new { }, visitor.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionCommentNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Like_on_a_hidden_comment_is_404()
    {
        await CreateAdministratorAndSignInAsync();
        var session = await SeedSessionAsync(isActive: true);
        var visitor = await SignInApprovedVisitorAsync();
        var visitorId = Guid.Parse(VisitorIdFromToken(visitor.AccessToken));
        // A hidden comment is not visible to the audience, so it cannot be liked.
        await SeedRawCommentAsync(session.Id, visitorId, "hidden", SessionCommentStatus.Hidden, true);
        var hiddenId = await LatestCommentIdAsync(session.Id, "hidden");

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/comments/{hiddenId}/like",
            new { }, visitor.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> SubmitCommentAsync(Guid sessionId, string body, string token)
    {
        var submit = await PostAuthAsync(
            $"/api/v1/sessions/{sessionId}/comments",
            new SubmitSessionCommentRequest { Body = body }, token);
        return (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionCommentSubmitted>>())!.Data!.Id;
    }

    private async Task<Guid> LatestCommentIdAsync(Guid sessionId, string body)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.SessionComments
            .Where(c => c.SessionId == sessionId && c.Body == body)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.Id)
            .FirstAsync();
    }

    private async Task<AuthTokens> SignInApprovedVisitorAsync()
    {
        var email = $"sc-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var envelope = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return envelope.Data!.Tokens!;
    }

    private async Task<Session> SeedSessionAsync(bool isActive)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall C", NameArabic = "قاعة ج",
            Capacity = 100,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Comments Session",
            TitleArabic = "جلسة التعليقات",
            HallId = hall.Id,
            StartUtc = DateTimeOffset.UtcNow.AddMinutes(-15),
            EndUtc = DateTimeOffset.UtcNow.AddMinutes(45),
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task SeedRawCommentAsync(
        Guid sessionId, Guid userId, string body, SessionCommentStatus status, bool isActive)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SessionComments.Add(new SessionComment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            Body = body,
            Status = status,
            AiFilterVerdict = "test-seed",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sc-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "SC Admin",
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
                Email = email, Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private static string VisitorIdFromToken(string accessToken)
    {
        var parts = accessToken.Split('.');
        var payload = parts[1];
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("sub").GetString()!;
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
