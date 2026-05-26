using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Notifications;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Implements password recovery — forgot-password, reset-password (with the
/// emailed code) and change-password (SIMF-API-001 section 12.7, SIMF-FDS-001).
/// A completed reset or change ends every session for the account, atomically.
/// </summary>
public sealed class PasswordService(
    IUserAccountRepository accounts,
    IAccountCodeRepository accountCodeRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITransactionRunner transactionRunner,
    IEmailQueue emailQueue,
    INotificationDispatcher notifications,
    IAuditLog auditLog,
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
        var now = timeProvider.GetUtcNow();

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
                // H10 / H23 — D-065 / D-083: the code row is persisted;
                // the enqueue is a side-effect on a different scope.
                // TryEnqueueAsync owns the failure-audit pattern so every
                // credential-flow email-dispatch site uses the same shape.
                await emailQueue.TryEnqueueAsync(
                    BuildResetEmail(user.Email!, code),
                    purpose: "PasswordReset",
                    subjectEmail: user.Email!,
                    subjectUserId: user.Id,
                    auditLog: auditLog,
                    logger: logger,
                    cancellationToken: cancellationToken);
                // D-099: in-app trail for the credential email — visible
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
        var now = timeProvider.GetUtcNow();

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

        if (!CodesMatch(code.Code, request.Code))
        {
            code.AttemptCount++;
            await accountCodeRepository.UpdateAsync(code, cancellationToken);
            await AuditAsync(AuditEvents.PasswordResetCodeIncorrect, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthResetCodeInvalid,
                $"attempt {code.AttemptCount}", cancellationToken);
            throw new ApiException(ErrorCodes.AuthResetCodeInvalid, 400,
                "The reset code is not correct.",
                "رمز إعادة التعيين غير صحيح.");
        }

        // The code authorised this — set the password, consume the code, clear
        // the forced-change flag and end every session, all in one transaction.
        await transactionRunner.ExecuteAsync(
            async token =>
            {
                await SetPasswordAsync(user, request.NewPassword);
                code.ConsumedAt = now;
                await accountCodeRepository.UpdateAsync(code, token);
                await ClearChangeFlagAndEndSessionsAsync(user, now, token);
            },
            cancellationToken);

        await AuditAsync(AuditEvents.PasswordResetCompleted, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);
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
                var result = await accounts.ChangePasswordAsync(
                    user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded)
                {
                    // H11 — D-066: separate the wrong-current-password
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

                await ClearChangeFlagAndEndSessionsAsync(
                    user, timeProvider.GetUtcNow(), token);
            },
            cancellationToken);

        await AuditAsync(AuditEvents.PasswordChanged, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);
        logger.LogInformation("Password changed for {Email}", user.Email);
        return new ChangePasswordResponse(true);
    }

    /// <summary>
    /// Sets a new password without the old one — the six-digit code already
    /// authorised it. Remove-then-add runs the Identity password validators and
    /// rolls the security stamp; the caller's transaction keeps the pair atomic.
    /// </summary>
    private async Task SetPasswordAsync(SimfUser user, string newPassword)
    {
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
    }

    /// <summary>
    /// Clears the forced-change flag and ends every session — a new password
    /// must invalidate the old ones (the security stamp moved when the password
    /// was set; the refresh tokens are revoked here).
    /// </summary>
    private async Task ClearChangeFlagAndEndSessionsAsync(
        SimfUser user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        user.PasswordChangeRequired = false;
        user.UpdatedAt = now;
        await accounts.UpdateAsync(user).EnsureSuccessAsync();
        await refreshTokenRepository.RevokeAllForUserAsync(user.Id, now, cancellationToken);
    }

    private async Task<string> IssueResetCodeAsync(
        SimfUser user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var previous = await accountCodeRepository.GetLatestUnconsumedAsync(
            user.Id, AccountCodePurpose.PasswordReset, cancellationToken);
        if (previous is not null)
        {
            previous.ConsumedAt = now;
            await accountCodeRepository.UpdateAsync(previous, cancellationToken);
        }

        var code = new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = AccountCodePurpose.PasswordReset,
            Code = VerificationCodeGenerator.Generate(),
            CreatedAt = now,
            ExpiresAt = now.Add(ResetCodeLifetime),
        };
        await accountCodeRepository.AddAsync(code, cancellationToken);
        return code.Code;
    }

    /// <summary>
    /// H23 — D-083: returns the EmailMessage rather than enqueueing
    /// directly so the caller pairs it with `IEmailQueue.TryEnqueueAsync`
    /// — one helper, one failure audit pattern across all four call
    /// sites (password reset, sign-up, resend verification, sign-in OTP).
    /// </summary>
    private static EmailMessage BuildResetEmail(string email, string code)
    {
        var minutes = (int)ResetCodeLifetime.TotalMinutes;
        var body =
            $"<p>Your SIMF password reset code is <strong>{code}</strong>.</p>" +
            $"<p>The code expires in {minutes} minutes. If you did not request a " +
            "password reset, you can ignore this email.</p>";
        return new EmailMessage(email, "SIMF password reset", body);
    }

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
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored),
            Encoding.UTF8.GetBytes(supplied));
}
