// Tests: SIMF.Api.Tests/BadgeAuthTests.cs
// Tests: SIMF.Api.Tests/BadgeSelfClaimProfileTests.cs (the placeholder
//        profile is filled from the capture step at complete)
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Security;
using SIMF.Common;
using SIMF.Common.Badges;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.Badges;
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
    // Gates badge activation for walk-in-minted accounts.
    IOptionsMonitor<WalkInModeOptions> walkInMode,
    ILogger<BadgeAuthService> logger) : IBadgeAuthService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private const int MaxAttempts = 5;
    private const string PlaceholderEmailSuffix = "@simf.local";

    // The local-part the walk-in desk synthesizes when it registers
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
        var holder = await ResolveApprovedHolderAsync(request.QrId, cancellationToken);
        if (holder is null)
        {
            return new ResolveBadgeResponse(false, false, null, false, null);
        }

        // No account yet — the app asks for an email and activation creates one.
        // Deliberately the SAME shape a placeholder walk-in already returned, so
        // this reveals nothing a badge could not already tell an anonymous
        // caller. Whether the holder is ALLOWED to claim it is decided at the
        // start step, again matching the placeholder path.
        if (holder.Account is null)
        {
            return new ResolveBadgeResponse(
                true, false, holder.ProfileName, true, null);
        }

        var user = holder.Account;
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
        // An attendee with no account has nothing to sign in as, and falls into
        // the same generic refusal below rather than a distinguishable one.
        var user = (await ResolveApprovedHolderAsync(request.QrId, cancellationToken))?.Account;
        if (user is null)
        {
            // Unknown / not-approved QR — write the same failed-sign-in audit the
            // password path writes for bad credentials, then throw the same
            // generic invalid-credentials error, so an unknown badge is
            // indistinguishable from a wrong password (the public badge is never
            // a valid-QR oracle and never bypasses the password).
            await auditLog.WriteFailureAsync(
                AuditEvents.SignInBadCredentials,
                null,
                errorCode: ErrorCodes.AuthInvalidCredentials,
                detail: "badge",
                cancellationToken: cancellationToken);
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
        var holder = await ResolveApprovedHolderAsync(request.QrId, cancellationToken)
            ?? throw BadgeNotFound();

        var now = timeProvider.SimfNow();

        // No account yet: verify an address the holder nominates, and create the
        // account for them at the complete step. Same two factors as every other
        // branch here - the badge in their hand, and control of an inbox.
        if (holder.Account is null)
        {
            return await StartAccountCreationAsync(holder, request, now, cancellationToken);
        }

        var user = holder.Account;
        EnsureNotAlreadyActivated(user);

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
            // A walk-in badge grants PHYSICAL ACCESS ONLY by default.
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

    /// <summary>Start of the badge-to-account flow for an attendee who holds no
    /// account: pin the address the code is sent to, and email the code.
    ///
    /// <para>The address is pinned ON THE CODE ROW rather than stashed against an
    /// account, because there is no account to stash it on yet. That is what keeps
    /// verify-then-attach honest here: the completing request carries the code and
    /// never the address, so holding a code cannot bind an address the code was
    /// never sent to. A retry simply issues a newer code with a newer
    /// address.</para></summary>
    private async Task<BadgeActivationStartResponse> StartAccountCreationAsync(
        BadgeHolder holder,
        BadgeActivationStartRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!MaySelfActivateWithoutAccount(holder))
        {
            throw BadgeNotFound();
        }

        var email = (request.Email ?? string.Empty).Trim();
        if (email.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 400,
                "An email address is required to activate this account.",
                "البريد الإلكتروني مطلوب لتفعيل هذا الحساب.");
        }

        // Refuse an address that already signs someone in - but not one left
        // behind by this same flow failing between the two databases, or the
        // holder would be stuck behind a 409 forever with no way to finish. The
        // complete step re-checks and takes that account over.
        if (await accounts.FindByEmailAsync(email, cancellationToken) is { } existing
            && await profiles.FindAsync(existing.Id, cancellationToken) is not null)
        {
            throw new ApiException(
                ErrorCodes.AuthEmailAlreadyRegistered, 409,
                "That email address is already in use.",
                "البريد الإلكتروني مستخدم بالفعل.");
        }

        var code = await IssueProfileCodeAsync(holder.ProfileId, email, now, cancellationToken);

        await emailQueue.TryEnqueueAsync(
            await BuildActivationEmailAsync(email, code, cancellationToken),
            "badge-activation", email, null, auditLog, logger, cancellationToken);

        // No SubjectUserId: there is no account to name yet, and a profile id in
        // a column that means "account" would make the audit trail lie.
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BadgeActivationStarted,
            Outcome = AuditOutcome.Success,
            SubjectEmail = email,
            Detail = $"account creation; profileId={holder.ProfileId}",
        }, cancellationToken);

        return new BadgeActivationStartResponse(
            MaskEmail(email), (int)CodeLifetime.TotalSeconds);
    }

    public async Task<BadgeActivationCompleteResponse> CompleteActivationAsync(
        BadgeActivationCompleteRequest request, CancellationToken cancellationToken = default)
    {
        var holder = await ResolveApprovedHolderAsync(request.QrId, cancellationToken)
            ?? throw BadgeNotFound();

        if (holder.Account is null)
        {
            return await CompleteAccountCreationAsync(holder, request, cancellationToken);
        }

        var user = holder.Account;
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
            // Atomic increment, decided on the returned count. A read-modify-write
            // here lets concurrent wrong tries each read 0 and write 1, so the cap
            // never trips and a 6-digit code becomes brute-forceable — which for
            // badge activation means control of someone else's badge account.
            var attempts = await accountCodeRepository.IncrementAttemptCountAsync(
                code.Id, cancellationToken);
            if (attempts >= MaxAttempts)
            {
                await accountCodeRepository.TryConsumeAsync(code.Id, now, cancellationToken);
            }
            await AuditFailureAsync(user, ErrorCodes.AuthResetCodeInvalid,
                $"attempt {attempts}", cancellationToken);
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

        // Resolve + validate the captured profile fields against the
        // live App-DB lookups BEFORE any write, exactly like the pending-email
        // re-check above, so a bad country code or a deactivated interest fails
        // cleanly instead of half-activating the badge.
        var nationalityId = await ResolveNationalityIdAsync(
            request.NationalityCode, cancellationToken);
        var interestIds = await ResolveInterestIdsAsync(
            request.InterestIds, cancellationToken);

        // Fill the placeholder profile FIRST, in its own App-DB unit of
        // work (no transaction spans the two databases). Ordering matters: if
        // the password step below then fails (a policy rejection, an email race), the
        // badge is still unactivated and the holder simply retries — the profile write
        // is idempotent and the retry overwrites it. The reverse order would leave an
        // activated account whose retry is refused by EnsureNotAlreadyActivated, with
        // the placeholder name never filled.
        var realName = FirstNonBlank(request.EnglishName, request.ArabicName);
        await FillPlaceholderProfileAsync(
            user.Id, request, nationalityId, interestIds, now, cancellationToken);

        var consumed = false;
        await transactionRunner.ExecuteAsync(
            async token =>
            {
                // Consume first: the conditional UPDATE is the gate that makes the
                // code single-use. Two concurrent activations carrying one valid OTP
                // both passed the unconsumed read above, and only the caller that
                // flips ConsumedAt may attach an email and display name to the
                // placeholder account — otherwise the second holder's details
                // overwrite the first's. It runs inside the transaction, so a
                // rejected password rolls the consumption back and a retry works.
                consumed = await accountCodeRepository.TryConsumeAsync(code.Id, now, token);
                if (!consumed)
                {
                    return;
                }
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
                // A bulk-generated badge account carries a GENERATED
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
            },
            cancellationToken);

        if (!consumed)
        {
            await AuditFailureAsync(user, ErrorCodes.AuthResetCodeInvalid,
                "already_consumed", cancellationToken);
            throw InvalidCode();
        }

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

    /// <summary>Completion of the badge-to-account flow for an attendee who held
    /// no account: prove the code, then create the account and link it to them.
    ///
    /// <para>The address comes off the CODE ROW, never off the request. That is
    /// the whole point of pinning it at the start step - otherwise whoever holds
    /// a code could bind an address the code was never sent to, and the emailed
    /// code would stop being evidence of anything.</para></summary>
    private async Task<BadgeActivationCompleteResponse> CompleteAccountCreationAsync(
        BadgeHolder holder,
        BadgeActivationCompleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!MaySelfActivateWithoutAccount(holder))
        {
            throw BadgeNotFound();
        }

        var now = timeProvider.SimfNow();
        var code = await accountCodeRepository.GetLatestUnconsumedForProfileAsync(
            holder.ProfileId, AccountCodePurpose.BadgeActivationOtp, cancellationToken);

        if (code is null)
        {
            await AuditProfileFailureAsync(holder, ErrorCodes.AuthResetCodeInvalid, "no code", cancellationToken);
            throw InvalidCode();
        }
        if (code.AttemptCount >= MaxAttempts)
        {
            await AuditProfileFailureAsync(holder, ErrorCodes.AuthResetCodeInvalid, "attempt cap", cancellationToken);
            throw InvalidCode();
        }
        if (now >= code.ExpiresAt)
        {
            await AuditProfileFailureAsync(holder, ErrorCodes.AuthResetCodeExpired, "expired", cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthResetCodeExpired, 400,
                "The verification code has expired. Request a new one.",
                "انتهت صلاحية رمز التحقق. اطلب رمزًا جديدًا.");
        }
        if (!CodesMatch(code.Code, AccountCodeHasher.Hash(request.Code)))
        {
            code.AttemptCount++;
            await accountCodeRepository.UpdateAsync(code, cancellationToken);
            await AuditProfileFailureAsync(holder, ErrorCodes.AuthResetCodeInvalid,
                $"attempt {code.AttemptCount}", cancellationToken);
            throw InvalidCode();
        }

        var email = code.PendingEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            // A code for this flow is always issued with the address it was sent
            // to. Without one there is nothing to attach, so fail closed rather
            // than fall back to anything the request supplied.
            await AuditProfileFailureAsync(
                holder, ErrorCodes.AuthAccountNotFound, "no pinned email", cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 400,
                "An email address is required to activate this account.",
                "البريد الإلكتروني مطلوب لتفعيل هذا الحساب.");
        }

        // Resolve the captured profile fields against the live lookups BEFORE any
        // write, so a bad country code fails cleanly rather than half-way.
        var nationalityId = await ResolveNationalityIdAsync(
            request.NationalityCode, cancellationToken);
        var interestIds = await ResolveInterestIdsAsync(
            request.InterestIds, cancellationToken);
        var realName = FirstNonBlank(request.EnglishName, request.ArabicName);

        var user = await CreateOrAdoptAccountAsync(
            email, realName ?? holder.ProfileName, request.NewPassword, now, cancellationToken);

        // Link + fill in ONE App-DB write. The account already exists at this
        // point and the two databases have no shared transaction, so this is the
        // window that can fail: it leaves an account nobody points at, which the
        // adoption path above takes back on the holder's next attempt. Doing the
        // link and the profile fill together is what stops a retry being refused
        // with the captured details never written.
        var profile = await profiles.GetByProfileIdWithInterestsAsync(
            holder.ProfileId, cancellationToken);
        if (profile is null)
        {
            throw BadgeNotFound();
        }
        if (profile.UserId is not null)
        {
            // Another completion won the race and linked it first. Fail closed
            // rather than move the profile onto a second account - the filtered
            // unique index would refuse it anyway, less legibly.
            throw new ApiException(
                ErrorCodes.BadgeAlreadyActivated, 409,
                "This badge already has an account. Sign in with your email and password.",
                "هذه الشارة لديها حساب بالفعل. سجّل الدخول بالبريد الإلكتروني وكلمة المرور.");
        }
        profile.UserId = user.Id;
        if (FirstNonBlank(request.EnglishName) is { } englishName) { profile.Name = englishName; }
        if (FirstNonBlank(request.ArabicName) is { } arabicName) { profile.NameArabic = arabicName; }
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
        profile.UpdatedAt = now;
        await profiles.SaveAppChangesAsync(cancellationToken);

        code.ConsumedAt = now;
        await accountCodeRepository.UpdateAsync(code, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BadgeActivationCompleted,
            Outcome = AuditOutcome.Success,
            SubjectUserId = user.Id,
            SubjectEmail = user.Email,
            ActorUserId = user.Id,
            Detail = $"account created for profileId={holder.ProfileId}",
        }, cancellationToken);

        logger.LogInformation(
            "Badge activation created account {UserId} for attendee {ProfileId}",
            user.Id, holder.ProfileId);
        return new BadgeActivationCompleteResponse(true);
    }

    /// <summary>The account for a verified address: a new one, or the one left
    /// behind by an attempt that created it and then failed before linking it.
    ///
    /// <para>Adoption is safe because it is gated on the address having been
    /// PROVEN by the code just verified, and on no attendee pointing at the
    /// account. The password is replaced rather than kept, so an account that
    /// reached this state by any route other than an interrupted retry cannot
    /// carry a password its new owner did not choose.</para></summary>
    private async Task<SimfUser> CreateOrAdoptAccountAsync(
        string email, string displayName, string password,
        DateTime now, CancellationToken cancellationToken)
    {
        var existing = await accounts.FindByEmailAsync(email, cancellationToken);
        if (existing is null)
        {
            // The field-set a badge holder's account has always had, from the
            // bulk mint: approved, confirmed, a visitor, no forced change.
            var created = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
                PasswordChangeRequired = false,
                CreatedAt = now,
                StateChangedAt = now,
            };
            var result = await accounts.CreateAsync(created, password, cancellationToken);
            if (!result.Succeeded)
            {
                throw new ApiException(
                    ErrorCodes.AuthPasswordPolicy, 400,
                    "The new password is not allowed: "
                        + string.Join("; ", result.Errors.Select(e => e.Description)),
                    "كلمة المرور الجديدة غير مسموح بها.");
            }
            return created;
        }

        var claimedBy = await profiles.FindAsync(existing.Id, cancellationToken);
        if (claimedBy is not null)
        {
            throw new ApiException(
                ErrorCodes.AuthEmailAlreadyRegistered, 409,
                "That email address is already in use.",
                "البريد الإلكتروني مستخدم بالفعل.");
        }

        await accounts.RemovePasswordAsync(existing, cancellationToken).EnsureSuccessAsync();
        var added = await accounts.AddPasswordAsync(existing, password, cancellationToken);
        if (!added.Succeeded)
        {
            throw new ApiException(
                ErrorCodes.AuthPasswordPolicy, 400,
                "The new password is not allowed: "
                    + string.Join("; ", added.Errors.Select(e => e.Description)),
                "كلمة المرور الجديدة غير مسموح بها.");
        }
        existing.DisplayName = displayName;
        existing.EmailConfirmed = true;
        existing.AccountState = AccountState.Approved;
        existing.UpdatedAt = now;
        await accounts.UpdateAsync(existing, cancellationToken).EnsureSuccessAsync();
        return existing;
    }

    /// <summary>The failure audit for the pre-account flow. Carries the attendee
    /// in the detail rather than putting a profile id in a column that means
    /// "account".</summary>
    private Task AuditProfileFailureAsync(
        BadgeHolder holder, string errorCode, string detail, CancellationToken cancellationToken) =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BadgeActivationFailed,
            Outcome = AuditOutcome.Failure,
            ErrorCode = errorCode,
            Detail = $"account creation; profileId={holder.ProfileId}; {detail}",
        }, cancellationToken);

    // -- Helpers --------------------------------------------------------------

    /// <summary>Resolves the wire ISO country code to the
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

    /// <summary>The distinct picked interest ids, after checking every
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
    /// Writes the captured profile fields onto the badge's placeholder
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

    /// <summary>Who a badge resolved to: the attendee, always, and the account
    /// they sign in with, only if they ever asked for one. Most badges resolve
    /// with <see cref="Account"/> null - a bulk order prints them long before
    /// anyone claims one.</summary>
    private sealed record BadgeHolder(
        Guid ProfileId, string ProfileName, Guid? BadgeBatchId, SimfUser? Account);

    /// <summary>Whether a badge with no account behind it may be claimed as one
    /// by whoever is holding it.
    ///
    /// <para>A badge from a bulk order was handed to a named person under a
    /// controlled distribution, so possession is evidence. A badge with no order
    /// behind it came from the walk-in desk and is in open circulation, where
    /// anyone who photographed one across a room could otherwise claim a full app
    /// account - sign-in, contacts, meeting requests - from the picture. That
    /// stays refused unless an operator deliberately arms it, and then the
    /// refusal is the same "badge not recognised" an unknown QR gets, so it is
    /// never an oracle for which badges exist.</para>
    ///
    /// <para>This distinction used to be read off the synthesized login address
    /// (<c>walkin-</c> against <c>badge-</c>). It reads off the attendee's order
    /// now, which is what let those placeholder accounts be retired without
    /// losing it. Everyone belongs to an order, so the test is specifically NOT
    /// "has an order" — it is "belongs to one somebody placed", because whoever
    /// arrived on their own is filed under the seeded direct-registration
    /// order.</para></summary>
    private bool MaySelfActivateWithoutAccount(BadgeHolder holder) =>
        (holder.BadgeBatchId is { } order && order != BadgeBatch.DirectRegistrationId)
        || walkInMode.CurrentValue.BadgeActivationAllowedForWalkIns;

    /// <summary>Resolves a QR to its holder, only while the attendee is Approved
    /// and any account they hold is too; null for unknown / not-approved QRs.
    /// </summary>
    private async Task<BadgeHolder?> ResolveApprovedHolderAsync(
        string? qrId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(qrId)) { return null; }

        // An OFFLINE badge id is never resolvable here.
        //
        // Every endpoint on this service is AllowAnonymous, and their safety
        // rests on a scanned QR being unguessable: a minted QrId is 12 random
        // Crockford characters (~59 bits). An offline id is NOT — it is
        // 'W' plus a desk sequence, so the live ids at an event are a few
        // thousand consecutive numbers. Left resolvable, this turns
        // resolve-badge into an anonymous roster oracle that returns the
        // holder's display name for any guess.
        //
        // Refusing costs nothing: a walk-in badge is physical access only, and
        // badge activation is already blocked for these accounts further down.
        // The bearer of a real offline badge presents the ENCRYPTED blob, which
        // is unguessable and is what the gate reads.
        //
        // This guard matters MORE now that a badge with no account can be
        // claimed as one: a guessable id would be an anonymous way to enumerate
        // attendees and then take an account in their name.
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

        // No account is the ORDINARY case, not a failure: an attendee holds a
        // badge that opens gates long before anyone decides they also want the
        // app. Signing in still needs an account, and the callers that need one
        // say so themselves rather than being refused here.
        if (resolution.UserId is not { } holderUserId)
        {
            return new BadgeHolder(
                resolution.UserProfileId, resolution.DisplayName,
                resolution.BadgeBatchId, Account: null);
        }

        // An account exists but is not approved: refuse the whole holder. The
        // attendee may pass a gate on the profile's own state, but the account is
        // the thing being signed into here, and a disabled one is not a door.
        var user = await accounts.FindByIdAsync(holderUserId, cancellationToken);
        return user is { AccountState: AccountState.Approved }
            ? new BadgeHolder(
                resolution.UserProfileId, resolution.DisplayName,
                resolution.BadgeBatchId, user)
            : null;
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
            await accountCodeRepository.TryConsumeAsync(previous.Id, now, cancellationToken);
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

    /// <summary>The same, for an attendee with no account. The nominated address
    /// is pinned on the row so the complete step reads it from here rather than
    /// from the request.</summary>
    private async Task<string> IssueProfileCodeAsync(
        Guid userProfileId, string email, DateTime now, CancellationToken cancellationToken)
    {
        var previous = await accountCodeRepository.GetLatestUnconsumedForProfileAsync(
            userProfileId, AccountCodePurpose.BadgeActivationOtp, cancellationToken);
        if (previous is not null)
        {
            previous.ConsumedAt = now;
            await accountCodeRepository.UpdateAsync(previous, cancellationToken);
        }
        var value = VerificationCodeGenerator.Generate();
        await accountCodeRepository.AddAsync(new AccountCode
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfileId,
            PendingEmail = email,
            Purpose = AccountCodePurpose.BadgeActivationOtp,
            // Store the keyed hash; `value` (plaintext) is emailed.
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
    /// True for an account minted by the WALK-IN desk with no real email
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
        ConstantTime.Matches(stored, supplied);

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
