// Tests: SIMF.Api.Tests/PasswordTests.cs (forgot / reset / change, and the
//        Registered -> EmailVerified promotion a consumed reset code earns),
//        SIMF.Api.Tests/PasswordResetExpiryTests.cs (expired code),
//        SIMF.Api.Tests/AccountCodeConcurrencyTests.cs (atomic attempt cap and
//        single-use consumption under a concurrent burst).
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Security;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Notifications;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Implements password recovery — forgot-password, reset-password (with the
/// emailed code) and change-password (SIMF-API-001 section 12.7, SIMF-FDS-001).
/// A completed reset or change ends every session for the account, atomically,
/// and revokes the account's biometric device keys with them.
/// </summary>
public sealed class PasswordService(
    IUserAccountRepository accounts,
    IAccountCodeRepository accountCodeRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ISecondFactorTokenRepository secondFactorTokenRepository,
    IPasswordHistoryRepository passwordHistory,
    IDeviceKeyService deviceKeys,
    ITransactionRunner transactionRunner,
    IEmailQueue emailQueue,
    IEmailTemplateResolver emailTemplates,
    INotificationDispatcher notifications,
    IAuditLog auditLog,
    IOptions<IdentityLifecycleOptions> lifecycleOptions,
    TimeProvider timeProvider,
    ILogger<PasswordService> logger) : IPasswordService
{
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResetRequestWindow = TimeSpan.FromHours(1);
    private const int MaxResetAttempts = 5;
    private const int MaxResetCodesPerWindow = 5;

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByEmailAsync(request.Email);
        var now = timeProvider.SimfNow();

        if (user is not null
            && user.AccountState is not (AccountState.Disabled or AccountState.Rejected))
        {
            // Cap how many reset codes one account may be sent, so forgot-password
            // cannot be used to flood a victim's inbox. Capped requests are
            // dropped quietly — the response below never changes.
            var recentCodes = await accountCodeRepository.CountCreatedSinceAsync(
                user.Id, AccountCodePurpose.PasswordReset, now - ResetRequestWindow,
                cancellationToken);
            if (recentCodes < MaxResetCodesPerWindow)
            {
                var code = await IssueResetCodeAsync(user, now, cancellationToken);
                // The code row is persisted;
                // the enqueue is a side-effect on a different scope.
                // TryEnqueueAsync owns the failure-audit pattern so every
                // credential-flow email-dispatch site uses the same shape.
                await emailQueue.TryEnqueueAsync(
                    await BuildResetEmailAsync(user.Email!, code, cancellationToken),
                    purpose: "PasswordReset",
                    subjectEmail: user.Email!,
                    subjectUserId: user.Id,
                    auditLog: auditLog,
                    logger: logger,
                    cancellationToken: cancellationToken);
                // In-app trail for the credential email — visible
                // after the user signs in.
                await notifications.TryDispatchAsync(new NotificationRequest
                {
                    UserId = user.Id,
                    Kind = NotificationKind.CredentialPasswordResetRequested,
                    Title = "Password-reset code requested",
                    TitleArabic = "تم طلب رمز إعادة تعيين كلمة المرور",
                    Body = "A password-reset code was sent to your email address.",
                    BodyArabic = "تم إرسال رمز إعادة تعيين كلمة المرور إلى بريدك الإلكتروني.",
                    Severity = NotificationSeverity.Info,
                    SendEmail = false,
                }, logger, cancellationToken);
                await AuditAsync(AuditEvents.ForgotPasswordRequested, AuditOutcome.Success,
                    user.Email!, user.Id, cancellationToken: cancellationToken);
            }
            else
            {
                await AuditAsync(AuditEvents.ForgotPasswordRequested, AuditOutcome.Failure,
                    user.Email!, user.Id, ErrorCodes.RateLimitExceeded,
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            await AuditAsync(AuditEvents.ForgotPasswordRequested, AuditOutcome.Failure,
                request.Email, errorCode: ErrorCodes.AuthAccountNotFound,
                cancellationToken: cancellationToken);
        }

        // The same response either way — it never reveals whether the account exists.
        return new ForgotPasswordResponse((int)ResetCodeLifetime.TotalSeconds);
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByEmailAsync(request.Email);
        var now = timeProvider.SimfNow();

        var code = user is null
            ? null
            : await accountCodeRepository.GetLatestUnconsumedAsync(
                user.Id, AccountCodePurpose.PasswordReset, cancellationToken);

        // Every failure returns the same generic code — it never reveals which
        // step failed — while the audit log records the specific reason.
        if (user is null || code is null)
        {
            await AuditAsync(AuditEvents.PasswordResetAccountNotFound, AuditOutcome.Failure,
                request.Email, user?.Id, ErrorCodes.AuthResetCodeInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthResetCodeInvalid, 400,
                "The reset code is not valid.",
                "رمز إعادة التعيين غير صالح.");
        }

        if (code.AttemptCount >= MaxResetAttempts)
        {
            await AuditAsync(AuditEvents.PasswordResetAttemptCapReached, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthResetCodeInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthResetCodeInvalid, 400,
                "Too many incorrect attempts. Request a new reset code.",
                "محاولات غير صحيحة كثيرة. اطلب رمز إعادة تعيين جديدًا.");
        }

        if (now >= code.ExpiresAt)
        {
            await AuditAsync(AuditEvents.PasswordResetCodeExpired, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthResetCodeExpired,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthResetCodeExpired, 400,
                "The reset code has expired. Request a new one.",
                "انتهت صلاحية رمز إعادة التعيين. اطلب رمزًا جديدًا.");
        }

        if (!CodesMatch(code.Code, AccountCodeHasher.Hash(request.Code)))
        {
            // Increment atomically and decide on the returned count. The cap check
            // above read AttemptCount before this guess was compared, so a
            // read-modify-write here lets concurrent wrong tries each read 0 and
            // write 1, stretching the budget past MaxResetAttempts and putting the
            // whole 10^6 space in reach. Burning the code once the budget is spent
            // stops it being ground down across separate calls.
            var attempts = await accountCodeRepository.IncrementAttemptCountAsync(
                code.Id, cancellationToken);
            if (attempts >= MaxResetAttempts)
            {
                await accountCodeRepository.TryConsumeAsync(code.Id, now, cancellationToken);
            }
            await AuditAsync(AuditEvents.PasswordResetCodeIncorrect, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthResetCodeInvalid,
                $"attempt {attempts}", cancellationToken);
            throw new ApiException(ErrorCodes.AuthResetCodeInvalid, 400,
                "The reset code is not correct.",
                "رمز إعادة التعيين غير صحيح.");
        }

        // The code authorised this — consume it, set the password, clear the
        // forced-change flag and end every session, all in one transaction.
        // Consuming FIRST is what makes the code single-use: the conditional
        // UPDATE is the gate, because two concurrent submits of the same valid
        // code both passed the unconsumed read above and only one may set a
        // password. It is inside the transaction, so a policy-rejected password
        // rolls the consumption back and leaves the code usable for a retry.
        var consumed = false;
        var promotedToVerified = false;
        await transactionRunner.ExecuteAsync(
            async token =>
            {
                consumed = await accountCodeRepository.TryConsumeAsync(code.Id, now, token);
                if (!consumed)
                {
                    return;
                }
                await SetPasswordAsync(user, request.NewPassword);
                // Consuming an emailed reset code proves control of the mailbox
                // — the same fact the emailed verification code proves. An
                // account left at Registered was otherwise stranded: it can be
                // sent a reset code (ForgotPasswordAsync refuses only Disabled
                // and Rejected), enter it, change its password, and still be
                // told to "verify your email address before signing in", with
                // no way to verify. The system watched the user prove the very
                // thing it went on refusing to accept.
                //
                // EmailVerified is the WEAKEST post-registration state: approval
                // is still required, so this closes a lockout without granting
                // anything. Mutating before ClearChangeFlagAndEndSessionsAsync
                // means its UpdateAsync persists both in one write.
                promotedToVerified = user.AccountState == AccountState.Registered;
                if (promotedToVerified)
                {
                    user.EmailConfirmed = true;
                    user.AccountState = AccountState.EmailVerified;
                }
                await ClearChangeFlagAndEndSessionsAsync(user, now, token);
            },
            cancellationToken);

        if (!consumed)
        {
            await AuditAsync(AuditEvents.PasswordResetCodeIncorrect, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthResetCodeInvalid,
                "already_consumed", cancellationToken);
            throw new ApiException(ErrorCodes.AuthResetCodeInvalid, 400,
                "The reset code is not valid.",
                "رمز إعادة التعيين غير صالح.");
        }

        await AuditAsync(AuditEvents.PasswordResetCompleted, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);

        // A state change on the authentication surface gets its own row, under
        // the same event the sign-up path writes, so "how did this account
        // become verified" has one answer to search for either way.
        if (promotedToVerified)
        {
            await AuditAsync(AuditEvents.EmailVerificationSucceeded, AuditOutcome.Success,
                user.Email!, user.Id, cancellationToken: cancellationToken);
        }

        // Security notice — in-app row + email confirming the reset.
        // Wrapped in TryDispatchAsync so a notification failure never
        // re-throws after the password is already changed.
        var resetTime = now.ToString("u",
            System.Globalization.CultureInfo.InvariantCulture);
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = user.Id,
            Kind = NotificationKind.AccountPasswordResetCompleted,
            Title = "Your SIMF password was reset",
            TitleArabic = "تمت إعادة تعيين كلمة المرور",
            Body = $"Your SIMF password was reset successfully at {resetTime}. "
                + "If you did not request this, contact the SIMF security team immediately.",
            BodyArabic = $"تمت إعادة تعيين كلمة المرور الخاصة بك في SIMF بنجاح بتاريخ {resetTime}. "
                + "إذا لم تكن أنت من طلب ذلك، تواصل مع فريق الأمن في SIMF فوراً.",
            Severity = NotificationSeverity.Warning,
            SendEmail = true,
        }, logger, cancellationToken);

        logger.LogInformation("Password reset for {Email}", user.Email);
        return new ResetPasswordResponse(true);
    }

    public async Task<ChangePasswordResponse> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken)
            ?? throw new ApiException(ErrorCodes.AuthAccountNotFound, 404,
                "The account was not found.",
                "لم يتم العثور على الحساب.");

        await transactionRunner.ExecuteAsync(
            async token =>
            {
                // A7-20 — reject reuse before the change (no-op when disabled).
                var retiredHash = user.PasswordHash;
                await EnforceNoPasswordReuseAsync(user, retiredHash, request.NewPassword);

                var result = await accounts.ChangePasswordAsync(
                    user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded)
                {
                    // Separate the wrong-current-password
                    // path from the policy-rejected-new-password path so
                    // SOC can spot brute-force-current attempts vs the
                    // user typing weak new passwords. Detail carries the
                    // Identity error codes so the timeline is forensically
                    // reconstructable from the row alone.
                    var isMismatch = result.Errors.Any(
                        error => error.Code == "PasswordMismatch");
                    var errorCodeSummary = string.Join(
                        ",", result.Errors.Select(error => error.Code));
                    await AuditAsync(AuditEvents.PasswordChangeFailed, AuditOutcome.Failure,
                        user.Email!, user.Id,
                        isMismatch
                            ? ErrorCodes.AuthInvalidCredentials
                            : ErrorCodes.AuthPasswordPolicy,
                        detail: isMismatch
                            ? $"WrongCurrent: {errorCodeSummary}"
                            : $"PolicyRejected: {errorCodeSummary}",
                        cancellationToken: token);
                    if (isMismatch)
                    {
                        throw new ApiException(ErrorCodes.AuthInvalidCredentials, 400,
                            "The current password is not correct.",
                            "كلمة المرور الحالية غير صحيحة.");
                    }

                    throw PasswordRejected(result);
                }

                await RecordPasswordHistoryAsync(user.Id, retiredHash);
                await ClearChangeFlagAndEndSessionsAsync(
                    user, timeProvider.SimfNow(), token);
            },
            cancellationToken);

        await AuditAsync(AuditEvents.PasswordChanged, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);

        await SendPasswordChangedNoticeAsync(
            user, timeProvider.SimfNow(), cancellationToken);

        logger.LogInformation("Password changed for {Email}", user.Email);
        return new ChangePasswordResponse(true);
    }

    public async Task<CompletePasswordChangeResponse> CompletePasswordChangeAsync(
        CompletePasswordChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();

        // The single-use ticket the sign-in password step issued is the
        // authorisation here — it proves the current password was already
        // verified at sign-in, so no password is re-collected. The checks
        // mirror SignInService.GetValidTicketAsync (kind, consumed, expiry);
        // every failure returns the same generic code so the endpoint cannot be
        // used as an account-existence oracle.
        var ticket = await secondFactorTokenRepository.GetByTokenHashAsync(
            OpaqueToken.Hash(request.PasswordChangeToken), cancellationToken);

        if (ticket is null
            || ticket.Kind != SecondFactorKind.PasswordChange
            || ticket.ConsumedAt is not null)
        {
            await AuditAsync(AuditEvents.PasswordChangeFailed, AuditOutcome.Failure,
                null, ticket?.UserId, ErrorCodes.AuthMfaTokenInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthMfaTokenInvalid, 400,
                "The sign-in session is no longer valid. Sign in again.",
                "جلسة تسجيل الدخول لم تعد صالحة. سجّل الدخول مرة أخرى.");
        }

        if (now >= ticket.ExpiresAt)
        {
            await AuditAsync(AuditEvents.PasswordChangeFailed, AuditOutcome.Failure,
                null, ticket.UserId, ErrorCodes.AuthMfaTokenExpired,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthMfaTokenExpired, 400,
                "The sign-in session has expired. Sign in again.",
                "انتهت صلاحية جلسة تسجيل الدخول. سجّل الدخول مرة أخرى.");
        }

        var user = await accounts.FindByIdAsync(ticket.UserId, cancellationToken);

        // The flag is what the sign-in step checked when it issued the ticket;
        // if the account is gone or a concurrent change already cleared it,
        // treat the ticket as spent (same generic code — no oracle).
        if (user is null || !user.PasswordChangeRequired)
        {
            await AuditAsync(AuditEvents.PasswordChangeFailed, AuditOutcome.Failure,
                user?.Email, ticket.UserId, ErrorCodes.AuthMfaTokenInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthMfaTokenInvalid, 400,
                "The sign-in session is no longer valid. Sign in again.",
                "جلسة تسجيل الدخول لم تعد صالحة. سجّل الدخول مرة أخرى.");
        }

        // The ticket authorised this — set the password, consume the ticket,
        // clear the forced-change flag and end every session, all in one
        // transaction. A policy-rejected new password throws inside the
        // transaction and rolls back, leaving the ticket valid for a retry
        // within its lifetime.
        var ticketConsumed = false;
        await transactionRunner.ExecuteAsync(
            async token =>
            {
                ticketConsumed = await secondFactorTokenRepository.TryConsumeAsync(
                    ticket.Id, now, token);
                if (!ticketConsumed)
                {
                    return;
                }
                await SetPasswordAsync(user, request.NewPassword);
                await ClearChangeFlagAndEndSessionsAsync(user, now, token);
            },
            cancellationToken);

        if (!ticketConsumed)
        {
            await AuditAsync(AuditEvents.PasswordChangeFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthMfaTokenInvalid,
                "already_consumed", cancellationToken);
            throw new ApiException(ErrorCodes.AuthMfaTokenInvalid, 400,
                "The sign-in session is no longer valid. Sign in again.",
                "جلسة تسجيل الدخول لم تعد صالحة. سجّل الدخول مرة أخرى.");
        }

        await AuditAsync(AuditEvents.PasswordChanged, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);

        await SendPasswordChangedNoticeAsync(user, now, cancellationToken);

        logger.LogInformation("Forced password change completed for {Email}", user.Email);
        return new CompletePasswordChangeResponse(true);
    }

    /// <summary>
    /// Sets a new password without the old one — the six-digit code already
    /// authorised it. Remove-then-add runs the Identity password validators and
    /// rolls the security stamp; the caller's transaction keeps the pair atomic.
    /// </summary>
    private async Task SetPasswordAsync(SimfUser user, string newPassword)
    {
        // A7-20 — reject a password that matches the current one or a recent
        // retired one (no-op when history is disabled).
        var retiredHash = user.PasswordHash;
        await EnforceNoPasswordReuseAsync(user, retiredHash, newPassword);

        var removeResult = await accounts.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            throw new ApiException(ErrorCodes.InternalError, 500,
                "The password could not be reset.",
                "تعذّر إعادة تعيين كلمة المرور.");
        }

        var addResult = await accounts.AddPasswordAsync(user, newPassword);
        if (!addResult.Succeeded)
        {
            throw PasswordRejected(addResult);
        }

        await RecordPasswordHistoryAsync(user.Id, retiredHash);
    }

    /// <summary>A7-20 — throws when the new password matches the current password or
    /// one of the most recent <c>PasswordHistoryCount</c> retired passwords. No-op
    /// when history is disabled (count &lt;= 0).</summary>
    private async Task EnforceNoPasswordReuseAsync(SimfUser user, string? currentHash, string newPassword)
    {
        var count = lifecycleOptions.Value.PasswordHistoryCount;
        if (count <= 0)
        {
            return;
        }
        if (!string.IsNullOrEmpty(currentHash)
            && accounts.PasswordHashMatches(user, currentHash, newPassword))
        {
            throw PasswordReused();
        }
        foreach (var hash in await passwordHistory.GetRecentHashesAsync(user.Id, count))
        {
            if (accounts.PasswordHashMatches(user, hash, newPassword))
            {
                throw PasswordReused();
            }
        }
    }

    /// <summary>A7-20 — records the retired hash and prunes to the configured depth.
    /// No-op when history is disabled or there was no prior password.</summary>
    private Task RecordPasswordHistoryAsync(Guid userId, string? retiredHash)
    {
        var count = lifecycleOptions.Value.PasswordHistoryCount;
        if (count <= 0 || string.IsNullOrEmpty(retiredHash))
        {
            return Task.CompletedTask;
        }
        return passwordHistory.RecordAsync(userId, retiredHash, count);
    }

    private static ApiException PasswordReused() =>
        new(ErrorCodes.AuthPasswordPolicy, 400,
            "This password was used recently. Choose a password you have not used before.",
            "تم استخدام كلمة المرور هذه مؤخراً. اختر كلمة مرور لم تستخدمها من قبل.");

    /// <summary>
    /// Clears the forced-change flag and ends every way back in — a new password
    /// must invalidate the old ones (the security stamp moved when the password
    /// was set; the refresh tokens and the device keys are revoked here).
    /// </summary>
    private async Task ClearChangeFlagAndEndSessionsAsync(
        SimfUser user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        user.PasswordChangeRequired = false;
        user.UpdatedAt = now;
        // A7-13 (NCA) — stamp the password age so the expiry clock restarts. This
        // is the single point every change / reset / forced-complete path passes
        // through, so the timestamp stays accurate without touching each set site.
        user.PasswordChangedAt = now;
        await accounts.UpdateAsync(user).EnsureSuccessAsync();
        await refreshTokenRepository.RevokeAllForUserAsync(user.Id, now, cancellationToken);

        // Revoking the refresh tokens alone used to leave the biometric device
        // keys alive, and sign-in-with-device-key is anonymous, so an attacker who
        // had enrolled one simply minted a fresh session after the victim did the
        // one thing every security notice tells them to do. The remedy has to
        // remove every credential, not just the sessions. Deliberately applied to
        // a VOLUNTARY change too: that is frequently the user's own response to
        // suspecting someone else has their account.
        await deviceKeys.RevokeAllForUserAsync(user.Id, cancellationToken);
    }

    /// <summary>
    /// The security notice — in-app row + email — confirming a
    /// password change. Shared by the authenticated change path
    /// (<see cref="ChangePasswordAsync"/>) and the forced-change ticket path
    /// (<see cref="CompletePasswordChangeAsync"/>). Wrapped in
    /// <c>TryDispatchAsync</c> so a notification failure never re-throws after
    /// the password is already changed.
    /// </summary>
    private Task SendPasswordChangedNoticeAsync(
        SimfUser user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var changedTime = now.ToString("u",
            System.Globalization.CultureInfo.InvariantCulture);
        return notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = user.Id,
            Kind = NotificationKind.AccountPasswordChanged,
            Title = "Your SIMF password was changed",
            TitleArabic = "تم تغيير كلمة المرور",
            Body = $"Your SIMF password was changed at {changedTime}. "
                + "If you did not do this, contact the SIMF security team immediately.",
            BodyArabic = $"تم تغيير كلمة المرور الخاصة بك في SIMF بتاريخ {changedTime}. "
                + "إذا لم تكن أنت من قام بذلك، تواصل مع فريق الأمن في SIMF فوراً.",
            Severity = NotificationSeverity.Warning,
            SendEmail = true,
        }, logger, cancellationToken);
    }

    private async Task<string> IssueResetCodeAsync(
        SimfUser user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var previous = await accountCodeRepository.GetLatestUnconsumedAsync(
            user.Id, AccountCodePurpose.PasswordReset, cancellationToken);
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
            Purpose = AccountCodePurpose.PasswordReset,
            Code = AccountCodeHasher.Hash(plaintext),
            CreatedAt = now,
            ExpiresAt = now.Add(ResetCodeLifetime),
        };
        await accountCodeRepository.AddAsync(code, cancellationToken);
        return plaintext;
    }

    /// <summary>
    /// Returns the EmailMessage rather than enqueueing
    /// directly so the caller pairs it with `IEmailQueue.TryEnqueueAsync`
    /// — one helper, one failure audit pattern across all four call
    /// sites (password reset, sign-up, resend verification, sign-in OTP).
    /// </summary>
    private Task<EmailMessage> BuildResetEmailAsync(
        string email, string code, CancellationToken cancellationToken) =>
        emailTemplates.RenderAsync(
            EmailTemplateType.PasswordReset, email,
            EmailTokens.ForCode(code, ResetCodeLifetime), cancellationToken);

    private static DataValidationException PasswordRejected(UserOperationResult result) =>
        new(
            "The new password is not allowed.",
            "كلمة المرور الجديدة غير مسموح بها.",
            result.Errors
                .Select(error => new ApiErrorDetail
                {
                    Field = "newPassword",
                    Message = error.Description,
                    MessageArabic = IdentityErrorTranslator.ToArabic(error),
                })
                .ToList());

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
        ConstantTime.Matches(stored, supplied);
}
