// D-426 — exhibitor ("Other") lead capture: scan a visitor badge → capture to
// My Visitors + return the full card; visitor-tier callers are forbidden.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Contacts;
using SIMF.Contracts.Exhibitors;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ExhibitorVisitorScanTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ExhibitorVisitorScanTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Exhibitor_scans_visitor_badge_captures_and_returns_full_card()
    {
        var (exhibitorToken, exhibitorId) = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitorId, "Acme Booth", "جناح أكمي");

        var (_, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "BADGE2026XYZ", "Visitor One", "الزائر");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "badge2026xyz", Note = "booth A3" },
            exhibitorToken);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var card = (await scan.Content.ReadFromJsonAsync<ApiResult<VisitorCard>>())!.Data!;
        Assert.Equal("Visitor One", card.Name);
        Assert.True(card.Available);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", exhibitorToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorVisitorRow>>>())!.Data!;
        Assert.Single(rows);
        Assert.Equal("Visitor One", rows[0].Card.Name);
        Assert.Equal("booth A3", rows[0].Note);
    }

    [Fact]
    public async Task Visitor_caller_cannot_scan_badges_403()
    {
        var (visitorToken, visitorId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(visitorId, "SELFBADGE", "Self", "نفسي");

        var (_, targetId) = await CreateApprovedUserAsync();
        await SeedVisitorWithQrAsync(targetId, "OTHERBADGE", "Other", "آخر");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "OTHERBADGE" }, visitorToken);
        Assert.Equal(HttpStatusCode.Forbidden, scan.StatusCode);

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", visitorToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Unknown_badge_returns_404()
    {
        var (exhibitorToken, exhibitorId) = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitorId, "Empty Booth", "جناح");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = "NOSUCHBADGE" }, exhibitorToken);
        Assert.Equal(HttpStatusCode.NotFound, scan.StatusCode);
    }

    // -- seeding ---------------------------------------------------------------

    private async Task SeedExhibitorProfileAsync(Guid userId, string name, string nameArabic)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // Reuse the seeder's canonical "Exhibitor" profile type — D-611 added a
        // unique index on the active ProfileType.Name (the admin service already
        // returns 409 for the same duplicate), so seeding a second "Exhibitor"
        // row now collides.
        var type = await appDb.ProfileTypes
            .FirstAsync(profileType => profileType.Name == "Exhibitor" && profileType.IsActive);
        var countryId = await appDb.Countries.AsNoTracking()
            .Where(c => c.IsActive).Select(c => c.Id).FirstAsync();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            NameArabic = nameArabic,
            ProfileTypeId = type.Id,
            NationalityId = countryId,
            PlaceOfBirth = string.Empty,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
    }

    private async Task SeedVisitorWithQrAsync(
        Guid userId, string qrId, string name, string nameArabic)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var countryId = await appDb.Countries.AsNoTracking()
            .Where(c => c.IsActive).Select(c => c.Id).FirstAsync();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            NameArabic = nameArabic,
            QrId = qrId,
            NationalityId = countryId,
            PlaceOfBirth = string.Empty,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
    }

    private async Task<(string AccessToken, Guid UserId)> CreateApprovedUserAsync()
    {
        var email = $"ex-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync("/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync("/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);

        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        return (token, UserIdFromToken(token));
    }

    private static Guid UserIdFromToken(string accessToken)
    {
        var payload = accessToken.Split('.')[1];
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
        using var doc = System.Text.Json.JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
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
