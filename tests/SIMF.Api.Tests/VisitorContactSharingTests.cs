// SIMF-FDS-014 §5.4-5.7 (D-286, Track 2) — visitor-to-visitor contact sharing:
// share-token mint/rotate, token→live-card resolve, save/list/remove, vCard.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Contacts;
using SIMF.Domain.Organisations;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class VisitorContactSharingTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public VisitorContactSharingTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Share_token_is_minted_once_and_is_idempotent()
    {
        var (token, _) = await CreateApprovedVisitorAsync();
        var first = await GetShareTokenAsync(token);
        var second = await GetShareTokenAsync(token);
        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Rotate_changes_the_token_and_the_old_one_stops_resolving()
    {
        var (subjectToken, subjectId) = await CreateApprovedVisitorAsync();
        await SeedProfileAsync(subjectId, "Subject One", "الأول");
        var (ownerToken, _) = await CreateApprovedVisitorAsync();

        var oldCode = await GetShareTokenAsync(subjectToken);
        Assert.Equal(HttpStatusCode.OK, (await ResolveAsync(ownerToken, oldCode)).StatusCode);

        var rotate = await PostAuthAsync("/api/v1/app/account/share-token/rotate", new { }, subjectToken);
        Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);
        var newCode = (await rotate.Content
            .ReadFromJsonAsync<ApiResult<VisitorShareTokenResponse>>())!.Data!.Token;
        Assert.NotEqual(oldCode, newCode);

        Assert.Equal(HttpStatusCode.NotFound, (await ResolveAsync(ownerToken, oldCode)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ResolveAsync(ownerToken, newCode)).StatusCode);
    }

    [Fact]
    public async Task Resolve_returns_the_live_card_from_the_subjects_profile()
    {
        var (subjectToken, subjectId) = await CreateApprovedVisitorAsync();
        var (countryId, countryEn, _) = await FirstActiveCountryAsync();
        var orgId = await SeedOrganisationAsync("Acme Naval", "أكمي البحرية");
        await SeedProfileAsync(subjectId, "Captain Subject", "الكابتن",
            jobTitle: "Captain", saudiMobile: "+966500000000",
            organisationId: orgId, nationalityId: countryId);
        var code = await GetShareTokenAsync(subjectToken);

        var (ownerToken, _) = await CreateApprovedVisitorAsync();
        var resolve = await ResolveAsync(ownerToken, code);
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        var card = (await resolve.Content.ReadFromJsonAsync<ApiResult<VisitorCard>>())!.Data!;

        Assert.True(card.Available);
        Assert.Equal("Captain Subject", card.Name);
        Assert.Equal("Captain", card.JobTitle);
        Assert.Equal("Acme Naval", card.Organisation);
        Assert.Equal("+966500000000", card.SaudiMobile);
        Assert.Equal(countryId, card.CountryId);
        Assert.Equal(countryEn, card.CountryName);
        Assert.False(string.IsNullOrWhiteSpace(card.Email)); // resolved cross-DB from Identity
    }

    [Fact]
    public async Task Save_is_idempotent_and_appears_in_my_contacts()
    {
        var (subjectToken, subjectId) = await CreateApprovedVisitorAsync();
        await SeedProfileAsync(subjectId, "Saved Subject", "محفوظ");
        var code = await GetShareTokenAsync(subjectToken);
        var (ownerToken, _) = await CreateApprovedVisitorAsync();

        var save1 = await PostAuthAsync("/api/v1/app/contacts/save",
            new SaveContactRequest { Token = code, Note = "met at booth" }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, save1.StatusCode);
        var row1 = (await save1.Content.ReadFromJsonAsync<ApiResult<SavedContactRow>>())!.Data!;
        Assert.Equal("Saved Subject", row1.Name);
        Assert.Equal("met at booth", row1.Note);

        // Re-save the same subject is idempotent (same row) and refreshes the note.
        var save2 = await PostAuthAsync("/api/v1/app/contacts/save",
            new SaveContactRequest { Token = code, Note = "updated note" }, ownerToken);
        var row2 = (await save2.Content.ReadFromJsonAsync<ApiResult<SavedContactRow>>())!.Data!;
        Assert.Equal(row1.Id, row2.Id);

        var list = await ListSavedAsync(ownerToken);
        var listed = Assert.Single(list, r => r.Id == row1.Id);
        Assert.Equal("updated note", listed.Note);
    }

    [Fact]
    public async Task Save_own_token_is_400()
    {
        var (token, subjectId) = await CreateApprovedVisitorAsync();
        await SeedProfileAsync(subjectId, "Self", "نفسي");
        var code = await GetShareTokenAsync(token);

        var response = await PostAuthAsync("/api/v1/app/contacts/save",
            new SaveContactRequest { Token = code }, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);
    }

    [Fact]
    public async Task Remove_soft_deletes_and_drops_from_list()
    {
        var (subjectToken, subjectId) = await CreateApprovedVisitorAsync();
        await SeedProfileAsync(subjectId, "To Remove", "للحذف");
        var code = await GetShareTokenAsync(subjectToken);
        var (ownerToken, _) = await CreateApprovedVisitorAsync();
        var save = await PostAuthAsync("/api/v1/app/contacts/save",
            new SaveContactRequest { Token = code }, ownerToken);
        var id = (await save.Content.ReadFromJsonAsync<ApiResult<SavedContactRow>>())!.Data!.Id;

        var del = await DeleteAuthAsync($"/api/v1/app/contacts/{id}", ownerToken);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        Assert.DoesNotContain(await ListSavedAsync(ownerToken), r => r.Id == id);

        // Idempotent.
        var del2 = await DeleteAuthAsync($"/api/v1/app/contacts/{id}", ownerToken);
        Assert.Equal(HttpStatusCode.OK, del2.StatusCode);
    }

    [Fact]
    public async Task Resolve_unknown_token_is_404()
    {
        var (ownerToken, _) = await CreateApprovedVisitorAsync();
        var response = await ResolveAsync(ownerToken, "ZZZZZZZZZZZZ");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Vcard_export_returns_a_vcard()
    {
        var (subjectToken, subjectId) = await CreateApprovedVisitorAsync();
        await SeedProfileAsync(subjectId, "VCard Subject", "بطاقة",
            jobTitle: "Director", saudiMobile: "+966511111111");
        var code = await GetShareTokenAsync(subjectToken);
        var (ownerToken, _) = await CreateApprovedVisitorAsync();
        var save = await PostAuthAsync("/api/v1/app/contacts/save",
            new SaveContactRequest { Token = code }, ownerToken);
        var id = (await save.Content.ReadFromJsonAsync<ApiResult<SavedContactRow>>())!.Data!.Id;

        var vcard = await GetAuthAsync($"/api/v1/app/contacts/{id}/vcard", ownerToken);
        Assert.Equal(HttpStatusCode.OK, vcard.StatusCode);
        var body = await vcard.Content.ReadAsStringAsync();
        Assert.Contains("BEGIN:VCARD", body);
        Assert.Contains("VCard Subject", body);
        Assert.Contains("TITLE:Director", body);
        Assert.Contains("TEL", body);
    }

    [Fact]
    public async Task Share_token_requires_authentication()
    {
        var response = await _client.GetAsync("/api/v1/app/account/share-token");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> GetShareTokenAsync(string token)
    {
        var response = await GetAuthAsync("/api/v1/app/account/share-token", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<VisitorShareTokenResponse>>())!.Data!.Token;
    }

    private Task<HttpResponseMessage> ResolveAsync(string token, string code) =>
        PostAuthAsync("/api/v1/app/contacts/resolve",
            new ResolveContactRequest { Token = code }, token);

    private async Task<IReadOnlyList<SavedContactRow>> ListSavedAsync(string token)
    {
        var response = await GetAuthAsync("/api/v1/app/contacts", token);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SavedContactRow>>>())!.Data!;
    }

    private async Task SeedProfileAsync(
        Guid userId, string name, string nameArabic,
        string? jobTitle = null, string? saudiMobile = null,
        Guid? organisationId = null, int? nationalityId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var countryId = nationalityId ?? await appDb.Countries.AsNoTracking()
            .Where(c => c.IsActive).Select(c => c.Id).FirstAsync();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            NameArabic = nameArabic,
            JobTitle = jobTitle,
            SaudiMobile = saudiMobile,
            OrganisationId = organisationId,
            NationalityId = countryId,
            PlaceOfBirth = string.Empty,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
    }

    private async Task<Guid> SeedOrganisationAsync(string nameEn, string nameAr)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = nameEn,
            NameArabic = nameAr,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Organisations.Add(org);
        await appDb.SaveChangesAsync();
        return org.Id;
    }

    private async Task<(int Id, string? NameEn, string NameArabic)> FirstActiveCountryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var country = await appDb.Countries.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.Name, c.NameArabic })
            .FirstAsync();
        return (country.Id, country.Name, country.NameArabic);
    }

    private async Task<(string AccessToken, Guid UserId)> CreateApprovedVisitorAsync()
    {
        var email = $"vc-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync("/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync("/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        // D-373 — registration enables 2FA; this auth plumbing needs the
        // direct-token path (the admin-disabled scenario).
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
