using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Implements account creation — sign-up, email verification and code resend
/// (SIMF-API-001 section 12.4, SIMF-FDS-001). It takes a self-registered
/// account as far as <see cref="AccountState.EmailVerified"/>; the registration
/// profile and the approval workflow belong to SIMF-FDS-002.
/// </summary>
public sealed class RegistrationService(
    UserManager<SimfUser> userManager,
    IAccountCodeRepository accountCodeRepository,
    IEmailQueue emailQueue,
    ITransactionRunner transactionRunner,
    TimeProvider timeProvider,
    ILogger<RegistrationService> logger) : IRegistrationService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private const int MaxCodeAttempts = 5;

    public async Task<SignUpResponse> SignUpAsync(
        SignUpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            throw new ApiException(
                ErrorCodes.AuthEmailAlreadyRegistered,
                409,
                "An account with this email address already exists.");
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
                var createResult = await userManager.CreateAsync(user, request.Password);
                if (!createResult.Succeeded)
                {
                    var details = createResult.Errors
                        .Select(error => new ApiErrorDetail
                        {
                            Field = "password",
                            Message = error.Description,
                        })
                        .ToList();
                    throw new DataValidationException(
                        "The account could not be created.", details);
                }

                issuedCode = await IssueVerificationCodeAsync(user, now, token);
            },
            cancellationToken);

        EnqueueVerificationEmail(user.Email!, issuedCode!.Code);
        logger.LogInformation("Account registered for {Email}", user.Email);
        return new SignUpResponse(user.Email!, (int)CodeLifetime.TotalSeconds);
    }

    public async Task<VerifyEmailResponse> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            throw new ApiException(
                ErrorCodes.AuthAccountNotFound,
                404,
                "No account was found for this email address.");
        }

        if (user.AccountState != AccountState.Registered)
        {
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "This account's email address is already verified.");
        }

        var now = timeProvider.GetUtcNow();
        var code = await accountCodeRepository.GetLatestUnconsumedAsync(
            user.Id, AccountCodePurpose.EmailVerification, cancellationToken);

        if (code is null)
        {
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "No verification code is outstanding. Request a new one.");
        }

        if (now >= code.ExpiresAt)
        {
            throw new ApiException(
                ErrorCodes.AuthCodeExpired,
                400,
                "The verification code has expired. Request a new one.");
        }

        if (code.AttemptCount >= MaxCodeAttempts)
        {
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "Too many incorrect attempts. Request a new code.");
        }

        if (!CodesMatch(code.Code, request.Code))
        {
            code.AttemptCount++;
            await accountCodeRepository.UpdateAsync(code, cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "The verification code is not correct.");
        }

        await transactionRunner.ExecuteAsync(
            async token =>
            {
                code.ConsumedAt = now;
                await accountCodeRepository.UpdateAsync(code, token);

                user.EmailConfirmed = true;
                user.AccountState = AccountState.EmailVerified;
                user.UpdatedAt = now;
                await userManager.UpdateAsync(user);
            },
            cancellationToken);

        logger.LogInformation("Email verified for {Email}", user.Email);
        return new VerifyEmailResponse(user.Email!, true);
    }

    public async Task<ResendCodeResponse> ResendCodeAsync(
        ResendCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            throw new ApiException(
                ErrorCodes.AuthAccountNotFound,
                404,
                "No account was found for this email address.");
        }

        if (user.AccountState != AccountState.Registered)
        {
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "This account's email address is already verified.");
        }

        var now = timeProvider.GetUtcNow();
        var code = await IssueVerificationCodeAsync(user, now, cancellationToken);
        EnqueueVerificationEmail(user.Email!, code.Code);
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

    private void EnqueueVerificationEmail(string email, string code)
    {
        var minutes = (int)CodeLifetime.TotalMinutes;
        var body =
            $"<p>Your SIMF email verification code is <strong>{code}</strong>.</p>" +
            $"<p>The code expires in {minutes} minutes.</p>";
        emailQueue.Enqueue(new EmailMessage(email, "SIMF email verification", body));
    }

    /// <summary>Compares the codes in constant time, so no timing side channel leaks.</summary>
    private static bool CodesMatch(string stored, string supplied) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored),
            Encoding.UTF8.GetBytes(supplied));
}
