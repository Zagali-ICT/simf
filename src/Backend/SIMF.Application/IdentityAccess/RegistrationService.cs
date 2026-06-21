// Tests: SIMF.Api.Tests/RegistrationEndpointsTests.cs (sign-up: new account,
//        unverified restart, existing-verified deflect; verify-email; resend-code)
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Application.Operations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Notifications;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Implements account creation — sign-up, email verification and code resend
/// (SIMF-API-001 section 12.4, SIMF-FDS-001). It takes a self-registered
/// account as far as <see cref="AccountState.EmailVerified"/>; the registration
/// profile and the approval workflow belong to SIMF-FDS-002. Every outcome is
/// written to the operation log.
/// </summary>
public sealed class RegistrationService(
    IUserAccountRepository accounts,
    IAccountCodeRepository accountCodeRepository,
    IEmailQueue emailQueue,
    INotificationDispatcher notifications,
    ITransactionRunner transactionRunner,
    IAuditLog auditLog,
    IOperationsToggleService operationsToggles,
    TimeProvider timeProvider,
    ILogger<RegistrationService> logger) : IRegistrationService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendWindow = TimeSpan.FromHours(1);
    private const int MaxCodeAttempts = 5;
    private const int MaxCodesPerWindow = 5;

    public async Task<SignUpResponse> SignUpAsync(
        SignUpRequest request,
        CancellationToken cancellationToken = default)
    {
        // D-166 (gap doc G4, PDF §2.3) — honour the registration gate
        // before touching the Identity DB. Closed gate → 403 with the
        // typed REGISTRATION_CLOSED code; the user is not created, no
        // email is sent.
        if (!await operationsToggles.IsRegistrationOpenAsync(cancellationToken))
        {
            await AuditAsync(
                AuditEvents.SignUpRejectedRegistrationClosed, AuditOutcome.Failure,
                request.Email, errorCode: ErrorCodes.RegistrationClosed,
                cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.RegistrationClosed,
                403,
                "Registration is currently closed. Please try again later.",
                "التسجيل مغلق حالياً. يرجى المحاولة لاحقاً.");
        }

        var now = timeProvider.GetUtcNow();
        var existing = await accounts.FindByEmailAsync(request.Email);

        // D-198 — enumeration-resistant sign-up. A brand-new email, a
        // restart of an unverified account, and an attempt against an
        // already-verified account all return the SAME SignUpResponse
        // shape, so sign-up never reveals whether an email is registered
        // and an honest user who forgot an unfinished sign-up just
        // continues instead of hitting a dead "you already have an
        // account" wall.
        if (existing is not null)
        {
            return existing.AccountState == AccountState.Registered
                ? await RestartUnverifiedAccountAsync(existing, request.Password, now, cancellationToken)
                : await DeflectExistingVerifiedAccountAsync(existing, cancellationToken);
        }

        var user = new SimfUser
        {
            UserName = request.Email,
            Email = request.Email,
            // TODO(SIMF-FDS-002): replaced by the real name at profile completion.
            DisplayName = request.Email,
            AccountState = AccountState.Registered,
            CreatedAt = now,
            // D-373 — the email-OTP second factor is ON for every new visitor
            // account (owner rule); an admin may disable it per account via
            // the CP. Previously off by default (D-033).
            TwoFactorEnabled = true,
        };

        string? issuedCode = null;

        // The user row and its first verification code must commit together —
        // otherwise a failure leaves an account that can never be verified.
        await transactionRunner.ExecuteAsync(
            async token =>
            {
                var createResult = await accounts.CreateAsync(user, request.Password);
                if (!createResult.Succeeded)
                {
                    throw ToAccountCreationException(createResult);
                }

                issuedCode = await IssueVerificationCodeAsync(user, now, token);
            },
            cancellationToken);

        await EnqueueVerificationCodeAsync(user, issuedCode!, cancellationToken);
        await AuditAsync(
            AuditEvents.SignUpSucceeded, AuditOutcome.Success, user.Email!,
            userId: user.Id, cancellationToken: cancellationToken);
        logger.LogInformation("Account registered for {Email}", user.Email);
        return new SignUpResponse(user.Email!, (int)CodeLifetime.TotalSeconds);
    }

    public async Task<VerifyEmailResponse> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByEmailAsync(request.Email);
        if (user is null)
        {
            await AuditAsync(
                AuditEvents.EmailVerificationAccountNotFound, AuditOutcome.Failure, request.Email,
                errorCode: ErrorCodes.AuthAccountNotFound, cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthAccountNotFound,
                404,
                "No account was found for this email address.",
                "لم يتم العثور على حساب بهذا البريد الإلكتروني.");
        }

        if (user.AccountState != AccountState.Registered)
        {
            await AuditAsync(
                AuditEvents.EmailVerificationAccountNotRegistered, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthCodeInvalid, cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "This account's email address is already verified.",
                "تم التحقق من بريد هذا الحساب مسبقًا.");
        }

        var now = timeProvider.GetUtcNow();
        var code = await accountCodeRepository.GetLatestUnconsumedAsync(
            user.Id, AccountCodePurpose.EmailVerification, cancellationToken);

        if (code is null)
        {
            await AuditAsync(
                AuditEvents.EmailVerificationCodeIncorrect, AuditOutcome.Failure, user.Email!,
                user.Id, ErrorCodes.AuthCodeInvalid, cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "No verification code is outstanding. Request a new one.",
                "لا يوجد رمز تحقق فعّال. اطلب رمزًا جديدًا.");
        }

        if (now >= code.ExpiresAt)
        {
            await AuditAsync(
                AuditEvents.EmailVerificationCodeExpired, AuditOutcome.Failure, user.Email!,
                user.Id, ErrorCodes.AuthCodeExpired, cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthCodeExpired,
                400,
                "The verification code has expired. Request a new one.",
                "انتهت صلاحية رمز التحقق. اطلب رمزًا جديدًا.");
        }

        if (code.AttemptCount >= MaxCodeAttempts)
        {
            await AuditAsync(
                AuditEvents.EmailVerificationAttemptCapReached, AuditOutcome.Failure, user.Email!,
                user.Id, ErrorCodes.AuthCodeInvalid, cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "Too many incorrect attempts. Request a new code.",
                "محاولات غير صحيحة كثيرة. اطلب رمزًا جديدًا.");
        }

        if (!CodesMatch(code.Code, AccountCodeHasher.Hash(request.Code)))
        {
            code.AttemptCount++;
            await accountCodeRepository.UpdateAsync(code, cancellationToken);
            await AuditAsync(
                AuditEvents.EmailVerificationCodeIncorrect, AuditOutcome.Failure, user.Email!,
                user.Id, ErrorCodes.AuthCodeInvalid, cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "The verification code is not correct.",
                "رمز التحقق غير صحيح.");
        }

        await transactionRunner.ExecuteAsync(
            async token =>
            {
                code.ConsumedAt = now;
                await accountCodeRepository.UpdateAsync(code, token);

                user.EmailConfirmed = true;
                user.AccountState = AccountState.EmailVerified;
                user.UpdatedAt = now;
                await accounts.UpdateAsync(user).EnsureSuccessAsync();
            },
            cancellationToken);

        await AuditAsync(
            AuditEvents.EmailVerificationSucceeded, AuditOutcome.Success, user.Email!,
            userId: user.Id, cancellationToken: cancellationToken);

        // D-111: welcome the user — in-app row visible the next time they
        // sign in + a separate welcome email. Wrapped in TryDispatchAsync
        // so a notification failure never re-throws after the email is
        // already verified. No PreRenderedEmailHtml — the dispatcher
        // falls back to wrapping Body in <p>, which is enough for a
        // simple welcome.
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = user.Id,
            Kind = NotificationKind.AccountWelcome,
            Title = "Welcome to SIMF",
            TitleArabic = "مرحباً بك في SIMF",
            Body = $"Welcome to SIMF, {user.DisplayName ?? user.Email}. You can now sign in and complete your profile.",
            BodyArabic = $"أهلاً بك في SIMF، {user.DisplayName ?? user.Email}. يمكنك الآن تسجيل الدخول واستكمال ملفك الشخصي.",
            Severity = NotificationSeverity.Success,
            SendEmail = true,
        }, logger, cancellationToken);

        logger.LogInformation("Email verified for {Email}", user.Email);
        return new VerifyEmailResponse(user.Email!, true);
    }

    public async Task<ResendCodeResponse> ResendCodeAsync(
        ResendCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByEmailAsync(request.Email);
        if (user is null)
        {
            await AuditAsync(
                AuditEvents.ResendCodeAccountNotFound, AuditOutcome.Failure, request.Email,
                errorCode: ErrorCodes.AuthAccountNotFound, cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthAccountNotFound,
                404,
                "No account was found for this email address.",
                "لم يتم العثور على حساب بهذا البريد الإلكتروني.");
        }

        if (user.AccountState != AccountState.Registered)
        {
            await AuditAsync(
                AuditEvents.ResendCodeAccountNotRegistered, AuditOutcome.Failure,
                user.Email!, user.Id, ErrorCodes.AuthCodeInvalid, cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "This account's email address is already verified.",
                "تم التحقق من بريد هذا الحساب مسبقًا.");
        }

        var now = timeProvider.GetUtcNow();

        // Cap how often a code may be re-issued for one account, independent of
        // the per-IP rate limiter (resend abuse is keyed on the email).
        await EnsureVerificationCodeCapNotReachedAsync(user, now, cancellationToken);

        var code = await IssueVerificationCodeAsync(user, now, cancellationToken);
        // H10 / H23 — D-065 / D-083: same shape as sign-up; helper owns
        // the failure-audit pattern.
        await emailQueue.TryEnqueueAsync(
            BuildVerificationEmail(user.Email!, code),
            purpose: "ResendVerification",
            subjectEmail: user.Email!,
            subjectUserId: user.Id,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);
        // D-099: same in-app trail as the initial sign-up dispatch.
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = user.Id,
            Kind = NotificationKind.CredentialEmailVerificationResent,
            Title = "Verification code re-issued",
            TitleArabic = "تم إعادة إصدار رمز التحقق",
            Body = "A new email-verification code was sent to your address.",
            BodyArabic = "تم إرسال رمز تحقق جديد إلى عنوانك.",
            Severity = NotificationSeverity.Info,
            SendEmail = false,
        }, logger, cancellationToken);
        await AuditAsync(
            AuditEvents.ResendCodeIssued, AuditOutcome.Success, user.Email!,
            userId: user.Id, cancellationToken: cancellationToken);
        logger.LogInformation("Verification code re-issued for {Email}", user.Email);
        return new ResendCodeResponse(user.Email!, (int)CodeLifetime.TotalSeconds);
    }

    /// <summary>
    /// Enforces the per-account verification-code cap shared by the resend and
    /// unverified-restart paths: at most <see cref="MaxCodesPerWindow"/>
    /// EmailVerification codes per <see cref="ResendWindow"/>. Audits and
    /// throws 429 when the cap is reached. The new-account path is
    /// intentionally uncapped — the account did not exist a moment earlier.
    /// </summary>
    private async Task EnsureVerificationCodeCapNotReachedAsync(
        SimfUser user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var recentCodes = await accountCodeRepository.CountCreatedSinceAsync(
            user.Id, AccountCodePurpose.EmailVerification, now - ResendWindow, cancellationToken);
        if (recentCodes >= MaxCodesPerWindow)
        {
            await AuditAsync(
                AuditEvents.ResendCodeCapReached, AuditOutcome.Failure, user.Email!,
                user.Id, ErrorCodes.RateLimitExceeded, cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.RateLimitExceeded,
                429,
                "Too many verification codes have been requested. Try again later.",
                "تم طلب رموز تحقق كثيرة. حاول مرة أخرى لاحقًا.");
        }
    }

    private async Task<SignUpResponse> RestartUnverifiedAccountAsync(
        SimfUser user,
        string newPassword,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // D-198 — the account was never verified, so no one owns it yet:
        // treat re-sign-up as "start over". The newly-typed password wins
        // and a fresh verification code is issued. The per-account code cap
        // (shared with resend) still applies so this path can't be abused to
        // mint unlimited codes.
        await EnsureVerificationCodeCapNotReachedAsync(user, now, cancellationToken);

        string? issuedCode = null;
        await transactionRunner.ExecuteAsync(
            async token =>
            {
                // Replace the password without knowing the old one
                // (ChangePassword needs the current password): remove then
                // add. A weak new password fails AddPassword and rolls the
                // whole transaction back.
                await accounts.RemovePasswordAsync(user, token).EnsureSuccessAsync();
                var addResult = await accounts.AddPasswordAsync(user, newPassword, token);
                if (!addResult.Succeeded)
                {
                    throw ToAccountCreationException(addResult);
                }

                // Roll the security stamp so any access token minted against
                // the previous credential is invalidated (H5 — D-060).
                await accounts.UpdateSecurityStampAsync(user, token);
                user.UpdatedAt = now;
                await accounts.UpdateAsync(user).EnsureSuccessAsync();

                issuedCode = await IssueVerificationCodeAsync(user, now, token);
            },
            cancellationToken);

        await EnqueueVerificationCodeAsync(user, issuedCode!, cancellationToken);
        await AuditAsync(
            AuditEvents.SignUpRestartedUnverified, AuditOutcome.Success, user.Email!,
            userId: user.Id, cancellationToken: cancellationToken);
        logger.LogInformation("Sign-up restarted for unverified account {Email}", user.Email);
        return new SignUpResponse(user.Email!, (int)CodeLifetime.TotalSeconds);
    }

    private async Task<SignUpResponse> DeflectExistingVerifiedAccountAsync(
        SimfUser user,
        CancellationToken cancellationToken)
    {
        // D-198 — the email belongs to a real (already-verified) account.
        // We must not reveal that, and must not let a stranger's password or
        // a verification code touch the account. Email the OWNER a heads-up
        // and return the same generic response a fresh sign-up would.
        await emailQueue.TryEnqueueAsync(
            BuildAccountExistsEmail(user.Email!),
            purpose: "SignUpExistingAccount",
            subjectEmail: user.Email!,
            subjectUserId: user.Id,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);
        await AuditAsync(
            AuditEvents.SignUpExistingAccountDeflected, AuditOutcome.Failure, user.Email!,
            userId: user.Id, errorCode: ErrorCodes.AuthEmailAlreadyRegistered,
            cancellationToken: cancellationToken);
        logger.LogInformation(
            "Sign-up against existing verified account {Email} — owner notified", user.Email);
        return new SignUpResponse(user.Email!, (int)CodeLifetime.TotalSeconds);
    }

    /// <summary>
    /// Sends the verification code by email and drops the in-app "code
    /// sent" trail. Shared by the new-account and unverified-restart paths
    /// (D-198). H10 / H23 — D-065 / D-083: TryEnqueueAsync owns the
    /// failure-audit pattern; D-099: the in-app row is visible after the
    /// user signs in.
    /// </summary>
    private async Task EnqueueVerificationCodeAsync(
        SimfUser user,
        string code,
        CancellationToken cancellationToken)
    {
        await emailQueue.TryEnqueueAsync(
            BuildVerificationEmail(user.Email!, code),
            purpose: "EmailVerification",
            subjectEmail: user.Email!,
            subjectUserId: user.Id,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = user.Id,
            Kind = NotificationKind.CredentialEmailVerificationSent,
            Title = "Verification code sent",
            TitleArabic = "تم إرسال رمز التحقق",
            Body = "An email-verification code was sent to your address.",
            BodyArabic = "تم إرسال رمز التحقق من البريد إلى عنوانك.",
            Severity = NotificationSeverity.Info,
            SendEmail = false,
        }, logger, cancellationToken);
    }

    /// <summary>
    /// Maps a failed account-creation / add-password result to the
    /// bilingual <see cref="DataValidationException"/> the sign-up surfaces
    /// shape their 400 from. Shared by the new-account and restart paths
    /// (D-198).
    /// </summary>
    private static DataValidationException ToAccountCreationException(UserOperationResult result)
    {
        var details = result.Errors
            .Select(error => new ApiErrorDetail
            {
                Field = "password",
                Message = error.Description,
                MessageArabic = IdentityErrorTranslator.ToArabic(error),
            })
            .ToList();
        return new DataValidationException(
            "The account could not be created.",
            "تعذّر إنشاء الحساب.",
            details);
    }

    private async Task<string> IssueVerificationCodeAsync(
        SimfUser user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Invalidate any outstanding code — only the newest one is valid.
        var previous = await accountCodeRepository.GetLatestUnconsumedAsync(
            user.Id, AccountCodePurpose.EmailVerification, cancellationToken);
        if (previous is not null)
        {
            previous.ConsumedAt = now;
            await accountCodeRepository.UpdateAsync(previous, cancellationToken);
        }

        // M3 (security) — store only the keyed hash; return the plaintext to email.
        var plaintext = VerificationCodeGenerator.Generate();
        var code = new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = AccountCodePurpose.EmailVerification,
            Code = AccountCodeHasher.Hash(plaintext),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime),
        };
        await accountCodeRepository.AddAsync(code, cancellationToken);
        return plaintext;
    }

    /// <summary>
    /// H23 — D-083: builds the verification email; caller pairs with
    /// `IEmailQueue.TryEnqueueAsync` which owns the failure audit.
    /// </summary>
    private static EmailMessage BuildVerificationEmail(string email, string code)
    {
        var minutes = (int)CodeLifetime.TotalMinutes;
        var body =
            $"<p>Your SIMF email verification code is <strong>{code}</strong>.</p>" +
            $"<p>The code expires in {minutes} minutes.</p>";
        return new EmailMessage(email, "SIMF email verification", body);
    }

    /// <summary>
    /// D-198 — the heads-up sent to the OWNER of an existing verified
    /// account when someone attempts to sign up again with their email. It
    /// deliberately confirms no account detail; it only points a legitimate
    /// owner at sign-in / password-reset.
    /// </summary>
    private static EmailMessage BuildAccountExistsEmail(string email)
    {
        var body =
            "<p>Someone tried to create a SIMF account with this email address, " +
            "but an account already exists.</p>" +
            "<p>If this was you, please sign in instead — or use the " +
            "&quot;forgot password&quot; option if you don't remember your password.</p>" +
            "<p>If this wasn't you, you can safely ignore this email.</p>";
        return new EmailMessage(email, "SIMF account already exists", body);
    }

    private Task AuditAsync(
        string eventType,
        AuditOutcome outcome,
        string email,
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
