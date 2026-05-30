// D-190 (D-186 follow-up, mobile sign-up unblock) — integration
// tests for the public GET /api/v1/account/profile-types picker
// endpoint. Authenticated (not admin-only, not approval-gated) so
// mid-registration users can populate the Screen-2 dropdown.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ProfileTypePickerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ProfileTypePickerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Anonymous_call_returns_401()
    {
        // No Authorization header — the endpoint sits behind the
        // standard authentication floor.
        var response = await _client.GetAsync("/api/v1/account/profile-types");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_call_returns_active_visitor_scope_rows()
    {
        await SeedProfileTypeAsync($"Audience-{Guid.NewGuid():N}", isVisitor: true);
        await SeedProfileTypeAsync($"Partner-{Guid.NewGuid():N}", isVisitor: false);

        var token = await SignInVisitorAsync();
        var response = await GetAuthAsync("/api/v1/account/profile-types", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<ProfileTypePickerListResponse>>())!.Data!;
        // The picker returns both audience + partner rows (the mobile
        // filters client-side); every row must be active.
        Assert.NotEmpty(body.Items);
        Assert.Contains(body.Items, r => r.IsVisitor);
        Assert.Contains(body.Items, r => !r.IsVisitor);
    }

    [Fact]
    public async Task IsVisitor_true_returns_only_audience_rows()
    {
        await SeedProfileTypeAsync($"AudienceOnly-{Guid.NewGuid():N}", isVisitor: true);
        await SeedProfileTypeAsync($"PartnerExcluded-{Guid.NewGuid():N}", isVisitor: false);

        var token = await SignInVisitorAsync();
        var response = await GetAuthAsync(
            "/api/v1/account/profile-types?isVisitor=true", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<ProfileTypePickerListResponse>>())!.Data!;
        Assert.NotEmpty(body.Items);
        Assert.All(body.Items, row => Assert.True(row.IsVisitor));
    }

    [Fact]
    public async Task IsVisitor_false_returns_only_partner_rows()
    {
        await SeedProfileTypeAsync($"AudienceExcluded-{Guid.NewGuid():N}", isVisitor: true);
        await SeedProfileTypeAsync($"PartnerOnly-{Guid.NewGuid():N}", isVisitor: false);

        var token = await SignInVisitorAsync();
        var response = await GetAuthAsync(
            "/api/v1/account/profile-types?isVisitor=false", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<ProfileTypePickerListResponse>>())!.Data!;
        Assert.NotEmpty(body.Items);
        Assert.All(body.Items, row => Assert.False(row.IsVisitor));
    }

    [Fact]
    public async Task Inactive_profile_types_are_excluded()
    {
        var dormantId = await SeedProfileTypeAsync(
            $"DormantPick-{Guid.NewGuid():N}", isVisitor: true, isActive: false);

        var token = await SignInVisitorAsync();
        var response = await GetAuthAsync("/api/v1/account/profile-types", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<ProfileTypePickerListResponse>>())!.Data!;
        Assert.DoesNotContain(body.Items, row => row.Id == dormantId);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<Guid> SeedProfileTypeAsync(
        string name, bool isVisitor, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = new ProfileType
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = "اختبار",
            PageColor = "#3B82F6",
            UserType = UserType.Visitor,
            IsVisitor = isVisitor,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(row);
        await appDb.SaveChangesAsync();
        return row.Id;
    }

    private async Task<string> SignInVisitorAsync()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        return tokens.AccessToken;
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
