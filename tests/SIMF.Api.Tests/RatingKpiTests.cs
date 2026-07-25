// Dynamic-ratings admin KPI — GET /admin/feedback/ratings/kpi. Closes the gap
// the D-496 live verification surfaced: the responses-grid average had a test
// (FeedbackRatingsTests.Admin_list_returns_responses_with_average) but the
// per-type / per-question KPI aggregation (AdminRatingResponseService.GetKpiAsync)
// had none. Asserts exact counts + averages for the App (global, stars-only) and
// Session (per-session, Speaker/Sound/Light) seeded types. Mirrors
// FeedbackRatingsTests for the auth + submit helpers.
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

public sealed class RatingKpiTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RatingKpiTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Kpi_aggregates_per_type_counts_overall_and_per_question_averages()
    {
        // App (global, stars-only): two submissions — overall 2 and 4 -> avg 3.0.
        await SubmitAppOverallAsync(2);
        await SubmitAppOverallAsync(4);

        // Session (per-session, Speaker/Sound/Light): one submission — overall 5,
        // Speaker 5 / Sound 4 / Light 3.
        var sessionId = await SeedActiveSessionAsync();
        await SubmitSessionAsync(
            sessionId, overall: 5,
            scores: new() { ["Speaker"] = 5, ["Sound"] = 4, ["Light"] = 3 });

        var admin = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync("/api/v1/admin/feedback/ratings/kpi", admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var kpi = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminRatingKpiView>>())!.Data!;

        Assert.Equal(3, kpi.TotalResponses);

        var app = kpi.Types.Single(t => t.Code == "App");
        Assert.Equal(2, app.ResponseCount);
        Assert.Equal(3.0, app.AverageOverall, 3);
        Assert.Empty(app.Questions); // App carries no questions (stars-only)

        var session = kpi.Types.Single(t => t.Code == "Session");
        Assert.Equal(1, session.ResponseCount);
        Assert.Equal(5.0, session.AverageOverall, 3);

        var speaker = session.Questions.Single(q => q.Text == "Speaker");
        Assert.Equal(1, speaker.Count);
        Assert.Equal(5.0, speaker.Average, 3);

        var sound = session.Questions.Single(q => q.Text == "Sound");
        Assert.Equal(4.0, sound.Average, 3);

        var light = session.Questions.Single(q => q.Text == "Light");
        Assert.Equal(3.0, light.Average, 3);
    }

    [Fact]
    public async Task Kpi_is_forbidden_for_a_non_admin()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await GetAuthAsync("/api/v1/admin/feedback/ratings/kpi", visitor);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task SubmitAppOverallAsync(int overall)
    {
        var visitor = await SignInApprovedVisitorAsync();
        var form = await GetFormAsync("App", null, visitor);
        var response = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = form.RatingTypeId, OverallStars = overall },
            visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task SubmitSessionAsync(
        Guid sessionId, int overall, Dictionary<string, int> scores)
    {
        var (visitor, userId) = await SignInVisitorWithIdAsync();
        // Owner 2026-07-19 — a per-session rating requires attendance at that session.
        await RatingAttendance.SeedSessionAttendanceAsync(_factory, userId, sessionId);
        var form = await GetFormAsync("Session", sessionId, visitor);

        var answers = form.UngroupedQuestions
            .Where(q => scores.ContainsKey(q.Text))
            .Select(q => new RatingAnswerInput(q.Id, scores[q.Text]))
            .ToList();
        Assert.Equal(scores.Count, answers.Count); // the seeded questions were found

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/submit",
            new SubmitRatingRequest
            {
                RatingTypeId = form.RatingTypeId,
                TargetId = sessionId,
                OverallStars = overall,
                Answers = answers,
            },
            visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> SeedActiveSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall",
            NameArabic = "قاعة",
            Capacity = 10,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Keynote",
            TitleArabic = "الكلمة الرئيسية",
            HallId = hall.Id,
            Start = DateTimeOffset.UtcNow.AddHours(-1),
            End = DateTimeOffset.UtcNow.AddHours(1),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
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
        // attended the event so the global-scope (App) rating flows still run.
        await RatingAttendance.SeedEventAttendanceAsync(_factory, userId);
        return token;
    }

    /// <summary>Signs in a fresh approved visitor WITHOUT seeding attendance — for the
    /// per-session KPI submits that seed their own targeted attendance.</summary>
    private async Task<(string Token, Guid UserId)> SignInVisitorWithIdAsync()
    {
        var email = $"kpi-visitor-{Guid.NewGuid():N}@simf.test";
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
        var email = $"kpi-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "KPI Admin",
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
