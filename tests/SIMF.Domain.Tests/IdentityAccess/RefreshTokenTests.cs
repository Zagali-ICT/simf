using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Domain.Tests.IdentityAccess;

/// <summary>Unit tests for <see cref="RefreshToken.IsActive"/>.</summary>
public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsActive_is_true_when_not_revoked_and_not_expired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = Now.AddDays(30),
            RevokedAt = null,
        };

        Assert.True(token.IsActive(Now));
    }

    [Fact]
    public void IsActive_is_false_when_revoked()
    {
        var token = new RefreshToken
        {
            ExpiresAt = Now.AddDays(30),
            RevokedAt = Now.AddMinutes(-1),
        };

        Assert.False(token.IsActive(Now));
    }

    [Fact]
    public void IsActive_is_false_when_expired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = Now.AddMinutes(-1),
            RevokedAt = null,
        };

        Assert.False(token.IsActive(Now));
    }
}
