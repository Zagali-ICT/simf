using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Domain.Auditing;
using SIMF.Infrastructure.Identity;

namespace SIMF.Api.Authentication;

/// <summary>
/// Configures JWT bearer validation — hardened token parameters, the
/// security-stamp revocation check, audit of every rejected token, and the
/// standard <c>ApiResult</c> envelope on a 401.
/// </summary>
internal static class JwtBearerSetup
{
    public static void Configure(JwtBearerOptions options, JwtOptions jwt)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            // HS256 is pinned, so an alg-confusion or alg:none token is rejected.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = OnTokenValidatedAsync,
            OnAuthenticationFailed = OnAuthenticationFailedAsync,
            OnChallenge = OnChallengeAsync,
        };
    }

    /// <summary>
    /// Rejects an otherwise-valid token whose security stamp no longer matches
    /// the account — so sign-out and password change revoke live access tokens.
    ///
    /// H5 — D-060: the stamp claim MUST be present and non-empty in the
    /// token; the DB stamp MUST be present and non-empty on the user row;
    /// the comparison runs in constant time. Without these checks, a token
    /// that lacks the claim (or carries an empty one) would compare empty
    /// to empty against an un-stamped row and silently pass — the
    /// revocation control would do nothing for any such token.
    /// </summary>
    private static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirst("sub")?.Value;
        var tokenStamp = context.Principal?.FindFirst("security_stamp")?.Value;

        if (string.IsNullOrEmpty(tokenStamp))
        {
            await AuditRejectionAsync(context.HttpContext, "security stamp claim missing or empty");
            context.Fail("The session is no longer valid.");
            return;
        }

        // R3g — D-079: resolve the repository from request services rather
        // than UserManager directly. Same shape — the bearer events run
        // outside of a constructor injection scope.
        var accounts = context.HttpContext.RequestServices
            .GetRequiredService<IUserAccountRepository>();
        var user = userId is not null && Guid.TryParse(userId, out var parsedId)
            ? await accounts.FindByIdAsync(parsedId)
            : null;

        if (user is null
            || string.IsNullOrEmpty(user.SecurityStamp)
            || !FixedTimeEquals(user.SecurityStamp, tokenStamp))
        {
            await AuditRejectionAsync(context.HttpContext, "security stamp mismatch");
            context.Fail("The session is no longer valid.");
        }
    }

    /// <summary>Constant-time UTF-8 byte comparison so token stamp
    /// validation does not leak prefix information through timing.</summary>
    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    /// <summary>Audits a token rejected by validation — a bad signature, expiry, wrong issuer.</summary>
    private static Task OnAuthenticationFailedAsync(AuthenticationFailedContext context) =>
        AuditRejectionAsync(context.HttpContext, context.Exception.GetType().Name);

    /// <summary>Replaces the framework's empty 401 with the standard ApiResult shape.</summary>
    private static async Task OnChallengeAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(
            ApiResult<object>.Fail(new ApiError
            {
                Code = ErrorCodes.AuthInvalidCredentials,
                Message = "Authentication is required.",
                MessageArabic = "المصادقة مطلوبة.",
            }));
    }

    /// <summary>
    /// H26 — D-086: per-IP cap on bearer-rejection audit DB writes (the
    /// rest of the world reaches the API past UseRouting — pre-routing,
    /// the rate limiter is not in scope). An attacker flooding
    /// <c>Authorization: Bearer &lt;garbage&gt;</c> requests would
    /// otherwise force one synchronous <c>SaveChangesAsync</c> per
    /// request on the audit log (the IAuditLog impl writes synchronously
    /// — see SIMF.Infrastructure.Auditing.AuditLog).
    ///
    /// <para>The throttle keeps the structured log line (Serilog) always
    /// firing so SOC still sees the rejection storm, but drops the DB
    /// write for the same IP past <see cref="MaxAuditWritesPerWindow"/>
    /// in a 60-second sliding window. A future commit can add a
    /// fire-and-forget channel (mirroring the email queue) so the audit
    /// write itself is non-blocking — for now the rate cap is the
    /// minimum-viable DoS guard.</para>
    /// </summary>
    private const int MaxAuditWritesPerWindow = 10;
    private static readonly TimeSpan AuditWindow = TimeSpan.FromMinutes(1);

    private static async Task AuditRejectionAsync(HttpContext httpContext, string detail)
    {
        var services = httpContext.RequestServices;
        var logger = services.GetRequiredService<ILogger<JwtBearerOptions>>();
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Structured log line always fires — SOC's log forwarder sees
        // every rejection, regardless of audit-DB throttling.
        logger.LogWarning(
            "Bearer rejection from {Ip}: {Detail}", ip, detail);

        var cache = services.GetService<IMemoryCache>();
        if (cache is not null)
        {
            var cacheKey = "simf-bearer-rej:" + ip;
            var count = cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = AuditWindow;
                return 0;
            });
            count++;
            cache.Set(cacheKey, count, AuditWindow);
            if (count > MaxAuditWritesPerWindow)
            {
                // Storm — log a single "throttled" line on each subsequent
                // hit so the duration of the storm is visible in the log
                // file, but skip the DB row.
                logger.LogWarning(
                    "Bearer rejection audit suppressed from {Ip} — already wrote {Cap} rows in {Window}s.",
                    ip, MaxAuditWritesPerWindow, (int)AuditWindow.TotalSeconds);
                return;
            }
        }

        await services.GetRequiredService<IAuditLog>().WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AccessTokenRejected,
                Outcome = AuditOutcome.Failure,
                ErrorCode = ErrorCodes.AuthInvalidCredentials,
                Detail = detail,
            });
    }
}
