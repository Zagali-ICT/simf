// Tests: SIMF.Api.Tests/ChangeEmailTests.cs (send-otp: same-email, taken,
//        rate-limit; confirm: happy, wrong/expired code, attempt-cap, unauthorized)
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// #24 — self-service change of a signed-in user's login email. The design mirrors
/// the biometric-enrol step-up: a one-time code is emailed to the NEW address, and
/// the change only completes once the user submits it — proving they control the
/// new inbox. The code is stored as a keyed hash bound to the target address
/// (<c>Hash("{code}:{newEmail}")</c>), so a code issued for one address can never
/// confirm a different one, and the frozen <c>AccountCode</c> table needs no new
/// column. On confirm the login email + username move together, the address is
/// marked confirmed, the security stamp is rolled and every refresh token is
/// revoked — the same identity-change protection the admin edit path applies — so
/// the caller's old tokens die and the app must re-authenticate.
/// </summary>
public sealed class EmailChangeService(
    IUserAccountRepository accounts,
    IAccountCodeRepository accountCodes,
    IRefreshTokenRepository refreshTokenRepository,
    IEmailQueue emailQueue,
    IEmailTemplateResolver emailTemplates,
    ITransactionRunner transactionRunner,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<EmailChangeService> logger) : IEmailChangeService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendWindow = TimeSpan.FromHours(1);
    private const int MaxCodesPerWindow = 5;
    private const int MaxCodeAttempts = 5;
    // Client resend-button cooldown; the hard cap is MaxCodesPerWindow.
    private const int ResendCooldownSeconds = 120;

    private static readonly IReadOnlyDictionary<string, string> EmptyTokens =
        new Dictionary<string, string>();

    public async Task<SendEmailChangeOtpResponse> SendChangeOtpAsync(
        Guid userId, SendEmailChangeOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var newEmail = (request.NewEmail ?? string.Empty).Trim();
        var user = await LoadActiveUserAsync(userId, newEmail, cancellationToken);

        // Nothing to change — the new address is the account's current one. Told
        // apart from a real change so the client shows a clear message instead of
        // silently emailing a code to the address the user is already on.
        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            await AuditFailure(userId, newEmail, ErrorCodes.AuthEmailUnchanged,
                "same_email", cancellationToken);
            throw new ApiException(ErrorCodes.AuthEmailUnchanged, 400,
                "This is already your email address.",
                "هذا هو بريدك الإلكتروني الحالي بالفعل.");
        }

        var now = timeProvider.GetUtcNow();

        // Cap how many change codes one account may request per window, so a
        // signed-in session can't be used to spam an address with emails.
        var recent = await accountCodes.CountCreatedSinceAsync(
            userId, AccountCodePurpose.EmailChangeVerification,
            now - ResendWindow, cancellationToken);
        if (recent >= MaxCodesPerWindow)
        {
            await AuditFailure(userId, newEmail, ErrorCodes.RateLimitExceeded,
                "rate_limited", cancellationToken);
            throw new ApiException(ErrorCodes.RateLimitExceeded, 429,
                "Too many verification codes have been requested. Try again later.",
                "تم طلب رموز تحقق كثيرة. حاول مرة أخرى لاحقًا.");
        }

        // Enumeration resistance (mirrors the D-198 sign-up deflect): a target
        // already held by ANOTHER account returns the SAME success shape as a free
        // address but issues no code — the real owner is instead warned out-of-band
        // that someone tried to move an account onto their address. So an
        // authenticated caller cannot use the response to map which emails exist.
        // (The confirm path keeps its 409: there the caller must already hold a
        // valid code bound to the address, so it is not an enumeration oracle.)
        var existing = await accounts.FindByEmailAsync(newEmail, cancellationToken);
        if (existing is not null && existing.Id != userId)
        {
            await emailQueue.TryEnqueueAsync(
                await emailTemplates.RenderAsync(
                    EmailTemplateType.AccountExists, existing.Email!,
                    EmptyTokens, cancellationToken),
                purpose: "EmailChangeDeflected",
                subjectEmail: existing.Email!,
                subjectUserId: existing.Id,
                auditLog: auditLog,
                logger: logger,
                cancellationToken: cancellationToken);
            await AuditOtpRequested(userId, newEmail, "deflected", cancellationToken);
            return new SendEmailChangeOtpResponse(
                EmailMask.Mask(newEmail),
                (int)CodeLifetime.TotalSeconds,
                ResendCooldownSeconds);
        }

        // Only the newest code stays valid — consume any prior unconsumed one so a
        // resend (or a change of target address) invalidates the previous code.
        var previous = await accountCodes.GetLatestUnconsumedAsync(
            userId, AccountCodePurpose.EmailChangeVerification, cancellationToken);
        if (previous is not null)
        {
            await accountCodes.TryConsumeAsync(previous.Id, now, cancellationToken);
        }

        // Store only the keyed hash, bound to the target address; the plaintext is
        // emailed and never persisted.
        var plaintext = VerificationCodeGenerator.Generate();
        await accountCodes.AddAsync(new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = AccountCodePurpose.EmailChangeVerification,
            Code = AccountCodeHasher.Hash(BindCodeToEmail(plaintext, newEmail)),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime),
        }, cancellationToken);

        // The code goes to the NEW address — controlling that inbox is the proof.
        await emailQueue.TryEnqueueAsync(
            await emailTemplates.RenderAsync(
                EmailTemplateType.EmailChangeVerification, newEmail,
                EmailTokens.ForCode(plaintext, CodeLifetime), cancellationToken),
            purpose: "EmailChangeVerification",
            subjectEmail: newEmail,
            subjectUserId: userId,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);

        await AuditOtpRequested(userId, newEmail, "issued", cancellationToken);

        logger.LogInformation("Email-change code issued for user {UserId}", userId);
        return new SendEmailChangeOtpResponse(
            EmailMask.Mask(newEmail),
            (int)CodeLifetime.TotalSeconds,
            ResendCooldownSeconds);
    }

    public async Task<ConfirmEmailChangeResponse> ConfirmChangeAsync(
        Guid userId, ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var newEmail = (request.NewEmail ?? string.Empty).Trim();
        var user = await LoadActiveUserAsync(userId, newEmail, cancellationToken);

        // Re-authenticate with the current password before completing the change.
        // An access token proves a held session, not the owner's intent; requiring
        // the password (as change-password does) means a lost/leaked token alone
        // cannot seize the account by pointing the login email at an attacker's
        // inbox. Checked before the code so a wrong password fails fast.
        if (!await accounts.CheckPasswordAsync(
                user, request.CurrentPassword ?? string.Empty, cancellationToken))
        {
            await AuditFailure(userId, newEmail, ErrorCodes.AuthInvalidCredentials,
                "bad_password", cancellationToken);
            throw new ApiException(ErrorCodes.AuthInvalidCredentials, 401,
                "The password is not correct.",
                "كلمة المرور غير صحيحة.");
        }

        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            await AuditFailure(userId, newEmail, ErrorCodes.AuthEmailUnchanged,
                "same_email", cancellationToken);
            throw new ApiException(ErrorCodes.AuthEmailUnchanged, 400,
                "This is already your email address.",
                "هذا هو بريدك الإلكتروني الحالي بالفعل.");
        }

        // Re-check uniqueness — the address may have been taken between the two steps.
        await EnsureEmailFreeAsync(userId, newEmail, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var code = await accountCodes.GetLatestUnconsumedAsync(
            userId, AccountCodePurpose.EmailChangeVerification, cancellationToken);

        if (code is null)
        {
            await AuditFailure(userId, newEmail, ErrorCodes.AuthCodeInvalid,
                "no_code", cancellationToken);
            throw new ApiException(ErrorCodes.AuthCodeInvalid, 400,
                "No verification code is outstanding. Request a new one.",
                "لا يوجد رمز تحقق فعّال. اطلب رمزًا جديدًا.");
        }

        if (now >= code.ExpiresAt)
        {
            await accountCodes.TryConsumeAsync(code.Id, now, cancellationToken);
            await AuditFailure(userId, newEmail, ErrorCodes.AuthCodeExpired,
                "expired", cancellationToken);
            throw new ApiException(ErrorCodes.AuthCodeExpired, 400,
                "The verification code has expired. Request a new one.",
                "انتهت صلاحية رمز التحقق. اطلب رمزًا جديدًا.");
        }

        if (code.AttemptCount >= MaxCodeAttempts)
        {
            await AuditFailure(userId, newEmail, ErrorCodes.AuthCodeInvalid,
                "attempt_cap", cancellationToken);
            throw new ApiException(ErrorCodes.AuthCodeInvalid, 400,
                "Too many incorrect attempts. Request a new code.",
                "محاولات غير صحيحة كثيرة. اطلب رمزًا جديدًا.");
        }

        // The hash is bound to the target address, so a code emailed to address A
        // never confirms a change to address B — even with a valid-looking code.
        if (!CodesMatch(code.Code,
                AccountCodeHasher.Hash(BindCodeToEmail((request.Code ?? string.Empty).Trim(), newEmail))))
        {
            await accountCodes.IncrementAttemptCountAsync(code.Id, cancellationToken);
            await AuditFailure(userId, newEmail, ErrorCodes.AuthCodeInvalid,
                "mismatch", cancellationToken);
            throw new ApiException(ErrorCodes.AuthCodeInvalid, 400,
                "The verification code is not correct.",
                "رمز التحقق غير صحيح.");
        }

        // The previous address, captured before the transaction overwrites it, so
        // the security alert below can reach the owner's old inbox.
        var oldEmail = user.Email!;

        await transactionRunner.ExecuteAsync(async innerCt =>
        {
            // Single-use gate: only the caller that flips ConsumedAt from null to
            // now proceeds, so a concurrent double-submit applies the change once.
            if (!await accountCodes.TryConsumeAsync(code.Id, now, innerCt))
            {
                throw new ApiException(ErrorCodes.AuthCodeInvalid, 400,
                    "The verification code is no longer valid.",
                    "رمز التحقق لم يعد صالحًا.");
            }

            // UserName == Email for every account, so both move together; the code
            // proved the address, so it is confirmed. UpdateAsync re-normalizes
            // NormalizedEmail/NormalizedUserName, which back the unique indexes.
            user.Email = newEmail;
            user.UserName = newEmail;
            user.EmailConfirmed = true;
            user.UpdatedAt = now;
            var updateResult = await accounts.UpdateAsync(user, innerCt);
            if (!updateResult.Succeeded)
            {
                throw new ApiException(ErrorCodes.InternalError, 500,
                    "The email address could not be updated.",
                    "تعذّر تحديث البريد الإلكتروني.");
            }

            // An email change is an identity change: roll the stamp (invalidates
            // every live access token via the security_stamp claim) and revoke
            // refresh tokens, so no stale session survives under the old identity.
            await accounts.UpdateSecurityStampAsync(user, innerCt);
            await refreshTokenRepository.RevokeAllForUserAsync(userId, now, innerCt);

            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.EmailChanged,
                Outcome = AuditOutcome.Success,
                ActorUserId = userId,
                SubjectUserId = userId,
                SubjectEmail = newEmail,
                Detail = $"to={EmailMask.Mask(newEmail)}",
            }, innerCt);
        }, cancellationToken);

        // Warn the PREVIOUS address out-of-band: if this change was not the owner's
        // (a hijacked session), this alert to the address they still control is the
        // early-warning signal — their sessions are now revoked and the new login
        // email is the attacker's. TryEnqueueAsync owns the failure-audit pattern,
        // so a mail hiccup never re-throws after the change already committed.
        await emailQueue.TryEnqueueAsync(
            await emailTemplates.RenderAsync(
                EmailTemplateType.EmailChangedNotice, oldEmail,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["NewEmail"] = EmailMask.Mask(newEmail),
                },
                cancellationToken),
            purpose: "EmailChangedNotice",
            subjectEmail: oldEmail,
            subjectUserId: userId,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);

        logger.LogInformation("Login email changed for user {UserId}", userId);
        return new ConfirmEmailChangeResponse(true);
    }

    /// <summary>
    /// Loads the signed-in caller. A missing or disabled account is reported as
    /// 401 (the token proved a caller, so this only fires on a deleted/disabled
    /// account or a bad id) — never leaking whether the account exists.
    /// </summary>
    private async Task<SimfUser> LoadActiveUserAsync(
        Guid userId, string newEmail, CancellationToken cancellationToken)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.Email)
            || user.AccountState == AccountState.Disabled)
        {
            await AuditFailure(userId, newEmail, ErrorCodes.AuthAccountNotFound,
                "account_unavailable", cancellationToken);
            throw new ApiException(ErrorCodes.AuthAccountNotFound, 401,
                "Account unavailable.",
                "الحساب غير متاح.");
        }
        return user;
    }

    /// <summary>Rejects a new address already held by another account (409). A hit
    /// on the caller's own row is impossible here — the same-email guard runs first.</summary>
    private async Task EnsureEmailFreeAsync(
        Guid userId, string newEmail, CancellationToken cancellationToken)
    {
        var existing = await accounts.FindByEmailAsync(newEmail, cancellationToken);
        if (existing is not null && existing.Id != userId)
        {
            await AuditFailure(userId, newEmail, ErrorCodes.AuthEmailAlreadyRegistered,
                "taken", cancellationToken);
            throw new ApiException(ErrorCodes.AuthEmailAlreadyRegistered, 409,
                "An account with this email address already exists.",
                "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل.");
        }
    }

    // Binds a code to its target address so the stored hash only validates for
    // that exact (case-insensitive) address — no target field on the frozen row.
    private static string BindCodeToEmail(string code, string email) =>
        $"{code}:{email.ToLowerInvariant()}";

    private Task AuditOtpRequested(
        Guid userId, string newEmail, string detail,
        CancellationToken cancellationToken) =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.EmailChangeOtpRequested,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            SubjectUserId = userId,
            SubjectEmail = newEmail,
            Detail = $"{detail}; to={EmailMask.Mask(newEmail)}",
        }, cancellationToken);

    private Task AuditFailure(
        Guid userId, string email, string errorCode, string reason,
        CancellationToken cancellationToken) =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.EmailChangeFailed,
            Outcome = AuditOutcome.Failure,
            ActorUserId = userId,
            SubjectUserId = userId,
            SubjectEmail = email,
            ErrorCode = errorCode,
            Detail = $"reason={reason}",
        }, cancellationToken);

    /// <summary>Compares the codes in constant time, so no timing side channel leaks.</summary>
    private static bool CodesMatch(string stored, string supplied) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored),
            Encoding.UTF8.GetBytes(supplied));
}
