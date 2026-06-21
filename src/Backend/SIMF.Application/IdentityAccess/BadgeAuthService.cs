// Tests: SIMF.Api.Tests/BadgeAuthTests.cs
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.AccessControl.Abstractions;
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
/// Part B — badge-QR sign-in / activation. See <see cref="IBadgeAuthService"/>.
///
/// <para>Security model (owner decision): the badge QR (physical possession) +
/// control of an email inbox are the two factors for setting a first password.
/// When the resolved account already has a real email, the verification code is
/// sent to that on-file address (only its owner can finish); when the account
/// has only a placeholder (a walk-in registered without an email), the holder
/// supplies an email which is verified and attached. The flow never reveals the
/// on-file email except masked, and only an approved account resolves.</para>
/// </summary>
internal sealed class BadgeAuthService(
    IQrResolver qrResolver,
    IUserAccountRepository accounts,
    IAccountCodeRepository accountCodeRepository,
    IEmailQueue emailQueue,
    IAuditLog auditLog,
    ITransactionRunner transactionRunner,
    TimeProvider timeProvider,
    ILogger<BadgeAuthService> logger) : IBadgeAuthService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private const int MaxAttempts = 5;
    private const string PlaceholderEmailSuffix = "@simf.local";

    public async Task<ResolveBadgeResponse> ResolveAsync(
        ResolveBadgeRequest request, CancellationToken cancellationToken = default)
    {
        var user = await ResolveApprovedUserAsync(request.QrId, cancellationToken);
        if (user is null)
        {
            return new ResolveBadgeResponse(false, false, null, false, null);
        }

        if (HasPassword(user))
        {
            // Already has a password — the app routes to the normal sign-in.
            return new ResolveBadgeResponse(true, true, user.DisplayName, false, null);
        }

        var needsEmail = IsPlaceholderEmail(user.Email);
        return new ResolveBadgeResponse(
            true, false, user.DisplayName,
            needsEmail,
            needsEmail ? null : MaskEmail(user.Email));
    }

    public async Task<BadgeActivationStartResponse> StartActivationAsync(
        BadgeActivationStartRequest request, CancellationToken cancellationToken = default)
    {
        var user = await ResolveApprovedUserAsync(request.QrId, cancellationToken)
            ?? throw BadgeNotFound();
        EnsureNotAlreadyActivated(user);

        var now = timeProvider.GetUtcNow();
        string targetEmail;
        string code;

        if (!IsPlaceholderEmail(user.Email))
        {
            // The account already has a real email — send the code there and
            // ignore any client-supplied address (defeats badge-photo takeover).
            targetEmail = user.Email!;
            code = await IssueCodeAsync(user.Id, now, cancellationToken);
        }
        else
        {
            // No real email on file — the holder must supply one to verify + attach.
            var email = (request.Email ?? string.Empty).Trim();
            if (email.Length == 0)
            {
                throw new ApiException(
                    ErrorCodes.AuthAccountNotFound, 400,
                    "An email address is required to activate this account.",
                    "البريد الإلكتروني مطلوب لتفعيل هذا الحساب.");
            }

            var existing = await accounts.FindByEmailAsync(email, cancellationToken);
            if (existing is not null && existing.Id != user.Id)
            {
                throw new ApiException(
                    ErrorCodes.AuthEmailAlreadyRegistered, 409,
                    "That email address is already in use.",
                    "البريد الإلكتروني مستخدم بالفعل.");
            }

            // Attach the (still unconfirmed) email and issue the code atomically.
            var issued = string.Empty;
            await transactionRunner.ExecuteAsync(
                async token =>
                {
                    user.UserName = email;
                    user.Email = email;
                    user.EmailConfirmed = false;
                    user.UpdatedAt = now;
                    await accounts.UpdateAsync(user, token).EnsureSuccessAsync();
                    issued = await IssueCodeAsync(user.Id, now, token);
                },
                cancellationToken);
            targetEmail = email;
            code = issued;
        }

        await emailQueue.TryEnqueueAsync(
            BuildActivationEmail(targetEmail, code),
            "badge-activation", targetEmail, user.Id, auditLog, logger, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BadgeActivationStarted,
            Outcome = AuditOutcome.Success,
            SubjectUserId = user.Id,
            SubjectEmail = targetEmail,
            ActorUserId = user.Id,
        }, cancellationToken);

        return new BadgeActivationStartResponse(
            MaskEmail(targetEmail), (int)CodeLifetime.TotalSeconds);
    }

    public async Task<BadgeActivationCompleteResponse> CompleteActivationAsync(
        BadgeActivationCompleteRequest request, CancellationToken cancellationToken = default)
    {
        var user = await ResolveApprovedUserAsync(request.QrId, cancellationToken)
            ?? throw BadgeNotFound();
        EnsureNotAlreadyActivated(user);

        var now = timeProvider.GetUtcNow();
        var code = await accountCodeRepository.GetLatestUnconsumedAsync(
            user.Id, AccountCodePurpose.BadgeActivationOtp, cancellationToken);

        if (code is null)
        {
            await AuditFailureAsync(user, ErrorCodes.AuthResetCodeInvalid, "no code", cancellationToken);
            throw InvalidCode();
        }
        if (code.AttemptCount >= MaxAttempts)
        {
            await AuditFailureAsync(user, ErrorCodes.AuthResetCodeInvalid, "attempt cap", cancellationToken);
            throw InvalidCode();
        }
        if (now >= code.ExpiresAt)
        {
            await AuditFailureAsync(user, ErrorCodes.AuthResetCodeExpired, "expired", cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthResetCodeExpired, 400,
                "The verification code has expired. Request a new one.",
                "انتهت صلاحية رمز التحقق. اطلب رمزًا جديدًا.");
        }
        if (!CodesMatch(code.Code, AccountCodeHasher.Hash(request.Code)))
        {
            code.AttemptCount++;
            await accountCodeRepository.UpdateAsync(code, cancellationToken);
            await AuditFailureAsync(user, ErrorCodes.AuthResetCodeInvalid,
                $"attempt {code.AttemptCount}", cancellationToken);
            throw InvalidCode();
        }

        await transactionRunner.ExecuteAsync(
            async token =>
            {
                var add = await accounts.AddPasswordAsync(user, request.NewPassword, token);
                if (!add.Succeeded)
                {
                    throw new ApiException(
                        ErrorCodes.AuthPasswordPolicy, 400,
                        "The new password is not allowed: "
                            + string.Join("; ", add.Errors.Select(e => e.Description)),
                        "كلمة المرور الجديدة غير مسموح بها.");
                }
                user.EmailConfirmed = true;
                user.UpdatedAt = now;
                await accounts.UpdateAsync(user, token).EnsureSuccessAsync();
                code.ConsumedAt = now;
                await accountCodeRepository.UpdateAsync(code, token);
            },
            cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BadgeActivationCompleted,
            Outcome = AuditOutcome.Success,
            SubjectUserId = user.Id,
            SubjectEmail = user.Email,
            ActorUserId = user.Id,
        }, cancellationToken);

        logger.LogInformation("Badge activation completed for {UserId}", user.Id);
        return new BadgeActivationCompleteResponse(true);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>Resolves a QR to its owning <see cref="SimfUser"/> only when the
    /// account is Approved; null for unknown / not-approved QRs.</summary>
    private async Task<SimfUser?> ResolveApprovedUserAsync(
        string? qrId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(qrId)) { return null; }
        var resolution = await qrResolver.ResolveAsync(
            qrId.Trim().ToUpperInvariant(), cancellationToken);
        if (resolution is null || resolution.AccountState != AccountState.Approved)
        {
            return null;
        }
        var user = await accounts.FindByIdAsync(resolution.UserId, cancellationToken);
        return user is { AccountState: AccountState.Approved } ? user : null;
    }

    private void EnsureNotAlreadyActivated(SimfUser user)
    {
        if (HasPassword(user))
        {
            throw new ApiException(
                ErrorCodes.BadgeAlreadyActivated, 409,
                "This account already has a password. Sign in with your email and password.",
                "هذا الحساب لديه كلمة مرور بالفعل. سجّل الدخول بالبريد الإلكتروني وكلمة المرور.");
        }
    }

    /// <summary>Consumes any prior unconsumed activation code and issues a fresh
    /// one. Returns the new code's value so the caller can email it.</summary>
    private async Task<string> IssueCodeAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var previous = await accountCodeRepository.GetLatestUnconsumedAsync(
            userId, AccountCodePurpose.BadgeActivationOtp, cancellationToken);
        if (previous is not null)
        {
            previous.ConsumedAt = now;
            await accountCodeRepository.UpdateAsync(previous, cancellationToken);
        }
        var value = VerificationCodeGenerator.Generate();
        await accountCodeRepository.AddAsync(new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = AccountCodePurpose.BadgeActivationOtp,
            // M3 (security) — store the keyed hash; `value` (plaintext) is emailed.
            Code = AccountCodeHasher.Hash(value),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime),
        }, cancellationToken);
        return value;
    }

    private Task AuditFailureAsync(
        SimfUser user, string errorCode, string detail, CancellationToken cancellationToken) =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BadgeActivationFailed,
            Outcome = AuditOutcome.Failure,
            SubjectUserId = user.Id,
            SubjectEmail = user.Email,
            ActorUserId = user.Id,
            ErrorCode = errorCode,
            Detail = detail,
        }, cancellationToken);

    private static bool HasPassword(SimfUser user) =>
        !string.IsNullOrEmpty(user.PasswordHash);

    private static bool IsPlaceholderEmail(string? email) =>
        string.IsNullOrWhiteSpace(email)
        || email.EndsWith(PlaceholderEmailSuffix, StringComparison.OrdinalIgnoreCase);

    private static ApiException BadgeNotFound() =>
        new(ErrorCodes.AuthAccountNotFound, 404,
            "The badge was not recognised.",
            "تعذّر التعرّف على الشارة.");

    private static ApiException InvalidCode() =>
        new(ErrorCodes.AuthResetCodeInvalid, 400,
            "The verification code is not valid.",
            "رمز التحقق غير صالح.");

    private static bool CodesMatch(string stored, string supplied) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored),
            Encoding.UTF8.GetBytes(supplied ?? string.Empty));

    private static EmailMessage BuildActivationEmail(string email, string code)
    {
        var minutes = (int)CodeLifetime.TotalMinutes;
        var body =
            $"<p>Your SIMF account activation code is <strong>{code}</strong>.</p>" +
            $"<p>Enter it in the app to set your password. The code expires in {minutes} minutes.</p>";
        return new EmailMessage(email, "SIMF account activation", body);
    }

    /// <summary>Masks an email for display: <c>khalid@gmail.com</c> →
    /// <c>k****@gmail.com</c> (first char + domain).</summary>
    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) { return string.Empty; }
        var at = email.IndexOf('@');
        if (at <= 0) { return "****"; }
        var local = email[..at];
        var domain = email[at..];
        var head = local[0];
        return $"{head}****{domain}";
    }
}
