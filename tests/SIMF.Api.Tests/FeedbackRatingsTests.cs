// D-199 (gap doc / Mockup page 40) — forum rating: one-per-user upsert +
// admin list with average. Mirrors DelegationsTests / SeatReservationsTests.
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
    public async Task Visitor_can_submit_a_rating()
    {
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/rate",
            new RateRequest(5, "Excellent forum"), visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var view = (await response.Content
            .ReadFromJsonAsync<ApiResult<RatingView>>())!.Data!;
        Assert.Equal(5, view.Stars);
        Assert.Equal("Excellent forum", view.Comment);
        Assert.Null(view.UpdatedAt);
    }

    [Fact]
    public async Task Visitor_can_submit_per_element_scores()
    {
        // D-463 (Figma 1116:16894 "قيّم العناصر") — the four optional element
        // scores persist and echo back on the view.
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/rate",
            new RateRequest(4, "great",
                OrganizationStars: 5, ContentStars: 4,
                AppStars: 3, VenueStars: 2),
            visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var view = (await response.Content
            .ReadFromJsonAsync<ApiResult<RatingView>>())!.Data!;
        Assert.Equal(4, view.Stars);
        Assert.Equal(5, view.OrganizationStars);
        Assert.Equal(4, view.ContentStars);
        Assert.Equal(3, view.AppStars);
        Assert.Equal(2, view.VenueStars);
    }

    [Fact]
    public async Task Element_scores_are_optional_and_default_to_null()
    {
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/rate",
            new RateRequest(5, null), visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var view = (await response.Content
            .ReadFromJsonAsync<ApiResult<RatingView>>())!.Data!;
        Assert.Null(view.OrganizationStars);
        Assert.Null(view.ContentStars);
        Assert.Null(view.AppStars);
        Assert.Null(view.VenueStars);
    }

    [Fact]
    public async Task Out_of_range_element_score_is_rejected_with_400()
    {
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/rate",
            new RateRequest(4, null, OrganizationStars: 6), visitor);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rating_twice_upserts_the_single_row_for_the_user()
    {
        var visitor = await SignInApprovedVisitorAsync();

        var first = await PostAuthAsync(
            "/api/v1/app/feedback/rate",
            new RateRequest(3, "Good"), visitor);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstView = (await first.Content
            .ReadFromJsonAsync<ApiResult<RatingView>>())!.Data!;

        var second = await PostAuthAsync(
            "/api/v1/app/feedback/rate",
            new RateRequest(4, "Better on day two"), visitor);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondView = (await second.Content
            .ReadFromJsonAsync<ApiResult<RatingView>>())!.Data!;

        // Same row (upsert), updated values, UpdatedAt now stamped.
        Assert.Equal(firstView.Id, secondView.Id);
        Assert.Equal(4, secondView.Stars);
        Assert.Equal("Better on day two", secondView.Comment);
        Assert.NotNull(secondView.UpdatedAt);
    }

    [Fact]
    public async Task Out_of_range_stars_is_rejected_with_400()
    {
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            "/api/v1/app/feedback/rate",
            new RateRequest(6, null), visitor);
        // FluentValidation rejects before the handler runs.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_rate_is_401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/feedback/rate", new RateRequest(5, null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_list_returns_ratings_with_average()
    {
        // Two visitors rate 2 and 4 -> average 3.0 across these two rows.
        var v1 = await SignInApprovedVisitorAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await PostAuthAsync("/api/v1/app/feedback/rate", new RateRequest(2, "meh"), v1)).StatusCode);

        var v2 = await SignInApprovedVisitorAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await PostAuthAsync("/api/v1/app/feedback/rate", new RateRequest(4, "nice"), v2)).StatusCode);

        var admin = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/feedback/ratings",
            new GridQuery { Skip = 0, Top = 50 }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminRatingsPage>>())!.Data!;
        Assert.True(page.RatingCount >= 2);
        Assert.True(page.AverageStars > 0);
        Assert.NotEmpty(page.Ratings.Items);
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

    // -- Helpers --------------------------------------------------------------

    private async Task<string> SignInApprovedVisitorAsync()
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
        // D-373 — registration enables 2FA; this auth plumbing needs the
        // direct-token path (the admin-disabled scenario).
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
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
