// Wave 2 — session favourites (المفضلة) heart on the session-summaries (1388:8392)
// + my-sessions (1388:9067) screens: POST/DELETE /app/sessions/{id}/favourite +
// GET /app/sessions/favourites.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionFavouriteTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionFavouriteTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Favourite_then_list_then_unfavourite_round_trips()
    {
        var token = await CreateApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync();

        var add = await SendAuthAsync(
            HttpMethod.Post, $"/api/v1/app/sessions/{sessionId}/favourite", token);
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var listed = await ListFavouritesAsync(token);
        Assert.Contains(sessionId, listed);

        // Idempotent — a second add does not duplicate.
        await SendAuthAsync(
            HttpMethod.Post, $"/api/v1/app/sessions/{sessionId}/favourite", token);
        Assert.Single(await ListFavouritesAsync(token), id => id == sessionId);

        var remove = await SendAuthAsync(
            HttpMethod.Delete, $"/api/v1/app/sessions/{sessionId}/favourite", token);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        Assert.DoesNotContain(sessionId, await ListFavouritesAsync(token));
    }

    [Fact]
    public async Task Favourite_an_unknown_session_returns_404()
    {
        var token = await CreateApprovedVisitorAsync();
        var response = await SendAuthAsync(
            HttpMethod.Post, $"/api/v1/app/sessions/{Guid.NewGuid()}/favourite", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Favourites_are_per_user()
    {
        var tokenA = await CreateApprovedVisitorAsync();
        var tokenB = await CreateApprovedVisitorAsync();
        var sessionId = await SeedSessionAsync();

        await SendAuthAsync(
            HttpMethod.Post, $"/api/v1/app/sessions/{sessionId}/favourite", tokenA);

        Assert.Contains(sessionId, await ListFavouritesAsync(tokenA));
        Assert.DoesNotContain(sessionId, await ListFavouritesAsync(tokenB));
    }

    [Fact]
    public async Task Favourites_list_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/app/sessions/favourites");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<IReadOnlyList<Guid>> ListFavouritesAsync(string token)
    {
        var response = await SendAuthAsync(
            HttpMethod.Get, "/api/v1/app/sessions/favourites", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<List<Guid>>>())!.Data!;
    }

    private async Task<Guid> SeedSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = 100, IsActive = true, CreatedAt = now,
        };
        app.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Session", TitleArabic = "جلسة",
            HallId = hall.Id,
            Start = now, End = now.AddHours(1),
            IsActive = true, CreatedAt = now,
        };
        app.Sessions.Add(session);
        await app.SaveChangesAsync();
        return session.Id;
    }

    private async Task<string> CreateApprovedVisitorAsync()
    {
        var email = $"fav-visitor-{Guid.NewGuid():N}@simf.test";
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
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        return (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthAsync(
        HttpMethod method, string url, string token)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
