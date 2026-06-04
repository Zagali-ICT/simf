namespace SIMF.Api.Tests;

/// <summary>
/// A <see cref="SimfApiFactory"/> with a deliberately low rate limit, used by
/// the rate-limit tests. Its own host means the low limit does not affect any
/// other test class.
/// </summary>
public sealed class RateLimitedApiFactory : SimfApiFactory
{
    public RateLimitedApiFactory()
    {
        Environment.SetEnvironmentVariable("RateLimit__PermitLimit", "3");
    }
}
