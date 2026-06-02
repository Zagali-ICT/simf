// D-169 (gap doc G6, PDF §2.7.2 + §2.10) — public submission +
// moderator surface for session questions.
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
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionQuestionsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionQuestionsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submit_inside_live_window_returns_OK_with_queue_position_zero()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "What about CPS resilience?", IsAtVenue = true },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!;
        Assert.Equal(session.Id, result.SessionId);
        Assert.Equal(0, result.Order);
    }

    [Fact]
    public async Task Submit_lands_pending_with_computed_phase()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Pipeline?", IsAtVenue = true },
            visitor.AccessToken);
        var id = (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.SessionQuestions.AsNoTracking().SingleAsync(q => q.Id == id);
        // P3.3 — D-212: every new question awaits the Committee (Pending); a live
        // session (StartUtc 15 min ago) yields the Live phase.
        Assert.Equal(QuestionStatus.Pending, row.Status);
        Assert.Equal(QuestionPhase.Live, row.Phase);
    }

    [Fact]
    public async Task Submit_outside_live_window_returns_SESSION_NOT_LIVE_FOR_QUESTIONS()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedFutureSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Too early", IsAtVenue = true },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionNotLiveForQuestions, body.Error!.Code);
    }

    [Fact]
    public async Task Submit_with_empty_text_is_SESSION_QUESTION_INVALID()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "   ", IsAtVenue = true },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionQuestionInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Moderator_queue_lists_committee_approved_questions_with_submitter_projection()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Q1", IsAtVenue = true },
            visitor.AccessToken);
        var qid = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        // P3.3 — D-212: a Pending question is NOT on the moderator desk yet.
        var pending = await GetAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions/moderate", admin);
        Assert.Empty((await pending.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!);

        // The Committee approves it (stage 2); then the desk shows it (stage 3).
        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);

        var response = await GetAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions/moderate", admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.Single(rows);
        Assert.Equal(QuestionStatus.Approved, rows[0].Status);
        Assert.False(string.IsNullOrEmpty(rows[0].SubmittedByDisplayName));
    }

    [Fact]
    public async Task Hide_then_unhide_round_trips_state_and_is_idempotent()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Q1", IsAtVenue = true },
            visitor.AccessToken);
        var qid = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        var hide = await PutAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = true }, admin);
        var rowHidden = (await hide.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.True(rowHidden.IsHidden);

        var unhide = await PutAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = false }, admin);
        var rowUnhidden = (await unhide.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.False(rowUnhidden.IsHidden);

        // Idempotent re-call
        var unhideAgain = await PutAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = false }, admin);
        Assert.Equal(HttpStatusCode.OK, unhideAgain.StatusCode);
    }

    [Fact]
    public async Task Push_marks_question_pushed_with_timestamp_and_is_idempotent()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Q1", IsAtVenue = true },
            visitor.AccessToken);
        var qid = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        var push = await PutAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions/{qid}/push",
            new { }, admin);
        var row = (await push.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.True(row.IsPushed);
        Assert.NotNull(row.PushedAt);

        // Idempotent re-call keeps the original PushedAt
        var pushAgain = await PutAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions/{qid}/push",
            new { }, admin);
        var row2 = (await pushAgain.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.Equal(row.PushedAt, row2.PushedAt);
    }

    [Fact]
    public async Task Submit_with_Host_recipient_round_trips_in_moderator_queue()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var submit = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest
            {
                QuestionText = "For the host",
                IsAtVenue = true,
                Recipient = SessionQuestionRecipient.Host,
            },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var qid = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        // P3.3 — D-212: approve it through the Committee so it reaches the desk.
        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);

        var queue = await GetAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions/moderate", admin);
        var rows = (await queue.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.Contains(rows, r =>
            r.QuestionText == "For the host"
            && r.Recipient == SessionQuestionRecipient.Host);
    }

    [Fact]
    public async Task Submit_without_at_venue_flag_is_403_NOT_AT_VENUE()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Remote troll", IsAtVenue = false },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.NotAtVenue, body.Error!.Code);
    }

    [Fact]
    public async Task Non_admin_non_moderator_caller_is_forbidden_on_moderator_queue()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await GetAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions/moderate", visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Granted_moderator_can_read_queue_without_admin_role()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        // Assign the visitor's user id as a moderator of this session.
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var visitorId = Guid.Parse(VisitorIdFromToken(visitor.AccessToken));
            appDb.SessionModerators.Add(new SessionModerator
            {
                SessionId = session.Id,
                UserId = visitorId,
                AssignedByUserId = visitorId, // self-stamped for the test
                AssignedAt = DateTimeOffset.UtcNow,
            });
            await appDb.SaveChangesAsync();
        }

        var response = await GetAuthAsync(
            $"/api/v1/sessions/{session.Id}/questions/moderate", visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>Create + sign in a visitor and bump them to Approved so
    /// the RequireApprovedAccount policy lets them through.</summary>
    private async Task<AuthTokens> SignInApprovedVisitorAsync()
    {
        var email = $"sq-visitor-{Guid.NewGuid():N}@simf.test";
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

    private async Task<(Session session, Hall hall)> SeedLiveSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall A", NameArabic = "قاعة أ",
            Capacity = 100,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Live Session",
            TitleArabic = "جلسة مباشرة",
            HallId = hall.Id,
            StartUtc = DateTimeOffset.UtcNow.AddMinutes(-15),
            EndUtc = DateTimeOffset.UtcNow.AddMinutes(45),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (session, hall);
    }

    private async Task<Session> SeedFutureSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall B", NameArabic = "قاعة ب",
            Capacity = 50,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Tomorrow's Session",
            TitleArabic = "جلسة غد",
            HallId = hall.Id,
            StartUtc = DateTimeOffset.UtcNow.AddDays(1),
            EndUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sq-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "SQ Admin",
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

    /// <summary>Decode the "sub" claim from a JWT for the
    /// per-session-moderator assignment test.</summary>
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
}
