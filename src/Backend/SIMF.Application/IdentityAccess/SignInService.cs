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
    IUserAccountRepository accounts,
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
        var user = await accounts.FindByEmailAsync(request.Email);

        if (user is not null && await accounts.IsLockedOutAsync(user))
        {
            await AuditAsync(AuditEvents.SignInAccountLockedOut, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthAccountLocked,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthAccountLocked, 423,
                "The account is locked after too many attempts. Try again later.",
                "تم قفل الحساب بعد محاولات كثيرة. حاول مرة أخرى لاحقًا.");
        }

        if (user is null || !await accounts.CheckPasswordAsync(user, request.Password))
        {
            if (user is not null)
            {
                await accounts.AccessFailedAsync(user);
            }

            // One generic response — it never reveals whether the email exists.
            await AuditAsync(AuditEvents.SignInBadCredentials, AuditOutcome.Failure,
                request.Email, user?.Id, ErrorCodes.AuthInvalidCredentials,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthInvalidCredentials, 401,
                "The email address or password is not correct.",
                "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
        }

        await accounts.ResetAccessFailedCountAsync(user);

        // H19 — D-080: account-state block fires BEFORE the password-change
        // gate (was reversed pre-H19 — Disabled/Registered users with the
        // flag set were getting an enumeration oracle via the more-specific
        // PasswordChangeRequired error, when they should hit the
        // disabled/unverified block instead).
        var (blockCode, blockMessage, blockMessageArabic) = CheckAccountState(user);
        if (blockCode is not null)
        {
            await AuditAsync(AuditEvents.SignInStateBlocked, AuditOutcome.Failure,
                user.Email!, user.Id, blockCode, cancellationToken: cancellationToken);
            throw new ApiException(blockCode, 403, blockMessage!, blockMessageArabic!);
        }

        // H4 — D-059 + H19 — D-080: a SimfUser with PasswordChangeRequired=true
        // holds a seeded or admin-rotated credential they must change before
        // any session is minted. Now enforced at every token-mint path, not
        // just the initial password step — see RequirePasswordChangeNotRequired
        // helper used by VerifyTotpAsync / VerifyRecoveryCodeAsync /
        // VerifyOtpAsync, and by SessionService.RefreshAsync.
        RequirePasswordChangeNotRequired(user);
        if (user.PasswordChangeRequired)
        {
            // Defensive — the helper throws; this is unreachable. Audit at
            // sign-in level so SOC sees the trigger surface.
            await AuditAsync(AuditEvents.SignInPasswordChangeRequired,
                AuditOutcome.Failure, user.Email!, user.Id,
                ErrorCodes.AuthPasswordChangeRequired,
                cancellationToken: cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var roles = await accounts.GetRolesAsync(user);

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
            // P10 — D-051: surface AccountStateInfo on the response when
            // the user is non-Approved, and audit the guest sign-in
            // separately so SOC can spot it. The JWT itself also carries
            // the account_state claim, so 2FA-completed sign-ins are
            // covered by the claim alone.
            var stateInfo = BuildAccountStateInfo(user);
            if (stateInfo is not null)
            {
                await AuditAsync(AuditEvents.SignInAsGuest, AuditOutcome.Success,
                    user.Email!, user.Id, detail: stateInfo.State,
                    cancellationToken: cancellationToken);
            }
            return new SignInResponse(false, null, null, tokens, stateInfo);
        }

        // The second-factor flavour is the user's own choice, not just their
        // role: anyone with an authenticator key paired (the new
        // /account/profile → 2FA enrolment, D-040) completes sign-in with
        // TOTP; everyone else completes with an emailed code
        // (SIMF-FDS-001 §5.6 — read forward to D-040). The original
        // role-only rule (Control Panel → TOTP) is preserved as a fallback
        // for users who have a role but haven't enrolled.
        var authenticatorKey = await accounts.GetAuthenticatorKeyAsync(user);
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

        // H10 — D-065: the ticket + OTP code are persisted; if the
        // enqueue throws, the user holds a ticket and a code they will
        // never receive. Audit the gap distinctly.
        try
        {
            EnqueueSignInOtpEmail(user.Email!, otpCode!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Sign-in OTP email enqueue failed for {Email}", user.Email);
            await AuditAsync(AuditEvents.EmailEnqueueFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.InternalError,
                detail: $"SignInOtp: {ex.GetType().Name}",
                cancellationToken: cancellationToken);
        }
        return new SignInResponse(true, null, ticketValue);
    }

    public async Task<AuthTokens> VerifyTotpAsync(
        VerifyTotpRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await GetValidTicketAsync(
            request.MfaToken, SecondFactorKind.Totp, cancellationToken);
        var user = await accounts.FindByIdAsync(ticket.UserId, cancellationToken)
            ?? throw new ApiException(ErrorCodes.AuthMfaTokenInvalid, 400,
                "The sign-in session is no longer valid.",
                "جلسة تسجيل الدخول لم تعد صالحة.");

        await EnsureNotLockedOutAsync(user, cancellationToken);
        RequirePasswordChangeNotRequired(user);

        var secret = await accounts.GetAuthenticatorKeyAsync(user);
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
        await accounts.UpdateAsync(user).EnsureSuccessAsync();

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
        var user = await accounts.FindByIdAsync(ticket.UserId, cancellationToken)
            ?? throw new ApiException(ErrorCodes.AuthMfaTokenInvalid, 400,
                "The sign-in session is no longer valid.",
                "جلسة تسجيل الدخول لم تعد صالحة.");

        await EnsureNotLockedOutAsync(user, cancellationToken);
        RequirePasswordChangeNotRequired(user);

        var accepted = await recoveryCodes.VerifyAndConsumeAsync(
            user.Id, request.Code, cancellationToken);
        if (!accepted)
        {
            ticket.AttemptCount++;
            await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);
            await accounts.AccessFailedAsync(user);
            await AuditAsync(AuditEvents.TotpRecoveryCodeFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthRecoveryCodeInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthRecoveryCodeInvalid, 400,
                "The recovery code is not valid.",
                "رمز الاسترداد غير صالح.");
        }

        await accounts.ResetAccessFailedCountAsync(user);
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
        var user = await accounts.FindByIdAsync(ticket.UserId, cancellationToken)
            ?? throw new ApiException(ErrorCodes.AuthOtpTokenInvalid, 400,
                "The sign-in session is no longer valid.",
                "جلسة تسجيل الدخول لم تعد صالحة.");

        await EnsureNotLockedOutAsync(user, cancellationToken);
        RequirePasswordChangeNotRequired(user);

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
    /// Enforces the audience gate (P2, rekeyed by P7b). The CP surface
    /// is for <see cref="UserType.Admin"/> only; the visitor surfaces
    /// (Web, Flutter app) are for <see cref="UserType.Visitor"/> and
    /// <see cref="UserType.Other"/>. A mismatch audits one
    /// <c>SignIn.WrongSurface</c> row and throws 403.
    ///
    /// <para>The <paramref name="roles"/> argument is kept on the
    /// signature so the call site does not have to re-fetch them; the
    /// gate itself does not read roles any more — <c>UserType</c> is
    /// the authoritative discriminator (decision D-048).</para>
    /// </summary>
    private async Task EnforceAudienceAsync(
        SimfUser user,
        IList<string> roles,
        SignInAudience audience,
        CancellationToken cancellationToken)
    {
        _ = roles; // P7b — no longer read; signature kept stable.

        var isAdmin = user.UserType == UserType.Admin;
        var allowed = audience switch
        {
            SignInAudience.Cp => isAdmin,
            SignInAudience.Web or SignInAudience.App => !isAdmin,
            _ => false,
        };
        if (allowed)
        {
            return;
        }

        var (code, message, messageArabic) = audience == SignInAudience.Cp
            ? (ErrorCodes.AuthWrongSurfaceCp,
                "Sign in to the visitor app instead — this account is not allowed on the Control Panel.",
                "سجّل الدخول إلى تطبيق الزوار — هذا الحساب غير مسموح به في لوحة التحكم.")
            : (ErrorCodes.AuthWrongSurfaceWeb,
                "Sign in to the Control Panel instead — this account is not allowed on the visitor surfaces.",
                "سجّل الدخول إلى لوحة التحكم — هذا الحساب غير مسموح به في واجهات الزوار.");

        await AuditAsync(AuditEvents.SignInWrongSurface, AuditOutcome.Failure,
            user.Email!, user.Id, code, detail: audience.ToString(),
            cancellationToken: cancellationToken);
        throw new ApiException(code, 403, message, messageArabic);
    }

    /// <summary>
    /// The account states that **hard-block** sign-in (P10 — D-051). The
    /// only hard block left is <c>Disabled</c> (the operator's "this
    /// account is dead" lever) and <c>Registered</c> (the user has not
    /// verified their email yet). <c>PendingApproval</c> and
    /// <c>Rejected</c> now sign in successfully and receive an
    /// <see cref="AccountStateInfo"/> on the response + the
    /// <c>account_state</c> JWT claim, so the client routes them to the
    /// pending / rejected page (P11) — they are not granted any access
    /// beyond the state-banner surface, but the JWT is the gate the
    /// authorization handler (P11) checks. <c>EmailVerified</c> and
    /// <c>Approved</c> may always sign in (D-010 — auth and authz are
    /// separate concerns).
    /// </summary>
    private static (string? Code, string? Message, string? MessageArabic) CheckAccountState(
        SimfUser user) =>
        user.AccountState switch
        {
            AccountState.Registered => (
                ErrorCodes.AuthEmailNotVerified,
                "Verify your email address before signing in.",
                "يرجى التحقق من بريدك الإلكتروني قبل تسجيل الدخول."),
            AccountState.Disabled => (
                ErrorCodes.AuthAccountDisabled,
                "This account is not active.",
                "هذا الحساب غير نشط."),
            _ => (null, null, null),
        };

    /// <summary>
    /// Builds the <see cref="AccountStateInfo"/> surfaced on a
    /// non-Approved sign-in response (P10 — D-051). Null when the user
    /// is Approved or in any other state that doesn't need to inform
    /// the client (the hard-blocked states never reach this helper —
    /// they 403 before sign-in completes).
    /// </summary>
    private static AccountStateInfo? BuildAccountStateInfo(SimfUser user) =>
        user.AccountState switch
        {
            AccountState.PendingApproval => new AccountStateInfo(
                State: nameof(AccountState.PendingApproval),
                RejectionReason: null,
                RejectionReasonArabic: null,
                StateChangedAt: user.StateChangedAt),
            AccountState.Rejected => new AccountStateInfo(
                State: nameof(AccountState.Rejected),
                RejectionReason: user.RejectionReason,
                RejectionReasonArabic: user.RejectionReasonArabic,
                StateChangedAt: user.StateChangedAt),
            _ => null,
        };

    /// <summary>Blocks the second-factor step if the account locked out after the password step.</summary>
    private async Task EnsureNotLockedOutAsync(SimfUser user, CancellationToken cancellationToken)
    {
        if (await accounts.IsLockedOutAsync(user))
        {
            await AuditAsync(AuditEvents.SignInAccountLockedOut, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthAccountLocked,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthAccountLocked, 423,
                "The account is locked after too many attempts. Try again later.",
                "تم قفل الحساب بعد محاولات كثيرة. حاول مرة أخرى لاحقًا.");
        }
    }

    /// <summary>
    /// H19 — D-080: blocks every token-mint path (second-factor verify,
    /// recovery-code verify, OTP verify, refresh) when the user holds
    /// <c>PasswordChangeRequired=true</c>. Pre-H19 only the initial password
    /// step checked this — a holder of an existing MFA ticket or refresh
    /// token rotated forever after an admin set the flag. The fix is to
    /// enforce at every mint surface so the gate is end-to-end.
    /// </summary>
    private static void RequirePasswordChangeNotRequired(SimfUser user)
    {
        if (user.PasswordChangeRequired)
        {
            throw new ApiException(ErrorCodes.AuthPasswordChangeRequired, 403,
                "You must change your password before signing in. Use the password-reset flow to set a new one.",
                "يجب تغيير كلمة المرور قبل تسجيل الدخول. استخدم تدفّق إعادة تعيين كلمة المرور لتعيين كلمة جديدة.");
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
        var roles = await accounts.GetRolesAsync(user);
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
