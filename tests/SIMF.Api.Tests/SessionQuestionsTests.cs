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
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "What about CPS resilience?", IsAtVenue = true },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!;
        Assert.Equal(session.Id, result.SessionId);
        Assert.Equal(0, result.Order);
    }

    [Fact]
    public async Task Live_question_skips_AI_and_lands_directly_on_the_moderator_desk()
    {
        // Owner 2026-07-19 (two-path Q&A) — a question asked once the session is
        // LIVE bypasses the AI filter and the Scientific Committee and goes
        // STRAIGHT to the per-session moderator desk (lands Approved).
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Live pipeline?", IsAtVenue = true },
            visitor.AccessToken);
        var id = (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var row = await db.SessionQuestions.AsNoTracking().SingleAsync(q => q.Id == id);
            Assert.Equal(QuestionStatus.Approved, row.Status);
            Assert.Equal(QuestionPhase.Live, row.Phase);
            // NO AI ran for a live question — the verdict is null (not "stub-clean").
            Assert.Null(row.AiFilterVerdict);
        }

        // …and it is on the moderator desk immediately, with no committee step.
        var desk = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        var rows = (await desk.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.Single(rows);
        Assert.Equal(id, rows[0].Id);
    }

    [Fact]
    public async Task Pre_question_is_AI_screened_and_waits_for_the_committee()
    {
        // Owner 2026-07-19 (two-path Q&A) — a question asked BEFORE the session
        // goes live still runs the advisory AI filter and lands Pending for the
        // Scientific Committee; it is NOT on the moderator desk yet.
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedFutureSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Ask ahead?", IsAtVenue = false },
            visitor.AccessToken);
        var id = (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var row = await db.SessionQuestions.AsNoTracking().SingleAsync(q => q.Id == id);
            Assert.Equal(QuestionStatus.Pending, row.Status);
            Assert.Equal(QuestionPhase.Pre, row.Phase);
            // P4.2 — D-236: the advisory AI filter (stub) tagged a verdict without
            // blocking the submit.
            Assert.Equal("stub-clean", row.AiFilterVerdict);
        }

        // A Pending pre-question is NOT on the moderator desk until the committee approves.
        var desk = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        Assert.Empty((await desk.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!);
    }

    [Fact]
    public async Task Submit_for_a_future_session_is_accepted_without_a_venue_gate()
    {
        // #7 (owner) — a FUTURE session (before start) accepts questions from any
        // approved user with NO venue gate (asking ahead of time); the question
        // lands Pending in the Pre phase. (Previously rejected as "not live".)
        var session = await SeedFutureSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Ask ahead", IsAtVenue = false },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var id = (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.SessionQuestions.AsNoTracking().SingleAsync(q => q.Id == id);
        Assert.Equal(QuestionStatus.Pending, row.Status);
        Assert.Equal(QuestionPhase.Pre, row.Phase);
    }

    [Fact]
    public async Task Questions_open_well_before_start_for_a_future_session()
    {
        // #7 (owner) — no lower bound now: a session starting in 10 minutes
        // accepts questions (previously closed until 5 min before start), and the
        // pre-start slice needs no venue presence.
        var session = await SeedSessionWindowAsync(
            DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow.AddMinutes(70));
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Ten minutes early", IsAtVenue = false },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Questions_are_closed_after_the_session_ends()
    {
        // §7 ("تقفل بنهاية الجلسة") — questions close at the end of the session.
        var session = await SeedSessionWindowAsync(
            DateTimeOffset.UtcNow.AddMinutes(-70), DateTimeOffset.UtcNow.AddMinutes(-10));
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Too late", IsAtVenue = true },
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
            $"/api/v1/app/sessions/{session.Id}/questions",
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
        // A PRE question (future session) so the committee → desk flow applies; a
        // LIVE question would land on the desk directly (see the two-path tests).
        var session = await SeedFutureSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Q1", IsAtVenue = false },
            visitor.AccessToken);
        var qid = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        // P3.3 — D-212: a Pending question is NOT on the moderator desk yet.
        var pending = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        Assert.Empty((await pending.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!);

        // The Committee approves it (stage 2); then the desk shows it (stage 3).
        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);

        var response = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.Single(rows);
        Assert.Equal(QuestionStatus.Approved, rows[0].Status);
        Assert.False(string.IsNullOrEmpty(rows[0].SubmittedByDisplayName));
    }

    [Fact]
    public async Task Moderator_queue_redacts_the_submitter_email()
    {
        // A9 (D-185) — the dedicated submitter-email field is redacted server-side:
        // it is null on every moderator-queue row. (Note: DisplayName can itself be
        // the account email for a self-registered visitor — RegistrationService
        // seeds DisplayName = Email and the "replace with real name at profile
        // completion" TODO is unimplemented — so a raw-body @-scan is NOT a valid
        // guard here; that display-name exposure is a separate, broader concern.
        // This test pins exactly what A9c changed: the SubmittedByEmail field.)
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Q-redact", IsAtVenue = true },
            visitor.AccessToken);
        var qid = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;
        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);

        var response = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;

        var row = Assert.Single(rows);
        Assert.Null(row.SubmittedByEmail);                              // redacted (D-185)
        Assert.False(string.IsNullOrEmpty(row.SubmittedByDisplayName)); // name preserved
    }

    [Fact]
    public async Task Hide_then_unhide_round_trips_state_and_is_idempotent()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Q1", IsAtVenue = true },
            visitor.AccessToken);
        var qid = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        var hide = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = true }, admin);
        var rowHidden = (await hide.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.True(rowHidden.IsHidden);

        var unhide = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = false }, admin);
        var rowUnhidden = (await unhide.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.False(rowUnhidden.IsHidden);

        // Idempotent re-call
        var unhideAgain = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/hide",
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
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Q1", IsAtVenue = true },
            visitor.AccessToken);
        var qid = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        // S-8 — only an approved question can be pushed; approve it first.
        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);

        var push = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/push",
            new { }, admin);
        var row = (await push.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.True(row.IsPushed);
        Assert.NotNull(row.PushedAt);

        // Idempotent re-call keeps the original PushedAt
        var pushAgain = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/push",
            new { }, admin);
        var row2 = (await pushAgain.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.Equal(row.PushedAt, row2.PushedAt);
    }

    // S-8 — a pushed question that is then HIDDEN must drop its pushed marker so
    // it leaves the on-stage queue.
    [Fact]
    public async Task Hiding_a_pushed_question_clears_the_pushed_marker()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "On stage?", IsAtVenue = true },
            visitor.AccessToken);
        var qid = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;

        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);
        await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/push", new { }, admin);
        await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = true }, admin);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.SessionQuestions.AsNoTracking().SingleAsync(q => q.Id == qid);
        Assert.False(row.IsPushed);
        Assert.Null(row.PushedAt);
        Assert.Equal(QuestionStatus.Hidden, row.Status);
    }

    [Fact]
    public async Task Submit_with_Host_recipient_round_trips_in_moderator_queue()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
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
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        var rows = (await queue.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.Contains(rows, r =>
            r.QuestionText == "For the host"
            && r.Recipient == SessionQuestionRecipient.Host);
    }

    // S-5 (owner) — a non-geofenced LIVE hall has no arrival mechanism, so the
    // app's isAtVenue self-assert is no longer trusted: a remote question WITHOUT
    // a venue claim is accepted (remote Q&A works). Geofenced halls still gate on
    // a real HallAttendance arrival — see QuestionArrivalGatingTests.
    [Fact]
    public async Task Submit_without_at_venue_flag_accepts_remote_question_on_a_non_geofenced_hall()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Remote but welcome", IsAtVenue = false },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_non_moderator_caller_is_forbidden_on_moderator_queue()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (session, _) = await SeedLiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();

        var response = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", visitor.AccessToken);
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
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", visitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>Create + sign in a visitor and bump them to Approved so
    /// the RequireApprovedAccount policy lets them through.</summary>
    private async Task<AuthTokens> SignInApprovedVisitorAsync()
    {
        var email = $"sq-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        // D-373 — registration enables 2FA; this auth plumbing needs the
        // direct-token path (the admin-disabled scenario).
        AuthFlow.DisableTwoFactor(_factory, email);

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
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

    // §7 — seed an active session over an explicit time window (no geofence, so
    // the question gate falls back to the IsAtVenue self-assert).
    private async Task<Session> SeedSessionWindowAsync(
        DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall W", NameArabic = "قاعة و",
            Capacity = 50,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Window Session", TitleArabic = "جلسة زمنية",
            HallId = hall.Id,
            StartUtc = startUtc,
            EndUtc = endUtc,
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
            "/api/v1/app/auth/sign-in",
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
