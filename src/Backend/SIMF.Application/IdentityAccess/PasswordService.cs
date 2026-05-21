using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Implements password recovery — forgot-password, reset-password (with the
/// emailed code) and change-password (SIMF-API-001 section 12.4, SIMF-FDS-001).
/// A completed reset or change ends every session for the account.
/// </summary>
public sealed class PasswordService(
    UserManager<SimfUser> userManager,
    IAccountCodeRepository accountCodeRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITransactionRunner transactionRunner,
    IEmailQueue emailQueue,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<PasswordService> logger) : IPasswordService
{
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResetRequestWindow = TimeSpan.FromHours(1);
    private const int MaxResetAttempts = 5;
    private const int MaxResetCodesPerWindow = 5;

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
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
                EnqueueResetEmail(user.Email!, code);
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
        return new ForgotPasswordResponse(
            "If an account exists for that email address, a reset code has been sent.");
    }

    public async Task<PasswordChangedResponse> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        var now = timeProvider.GetUtcNow();

        var code = user is null
            ? null
            : await accountCodeRepository.GetLatestUnconsumedAsync(
                user.Id, AccountCodePurpose.PasswordReset, cancellationToken);

        if (user is null || code is null || code.AttemptCount >= MaxResetAttempts)
        {
            await AuditAsync(AuditEvents.PasswordResetFailed, AuditOutcome.Failure,
                request.Email, user?.Id, ErrorCodes.AuthResetCodeInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthResetCodeInvalid, 400,
                "The reset code is not valid.");
        }

        if (now >= code.ExpiresAt)
        {
            await AuditAsync(AuditEvents.PasswordResetFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthResetCodeExpired,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthResetCodeExpired, 400,
                "The reset code has expired. Request a new one.");
        }

        if (!CodesMatch(code.Code, request.Code))
        {
            code.AttemptCount++;
            await accountCodeRepository.UpdateAsync(code, cancellationToken);
            await AuditAsync(AuditEvents.PasswordResetFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthResetCodeInvalid,
                cancellationToken: cancellationToken);
            throw new ApiException(ErrorCodes.AuthResetCodeInvalid, 400,
                "The reset code is not correct.");
        }

        // The six-digit code already authorised this reset, so the password is
        // set directly. Remove-then-add runs the Identity password validators
        // and rolls the security stamp; the transaction keeps the pair atomic,
        // so a rejected new password leaves the old one intact.
        await transactionRunner.ExecuteAsync(
            async _ =>
            {
                await userManager.RemovePasswordAsync(user);
                var addResult = await userManager.AddPasswordAsync(user, request.NewPassword);
                if (!addResult.Succeeded)
                {
                    throw PasswordRejected(addResult);
                }
            },
            cancellationToken);

        code.ConsumedAt = now;
        await accountCodeRepository.UpdateAsync(code, cancellationToken);
        await ClearPasswordChangeRequiredAndEndSessionsAsync(user, now, cancellationToken);

        await AuditAsync(AuditEvents.PasswordResetCompleted, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);
        logger.LogInformation("Password reset for {Email}", user.Email);
        return new PasswordChangedResponse(true);
    }

    public async Task<PasswordChangedResponse> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new ApiException(ErrorCodes.AuthAccountNotFound, 404,
                "The account was not found.");

        var result = await userManager.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            await AuditAsync(AuditEvents.PasswordChangeFailed, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthInvalidCredentials,
                cancellationToken: cancellationToken);

            if (result.Errors.Any(error => error.Code == "PasswordMismatch"))
            {
                throw new ApiException(ErrorCodes.AuthInvalidCredentials, 400,
                    "The current password is not correct.");
            }

            throw PasswordRejected(result);
        }

        await ClearPasswordChangeRequiredAndEndSessionsAsync(
            user, timeProvider.GetUtcNow(), cancellationToken);

        await AuditAsync(AuditEvents.PasswordChanged, AuditOutcome.Success,
            user.Email!, user.Id, cancellationToken: cancellationToken);
        logger.LogInformation("Password changed for {Email}", user.Email);
        return new PasswordChangedResponse(true);
    }

    /// <summary>
    /// Clears the forced-change flag and ends every session — a new password
    /// must invalidate the old sessions (the security stamp moved when the
    /// password was set; the refresh tokens are revoked here).
    /// </summary>
    private async Task ClearPasswordChangeRequiredAndEndSessionsAsync(
        SimfUser user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        user.PasswordChangeRequired = false;
        user.UpdatedAt = now;
        await userManager.UpdateAsync(user);
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

    private void EnqueueResetEmail(string email, string code)
    {
        var minutes = (int)ResetCodeLifetime.TotalMinutes;
        var body =
            $"<p>Your SIMF password reset code is <strong>{code}</strong>.</p>" +
            $"<p>The code expires in {minutes} minutes. If you did not request a " +
            "password reset, you can ignore this email.</p>";
        emailQueue.Enqueue(new EmailMessage(email, "SIMF password reset", body));
    }

    private static DataValidationException PasswordRejected(IdentityResult result) =>
        new(
            "The new password is not allowed.",
            result.Errors
                .Select(error => new ApiErrorDetail
                {
                    Field = "newPassword",
                    Message = error.Description,
                })
                .ToList());

    private Task AuditAsync(
        string eventType,
        AuditOutcome outcome,
        string? email,
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
