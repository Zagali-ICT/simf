// D-194 — closes the "Tests: SIMF.Web.Tests/SimfCookieRefreshHandlerTests.cs
// (todo)" marker. Covers the two pure-logic pieces of the D-121
// cookie-refresh hook: the StoreTokens persistence round-trip and the
// NeedsRefresh decision boundary (exposed via InternalsVisibleTo).
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using SIMF.Contracts.Authentication;
using SIMF.Web;

namespace SIMF.Web.Tests;

public sealed class SimfCookieRefreshHandlerTests
{
    private static AuthTokens Tokens(int expiresInSeconds = 1800) =>
        new("access-token", "refresh-token", "Bearer", expiresInSeconds,
            new AuthUser(Guid.NewGuid(), "visitor@simf.test", "Visitor User"));

    [Fact]
    public void StoreTokens_persists_access_refresh_and_expires_at()
    {
        var props = new AuthenticationProperties();

        SimfCookieRefreshHandler.StoreTokens(props, Tokens(), DateTimeOffset.UtcNow);

        Assert.Equal("access-token", props.GetTokenValue("access_token"));
        Assert.Equal("refresh-token", props.GetTokenValue("refresh_token"));
        Assert.NotNull(props.GetTokenValue(SimfCookieRefreshHandler.ExpiresAtTokenName));
    }

    [Fact]
    public void StoreTokens_writes_expires_at_as_issuedAt_plus_lifetime_in_roundtrip_format()
    {
        var props = new AuthenticationProperties();
        var issuedAt = new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);

        SimfCookieRefreshHandler.StoreTokens(props, Tokens(expiresInSeconds: 1800), issuedAt);

        var raw = props.GetTokenValue(SimfCookieRefreshHandler.ExpiresAtTokenName);
        Assert.NotNull(raw);
        var parsed = DateTimeOffset.Parse(
            raw!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Assert.Equal(issuedAt.AddSeconds(1800), parsed);
    }

    [Fact]
    public void NeedsRefresh_is_true_when_expires_at_is_missing()
    {
        // A cookie minted before D-121 carries no expires_at — the safe
        // move is to refresh immediately rather than wait for a 401.
        Assert.True(SimfCookieRefreshHandler.NeedsRefresh(null));
        Assert.True(SimfCookieRefreshHandler.NeedsRefresh(string.Empty));
    }

    [Fact]
    public void NeedsRefresh_is_true_when_expires_at_is_unparseable()
    {
        Assert.True(SimfCookieRefreshHandler.NeedsRefresh("not-a-timestamp"));
    }

    [Fact]
    public void NeedsRefresh_is_true_when_within_the_two_minute_threshold()
    {
        var soon = DateTimeOffset.UtcNow.AddSeconds(90)
            .ToString("O", CultureInfo.InvariantCulture);
        Assert.True(SimfCookieRefreshHandler.NeedsRefresh(soon));
    }

    [Fact]
    public void NeedsRefresh_is_true_when_already_expired()
    {
        var past = DateTimeOffset.UtcNow.AddMinutes(-5)
            .ToString("O", CultureInfo.InvariantCulture);
        Assert.True(SimfCookieRefreshHandler.NeedsRefresh(past));
    }

    [Fact]
    public void NeedsRefresh_is_false_when_well_inside_the_valid_window()
    {
        var later = DateTimeOffset.UtcNow.AddMinutes(30)
            .ToString("O", CultureInfo.InvariantCulture);
        Assert.False(SimfCookieRefreshHandler.NeedsRefresh(later));
    }

    [Fact]
    public void StoreTokens_then_NeedsRefresh_is_false_for_a_fresh_30min_token()
    {
        // The end-to-end invariant: a token just stored with a 30-minute
        // lifetime does NOT immediately need refresh (this is the bug
        // D-121's expires_at write fixed — without it the very next
        // request would refresh).
        var props = new AuthenticationProperties();
        SimfCookieRefreshHandler.StoreTokens(props, Tokens(1800), DateTimeOffset.UtcNow);

        var raw = props.GetTokenValue(SimfCookieRefreshHandler.ExpiresAtTokenName);
        Assert.False(SimfCookieRefreshHandler.NeedsRefresh(raw));
    }
}
