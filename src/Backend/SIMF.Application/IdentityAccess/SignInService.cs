using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Implements sign-in (SIMF-API-001 section 12.4, SIMF-FDS-001 section 5). The
/// password step is always followed by a second factor: a Control Panel user —
/// anyone holding a role — completes it with an authenticator TOTP code;
/// every other user with a code emailed to them. Tokens are issued only once
/// the second factor passes.
/// </summary>
public sealed class SignInService(
    UserManager<SimfUser> userManager,
    ISecondFactorTokenRepository secondFactorTokenRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IAccountCodeRepository accountCodeRepository,
    IEmailQueue emailQueue,
    IJwtTokenService jwtTokenService,
    ITotpVerifier totpVerifier,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SignInService> logger) : ISignInService
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private const int MaxSecondFactorAttempts = 5;

    public async Task<SignInResponse> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is not null && await userManager.IsLockedOutAsync(user))
        {
            await AuditAsync(AuditEvents.SignInAccountLockedOut, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthAccountLocked, cancellationToken);
            throw new ApiException(ErrorCodes.AuthAccountLocked, 423,
                "The account is locked after too many attempts. Try again later.");
        }

        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            if (user is not null)
            {
                await userManager.AccessFailedAsync(user);
            }

            // One generic response — it never reveals whether the email exists.
            await AuditAsync(AuditEvents.SignInBadCredentials, AuditOutcome.Failure,
                request.Email, user?.Id, ErrorCodes.AuthInvalidCredentials, cancellationToken);
            throw new ApiException(ErrorCodes.AuthInvalidCredentials, 401,
                "The email address or password is not correct.");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var (blockCode, blockMessage) = CheckAccountState(user);
        if (blockCode is not null)
        {
            await AuditAsync(AuditEvents.SignInStateBlocked, AuditOutcome.Failure,
                user.Email!, user.Id, blockCode, cancellationToken);
            throw new ApiException(blockCode, 403, blockMessage!);
        }

        var now = timeProvider.GetUtcNow();
        var roles = await userManager.GetRolesAsync(user);

        // A user holding any role is a Control Panel user and uses TOTP; every
        // other user completes sign-in with an emailed code (SIMF-FDS-001 §5.6).
        var kind = roles.Count > 0 ? SecondFactorKind.Totp : SecondFactorKind.EmailOtp;
        var ticketValue = OpaqueToken.Generate();

        await secondFactorTokenRepository.AddAsync(
            new SecondFactorToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = OpaqueToken.Hash(ticketValue),
                Kind = kind,
                CreatedAt = now,
                ExpiresAt = now.Add(TicketLifetime),
            },
            cancellationToken);

        await AuditAsync(AuditEvents.SignInSecondFactorIssued, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);

        if (kind == SecondFactorKind.Totp)
        {
            return new SignInResponse(true, ticketValue, null);
        }

        var code = await IssueSignInOtpAsync(user, now, cancellationToken);
        EnqueueSignInOtpEmail(user.Email!, code);
        return new SignInResponse(true, null, ticketValue);
    }

    public async Task<AuthTokens> VerifyTotpAsync(
        VerifyTotpRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await GetValidTicketAsync(
            request.MfaToken, SecondFactorKind.Totp, cancellationToken);
        var user = await userManager.FindByIdAsync(ticket.UserId.ToString())
            ?? throw new ApiException(ErrorCodes.AuthMfaTokenInvalid, 400,
                "The sign-in session is no longer valid.");

        var secret = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(secret) || !totpVerifier.Verify(secret, request.Code))
        {
            ticket.AttemptCount++;
            await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);
            await AuditAsync(AuditEvents.SignInSecondFactorFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthTotpInvalid, cancellationToken);
            throw new ApiException(ErrorCodes.AuthTotpInvalid, 400,
                "The verification code is not correct.");
        }

        ticket.ConsumedAt = timeProvider.GetUtcNow();
        await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthTokens> VerifyOtpAsync(
        VerifyOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await GetValidTicketAsync(
            request.OtpToken, SecondFactorKind.EmailOtp, cancellationToken);
        var user = await userManager.FindByIdAsync(ticket.UserId.ToString())
            ?? throw new ApiException(ErrorCodes.AuthOtpTokenInvalid, 400,
                "The sign-in session is no longer valid.");

        var now = timeProvider.GetUtcNow();
        var code = await accountCodeRepository.GetLatestUnconsumedAsync(
            user.Id, AccountCodePurpose.SignInOtp, cancellationToken);

        if (code is null || now >= code.ExpiresAt)
        {
            await AuditAsync(AuditEvents.SignInSecondFactorFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthOtpExpired, cancellationToken);
            throw new ApiException(ErrorCodes.AuthOtpExpired, 400,
                "The code has expired. Sign in again to get a new one.");
        }

        if (!CodesMatch(code.Code, request.Code))
        {
            ticket.AttemptCount++;
            await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);
            await AuditAsync(AuditEvents.SignInSecondFactorFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthOtpInvalid, cancellationToken);
            throw new ApiException(ErrorCodes.AuthOtpInvalid, 400,
                "The code is not correct.");
        }

        code.ConsumedAt = now;
        await accountCodeRepository.UpdateAsync(code, cancellationToken);
        ticket.ConsumedAt = now;
        await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);
        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <summary>
    /// The account states that block sign-in. <c>EmailVerified</c>,
    /// <c>PendingApproval</c> and <c>Approved</c> may sign in — the access a
    /// not-yet-approved user then has is an authorisation concern (SIMF-RPM-001
    /// section 10, decision D-010).
    /// </summary>
    private static (string? Code, string? Message) CheckAccountState(SimfUser user) =>
        user.AccountState switch
        {
            AccountState.Registered =>
                (ErrorCodes.AuthEmailNotVerified, "Verify your email address before signing in."),
            AccountState.Disabled or AccountState.Rejected =>
                (ErrorCodes.AuthAccountDisabled, "This account is not active."),
            _ => (null, null),
        };

    private async Task<SecondFactorToken> GetValidTicketAsync(
        string tokenValue,
        SecondFactorKind expectedKind,
        CancellationToken cancellationToken)
    {
        var invalidCode = expectedKind == SecondFactorKind.Totp
            ? ErrorCodes.AuthMfaTokenInvalid
            : ErrorCodes.AuthOtpTokenInvalid;

        var ticket = await secondFactorTokenRepository.GetByTokenHashAsync(
            OpaqueToken.Hash(tokenValue), cancellationToken);

        if (ticket is null
            || ticket.Kind != expectedKind
            || ticket.ConsumedAt is not null
            || ticket.AttemptCount >= MaxSecondFactorAttempts)
        {
            throw new ApiException(invalidCode, 400, "The sign-in session is not valid.");
        }

        if (timeProvider.GetUtcNow() >= ticket.ExpiresAt)
        {
            var expiredCode = expectedKind == SecondFactorKind.Totp
                ? ErrorCodes.AuthMfaTokenExpired
                : ErrorCodes.AuthOtpTokenInvalid;
            throw new ApiException(expiredCode, 400, "The sign-in session has expired.");
        }

        return ticket;
    }

    private async Task<AuthTokens> IssueTokensAsync(SimfUser user, CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.CreateAccessToken(user, roles);

        var refreshValue = OpaqueToken.Generate();
        var now = timeProvider.GetUtcNow();
        await refreshTokenRepository.AddAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = OpaqueToken.Hash(refreshValue),
                CreatedAt = now,
                ExpiresAt = now.Add(RefreshTokenLifetime),
            },
            cancellationToken);

        await AuditAsync(AuditEvents.SignInSucceeded, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);
        logger.LogInformation("Sign-in completed for {Email}", user.Email);

        return new AuthTokens(
            accessToken.Value,
            refreshValue,
            "Bearer",
            accessToken.ExpiresInSeconds,
            new AuthUser(user.Id, user.Email!, user.DisplayName));
    }

    private async Task<string> IssueSignInOtpAsync(
        SimfUser user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var previous = await accountCodeRepository.GetLatestUnconsumedAsync(
            user.Id, AccountCodePurpose.SignInOtp, cancellationToken);
        if (previous is not null)
        {
            previous.ConsumedAt = now;
            await accountCodeRepository.UpdateAsync(previous, cancellationToken);
        }

        var code = new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = AccountCodePurpose.SignInOtp,
            Code = VerificationCodeGenerator.Generate(),
            CreatedAt = now,
            ExpiresAt = now.Add(OtpLifetime),
        };
        await accountCodeRepository.AddAsync(code, cancellationToken);
        return code.Code;
    }

    private void EnqueueSignInOtpEmail(string email, string code)
    {
        var minutes = (int)OtpLifetime.TotalMinutes;
        var body =
            $"<p>Your SIMF sign-in code is <strong>{code}</strong>.</p>" +
            $"<p>The code expires in {minutes} minutes.</p>";
        emailQueue.Enqueue(new EmailMessage(email, "SIMF sign-in code", body));
    }

    private Task AuditAsync(
        string eventType,
        AuditOutcome outcome,
        string email,
        Guid? userId = null,
        string? errorCode = null,
        CancellationToken cancellationToken = default) =>
        auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = eventType,
                Outcome = outcome,
                SubjectEmail = email,
                SubjectUserId = userId,
                ErrorCode = errorCode,
            },
            cancellationToken);

    /// <summary>Compares the codes in constant time, so no timing side channel leaks.</summary>
    private static bool CodesMatch(string stored, string supplied) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored),
            Encoding.UTF8.GetBytes(supplied));
}
