// Tests: SIMF.Api.Tests/BadgeAuthTests.cs
// Tests: SIMF.Api.Tests/BadgeSelfClaimProfileTests.cs (#10 phase 4 — the
//        placeholder profile is filled from the capture step at complete)
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Badges;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;

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
    ISignInService signInService,
    IAccountCodeRepository accountCodeRepository,
    IUserProfileRepository profiles,
    IEmailQueue emailQueue,
    IEmailTemplateResolver emailTemplates,
    IAuditLog auditLog,
    ITransactionRunner transactionRunner,
    TimeProvider timeProvider,
    // D-819 — gates badge activation for walk-in-minted accounts.
    IOptionsMonitor<WalkInModeOptions> walkInMode,
    ILogger<BadgeAuthService> logger) : IBadgeAuthService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private const int MaxAttempts = 5;
    private const string PlaceholderEmailSuffix = "@simf.local";

    // D-819 — the local-part the walk-in desk synthesizes when it registers
    // someone with no email (AdminAccountService.RegisterOnSiteAsync). Kept
    // distinct from the bulk-badge "badge-" prefix so the activation block
    // targets walk-ins without changing the shipped bulk-badge flow.
    private const string WalkInEmailPrefix = "walkin-";

    // Verify-then-attach: the holder-supplied email is stashed on the account's
    // Identity token store (AspNetUserTokens) at the start step and promoted to the
    // real account email only AFTER the code is verified at complete — the account
    // email is never rebound before verification. Mirrors the pending-secret store
    // in TotpEnrollmentService; the "[SIMF]" provider is the SIMF-owned token
    // namespace and the distinct token name never collides with the TOTP secret.
    private const string ActivationTokenProvider = "[SIMF]";
    private const string PendingEmailTokenName = "PendingActivationEmail";

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
            // Already has a password — the app routes to the normal sign-in and
            // shows the holder's name + masked on-file email on the password
            // step. A placeholder / @simf.local account keeps a null email.
            var maskedEmail = IsPlaceholderEmail(user.Email) ? null : MaskEmail(user.Email);
            return new ResolveBadgeResponse(true, true, user.DisplayName, false, maskedEmail);
        }

        var needsEmail = IsPlaceholderEmail(user.Email);
        return new ResolveBadgeResponse(
            true, false, user.DisplayName,
            needsEmail,
            needsEmail ? null : MaskEmail(user.Email));
    }

    public async Task<SignInResponse> SignInAsync(
        BadgeSignInRequest request, CancellationToken cancellationToken = default)
    {
        var user = await ResolveApprovedUserAsync(request.QrId, cancellationToken);
        if (user is null)
        {
            // Unknown / not-approved QR — write the same failed-sign-in audit the
            // password path writes for bad credentials, then throw the same
            // generic invalid-credentials error, so an unknown badge is
            // indistinguishable from a wrong password (the public badge is never
            // a valid-QR oracle and never bypasses the password).
            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.SignInBadCredentials,
                Outcome = AuditOutcome.Failure,
                ErrorCode = ErrorCodes.AuthInvalidCredentials,
                Detail = "badge",
            }, cancellationToken);
            throw new ApiException(ErrorCodes.AuthInvalidCredentials, 401,
                "The email address or password is not correct.",
                "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
        }

        // Delegate to the real sign-in pipeline (password + 2FA + lockout +
        // account-state). A resolved-but-passwordless account is NOT special-
        // cased: it passes straight through and fails as generic-invalid because
        // CheckPasswordAsync returns false with no password hash — so the badge
        // can never substitute for the password.
        return await signInService.SignInAsync(
            new SignInRequest
            {
                Email = user.Email!,
                Password = request.Password,
                Audience = SignInAudience.App,
            },
            cancellationToken);
    }

    public async Task<BadgeActivationStartResponse> StartActivationAsync(
        BadgeActivationStartRequest request, CancellationToken cancellationToken = default)
    {
        var user = await ResolveApprovedUserAsync(request.QrId, cancellationToken)
            ?? throw BadgeNotFound();
        EnsureNotAlreadyActivated(user);

        var now = timeProvider.SimfNow();
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
            // D-819 — a walk-in badge grants PHYSICAL ACCESS ONLY by default.
            //
            // This branch lets the QR holder nominate the address the code is
            // sent to, which is safe for a controlled bulk-badge batch but not
            // for walk-in badges in open circulation: anyone who photographs one
            // across a room could claim it as a full app account (sign-in,
            // networking, contacts, meeting requests). Refused with the SAME
            // "badge not recognised" an unknown QR returns, so this is not an
            // oracle for which badges exist.
            //
            // If a walk-in should get app access, a staffed desk attaches a real
            // email after an ID check — which routes to the branch above, where
            // the code goes to the verified owner and cannot be redirected.
            if (IsWalkInPlaceholderEmail(user.Email)
                && !walkInMode.CurrentValue.BadgeActivationAllowedForWalkIns)
            {
                throw BadgeNotFound();
            }

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

            // SECURITY (verify-then-attach): stash the supplied email as PENDING and
            // do NOT rebind user.Email yet. The account stays a placeholder
            // (IsPlaceholderEmail == true) until the code is verified at the complete
            // step, so a mistyped or hostile email can never brick or pre-empt the
            // badge's self-activation — a retry with a corrected email simply
            // overwrites the pending value. The email is promoted to the account only
            // after CompleteActivationAsync verifies the code.
            await accounts.SetAuthenticationTokenAsync(
                    user, ActivationTokenProvider, PendingEmailTokenName, email, cancellationToken)
                .EnsureSuccessAsync();
            code = await IssueCodeAsync(user.Id, now, cancellationToken);
            targetEmail = email;
        }

        await emailQueue.TryEnqueueAsync(
            await BuildActivationEmailAsync(targetEmail, code, cancellationToken),
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

        var now = timeProvider.SimfNow();
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

        // Verify-then-attach: a placeholder account attaches the email it stashed at
        // the start step only now that the code is proven. Resolve + re-check it up
        // front so a bad state fails cleanly before any write.
        string? pendingEmail = null;
        if (IsPlaceholderEmail(user.Email))
        {
            pendingEmail = await accounts.GetAuthenticationTokenAsync(
                user, ActivationTokenProvider, PendingEmailTokenName, cancellationToken);
            if (string.IsNullOrWhiteSpace(pendingEmail))
            {
                // No email was stashed (activation was never started, or the stash
                // was cleared) — fail closed and ask the holder to start again.
                await AuditFailureAsync(user, ErrorCodes.AuthAccountNotFound, "no pending email", cancellationToken);
                throw new ApiException(
                    ErrorCodes.AuthAccountNotFound, 400,
                    "An email address is required to activate this account.",
                    "البريد الإلكتروني مطلوب لتفعيل هذا الحساب.");
            }

            // Re-check uniqueness at attach time (guards a race where the email was
            // claimed by another account between start and complete).
            var existing = await accounts.FindByEmailAsync(pendingEmail, cancellationToken);
            if (existing is not null && existing.Id != user.Id)
            {
                throw new ApiException(
                    ErrorCodes.AuthEmailAlreadyRegistered, 409,
                    "That email address is already in use.",
                    "البريد الإلكتروني مستخدم بالفعل.");
            }
        }

        // #10 phase 4 — resolve + validate the captured profile fields against the
        // live App-DB lookups BEFORE any write, exactly like the pending-email
        // re-check above, so a bad country code or a deactivated interest fails
        // cleanly instead of half-activating the badge.
        var nationalityId = await ResolveNationalityIdAsync(
            request.NationalityCode, cancellationToken);
        var interestIds = await ResolveInterestIdsAsync(
            request.InterestIds, cancellationToken);

        // #10 phase 4 — fill the placeholder profile FIRST, in its own App-DB unit of
        // work (D-157: no transaction spans the two databases). Ordering matters: if
        // the password step below then fails (a policy rejection, an email race), the
        // badge is still unactivated and the holder simply retries — the profile write
        // is idempotent and the retry overwrites it. The reverse order would leave an
        // activated account whose retry is refused by EnsureNotAlreadyActivated, with
        // the placeholder name never filled.
        var realName = FirstNonBlank(request.EnglishName, request.ArabicName);
        await FillPlaceholderProfileAsync(
            user.Id, request, nationalityId, interestIds, now, cancellationToken);

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
                if (pendingEmail is not null)
                {
                    // Promote the verified pending email to the account's real login
                    // identity, then drop the stash.
                    user.UserName = pendingEmail;
                    user.Email = pendingEmail;
                    await accounts.RemoveAuthenticationTokenAsync(
                            user, ActivationTokenProvider, PendingEmailTokenName, token)
                        .EnsureSuccessAsync();
                }
                // #10 phase 4 — a bulk-generated badge account carries a GENERATED
                // display name ("VIP #3"). The holder has now identified themselves,
                // so promote the captured name to the account's display name too —
                // otherwise the app greets them by the placeholder forever.
                if (realName is not null)
                {
                    user.DisplayName = realName;
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

    /// <summary>#10 phase 4 — resolves the wire ISO country code to the
    /// <c>Country</c> PK, or null when the caller supplied none. An unknown /
    /// inactive code is a 400, matching the profile upsert.</summary>
    private async Task<int?> ResolveNationalityIdAsync(
        string? nationalityCode, CancellationToken cancellationToken)
    {
        var code = (nationalityCode ?? string.Empty).Trim();
        if (code.Length == 0) { return null; }

        return await profiles.ResolveCountryIdAsync(code, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ProfileNationalityUnknown, 400,
                $"Nationality code '{code}' is not supported.",
                $"الجنسية '{code}' غير مدعومة.");
    }

    /// <summary>#10 phase 4 — the distinct picked interest ids, after checking every
    /// one exists and is active. Empty when the caller picked none.</summary>
    private async Task<IReadOnlyList<Guid>> ResolveInterestIdsAsync(
        IReadOnlyCollection<Guid>? requested, CancellationToken cancellationToken)
    {
        var ids = (requested ?? []).Distinct().ToList();
        if (ids.Count == 0) { return []; }

        var active = await profiles.FilterActiveInterestIdsAsync(ids, cancellationToken);
        if (active.Count != ids.Count)
        {
            throw new ApiException(
                ErrorCodes.InterestInvalid, 400,
                "One or more selected interests are unknown or no longer active.",
                "بعض الاهتمامات المختارة غير معروفة أو لم تعد مفعّلة.");
        }
        return ids;
    }

    /// <summary>
    /// #10 phase 4 — writes the captured profile fields onto the badge's placeholder
    /// <c>UserProfile</c> (App DB). Every field is optional: a blank one leaves the
    /// existing value alone, so a client that sends nothing behaves exactly as it did
    /// before. Creates the row when the badge has none (a badge minted without a
    /// profile stub). Interests are ADDED, never removed — the holder edits the full
    /// set later on the profile screen.
    /// </summary>
    private async Task FillPlaceholderProfileAsync(
        Guid userId,
        BadgeActivationCompleteRequest request,
        int? nationalityId,
        IReadOnlyList<Guid> interestIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var englishName = FirstNonBlank(request.EnglishName);
        var arabicName = FirstNonBlank(request.ArabicName);
        if (englishName is null && arabicName is null
            && nationalityId is null && interestIds.Count == 0)
        {
            // Nothing captured — do not touch the row at all.
            return;
        }

        var profile = await profiles.GetWithInterestsAsync(userId, tracked: true, cancellationToken);
        if (profile is null)
        {
            profile = new UserProfile { UserId = userId, CreatedAt = now };
            profiles.Add(profile);
        }
        else
        {
            profile.UpdatedAt = now;
        }

        if (englishName is not null) { profile.Name = englishName; }
        if (arabicName is not null) { profile.NameArabic = arabicName; }
        if (nationalityId is { } countryId) { profile.NationalityId = countryId; }

        if (interestIds.Count > 0)
        {
            var alreadyPicked = profile.Interests.Select(interest => interest.Id).ToHashSet();
            var toAdd = interestIds.Where(id => !alreadyPicked.Contains(id)).ToList();
            if (toAdd.Count > 0)
            {
                foreach (var row in await profiles.GetInterestsByIdsAsync(toAdd, cancellationToken))
                {
                    profile.Interests.Add(row);
                }
            }
        }

        await profiles.SaveAppChangesAsync(cancellationToken);
    }

    /// <summary>The first of the supplied values that is not null/blank, trimmed;
    /// null when they all are.</summary>
    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) { return value.Trim(); }
        }
        return null;
    }

    /// <summary>Resolves a QR to its owning <see cref="SimfUser"/> only when the
    /// account is Approved; null for unknown / not-approved QRs.</summary>
    private async Task<SimfUser?> ResolveApprovedUserAsync(
        string? qrId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(qrId)) { return null; }

        // D-821 review — an OFFLINE badge id is never resolvable here.
        //
        // Every endpoint on this service is AllowAnonymous, and their safety
        // rests on a scanned QR being unguessable: a minted QrId is 12 random
        // Crockford characters (~59 bits). A D-820 offline id is NOT — it is
        // 'W' plus a desk sequence, so the live ids at an event are a few
        // thousand consecutive numbers. Left resolvable, this turns
        // resolve-badge into an anonymous roster oracle that returns the
        // holder's display name for any guess.
        //
        // Refusing costs nothing: a walk-in badge is physical access only, and
        // badge activation is already blocked for these accounts further down.
        // The bearer of a real offline badge presents the ENCRYPTED blob, which
        // is unguessable and is what the gate reads.
        if (OfflineBadgeId.IsOfflineBadge(qrId.Trim().ToUpperInvariant()))
        {
            return null;
        }

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
    private async Task<string> IssueCodeAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
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

    /// <summary>
    /// D-819 — true for an account minted by the WALK-IN desk with no real email
    /// (<c>walkin-{guid}@simf.local</c>). Deliberately narrower than
    /// <see cref="IsPlaceholderEmail"/>: the pre-existing bulk-badge path
    /// (<c>badge-{guid}@…</c>) keeps its shipped activation behaviour, because
    /// those badges are handed out deliberately from a controlled batch.
    /// </summary>
    private static bool IsWalkInPlaceholderEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.EndsWith(PlaceholderEmailSuffix, StringComparison.OrdinalIgnoreCase)
        && email.StartsWith(WalkInEmailPrefix, StringComparison.OrdinalIgnoreCase);

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

    private Task<EmailMessage> BuildActivationEmailAsync(
        string email, string code, CancellationToken cancellationToken) =>
        emailTemplates.RenderAsync(
            EmailTemplateType.BadgeActivation, email,
            EmailTokens.ForCode(code, CodeLifetime), cancellationToken);

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
