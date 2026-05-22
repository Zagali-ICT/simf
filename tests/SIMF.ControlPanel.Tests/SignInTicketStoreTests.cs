using Microsoft.Extensions.Caching.Memory;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Tests;

/// <summary>Tests for the one-time sign-in hand-off store.</summary>
public sealed class SignInTicketStoreTests
{
    [Fact]
    public void Stash_then_Redeem_returns_the_same_tokens()
    {
        var store = NewStore();
        var tokens = SampleTokens();

        var reference = store.Stash(tokens);

        Assert.Same(tokens, store.Redeem(reference));
    }

    [Fact]
    public void A_reference_can_be_redeemed_only_once()
    {
        var store = NewStore();
        var reference = store.Stash(SampleTokens());

        Assert.NotNull(store.Redeem(reference));
        Assert.Null(store.Redeem(reference));
    }

    [Fact]
    public void An_unknown_reference_redeems_to_null()
    {
        var store = NewStore();

        Assert.Null(store.Redeem(Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public void Each_stash_produces_a_distinct_reference()
    {
        var store = NewStore();

        Assert.NotEqual(store.Stash(SampleTokens()), store.Stash(SampleTokens()));
    }

    private static SignInTicketStore NewStore() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    private static AuthTokens SampleTokens() =>
        new("access-token", "refresh-token", "Bearer", 1800,
            new AuthUser(Guid.NewGuid(), "admin@simf.test", "Admin User"));
}
