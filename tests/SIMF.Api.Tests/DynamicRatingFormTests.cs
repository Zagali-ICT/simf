// Dynamic rating page (D-680) — the /rate screen reads code + targetId and posts
// to the same generic feedback endpoints. Covers the three new seeded rating
// codes: Event + Exhibition (global) and Day (per programme day), incl. the new
// PerDay branch in RatingFormService.ResolveTargetAsync.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Feedback;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class DynamicRatingFormTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public DynamicRatingFormTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("Event")]
    [InlineData("Exhibition")]
    public async Task Global_rating_form_needs_no_target_and_is_submittable(string code)
    {
        var visitor = await SignInApprovedVisitorAsync();

        var formResponse = await GetAuthAsync($"/api/v1/app/feedback/form?code={code}", visitor);
        Assert.Equal(HttpStatusCode.OK, formResponse.StatusCode);
        var form = (await formResponse.Content.ReadFromJsonAsync<ApiResult<RatingFormView>>())!.Data!;
        Assert.Equal(code, form.Code);
        Assert.Equal(RatingScope.Global, form.Scope);
        Assert.Null(form.TargetId);

        var submit = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = form.RatingTypeId, OverallStars = 5, Comment = "Great" },
            visitor);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var view = (await submit.Content.ReadFromJsonAsync<ApiResult<RatingSubmissionView>>())!.Data!;
        Assert.Equal(5, view.OverallStars);
        Assert.Null(view.TargetId);
    }

    [Fact]
    public async Task Day_form_without_a_target_is_400()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await GetAuthAsync("/api/v1/app/feedback/form?code=Day", visitor);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Day_submit_for_an_unknown_day_is_404()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { Code = "Day", TargetId = Guid.NewGuid(), OverallStars = 5 },
            visitor);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Day_submit_for_a_real_programme_day_succeeds()
    {
        var (visitor, userId) = await SignInVisitorWithIdAsync();
        var dayId = await SeedProgrammeDayAsync();
        // Owner 2026-07-19 — a Day rating requires having attended that day.
        await RatingAttendance.SeedDayAttendanceAsync(_factory, userId, dayId);

        var formResponse = await GetAuthAsync(
            $"/api/v1/app/feedback/form?code=Day&targetId={dayId}", visitor);
        Assert.Equal(HttpStatusCode.OK, formResponse.StatusCode);
        var form = (await formResponse.Content.ReadFromJsonAsync<ApiResult<RatingFormView>>())!.Data!;
        Assert.Equal(RatingScope.PerDay, form.Scope);
        Assert.Equal(dayId, form.TargetId);

        var submit = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { Code = "Day", TargetId = dayId, OverallStars = 4, Comment = "Good day" },
            visitor);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var view = (await submit.Content.ReadFromJsonAsync<ApiResult<RatingSubmissionView>>())!.Data!;
        Assert.Equal(4, view.OverallStars);
        Assert.Equal(dayId, view.TargetId);
    }

    [Fact]
    public async Task Day_rating_is_unlocked_by_a_venue_gate_checkin_that_day()
    {
        // Owner 2026-07-19 (blended signal) — a venue-gate Check-In scan on the day,
        // with NO in-hall attendance, unlocks the Day rating; exercises
        // AttendedDayAsync's GateScan branch + the event-local (+03:00) day window.
        var (visitor, userId) = await SignInVisitorWithIdAsync(); // no HallAttendance
        var dayId = await SeedProgrammeDayAsync();
        await RatingAttendance.SeedGateCheckInOnDayAsync(_factory, userId, dayId);

        var submit = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { Code = "Day", TargetId = dayId, OverallStars = 4 },
            visitor);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> SeedProgrammeDayAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // A far-future unique date avoids the one-active-day-per-date index.
        var date = new DateOnly(2030, 1, 1).AddDays(await db.ProgrammeDays.CountAsync());
        var day = new ProgrammeDay
        {
            Id = Guid.NewGuid(),
            Date = date,
            Title = "Rate Day",
            TitleArabic = "يوم التقييم",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.ProgrammeDays.Add(day);
        await db.SaveChangesAsync();
        return day.Id;
    }

    private async Task<string> SignInApprovedVisitorAsync()
    {
        var (token, userId) = await SignInVisitorWithIdAsync();
        // Owner 2026-07-19 — ratings now require attendance; mark the visitor as having
        // attended the event so the global-scope rating flows still run.
        await RatingAttendance.SeedEventAttendanceAsync(_factory, userId);
        return token;
    }

    /// <summary>Signs in a fresh approved visitor WITHOUT seeding attendance — for the
    /// per-day test that seeds its own targeted attendance.</summary>
    private async Task<(string Token, Guid UserId)> SignInVisitorWithIdAsync()
    {
        var email = $"rate-dyn-{Guid.NewGuid():N}@simf.test";
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
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
