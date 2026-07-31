// Tests: SIMF.Api.Tests/TokenIssuerParityTests.cs (every sign-in entry point
//        mints the same claim set and the same D-443 lifetime cap)
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Common;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// The one implementation of <see cref="ITokenIssuer"/>. See the interface for
/// why the entry points were collapsed onto it.
/// </summary>
public sealed class TokenIssuer(
    IUserAccountRepository accounts,
    IRefreshTokenRepository refreshTokenRepository,
    IUserProfileService userProfiles,
    IJwtTokenService jwtTokenService,
    IPermissionResolver permissionResolver,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<TokenIssuer> logger) : ITokenIssuer
{
    public async Task<AuthTokens> IssueAsync(
        SimfUser user,
        bool? secondFactorCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var roles = await accounts.GetRolesAsync(user);
        var permissions = await permissionResolver.ResolveForRolesAsync(roles, cancellationToken);
        var mobileAppRole = await userProfiles.ResolveMobileAppRoleAsync(user.Id, cancellationToken);
        var accessToken = jwtTokenService.CreateAccessToken(
            user, roles, permissions, mobileAppRole, secondFactorCompleted);

        var refreshValue = OpaqueToken.Generate();
        var now = timeProvider.SimfNow();
        await refreshTokenRepository.AddAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = OpaqueToken.Hash(refreshValue),
                CreatedAt = now,
                // D-443 (NCA finding): stamp the absolute session deadline
                // (sign-in + Jwt:SessionLifetimeHours). Rotation carries this
                // deadline forward instead of sliding, so the session is capped
                // at 24h from sign-in — on EVERY entry point, which is the cap
                // this extraction exists to keep from drifting.
                ExpiresAt = now.Add(jwtTokenService.RefreshTokenLifetime),
            },
            cancellationToken);

        // A7-31 / A1-19 (NCA) — record this successful sign-in (the activity
        // signal for dormant-account disable) and surface the PRIOR sign-in time
        // to the client for the "last signed in …" notice. UpdateAsync does not
        // roll the security stamp, so the access token just minted stays valid.
        var previousSignInAtUtc = user.LastSuccessfulSignInAtUtc;
        user.LastSuccessfulSignInAtUtc = now;
        await accounts.UpdateAsync(user).EnsureSuccessAsync();

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.RefreshTokenIssued,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
            },
            cancellationToken);
        logger.LogInformation("Session issued for {Email}", user.Email);

        return new AuthTokens(
            accessToken.Value,
            refreshValue,
            "Bearer",
            accessToken.ExpiresInSeconds,
            new AuthUser(user.Id, user.Email!, user.DisplayName),
            previousSignInAtUtc);
    }
}
