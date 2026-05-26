using System.Security.Cryptography;
using System.Text;
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
    ITransactionRunner transactionRunner,
    IAuditLog auditLog,
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
        if (await accounts.FindByEmailAsync(request.Email) is not null)
        {
            await AuditAsync(
                AuditEvents.SignUpDuplicateEmail, AuditOutcome.Failure, request.Email,
                errorCode: ErrorCodes.AuthEmailAlreadyRegistered, cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthEmailAlreadyRegistered,
                409,
                "An account with this email address already exists.",
                "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new SimfUser
        {
            UserName = request.Email,
            Email = request.Email,
            // TODO(SIMF-FDS-002): replaced by the real name at profile completion.
            DisplayName = request.Email,
            AccountState = AccountState.Registered,
            CreatedAt = now,
        };

        AccountCode? issuedCode = null;

        // The user row and its first verification code must commit together —
        // otherwise a failure leaves an account that can never be verified.
        await transactionRunner.ExecuteAsync(
            async token =>
            {
                var createResult = await accounts.CreateAsync(user, request.Password);
                if (!createResult.Succeeded)
                {
                    var details = createResult.Errors
                        .Select(error => new ApiErrorDetail
                        {
                            Field = "password",
                            Message = error.Description,
                            MessageArabic = IdentityErrorTranslator.ToArabic(error),
                        })
                        .ToList();
                    throw new DataValidationException(
                        "The account could not be created.",
                        "تعذّر إنشاء الحساب.",
                        details);
                }

                issuedCode = await IssueVerificationCodeAsync(user, now, token);
            },
            cancellationToken);

        // H10 / H23 — D-065 / D-083: TryEnqueueAsync owns the failure-
        // audit pattern. The user row + code are committed in the TX
        // above; this dispatch is the side-effect on a different scope.
        await emailQueue.TryEnqueueAsync(
            BuildVerificationEmail(user.Email!, issuedCode!.Code),
            purpose: "EmailVerification",
            subjectEmail: user.Email!,
            subjectUserId: user.Id,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);
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

        if (!CodesMatch(code.Code, request.Code))
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

        var code = await IssueVerificationCodeAsync(user, now, cancellationToken);
        // H10 / H23 — D-065 / D-083: same shape as sign-up; helper owns
        // the failure-audit pattern.
        await emailQueue.TryEnqueueAsync(
            BuildVerificationEmail(user.Email!, code.Code),
            purpose: "ResendVerification",
            subjectEmail: user.Email!,
            subjectUserId: user.Id,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);
        await AuditAsync(
            AuditEvents.ResendCodeIssued, AuditOutcome.Success, user.Email!,
            userId: user.Id, cancellationToken: cancellationToken);
        logger.LogInformation("Verification code re-issued for {Email}", user.Email);
        return new ResendCodeResponse(user.Email!, (int)CodeLifetime.TotalSeconds);
    }

    private async Task<AccountCode> IssueVerificationCodeAsync(
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

        var code = new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = AccountCodePurpose.EmailVerification,
            Code = VerificationCodeGenerator.Generate(),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime),
        };
        await accountCodeRepository.AddAsync(code, cancellationToken);
        return code;
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
