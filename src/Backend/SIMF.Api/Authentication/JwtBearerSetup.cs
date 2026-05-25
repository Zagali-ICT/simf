using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
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

        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<SimfUser>>();
        var user = userId is not null && Guid.TryParse(userId, out _)
            ? await userManager.FindByIdAsync(userId)
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

    private static Task AuditRejectionAsync(HttpContext httpContext, string detail) =>
        httpContext.RequestServices.GetRequiredService<IAuditLog>().WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AccessTokenRejected,
                Outcome = AuditOutcome.Failure,
                ErrorCode = ErrorCodes.AuthInvalidCredentials,
                Detail = detail,
            });
}
