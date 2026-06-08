using SIMF.Contracts.Authentication;

namespace SIMF.ApiClient.Tests;

/// <summary>Tests for the per-circuit authenticated-session holder.</summary>
public sealed class SimfAuthSessionTests
{
    [Theory]
    [InlineData("flow-abc@simf.test", "f••••c@simf.test")]
    [InlineData("ab@example.com", "a••••@example.com")]
    [InlineData("a@example.com", "a••••@example.com")]
    [InlineData("no-at-sign", "no-at-sign")]
    public void MaskEmail_keeps_only_the_first_and_last_local_character(string input, string expected)
    {
        Assert.Equal(expected, SimfAuthSession.MaskEmail(input));
    }

    [Theory]
    [InlineData("user@simf.test", true)]
    [InlineData("a@b", true)]
    [InlineData("no-at-sign", false)]
    [InlineData("   ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LooksLikeEmail_requires_a_non_empty_value_containing_an_at_sign(string? input, bool expected)
    {
        Assert.Equal(expected, SimfAuthSession.LooksLikeEmail(input));
    }

    [Theory]
    [InlineData("123456", true)]
    [InlineData("000000", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("12345a", false)]
    [InlineData("12 456", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSixDigitCode_accepts_only_exactly_six_ascii_digits(string? input, bool expected)
    {
        Assert.Equal(expected, SimfAuthSession.IsSixDigitCode(input));
    }

    [Fact]
    public void A_new_session_is_not_signed_in()
    {
        var session = new SimfAuthSession();

        Assert.False(session.IsSignedIn);
        Assert.Null(session.User);
        Assert.Null(session.Tokens);
    }

    [Fact]
    public void Complete_records_the_tokens_and_clears_the_pending_state()
    {
        var session = new SimfAuthSession
        {
            PendingEmail = "user@simf.test",
            PendingMfaToken = "mfa-ticket",
            PendingOtpToken = "otp-ticket",
        };
        var tokens = SampleTokens();

        session.Complete(tokens);

        Assert.True(session.IsSignedIn);
        Assert.Equal(tokens, session.Tokens);
        Assert.Equal("user@simf.test", session.User!.Email);
        Assert.Null(session.PendingEmail);
        Assert.Null(session.PendingMfaToken);
        Assert.Null(session.PendingOtpToken);
    }

    [Fact]
    public void Clear_signs_the_session_out()
    {
        var session = new SimfAuthSession();
        session.Complete(SampleTokens());

        session.Clear();

        Assert.False(session.IsSignedIn);
        Assert.Null(session.Tokens);
        Assert.Null(session.User);
    }

    private static AuthTokens SampleTokens() =>
        new("access-token", "refresh-token", "Bearer", 1800,
            new AuthUser(Guid.NewGuid(), "user@simf.test", "Test User"));
}
