namespace SIMF.Api.Tests;

/// <summary>
/// A <see cref="SimfApiFactory"/> with the per-IP cap left permissive and
/// the per-email cap tightened to 3 — so the per-email partition is the
/// one that trips first in the H7 (D-062) test. Its own host means the
/// low limit does not affect any other test class.
/// </summary>
public sealed class EmailRateLimitedApiFactory : SimfApiFactory
{
    public EmailRateLimitedApiFactory()
    {
        // The base SimfApiFactory sets RateLimit__PermitLimit = 100000;
        // leave that and tighten only the email cap so this test
        // demonstrates the per-email partition independently.
        Environment.SetEnvironmentVariable("RateLimit__EmailPermitLimit", "3");
    }
}
