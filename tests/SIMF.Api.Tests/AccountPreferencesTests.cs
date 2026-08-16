// `accessibility-server-sync` — GET / PUT /app/account/preferences, the server
// half of the app's five accessibility choices. The shipped Flutter app already
// calls both (accessibility_preferences_repository.dart) and swallows failures,
// so these tests pin the wire contract byte for byte: the camelCase field names,
// the stable textSize enum NAME (never an index), and the per-field defaults
// (captions ON, the rest off).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Account;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AccountPreferencesTests : IClassFixture<SimfApiFactory>
{
    private const string Url = "/api/v1/app/account/preferences";

    /// <summary>The wire is camelCase (FastEndpoints' web defaults), so the raw
    /// body is decoded with the same options the app's client uses.</summary>
    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AccountPreferencesTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Preferences_are_the_defaults_for_an_account_that_never_saved()
    {
        var (token, _) = await CreateApprovedVisitorAsync();

        var response = await GetAuthAsync(Url, token);

        // Defaults, not a 404 — the app's first read on a fresh device must work.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var data = ReadPreferences(raw);

        Assert.Equal(AccountPreferences.TextSizeNormal, data.TextSize);
        Assert.False(data.HighContrast);
        Assert.False(data.ReduceMotion);
        Assert.False(data.ScreenReaderAssist);
        Assert.True(data.Captions);

        // The load-bearing half. Defaults alone are ambiguous — they are also what a
        // user who deliberately chose them reads back — and the app APPLIES whatever
        // the GET returns over its local cache at sign-in. Without configured:false a
        // low-vision user who had already set extraLarge + high contrast on the
        // device would be silently reset to normal on their first sign-in after this
        // endpoint shipped. False here is what tells the app to keep, and upload,
        // what it already has.
        Assert.False(data.Configured);

        // The shipped app decodes these exact camelCase keys and silently falls
        // back on anything else, so the names are part of the contract.
        Assert.Contains("\"textSize\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"highContrast\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"reduceMotion\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"screenReaderAssist\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"captions\"", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saved_preferences_round_trip_through_a_second_read()
    {
        var (token, userId) = await CreateApprovedVisitorAsync();

        // Every value flipped away from its default — captions especially, since
        // its column defaults to 1 and an explicit false must still be written.
        var saved = await PutAuthAsync(
            Url, token, BuildBody(AccountPreferences.TextSizeExtraLarge, true, true, true, false));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedData = ReadPreferences(await saved.Content.ReadAsStringAsync());
        Assert.Equal(AccountPreferences.TextSizeExtraLarge, savedData.TextSize);
        Assert.False(savedData.Captions);

        var reread = await GetAuthAsync(Url, token);
        Assert.Equal(HttpStatusCode.OK, reread.StatusCode);
        var data = ReadPreferences(await reread.Content.ReadAsStringAsync());

        Assert.Equal(AccountPreferences.TextSizeExtraLarge, data.TextSize);
        Assert.True(data.HighContrast);
        Assert.True(data.ReduceMotion);
        Assert.True(data.ScreenReaderAssist);
        Assert.False(data.Captions);

        // A saved account reads back configured:true — the flag flips on the write,
        // so from here the app may safely replace its local cache with these values.
        Assert.True(data.Configured);
        Assert.True(savedData.Configured);

        // Stored as the stable NAME, never as an enum index.
        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = app.UserProfiles.Single(row => row.UserId == userId);
        Assert.Equal("extraLarge", profile.AccessibilityTextSize);
        Assert.False(profile.AccessibilityCaptions);
    }

    [Fact]
    public async Task Saving_the_same_preferences_twice_is_idempotent()
    {
        var (token, userId) = await CreateApprovedVisitorAsync();
        var body = BuildBody(AccountPreferences.TextSizeLarge, true, false, false, false);

        var first = await PutAuthAsync(Url, token, body);
        var second = await PutAuthAsync(Url, token, body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Comparing the two PUT RESPONSES would assert nothing: SaveMineAsync echoes
        // the request back, so identical bodies produce identical responses even if
        // the second write did nothing at all. Re-READ instead, through the GET, so
        // the assertion is about what is actually stored.
        var rereadResponse = await GetAuthAsync(Url, token);
        var reread = ReadPreferences(await rereadResponse.Content.ReadAsStringAsync());
        Assert.Equal(AccountPreferences.TextSizeLarge, reread.TextSize);
        Assert.True(reread.HighContrast);
        Assert.False(reread.ReduceMotion);
        Assert.False(reread.ScreenReaderAssist);
        Assert.False(reread.Captions);

        // The second PUT must update the row the first one seeded, not add a sibling.
        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(1, app.UserProfiles.Count(row => row.UserId == userId));
    }

    [Fact]
    public async Task Saving_preferences_before_registration_leaves_the_profile_incomplete()
    {
        // The PUT seeds a stub profile row when there is none (same contract as
        // the ID-document upload). The stub must stay "no registration form
        // filled" — otherwise picking a text size would look like a completed
        // registration.
        var (token, userId) = await CreateApprovedVisitorAsync();

        await PutAuthAsync(
            Url, token, BuildBody(AccountPreferences.TextSizeSmall, false, false, false, true));

        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = app.UserProfiles.Single(row => row.UserId == userId);
        Assert.Equal(string.Empty, profile.Name);
        Assert.Equal(string.Empty, profile.NameArabic);
        Assert.Null(profile.ProfileTypeId);
    }

    [Fact]
    public async Task An_unknown_text_size_is_rejected_with_a_bilingual_error()
    {
        var (token, userId) = await CreateApprovedVisitorAsync();

        // "ExtraLarge" is the right choice in the wrong case — the app compares
        // the name byte for byte, so accepting it would read back as the fallback.
        var response = await PutAuthAsync(
            Url, token, BuildBody("ExtraLarge", false, false, false, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AccountPreferences>>())!;
        Assert.False(body.Success);
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.Message));
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));
        Assert.NotEqual(body.Error.Message, body.Error.MessageArabic);
        var detail = Assert.Single(body.Error.Details);
        Assert.Equal("textSize", detail.Field);
        Assert.Contains("extraLarge", detail.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(detail.MessageArabic));

        // Nothing was written — a rejected save must not seed a stub row either.
        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Empty(app.UserProfiles.Where(row => row.UserId == userId));
    }

    [Fact]
    public async Task Preferences_without_a_token_return_401()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync(Url)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.PutAsJsonAsync(
                Url, BuildBody(AccountPreferences.TextSizeNormal, false, false, false, true)))
                .StatusCode);
    }

    [Fact]
    public async Task Preferences_for_a_not_yet_approved_account_are_forbidden()
    {
        // A verified-but-not-approved visitor holds a valid token but fails the
        // RequireApprovedAccount policy on both verbs.
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await GetAuthAsync(Url, tokens.AccessToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await PutAuthAsync(
                Url, tokens.AccessToken,
                BuildBody(AccountPreferences.TextSizeNormal, false, false, false, true)))
                .StatusCode);
    }

    [Fact]
    public void The_stored_default_text_size_matches_the_contract_default()
    {
        // SIMF.Domain does not reference SIMF.Contracts, so the column default and
        // the wire default are two constants. Pin them together here.
        Assert.Equal(
            AccountPreferences.DefaultTextSize,
            UserProfile.DefaultAccessibilityTextSize);
        Assert.Contains(
            UserProfile.DefaultAccessibilityTextSize,
            AccountPreferences.AllowedTextSizes);
    }

    // -- Helpers --------------------------------------------------------------

    private static Dictionary<string, object> BuildBody(
        string textSize,
        bool highContrast,
        bool reduceMotion,
        bool screenReaderAssist,
        bool captions) =>
        new()
        {
            ["textSize"] = textSize,
            ["highContrast"] = highContrast,
            ["reduceMotion"] = reduceMotion,
            ["screenReaderAssist"] = screenReaderAssist,
            ["captions"] = captions,
        };

    private static AccountPreferences ReadPreferences(string rawJson)
    {
        var envelope = JsonSerializer.Deserialize<ApiResult<AccountPreferences>>(
            rawJson, WireJsonOptions)!;
        Assert.True(envelope.Success);
        return envelope.Data!;
    }

    private async Task<(string Token, Guid UserId)> CreateApprovedVisitorAsync()
    {
        var email = $"prefs-visitor-{Guid.NewGuid():N}@simf.test";
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
        // Approve BEFORE sign-in so the minted JWT carries account_state=Approved,
        // and disable 2FA so the password step issues tokens directly.
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var userId = identity.Users.Single(u => u.Email == email).Id;
        return (token, userId);
    }

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PutAuthAsync(
        string url, string token, Dictionary<string, object> body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
