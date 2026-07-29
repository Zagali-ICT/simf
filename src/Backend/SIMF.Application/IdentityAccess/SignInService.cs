// Tests: SIMF.Api.Tests/SignInTests.cs (email-verified sign-in surfaces
//        AccountStateInfo state=EmailVerified; second-factor + state gates)
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;

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
    IEmailTemplateResolver emailTemplates,
    IUserProfileService userProfiles,
    IJwtTokenService jwtTokenService,
    IPermissionResolver permissionResolver,
    ITotpVerifier totpVerifier,
    IRecoveryCodeService recoveryCodes,
    IAuditLog auditLog,
    IOptions<IdentityLifecycleOptions> lifecycleOptions,
    TimeProvider timeProvider,
    ILogger<SignInService> logger) : ISignInService
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan OtpRequestWindow = TimeSpan.FromHours(1);
    private const int MaxSecondFactorAttempts = 5;
    private const int MaxOtpRequestsPerWindow = 5;
    // #12 — client resend-button cooldown, 2 minutes (D-695); the hard cap is
    // MaxOtpRequestsPerWindow.
    private const int ResendCooldownSeconds = 120;

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

        var now = timeProvider.GetUtcNow();
        var roles = await accounts.GetRolesAsync(user);

        // Audience gate (P2) — runs *after* credentials and account state are
        // OK so a wrong-surface response can't be used as a credential-
        // existence oracle. Throws 403 with AUTH_WRONG_SURFACE_CP or
        // AUTH_WRONG_SURFACE_WEB and writes one SignIn.WrongSurface audit row.
        await EnforceAudienceAsync(user, roles, request.Audience, cancellationToken);

        // H4 — D-059 + H19 — D-080 + D-206: a SimfUser with
        // PasswordChangeRequired=true holds a seeded or admin-rotated credential
        // it must replace before any session is minted. This runs *after* the
        // audience gate so a wrong-surface caller hits AUTH_WRONG_SURFACE first
        // and cannot use the forced-change branch as an account-existence
        // oracle (the D-080 state-block ordering is likewise preserved above).
        //
        // D-206: the Control Panel hands the operator a single-use
        // password-change ticket (in place of the old
        // AUTH_PASSWORD_CHANGE_REQUIRED 403) so they can set a new password
        // in-flow and then sign in normally. The credential was already proven
        // at CheckPasswordAsync above, so the ticket — not a re-typed
        // password — authorises the change. Every other audience keeps the
        // 403. The gate still fires at every later token-mint path via
        // RequirePasswordChangeNotRequired (VerifyTotpAsync /
        // VerifyRecoveryCodeAsync / VerifyOtpAsync / SessionService.RefreshAsync),
        // so no session can be minted until the password is actually changed.
        // A7-13 (NCA) — enforce the admin-configured maximum password age. When the
        // password is older than the limit, persist the forced-change flag so the
        // change-ticket completion (which requires the flag in the DB) works and so
        // the gate survives a retry; the existing forced-change branch below then
        // drives the change (CP ticket / app 403). A completed change resets the
        // clock via PasswordService.ClearChangeFlagAndEndSessionsAsync.
        if (!user.PasswordChangeRequired && IsPasswordExpired(user, now))
        {
            user.PasswordChangeRequired = true;
            user.UpdatedAt = now;
            await accounts.UpdateAsync(user).EnsureSuccessAsync();
            await AuditAsync(AuditEvents.SignInPasswordExpired, AuditOutcome.Success,
                user.Email!, user.Id, cancellationToken: cancellationToken);
        }

        if (user.PasswordChangeRequired)
        {
            if (request.Audience != SignInAudience.Cp)
            {
                await AuditAsync(AuditEvents.SignInPasswordChangeRequired,
                    AuditOutcome.Failure, user.Email!, user.Id,
                    ErrorCodes.AuthPasswordChangeRequired,
                    cancellationToken: cancellationToken);
                throw new ApiException(ErrorCodes.AuthPasswordChangeRequired, 403,
                    "You must change your password before signing in. Use the password-reset flow to set a new one.",
                    "يجب تغيير كلمة المرور قبل تسجيل الدخول. استخدم تدفّق إعادة تعيين كلمة المرور لتعيين كلمة جديدة.");
            }

            var changeTicketValue = OpaqueToken.Generate();
            await secondFactorTokenRepository.AddAsync(
                new SecondFactorToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TokenHash = OpaqueToken.Hash(changeTicketValue),
                    Kind = SecondFactorKind.PasswordChange,
                    CreatedAt = now,
                    ExpiresAt = now.Add(TicketLifetime),
                },
                cancellationToken);
            await AuditAsync(AuditEvents.SignInPasswordChangeTicketIssued,
                AuditOutcome.Success, user.Email!, user.Id,
                cancellationToken: cancellationToken);
            return new SignInResponse(false, null, null, null, null, changeTicketValue);
        }

        // When 2FA is turned off for the account (myComment #34, D-033), the
        // password step IS the sign-in — issue tokens directly. This applies
        // to both Control Panel users and visitors.
        if (!user.TwoFactorEnabled)
        {
            var tokens = await IssueTokensAsync(
                user, cancellationToken, secondFactorCompleted: false);
            // P10 — D-051: surface AccountStateInfo on the response when
            // the user is non-Approved, and audit the guest sign-in
            // separately so SOC can spot it. The JWT itself also carries
            // the account_state claim, so 2FA-completed sign-ins are
            // covered by the claim alone.
            var stateInfo = await BuildAccountStateInfoAsync(user, cancellationToken);
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

        // H10 / H23 — D-065 / D-083: helper owns the failure-audit
        // pattern shared across all four credential-flow dispatch sites.
        await emailQueue.TryEnqueueAsync(
            await BuildSignInOtpEmailAsync(user.Email!, otpCode!, cancellationToken),
            purpose: "SignInOtp",
            subjectEmail: user.Email!,
            subjectUserId: user.Id,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);
        // BUG-015 — no in-app notification is written for a sign-in code. The
        // D-099 trail row fired on EVERY 2FA sign-in, so the notification centre
        // filled with "Sign-in code sent" rows and buried the meaningful ones
        // (account approved + QR, profile submitted). The code itself travels by
        // email, and the security trail is the SignInSecondFactorIssued audit row
        // above — neither needs a user-facing row. Security-relevant notices
        // (password changed / reset completed) are unaffected.
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
            await secondFactorTokenRepository.IncrementAttemptCountAsync(
                ticket.Id, cancellationToken);
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

        // Atomically consume the ticket — only the caller that flips ConsumedAt
        // from null to now proceeds to mint; a concurrent second verify of the
        // same ticket is rejected here, so one ticket yields exactly one session.
        if (!await secondFactorTokenRepository.TryConsumeAsync(
                ticket.Id, now, cancellationToken))
        {
            await AuditAsync(AuditEvents.SignInSecondFactorRejected, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthMfaTokenInvalid,
                "already_consumed", cancellationToken);
            throw new ApiException(ErrorCodes.AuthMfaTokenInvalid, 400,
                "The sign-in session is not valid.",
                "جلسة تسجيل الدخول غير صالحة.");
        }
        return await IssueTokensAsync(
            user, cancellationToken, secondFactorCompleted: true);
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
            // Round-1 audit #17 — a wrong recovery code is bounded by the ticket's
            // MaxSecondFactorAttempts only (matching the wrong-TOTP path at
            // VerifyTotpAsync and the wrong-OTP path at VerifyOtpAsync above); it must
            // NOT also drive the account-level lockout counter, or a user mistyping
            // their emergency fallback would lock themselves out of the very fallback
            // they depend on.
            await secondFactorTokenRepository.IncrementAttemptCountAsync(
                ticket.Id, cancellationToken);
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
        // Atomically consume the ticket — the recovery code was already verified
        // + consumed above; the ticket gate ensures a concurrent second verify of
        // the same ticket cannot mint a second session.
        if (!await secondFactorTokenRepository.TryConsumeAsync(
                ticket.Id, now, cancellationToken))
        {
            await AuditAsync(AuditEvents.SignInSecondFactorRejected, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthMfaTokenInvalid,
                "already_consumed", cancellationToken);
            throw new ApiException(ErrorCodes.AuthMfaTokenInvalid, 400,
                "The sign-in session is not valid.",
                "جلسة تسجيل الدخول غير صالحة.");
        }

        await AuditAsync(AuditEvents.TotpRecoveryCodeUsed, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);
        return await IssueTokensAsync(
            user, cancellationToken, secondFactorCompleted: true);
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

        if (!CodesMatch(code.Code, AccountCodeHasher.Hash(request.Code)))
        {
            await secondFactorTokenRepository.IncrementAttemptCountAsync(
                ticket.Id, cancellationToken);
            await AuditAsync(AuditEvents.SignInSecondFactorFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthOtpInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthOtpInvalid, 400,
                "The code is not correct.",
                "الرمز غير صحيح.");
        }

        // Atomically consume the ticket first — the gate that ensures a
        // concurrent second verify of the same ticket cannot mint twice.
        if (!await secondFactorTokenRepository.TryConsumeAsync(
                ticket.Id, now, cancellationToken))
        {
            await AuditAsync(AuditEvents.SignInSecondFactorRejected, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthOtpTokenInvalid,
                "already_consumed", cancellationToken);
            throw new ApiException(ErrorCodes.AuthOtpTokenInvalid, 400,
                "The sign-in session is not valid.",
                "جلسة تسجيل الدخول غير صالحة.");
        }
        // Single-use the OTP itself (the ticket gate above already decided the
        // mint; this marks the code consumed atomically).
        await accountCodeRepository.TryConsumeAsync(code.Id, now, cancellationToken);
        return await IssueTokensAsync(
            user, cancellationToken, secondFactorCompleted: true);
    }

    public async Task<ResendOtpResponse> ResendOtpAsync(
        ResendOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        // #12 — re-issue the emailed code for an in-progress 2FA ticket without
        // re-entering the password (the ticket already proved it). GetValidTicket
        // throws on a missing / consumed / attempt-exhausted / expired ticket.
        var ticket = await GetValidTicketAsync(
            request.OtpToken, SecondFactorKind.EmailOtp, cancellationToken);
        var user = await accounts.FindByIdAsync(ticket.UserId, cancellationToken)
            ?? throw new ApiException(ErrorCodes.AuthOtpTokenInvalid, 400,
                "The sign-in session is no longer valid.",
                "جلسة تسجيل الدخول لم تعد صالحة.");

        await EnsureNotLockedOutAsync(user, cancellationToken);

        var now = timeProvider.GetUtcNow();
        // Re-issue under the same per-hour budget (5/window) — a 6th resend
        // throws RateLimitExceeded (429), consuming any prior unconsumed code.
        var otpCode = await IssueSignInOtpAsync(user, now, cancellationToken);

        // The ticket (5 min) is shorter than the code (10 min), so refresh the
        // ticket window or the fresh code could outlive its verifiable session.
        // The ticket's AttemptCount is deliberately NOT reset here: resending
        // must not hand back a fresh brute-force budget — the 5-strike verify
        // counter stays monotonic across every code issued under this ticket.
        ticket.ExpiresAt = now.Add(TicketLifetime);
        await secondFactorTokenRepository.UpdateAsync(ticket, cancellationToken);

        await emailQueue.TryEnqueueAsync(
            await BuildSignInOtpEmailAsync(user.Email!, otpCode, cancellationToken),
            purpose: "SignInOtp",
            subjectEmail: user.Email!,
            subjectUserId: user.Id,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);
        await AuditAsync(AuditEvents.SignInSecondFactorIssued, AuditOutcome.Success,
            user.Email!, user.Id, detail: "resend", cancellationToken: cancellationToken);

        return new ResendOtpResponse(ResendCooldownSeconds);
    }

    /// <summary>
    /// Enforces the audience gate (P2, rekeyed by P7b). The CP surface
    /// is for <see cref="UserType.Admin"/> only; the visitor surfaces
    /// (Web, Flutter app) are for <see cref="UserType.Visitor"/>
    /// (D-186 collapsed the legacy Other type into Visitor). A
    /// mismatch audits one <c>SignIn.WrongSurface</c> row and throws 403.
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
    /// <summary>A7-13 (NCA) — true when password expiry is configured
    /// (<see cref="IdentityLifecycleOptions.PasswordMaxAgeDays"/> &gt; 0) and the
    /// password is older than the limit. The baseline is the last set time, or the
    /// account's creation time for accounts whose password predates the column.</summary>
    private bool IsPasswordExpired(SimfUser user, DateTimeOffset now)
    {
        var maxAgeDays = lifecycleOptions.Value.PasswordMaxAgeDays;
        if (maxAgeDays <= 0)
        {
            return false;
        }
        var baseline = user.PasswordChangedAtUtc ?? user.CreatedAt;
        return now - baseline > TimeSpan.FromDays(maxAgeDays);
    }

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
    /// non-Approved sign-in response (P10 — D-051; D-198 added the
    /// EmailVerified case). Null when the user is Approved or in any
    /// other state that doesn't need to inform the client (the
    /// hard-blocked states never reach this helper — they 403 before
    /// sign-in completes).
    /// </summary>
    private async Task<AccountStateInfo?> BuildAccountStateInfoAsync(
        SimfUser user, CancellationToken cancellationToken)
    {
        switch (user.AccountState)
        {
            case AccountState.EmailVerified:
                // D-198 — a verified user who has not yet submitted their
                // profile (EmailVerified only advances to PendingApproval on
                // the first profile save — UserProfileService.UpsertMineAsync).
                // Surfacing the state lets the client route them straight to
                // the profile form, the same way PendingApproval / Rejected
                // route to the state-banner page. The account_state JWT claim
                // also carries this on every token, so the signal still
                // reaches the client on the email-OTP path where this
                // response helper is not invoked.
                return new AccountStateInfo(
                    State: nameof(AccountState.EmailVerified),
                    RejectionReason: null,
                    RejectionReasonArabic: null,
                    StateChangedAt: user.StateChangedAt);
            case AccountState.PendingApproval:
                return new AccountStateInfo(
                    State: nameof(AccountState.PendingApproval),
                    RejectionReason: null,
                    RejectionReasonArabic: null,
                    StateChangedAt: user.StateChangedAt);
            case AccountState.Rejected:
                // D-106: rejection text lives on UserProfile now. Fetch it
                // (null when the row or the field is missing — the
                // state-banner still renders, just without the reason).
                var rejection = await userProfiles.GetRejectionTextAsync(
                    user.Id, cancellationToken);
                return new AccountStateInfo(
                    State: nameof(AccountState.Rejected),
                    RejectionReason: rejection?.English,
                    RejectionReasonArabic: rejection?.Arabic,
                    StateChangedAt: user.StateChangedAt);
            default:
                return null;
        }
    }

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

    /// <param name="secondFactorCompleted">Held-item #2c — true on every path
    /// that cleared TOTP, a recovery code or an emailed OTP; false only on the
    /// !TwoFactorEnabled fast path. Stated at the call site rather than inferred
    /// here, so a future issuance path has to answer the question consciously.</param>
    private async Task<AuthTokens> IssueTokensAsync(
        SimfUser user, CancellationToken cancellationToken, bool secondFactorCompleted)
    {
        var roles = await accounts.GetRolesAsync(user);
        var permissions = await permissionResolver.ResolveForRolesAsync(roles, cancellationToken);
        var mobileAppRole = await userProfiles.ResolveMobileAppRoleAsync(user.Id, cancellationToken);
        var accessToken = jwtTokenService.CreateAccessToken(
            user, roles, permissions, mobileAppRole, secondFactorCompleted);

        var refreshValue = OpaqueToken.Generate();
        var now = timeProvider.GetUtcNow();
        await refreshTokenRepository.AddAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = OpaqueToken.Hash(refreshValue),
                CreatedAt = now,
                // D-443 (NCA finding): stamp the absolute session deadline
                // (sign-in + Jwt:SessionLifetimeHours). Rotation carries this
                // deadline forward instead of sliding, so the session is
                // capped at 24h from sign-in.
                ExpiresAt = now.Add(jwtTokenService.RefreshTokenLifetime),
            },
            cancellationToken);

        // A7-31 / A1-19 (NCA) — record this successful sign-in (the activity signal
        // for dormant-account disable) and surface the PRIOR sign-in time to the
        // client for the "last signed in …" notice. UpdateAsync does not roll the
        // security stamp, so the access token just minted stays valid.
        var previousSignInAtUtc = user.LastSuccessfulSignInAtUtc;
        user.LastSuccessfulSignInAtUtc = now;
        await accounts.UpdateAsync(user).EnsureSuccessAsync();

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
            new AuthUser(user.Id, user.Email!, user.DisplayName),
            previousSignInAtUtc);
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
            await accountCodeRepository.TryConsumeAsync(previous.Id, now, cancellationToken);
        }

        // M3 (security) — store only the keyed hash; the plaintext is emailed.
        var plaintext = VerificationCodeGenerator.Generate();
        var code = new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = AccountCodePurpose.SignInOtp,
            Code = AccountCodeHasher.Hash(plaintext),
            CreatedAt = now,
            ExpiresAt = now.Add(OtpLifetime),
        };
        await accountCodeRepository.AddAsync(code, cancellationToken);
        return plaintext;
    }

    /// <summary>
    /// D-735 (was H23 — D-083): resolves the OTP email from the admin-editable
    /// template (else the code-owned default) and fills {Code}/{ExpiryMinutes}.
    /// Caller pairs it with `IEmailQueue.TryEnqueueAsync`.
    /// </summary>
    private Task<EmailMessage> BuildSignInOtpEmailAsync(
        string email, string code, CancellationToken cancellationToken) =>
        emailTemplates.RenderAsync(
            EmailTemplateType.SignInOtp, email,
            EmailTokens.ForCode(code, OtpLifetime), cancellationToken);

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
