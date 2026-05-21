using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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
    TimeProvider timeProvider,
    ILogger<RegistrationService> logger) : IRegistrationService
{
    private const int CodeExpiryMinutes = 10;
    private const int MaxCodeAttempts = 5;

    public async Task<SignUpResponse> SignUpAsync(
        SignUpRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
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
            DisplayName = request.Email,
            AccountState = AccountState.Registered,
            CreatedAt = now,
        };

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
            throw new DataValidationException("The account could not be created.", details);
        }

        var code = await IssueVerificationCodeAsync(user, now, cancellationToken);
        EnqueueVerificationEmail(user.Email!, code);

        logger.LogInformation("Account registered for {Email}", user.Email);
        return new SignUpResponse(user.Email!, CodeExpiryMinutes * 60);
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

        var now = timeProvider.GetUtcNow();
        var code = await accountCodeRepository.GetLatestActiveAsync(
            user.Id, AccountCodePurpose.EmailVerification, now, cancellationToken);

        if (code is null)
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

        if (code.Code != request.Code)
        {
            code.AttemptCount++;
            await accountCodeRepository.UpdateAsync(code, cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthCodeInvalid,
                400,
                "The verification code is not correct.");
        }

        code.ConsumedAt = now;
        await accountCodeRepository.UpdateAsync(code, cancellationToken);

        user.EmailConfirmed = true;
        user.AccountState = AccountState.EmailVerified;
        user.UpdatedAt = now;
        await userManager.UpdateAsync(user);

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

        var now = timeProvider.GetUtcNow();
        var code = await IssueVerificationCodeAsync(user, now, cancellationToken);
        EnqueueVerificationEmail(user.Email!, code);

        return new ResendCodeResponse(user.Email!, CodeExpiryMinutes * 60);
    }

    private async Task<string> IssueVerificationCodeAsync(
        SimfUser user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Invalidate any code still outstanding — only the newest one is valid.
        var previous = await accountCodeRepository.GetLatestActiveAsync(
            user.Id, AccountCodePurpose.EmailVerification, now, cancellationToken);
        if (previous is not null)
        {
            previous.ConsumedAt = now;
            await accountCodeRepository.UpdateAsync(previous, cancellationToken);
        }

        var value = VerificationCodeGenerator.Generate();
        var code = new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = AccountCodePurpose.EmailVerification,
            Code = value,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(CodeExpiryMinutes),
        };
        await accountCodeRepository.AddAsync(code, cancellationToken);
        return value;
    }

    private void EnqueueVerificationEmail(string email, string code)
    {
        var body =
            $"<p>Your SIMF email verification code is <strong>{code}</strong>.</p>" +
            $"<p>The code expires in {CodeExpiryMinutes} minutes.</p>";
        emailQueue.Enqueue(new EmailMessage(email, "SIMF email verification", body));
    }
}
