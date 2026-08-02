// Tests: SIMF.Api.Tests/OfflineBadgeUploadTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Badges;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Badges;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-809 — the offline badge desk's reconciliation upload.
///
/// <para>Deliberately a THIN layer over <see cref="IAdminUserProvisioningService
/// .RegisterOnSiteAsync"/> rather than its own write path. The desk registration
/// rules — profile-type scope, identity-document uniqueness, the quick-register
/// floor, auto-approval, the audit rows — are the same rules whether the desk had
/// a network or not, and a second implementation would drift from them. All this
/// class adds is the sequence-to-QR-id mapping and per-item error isolation.</para>
/// </summary>
internal sealed class OfflineBadgeUploadService(
    IAdminUserProvisioningService provisioning,
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    IOptionsMonitor<WalkInModeOptions> walkInMode,
    TimeProvider timeProvider,
    ILogger<OfflineBadgeUploadService> logger) : IOfflineBadgeUploadService
{
    /// <summary>
    /// D-813 - the storage cap that actually bites on this path.
    /// <c>UserProfile.Name</c> and <c>.NameArabic</c> are both NVARCHAR(50), and
    /// this path mirrors the desk's ONE captured name into both. The desk's name
    /// box has no length limit, so a longer name used to reach SQL Server, raise
    /// a truncation error, and come back as INTERNAL_ERROR "Retry it" - advice
    /// that can never succeed. The desk then keeps the row pending and
    /// re-uploads it for the rest of the event, so reconciliation never reaches
    /// zero. Named as its own rejection instead, which is both true and
    /// actionable.
    /// </summary>
    private const int NameMaxLength = 50;

    /// <summary>Cap on one upload. Large enough for a full desk shift, small
    /// enough that a request stays inside a normal timeout — the desk splits a
    /// longer backlog across calls, and each is independently idempotent.</summary>
    private const int MaxBatchSize = 500;

    public async Task<OfflineBadgeBatchResponse> UploadAsync(
        Guid actorUserId,
        OfflineBadgeBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!walkInMode.CurrentValue.OfflineUploadActive(timeProvider.GetUtcNow()))
        {
            throw new ApiException(
                ErrorCodes.OfflineUploadDisabled, 403,
                "Offline badge upload is not enabled.",
                "رفع بطاقات العمل دون اتصال غير مُفعّل.");
        }

        var items = request.Registrations ?? new List<OfflineBadgeRegistration>();
        if (items.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "The batch contains no registrations.",
                "الدفعة لا تحتوي على أي تسجيلات.");
        }
        if (items.Count > MaxBatchSize)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                $"A batch carries at most {MaxBatchSize} registrations.",
                $"الحد الأقصى للدفعة الواحدة {MaxBatchSize} تسجيل.");
        }

        // D-811 review — the desk captures a reduced field set by its nature (one
        // name, one identity document), which is exactly what QuickRegister
        // permits. Without it every row fails the full-desk nationality check, so
        // the whole batch is rejected one row at a time. Say so once, up front,
        // instead of returning 500 identical per-row rejections.
        if (!walkInMode.CurrentValue.QuickRegisterActive(timeProvider.GetUtcNow()))
        {
            throw new ApiException(
                ErrorCodes.OfflineUploadDisabled, 403,
                "Offline upload also needs WalkInMode:QuickRegister; an offline desk "
                    + "captures a reduced field set.",
                "رفع البطاقات دون اتصال يتطلب أيضًا تفعيل التسجيل السريع.");
        }

        // Profile types are a small, stable lookup — read once rather than once
        // per item, so a 500-badge batch stays at one query for the whole set.
        //
        // D-811 review — AUDIENCE TYPES ONLY. This endpoint is gated on
        // Visitors.RegisterOnsite, which PermissionCatalog grants to the staff
        // mobile app role while deliberately withholding Others.RegisterOnsite.
        // Without this filter a staff token could create partner-tier accounts
        // here that it cannot create on the online desk — the batch would have
        // been a way around that boundary.
        var profileTypes = await appDbContext.ProfileTypes
            .AsNoTracking()
            .Where(type => type.IsActive && type.Code != 0 && type.IsForVisitor)
            .Select(type => new { type.Id, type.Code, type.IsForVisitor })
            .ToListAsync(cancellationToken);

        var results = new List<OfflineBadgeUploadResult>(items.Count);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await UploadOneAsync(
                actorUserId,
                item,
                profileTypes.SingleOrDefault(type => type.Code == item.ProfileTypeCode)
                    is { } match
                        ? (match.Id, match.IsForVisitor)
                        : null,
                cancellationToken));
        }

        var response = new OfflineBadgeBatchResponse(
            Submitted: items.Count,
            Created: results.Count(r => r.Status == OfflineBadgeUploadStatus.Created),
            PendingApproval: results.Count(
                r => r.Status == OfflineBadgeUploadStatus.CreatedPendingApproval),
            AlreadyUploaded: results.Count(
                r => r.Status == OfflineBadgeUploadStatus.AlreadyUploaded),
            Rejected: results.Count(r => r.Status == OfflineBadgeUploadStatus.Rejected),
            Results: results);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminOfflineBadgeBatchUploaded,
            Outcome = response.Rejected == 0
                ? AuditOutcome.Success
                : AuditOutcome.Failure,
            ActorUserId = actorUserId,
            Detail = $"desk={request.DeskLabel ?? "(unnamed)"}; "
                + $"submitted={response.Submitted}; created={response.Created}; "
                + $"pending={response.PendingApproval}; "
                + $"alreadyUploaded={response.AlreadyUploaded}; "
                + $"rejected={response.Rejected}",
        }, cancellationToken);

        return response;
    }

    private async Task<OfflineBadgeUploadResult> UploadOneAsync(
        Guid actorUserId,
        OfflineBadgeRegistration item,
        (Guid Id, bool IsForVisitor)? profileType,
        CancellationToken cancellationToken)
    {
        if (!OfflineBadgeId.TryFormat(item.Sequence, out var qrId))
        {
            return Rejected(
                item.Sequence, string.Empty, ErrorCodes.OfflineBadgeInvalid,
                "The badge sequence is outside the accepted range.");
        }
        if (profileType is not { } type)
        {
            return Rejected(
                item.Sequence, qrId, ErrorCodes.OfflineBadgeInvalid,
                $"Profile-type code {item.ProfileTypeCode} is not a live profile type.");
        }
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return Rejected(
                item.Sequence, qrId, ErrorCodes.ValidationFailed,
                "The badge carries no name.");
        }

        // D-814 review - the pre-check comes FIRST, ahead of every shape rule.
        // A shift can be committed by the server and have its response lost, in
        // which case the desk still holds every row as pending and retries. If a
        // shape rule ran first, a row the server had ALREADY created would come
        // back Rejected instead of AlreadyUploaded, the desk would never mark it
        // done, and "waiting to upload" could never reach zero - while the
        // report told the operator a visitor has no account when the account
        // exists.
        //
// Cheap pre-check so a retried upload answers without attempting a write.
        // It is a read-then-insert and therefore racy; RegisterOnSiteAsync catches
        // the losing insert on IX_UserProfiles_QrId and reports the same outcome.
        if (await appDbContext.UserProfiles
                .AsNoTracking()
                .AnyAsync(profile => profile.QrId == qrId, cancellationToken))
        {
            return new OfflineBadgeUploadResult(
                item.Sequence, qrId, OfflineBadgeUploadStatus.AlreadyUploaded,
                null, null);
        }

        // Shape rules, only for rows this server has not already accepted.
        //
        // These reject values that used to be written, which is safe ONLY
        // because the desk can now correct a row and keep its sequence (D-814):
        // the badge in the visitor's hand stays valid and the same row uploads
        // again. Without that path this would strand printed badges.
        if (item.Name.Trim().Length > NameMaxLength
            || (item.NameArabic?.Trim().Length ?? 0) > NameMaxLength)
        {
            // Deliberately NOT truncated: the badge is printed with the full
            // name on it, and silently storing a different one would make the
            // paper and the record disagree.
            return Rejected(
                item.Sequence, qrId, ErrorCodes.ValidationFailed,
                $"The name is longer than {NameMaxLength} characters.");
        }
        if (DescribeMalformedIdentityDocument(item) is { } identityProblem)
        {
            return Rejected(
                item.Sequence, qrId, ErrorCodes.ValidationFailed, identityProblem);
        }

        var name = item.Name.Trim();
        var arabicName = string.IsNullOrWhiteSpace(item.NameArabic)
            ? name
            : item.NameArabic.Trim();
        var registration = new AdminWalkInRegistrationRequest
        {
            Email = item.Email,
            DisplayName = name,
            EnglishName = name,
            // A desk with no network captures ONE name in whatever script the
            // visitor writes. Mirroring it keeps both NOT NULL columns filled
            // without inventing a transliteration nobody verified.
            ArabicName = arabicName,
            ProfileTypeId = type.Id,
            NationalityCode = string.Empty,
            IsSaudi = !string.IsNullOrWhiteSpace(item.NationalId),
            NationalId = item.NationalId,
            IqamaNumber = item.IqamaNumber,
            PassportNumber = item.PassportNumber,
            SaudiMobile = item.SaudiMobile,
            InternationalMobile = item.InternationalMobile,
        };

        try
        {
            var created = await provisioning.RegisterOnSiteAsync(
                actorUserId, UserType.Visitor, registration, cancellationToken,
                // Constant true, not type.IsForVisitor: passing the type's own
                // flag made the desk-scope guard compare a value with itself and
                // therefore assert nothing. The lookup above already restricted
                // the set to audience types; this makes the service re-check it.
                expectedIsVisitor: true,
                presetQrId: qrId);

            // Read the account state rather than inferring it from the returned
            // QR. On every other desk path an empty QR means "not approved", but
            // an offline row is inserted WITH its badge id — the paper already
            // exists — so here the QR says nothing about approval.
            //
            // Two reads across the two databases, which is the same shape
            // QrResolver uses: a bare id resolved with a second query, never a
            // cross-database join (D-157).
            var state = await identityDbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == created.UserId)
                .Select(user => user.AccountState)
                .SingleOrDefaultAsync(cancellationToken);

            // Reporting the pending case distinctly matters: that badge is
            // already in someone's hand and WILL BE REFUSED at the gate until an
            // admin approves the account.
            return new OfflineBadgeUploadResult(
                item.Sequence,
                qrId,
                state == AccountState.Approved
                    ? OfflineBadgeUploadStatus.Created
                    : OfflineBadgeUploadStatus.CreatedPendingApproval,
                null,
                null);
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.OfflineBadgeSequenceTaken)
        {
            return new OfflineBadgeUploadResult(
                item.Sequence, qrId, OfflineBadgeUploadStatus.AlreadyUploaded,
                null, null);
        }
        catch (ApiException ex)
        {
            // A duplicate identity document, an unknown profile type, a rejected
            // shape — all reported against the row that caused them so the rest
            // of the batch still lands.
            return Rejected(item.Sequence, qrId, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            // D-811 review — isolation has to be per item for EVERY failure, not
            // just the expected ones. A deadlock victim, a command timeout or a
            // column truncation used to escape this loop and take the whole
            // response with it, leaving the desk unable to tell which sequences
            // landed — the precise outcome this class exists to prevent. The
            // detail is logged, never returned: it can carry store internals.
            logger.LogError(
                ex, "Offline badge row {Sequence} failed unexpectedly.", item.Sequence);
            return Rejected(
                item.Sequence, qrId, ErrorCodes.InternalError,
                "This registration could not be stored. Retry it.");
        }
    }

    /// <summary>
    /// D-814 - the identity-document shape rules, applied per item.
    ///
    /// <para>They could not be applied before. The batch calls
    /// <c>RegisterOnSiteAsync</c> directly, so no FastEndpoints validator runs on
    /// it, and rejecting a row meant a printed badge in a visitor's hand that
    /// nothing backed and nobody could fix - the desk store was append-only with
    /// no edit path. D-814 gave the desk a correction action (F3), which is what
    /// makes rejecting SAFE: the row comes back named, the operator fixes the
    /// number, the same badge uploads. The paper never changes.</para>
    ///
    /// <para>Why it matters that these run at all: the duplicate-identity guard
    /// keys off a blind-index HMAC of the number. A mistyped id is still unique,
    /// so it hashes to a value matching nothing, the guard never fires, and the
    /// same person collects a second badge at another desk. Checking the Luhn
    /// digit is what makes the guard mean anything on this path.</para>
    ///
    /// <para>Returns null when the row is fine. Only NON-EMPTY values are
    /// checked: whether a document is REQUIRED is the quick-register floor's
    /// decision, made later in the registration service, and duplicating it here
    /// would let the two drift.</para>
    /// </summary>
    private static string? DescribeMalformedIdentityDocument(OfflineBadgeRegistration item)
    {
        if (!string.IsNullOrWhiteSpace(item.NationalId)
            && !IdentityDocument.IsSaudiNationalId(item.NationalId.Trim()))
        {
            return "The national ID is not a valid number "
                + "(10 digits starting with 1, and the check digit must match).";
        }
        if (!string.IsNullOrWhiteSpace(item.IqamaNumber)
            && !IdentityDocument.IsIqamaNumber(item.IqamaNumber.Trim()))
        {
            return "The Iqama number is not a valid number "
                + "(10 digits starting with 2, and the check digit must match).";
        }
        if (!string.IsNullOrWhiteSpace(item.PassportNumber)
            && !IdentityDocument.IsOtherDocumentNumber(item.PassportNumber.Trim()))
        {
            return "The passport or document number must be at most "
                + $"{IdentityDocument.OtherDocumentMaxLength} characters.";
        }
        return null;
    }

    private static OfflineBadgeUploadResult Rejected(
        long sequence, string qrId, string errorCode, string message) =>
        new(sequence, qrId, OfflineBadgeUploadStatus.Rejected, errorCode, message);
}
