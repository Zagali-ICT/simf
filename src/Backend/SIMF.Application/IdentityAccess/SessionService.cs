using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Implements the session lifecycle — refresh-token rotation with stolen-token
/// reuse detection, and sign-out.
/// </summary>
public sealed class SessionService(
    IUserAccountRepository accounts,
    IRefreshTokenRepository refreshTokenRepository,
    ITransactionRunner transactionRunner,
    IJwtTokenService jwtTokenService,
    IPermissionResolver permissionResolver,
    IUserProfileService userProfiles,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SessionService> logger) : ISessionService
{
    public async Task<AuthTokens> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();
        var presented = await refreshTokenRepository.GetByTokenHashAsync(
            OpaqueToken.Hash(request.RefreshToken), cancellationToken);

        if (presented is null)
        {
            await AuditAsync(AuditEvents.RefreshTokenRejected, AuditOutcome.Failure,
                null, null, ErrorCodes.AuthRefreshTokenInvalid, "token not found", cancellationToken);
            throw new ApiException(ErrorCodes.AuthRefreshTokenInvalid, 401,
                "The refresh token is not valid.",
                "رمز التحديث غير صالح.");
        }

        // A token that was already revoked is being presented again — it has
        // been rotated away, so its reuse means it was stolen. Kill every
        // session for the account.
        if (presented.RevokedAt is not null)
        {
            await refreshTokenRepository.RevokeAllForUserAsync(
                presented.UserId, now, cancellationToken);
            var compromised = await accounts.FindByIdAsync(presented.UserId, cancellationToken);
            if (compromised is not null)
            {
                await accounts.UpdateSecurityStampAsync(compromised);
            }

            await AuditAsync(AuditEvents.RefreshTokenReused, AuditOutcome.Failure,
                compromised?.Email, presented.UserId, ErrorCodes.AuthRefreshTokenInvalid,
                $"reused token {presented.Id}", cancellationToken);
            logger.LogWarning(
                "Refresh-token reuse detected for user {UserId}; every session was revoked.",
                presented.UserId);
            throw new ApiException(ErrorCodes.AuthRefreshTokenInvalid, 401,
                "The refresh token is not valid.",
                "رمز التحديث غير صالح.");
        }

        if (now >= presented.ExpiresAt)
        {
            await AuditAsync(AuditEvents.RefreshTokenRejected, AuditOutcome.Failure,
                null, presented.UserId, ErrorCodes.AuthRefreshTokenExpired, "token expired",
                cancellationToken);
            throw new ApiException(ErrorCodes.AuthRefreshTokenExpired, 401,
                "The refresh token has expired. Sign in again.",
                "انتهت صلاحية رمز التحديث. سجّل الدخول مرة أخرى.");
        }

        var user = await accounts.FindByIdAsync(presented.UserId, cancellationToken)
            ?? throw new ApiException(ErrorCodes.AuthRefreshTokenInvalid, 401,
                "The refresh token is not valid.",
                "رمز التحديث غير صالح.");

        if (user.AccountState is AccountState.Disabled or AccountState.Rejected)
        {
            await AuditAsync(AuditEvents.RefreshTokenRejected, AuditOutcome.Failure,
                user.Email, user.Id, ErrorCodes.AuthAccountDisabled, "account not active",
                cancellationToken);
            throw new ApiException(ErrorCodes.AuthAccountDisabled, 403,
                "This account is not active.",
                "هذا الحساب غير نشط.");
        }

        // A refresh-token holder cannot mint new access tokens
        // after an admin sets PasswordChangeRequired. Pre-H19 the only check
        // was at the password step, so any live refresh token rotated forever.
        if (user.PasswordChangeRequired)
        {
            await AuditAsync(AuditEvents.RefreshTokenRejected, AuditOutcome.Failure,
                user.Email, user.Id, ErrorCodes.AuthPasswordChangeRequired,
                "password change required", cancellationToken);
            throw new ApiException(ErrorCodes.AuthPasswordChangeRequired, 403,
                "You must change your password before signing in again.",
                "يجب تغيير كلمة المرور قبل تسجيل الدخول مرة أخرى.");
        }

        // Rotate atomically — the presented token is revoked only if it is still
        // active, and its replacement is added, in one transaction. A conditional
        // revoke affecting no row means a concurrent request already rotated this
        // token, so there is no double-spend.
        var refreshValue = OpaqueToken.Generate();
        await transactionRunner.ExecuteAsync(
            async token =>
            {
                var rotatedRows = await refreshTokenRepository.RevokeIfActiveAsync(
                    presented.Id, now, token);
                if (rotatedRows == 0)
                {
                    throw new ApiException(ErrorCodes.AuthRefreshTokenInvalid, 401,
                        "The refresh token is not valid.",
                        "رمز التحديث غير صالح.");
                }

                await refreshTokenRepository.AddAsync(
                    new RefreshToken
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        TokenHash = OpaqueToken.Hash(refreshValue),
                        CreatedAt = now,
                        // The NCA-required absolute 24h session cap. The
                        // replacement inherits the chain's original deadline
                        // (sign-in + Jwt:SessionLifetimeHours) instead of
                        // sliding a fresh window, so rotation can never push a
                        // session past the cap — once now >= ExpiresAt the
                        // guard above rejects the refresh and forces re-login.
                        ExpiresAt = presented.ExpiresAt,
                        RotatedFromId = presented.Id,
                    },
                    token);
            },
            cancellationToken);

        var roles = await accounts.GetRolesAsync(user);
        var permissions = await permissionResolver.ResolveForRolesAsync(roles, cancellationToken);
        var mobileAppRole = await userProfiles.ResolveMobileAppRoleAsync(user.Id, cancellationToken);
        var accessToken = jwtTokenService.CreateAccessToken(user, roles, permissions, mobileAppRole);

        await AuditAsync(AuditEvents.RefreshTokenRotated, AuditOutcome.Success,
            user.Email, user.Id, cancellationToken: cancellationToken);

        return new AuthTokens(
            accessToken.Value,
            refreshValue,
            "Bearer",
            accessToken.ExpiresInSeconds,
            new AuthUser(user.Id, user.Email!, user.DisplayName));
    }

    public async Task SignOutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            // A valid token for an account that no longer exists — worth a trace.
            logger.LogWarning("Sign-out for unknown user {UserId}.", userId);
            return;
        }

        // Revoke every refresh token and roll the security stamp — this ends all
        // sessions, since the stamp is per-account and every access token carries
        // it.
        await refreshTokenRepository.RevokeAllForUserAsync(
            user.Id, timeProvider.SimfNow(), cancellationToken);
        await accounts.UpdateSecurityStampAsync(user);

        await AuditAsync(AuditEvents.SignOutSucceeded, AuditOutcome.Success,
            user.Email, user.Id, cancellationToken: cancellationToken);
        logger.LogInformation("Sign-out completed for {Email}", user.Email);
    }

    private Task AuditAsync(
        string eventType,
        AuditOutcome outcome,
        string? email,
        Guid? userId = null,
        string? errorCode = null,
        string? detail = null,
        CancellationToken cancellationToken = default) =>
        auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = eventType,
                Outcome = outcome,
                SubjectEmail = email,
                SubjectUserId = userId,
                ErrorCode = errorCode,
                Detail = detail,
            },
            cancellationToken);
}
