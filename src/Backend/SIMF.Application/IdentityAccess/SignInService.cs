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
    IRecoveryCodeService recoveryCodes,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SignInService> logger) : ISignInService
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan OtpRequestWindow = TimeSpan.FromHours(1);
    private const int MaxSecondFactorAttempts = 5;
    private const int MaxOtpRequestsPerWindow = 5;

    public async Task<SignInResponse> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is not null && await userManager.IsLockedOutAsync(user))
        {
            await AuditAsync(AuditEvents.SignInAccountLockedOut, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthAccountLocked,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthAccountLocked, 423,
                "The account is locked after too many attempts. Try again later.",
                "تم قفل الحساب بعد محاولات كثيرة. حاول مرة أخرى لاحقًا.");
        }

        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            if (user is not null)
            {
                await userManager.AccessFailedAsync(user);
            }

            // One generic response — it never reveals whether the email exists.
            await AuditAsync(AuditEvents.SignInBadCredentials, AuditOutcome.Failure,
                request.Email, user?.Id, ErrorCodes.AuthInvalidCredentials,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthInvalidCredentials, 401,
                "The email address or password is not correct.",
                "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var (blockCode, blockMessage, blockMessageArabic) = CheckAccountState(user);
        if (blockCode is not null)
        {
            await AuditAsync(AuditEvents.SignInStateBlocked, AuditOutcome.Failure,
                user.Email!, user.Id, blockCode, cancellationToken: cancellationToken);
            throw new ApiException(blockCode, 403, blockMessage!, blockMessageArabic!);
        }

        var now = timeProvider.GetUtcNow();
        var roles = await userManager.GetRolesAsync(user);

        // Audience gate (P2) — runs *after* credentials and account state are
        // OK so a wrong-surface response can't be used as a credential-
        // existence oracle. Throws 403 with AUTH_WRONG_SURFACE_CP or
        // AUTH_WRONG_SURFACE_WEB and writes one SignIn.WrongSurface audit row.
        await EnforceAudienceAsync(user, roles, request.Audience, cancellationToken);

        // When 2FA is turned off for the account (myComment #34, D-033), the
        // password step IS the sign-in — issue tokens directly. This applies
        // to both Control Panel users and visitors.
        if (!user.TwoFactorEnabled)
        {
            var tokens = await IssueTokensAsync(user, cancellationToken);
            return new SignInResponse(false, null, null, tokens);
        }

        // The second-factor flavour is the user's own choice, not just their
        // role: anyone with an authenticator key paired (the new
        // /account/profile → 2FA enrolment, D-040) completes sign-in with
        // TOTP; everyone else completes with an emailed code
        // (SIMF-FDS-001 §5.6 — read forward to D-040). The original
        // role-only rule (Control Panel → TOTP) is preserved as a fallback
        // for users who have a role but haven't enrolled.
        var authenticatorKey = await userManager.GetAuthenticatorKeyAsync(user);
        var kind = !string.IsNullOrEmpty(authenticatorKey) || roles.Count > 0
            ? SecondFactorKind.Totp
            : SecondFactorKind.EmailOtp;

        // The emailed code is issued (and re-issue-capped) before the ticket, so
        // a throttled visitor gets no ticket at all.
        string? otpCode = null;
        if (kind == SecondFactorKind.EmailOtp)
        {
            otpCode = await IssueSignInOtpAsync(user, now, cancellationToken);
        }

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
            user.Email!, user.Id, detail: kind.ToString(), cancellationToken: cancellationToken);

        if (kind == SecondFactorKind.Totp)
        {
            return new SignInResponse(true, ticketValue, null);
        }

        EnqueueSignInOtpEmail(user.Email!, otpCode!);
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
                "The sign-in session is no longer valid.",
                "جلسة تسجيل الدخول لم تعد صالحة.");

        await EnsureNotLockedOutAsync(user, cancellationToken);

        var secret = await userManager.GetAuthenticatorKeyAsync(user);
        var totp = totpVerifier.Verify(secret ?? string.Empty, request.Code);

        // Reject a wrong code — and a correct code whose time-step was already
        // used, which is a replay (RFC 6238 §5.2).
        var isReplay = totp.IsValid
            && user.LastUsedTotpTimestep is { } lastStep
            && totp.TimeStep <= lastStep;
        if (!totp.IsValid || isReplay)
        {
            ticket.AttemptCount++;
            await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);
            await AuditAsync(AuditEvents.SignInSecondFactorFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthTotpInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthTotpInvalid, 400,
                "The verification code is not correct.",
                "رمز التحقق غير صحيح.");
        }

        var now = timeProvider.GetUtcNow();
        user.LastUsedTotpTimestep = totp.TimeStep;
        user.UpdatedAt = now;
        await userManager.UpdateAsync(user);

        ticket.ConsumedAt = now;
        await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthTokens> VerifyRecoveryCodeAsync(
        VerifyRecoveryCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        // Same ticket the TOTP step uses — the recovery code is an
        // *alternative* second factor, not a bypass (D-040). A recovery
        // attempt counts against the ticket's MaxSecondFactorAttempts so
        // brute-force is bounded the same way as a wrong TOTP code.
        var ticket = await GetValidTicketAsync(
            request.MfaToken, SecondFactorKind.Totp, cancellationToken);
        var user = await userManager.FindByIdAsync(ticket.UserId.ToString())
            ?? throw new ApiException(ErrorCodes.AuthMfaTokenInvalid, 400,
                "The sign-in session is no longer valid.",
                "جلسة تسجيل الدخول لم تعد صالحة.");

        await EnsureNotLockedOutAsync(user, cancellationToken);

        var accepted = await recoveryCodes.VerifyAndConsumeAsync(
            user.Id, request.Code, cancellationToken);
        if (!accepted)
        {
            ticket.AttemptCount++;
            await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);
            await userManager.AccessFailedAsync(user);
            await AuditAsync(AuditEvents.TotpRecoveryCodeFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthRecoveryCodeInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthRecoveryCodeInvalid, 400,
                "The recovery code is not valid.",
                "رمز الاسترداد غير صالح.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var now = timeProvider.GetUtcNow();
        ticket.ConsumedAt = now;
        await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);

        await AuditAsync(AuditEvents.TotpRecoveryCodeUsed, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);
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
                "The sign-in session is no longer valid.",
                "جلسة تسجيل الدخول لم تعد صالحة.");

        await EnsureNotLockedOutAsync(user, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var code = await accountCodeRepository.GetLatestUnconsumedAsync(
            user.Id, AccountCodePurpose.SignInOtp, cancellationToken);

        if (code is null || now >= code.ExpiresAt)
        {
            await AuditAsync(AuditEvents.SignInSecondFactorFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthOtpExpired,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthOtpExpired, 400,
                "The code has expired. Sign in again to get a new one.",
                "انتهت صلاحية الرمز. سجّل الدخول مرة أخرى للحصول على رمز جديد.");
        }

        if (!CodesMatch(code.Code, request.Code))
        {
            ticket.AttemptCount++;
            await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);
            await AuditAsync(AuditEvents.SignInSecondFactorFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthOtpInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthOtpInvalid, 400,
                "The code is not correct.",
                "الرمز غير صحيح.");
        }

        code.ConsumedAt = now;
        await accountCodeRepository.UpdateAsync(code, cancellationToken);
        ticket.ConsumedAt = now;
        await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);
        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <summary>
    /// Enforces the audience gate (P2). A user with any CP role can sign in
    /// only from the Control Panel surface; a user without any CP role can
    /// sign in only from the visitor surfaces (Web / Flutter app). A mismatch
    /// audits one <c>SignIn.WrongSurface</c> row and throws 403.
    /// </summary>
    private async Task EnforceAudienceAsync(
        SimfUser user,
        IList<string> roles,
        SignInAudience audience,
        CancellationToken cancellationToken)
    {
        var isStaff = roles.Count > 0;
        var allowed = audience switch
        {
            SignInAudience.Cp => isStaff,
            SignInAudience.Web or SignInAudience.App => !isStaff,
            _ => false,
        };
        if (allowed)
        {
            return;
        }

        var (code, message, messageArabic) = audience == SignInAudience.Cp
            ? (ErrorCodes.AuthWrongSurfaceCp,
                "Sign in to the visitor website instead — this account is not allowed on the Control Panel.",
                "سجّل الدخول إلى موقع الزوار — هذا الحساب غير مسموح به في لوحة التحكم.")
            : (ErrorCodes.AuthWrongSurfaceWeb,
                "Sign in to the Control Panel instead — this account is not allowed on the visitor surfaces.",
                "سجّل الدخول إلى لوحة التحكم — هذا الحساب غير مسموح به في واجهات الزوار.");

        await AuditAsync(AuditEvents.SignInWrongSurface, AuditOutcome.Failure,
            user.Email!, user.Id, code, detail: audience.ToString(),
            cancellationToken: cancellationToken);
        throw new ApiException(code, 403, message, messageArabic);
    }

    /// <summary>
    /// The account states that block sign-in. <c>EmailVerified</c>,
    /// <c>PendingApproval</c> and <c>Approved</c> may sign in — the access a
    /// not-yet-approved user then has is an authorisation concern (SIMF-RPM-001
    /// section 10, decision D-010).
    /// </summary>
    private static (string? Code, string? Message, string? MessageArabic) CheckAccountState(
        SimfUser user) =>
        user.AccountState switch
        {
            AccountState.Registered => (
                ErrorCodes.AuthEmailNotVerified,
                "Verify your email address before signing in.",
                "يرجى التحقق من بريدك الإلكتروني قبل تسجيل الدخول."),
            AccountState.Disabled or AccountState.Rejected => (
                ErrorCodes.AuthAccountDisabled,
                "This account is not active.",
                "هذا الحساب غير نشط."),
            _ => (null, null, null),
        };

    /// <summary>Blocks the second-factor step if the account locked out after the password step.</summary>
    private async Task EnsureNotLockedOutAsync(SimfUser user, CancellationToken cancellationToken)
    {
        if (await userManager.IsLockedOutAsync(user))
        {
            await AuditAsync(AuditEvents.SignInAccountLockedOut, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthAccountLocked,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthAccountLocked, 423,
                "The account is locked after too many attempts. Try again later.",
                "تم قفل الحساب بعد محاولات كثيرة. حاول مرة أخرى لاحقًا.");
        }
    }

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
            await AuditAsync(AuditEvents.SignInSecondFactorRejected, AuditOutcome.Failure,
                null, ticket?.UserId, invalidCode, expectedKind.ToString(), cancellationToken);
            throw new ApiException(invalidCode, 400, "The sign-in session is not valid.",
                "جلسة تسجيل الدخول غير صالحة.");
        }

        if (timeProvider.GetUtcNow() >= ticket.ExpiresAt)
        {
            var expiredCode = expectedKind == SecondFactorKind.Totp
                ? ErrorCodes.AuthMfaTokenExpired
                : ErrorCodes.AuthOtpTokenInvalid;
            await AuditAsync(AuditEvents.SignInSecondFactorRejected, AuditOutcome.Failure,
                null, ticket.UserId, expiredCode, "expired", cancellationToken);
            throw new ApiException(expiredCode, 400, "The sign-in session has expired.",
                "انتهت صلاحية جلسة تسجيل الدخول.");
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

        await AuditAsync(AuditEvents.RefreshTokenIssued, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);
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
        // Cap how many sign-in codes one account may request — without this an
        // attacker could mint unlimited tickets and reset the attempt budget.
        var recentCodes = await accountCodeRepository.CountCreatedSinceAsync(
            user.Id, AccountCodePurpose.SignInOtp, now - OtpRequestWindow, cancellationToken);
        if (recentCodes >= MaxOtpRequestsPerWindow)
        {
            await AuditAsync(AuditEvents.SignInSecondFactorRejected, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.RateLimitExceeded,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.RateLimitExceeded, 429,
                "Too many sign-in codes have been requested. Try again later.",
                "تم طلب رموز تسجيل دخول كثيرة. حاول مرة أخرى لاحقًا.");
        }

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

    /// <summary>Compares the codes in constant time, so no timing side channel leaks.</summary>
    private static bool CodesMatch(string stored, string supplied) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored),
            Encoding.UTF8.GetBytes(supplied));
}
