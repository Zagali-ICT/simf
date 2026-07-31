// Dynamic ratings — app form/submit (upsert) + admin responses list with
// average. The "App" (global) + "Session" (per-session) types are seeded by
// RatingSeeder. Mirrors SeatReservationsTests / FaqTests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Feedback;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class FeedbackRatingsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public FeedbackRatingsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task App_form_exposes_overall_and_comment_config()
    {
        var visitor = await SignInApprovedVisitorAsync();

        var response = await GetAuthAsync("/api/v1/app/feedback/form?code=App", visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var form = (await response.Content
            .ReadFromJsonAsync<ApiResult<RatingFormView>>())!.Data!;
        Assert.Equal("App", form.Code);
        Assert.Equal(RatingScope.Global, form.Scope);
        Assert.True(form.HasOverallStars);
        Assert.True(form.AllowComment);
        Assert.Null(form.TargetId);
    }

    [Fact]
    public async Task Visitor_can_submit_app_rating()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var form = await GetFormAsync("App", null, visitor);

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest
            {
                RatingTypeId = form.RatingTypeId,
                OverallStars = 5,
                Comment = "Excellent forum",
            },
            visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var view = (await response.Content
            .ReadFromJsonAsync<ApiResult<RatingSubmissionView>>())!.Data!;
        Assert.Equal(5, view.OverallStars);
        Assert.Equal("Excellent forum", view.Comment);
        Assert.Null(view.TargetId);
        Assert.Null(view.UpdatedAt);
    }

    [Fact]
    public async Task Resubmitting_upserts_the_single_row_and_prefills()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var form = await GetFormAsync("App", null, visitor);

        var first = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = form.RatingTypeId, OverallStars = 3, Comment = "Good" },
            visitor);
        var firstView = (await first.Content
            .ReadFromJsonAsync<ApiResult<RatingSubmissionView>>())!.Data!;

        var second = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = form.RatingTypeId, OverallStars = 4, Comment = "Better" },
            visitor);
        var secondView = (await second.Content
            .ReadFromJsonAsync<ApiResult<RatingSubmissionView>>())!.Data!;

        Assert.Equal(firstView.Id, secondView.Id); // same row (upsert)
        Assert.Equal(4, secondView.OverallStars);
        Assert.Equal("Better", secondView.Comment);
        Assert.NotNull(secondView.UpdatedAt);

        // The form now prefills from the existing submission.
        var refetched = await GetFormAsync("App", null, visitor);
        Assert.Equal(4, refetched.Existing!.OverallStars);
        Assert.Equal("Better", refetched.Existing!.Comment);
    }

    [Fact]
    public async Task Missing_overall_on_a_type_that_requires_it_is_400()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var form = await GetFormAsync("App", null, visitor);

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = form.RatingTypeId, Comment = "no stars" },
            visitor);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Out_of_range_overall_is_rejected_with_400()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var form = await GetFormAsync("App", null, visitor);

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = form.RatingTypeId, OverallStars = 6 },
            visitor);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_submit_is_401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { Code = "App", OverallStars = 5 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Per_session_form_without_a_target_is_400()
    {
        var visitor = await SignInApprovedVisitorAsync();

        var response = await GetAuthAsync("/api/v1/app/feedback/form?code=Session", visitor);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Per_session_form_carries_the_watched_at_context()
    {
        // D-713 (item 8) — a per-session form returns the rated session's title +
        // start time so the app can show the "Watched {session} · {date}" header.
        var visitor = await SignInApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync();

        var form = await GetFormAsync("Session", sessionId, visitor);

        Assert.Equal(RatingScope.PerSession, form.Scope);
        Assert.Equal(sessionId, form.TargetId);
        Assert.Equal("Watched-At Session", form.TargetName);
        Assert.Equal("جلسة السياق", form.TargetNameArabic);
        Assert.NotNull(form.TargetStart);
    }

    [Fact]
    public async Task Per_session_submit_for_unknown_session_is_404()
    {
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest
            {
                Code = "Session",
                TargetId = Guid.NewGuid(), // no such session
                OverallStars = 5,
            },
            visitor);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_list_returns_responses_with_average()
    {
        var v1 = await SignInApprovedVisitorAsync();
        var f1 = await GetFormAsync("App", null, v1);
        Assert.Equal(HttpStatusCode.OK, (await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = f1.RatingTypeId, OverallStars = 2, Comment = "meh" }, v1)).StatusCode);

        var v2 = await SignInApprovedVisitorAsync();
        var f2 = await GetFormAsync("App", null, v2);
        Assert.Equal(HttpStatusCode.OK, (await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = f2.RatingTypeId, OverallStars = 4, Comment = "nice" }, v2)).StatusCode);

        var admin = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/feedback/ratings",
            new GridQuery { Skip = 0, Top = 50 }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminRatingResponsesPage>>())!.Data!;
        Assert.True(page.ResponseCount >= 2);
        Assert.True(page.AverageOverall > 0);
        Assert.NotEmpty(page.Responses.Items);
    }

    [Fact]
    public async Task Admin_list_is_forbidden_for_non_admin()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/feedback/ratings",
            new GridQuery { Skip = 0, Top = 50 }, visitor);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Owner 2026-07-19 — attendance gate (rate only what you attended) -------

    [Fact]
    public async Task A_visitor_who_did_not_attend_cannot_submit_an_app_rating()
    {
        var (visitor, _) = await SignInVisitorWithIdAsync(); // no attendance seeded
        var form = await GetFormAsync("App", null, visitor);

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = form.RatingTypeId, OverallStars = 5 },
            visitor);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RatingNotAttended, body.Error!.Code);
    }

    [Fact]
    public async Task The_form_reports_ineligible_before_attendance_and_eligible_after()
    {
        var (visitor, userId) = await SignInVisitorWithIdAsync();

        var before = await GetFormAsync("App", null, visitor);
        Assert.False(before.IsEligible); // form still loads, but flagged

        await RatingAttendance.SeedEventAttendanceAsync(_factory, userId);

        var after = await GetFormAsync("App", null, visitor);
        Assert.True(after.IsEligible);
        var ok = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = after.RatingTypeId, OverallStars = 5 },
            visitor);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task A_per_session_rating_requires_attendance_at_that_session()
    {
        var (visitor, userId) = await SignInVisitorWithIdAsync();
        var sessionId = await SeedSessionAsync();

        // Not checked in to this session → 403.
        var blocked = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { Code = "Session", TargetId = sessionId, OverallStars = 4 },
            visitor);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        // Attendance at a DIFFERENT session does not unlock this one → still 403.
        var otherSessionId = await SeedSessionAsync();
        await RatingAttendance.SeedSessionAttendanceAsync(_factory, userId, otherSessionId);
        var stillBlocked = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { Code = "Session", TargetId = sessionId, OverallStars = 4 },
            visitor);
        Assert.Equal(HttpStatusCode.Forbidden, stillBlocked.StatusCode);

        // Attendance at THIS session unlocks it.
        await RatingAttendance.SeedSessionAttendanceAsync(_factory, userId, sessionId);
        var ok = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { Code = "Session", TargetId = sessionId, OverallStars = 4 },
            visitor);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task A_venue_gate_checkin_alone_unlocks_a_global_rating()
    {
        // Owner 2026-07-19 (blended signal) — a venue-gate Check-In scan, with NO
        // in-hall attendance, is enough for a Global (App) rating; this exercises
        // AttendedEventAsync's GateScan branch + the UserProfile→scan id-hop.
        var (visitor, userId) = await SignInVisitorWithIdAsync(); // no HallAttendance
        var form = await GetFormAsync("App", null, visitor);
        Assert.False(form.IsEligible);

        await RatingAttendance.SeedGateCheckInAsync(
            _factory, userId, SimfClock.Now.AddHours(-1));

        var after = await GetFormAsync("App", null, visitor);
        Assert.True(after.IsEligible);
        var ok = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = after.RatingTypeId, OverallStars = 5 },
            visitor);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> SeedSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Rate Hall",
            NameArabic = "قاعة التقييم",
            Capacity = 10,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Watched-At Session",
            TitleArabic = "جلسة السياق",
            HallId = hall.Id,
            Start = SimfClock.Now.AddHours(-2),
            End = SimfClock.Now.AddHours(-1),
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<RatingFormView> GetFormAsync(string? code, Guid? targetId, string token)
    {
        var url = $"/api/v1/app/feedback/form?code={code}";
        if (targetId is { } t) { url += $"&targetId={t}"; }
        var response = await GetAuthAsync(url, token);
        return (await response.Content.ReadFromJsonAsync<ApiResult<RatingFormView>>())!.Data!;
    }

    private async Task<string> SignInApprovedVisitorAsync()
    {
        var (token, userId) = await SignInVisitorWithIdAsync();
        // Owner 2026-07-19 — ratings now require attendance; mark the visitor as having
        // attended the event so the existing (global-scope) rating flows still run.
        await RatingAttendance.SeedEventAttendanceAsync(_factory, userId);
        return token;
    }

    /// <summary>Signs in a fresh approved visitor WITHOUT seeding attendance — for the
    /// attendance-gate tests (not-attended → 403) and per-target tests that seed their
    /// own targeted attendance.</summary>
    private async Task<(string Token, Guid UserId)> SignInVisitorWithIdAsync()
    {
        var email = $"rate-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                ConfirmPassword = AuthFlow.Password,
            });
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(
                    _factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        var token = body.Data!.Tokens!.AccessToken;
        var userId = await RatingAttendance.UserIdAsync(_factory, email);
        return (token, userId);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"rate-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Rate Admin",
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
