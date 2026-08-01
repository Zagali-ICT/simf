// Tests: SIMF.Api.Tests/OfflineBadgeUploadTests.cs
using Microsoft.EntityFrameworkCore;
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
    TimeProvider timeProvider) : IOfflineBadgeUploadService
{
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

        // Profile types are a small, stable lookup — read once rather than once
        // per item, so a 500-badge batch stays at one query for the whole set.
        var profileTypes = await appDbContext.ProfileTypes
            .AsNoTracking()
            .Where(type => type.IsActive && type.Code != 0)
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
                expectedIsVisitor: type.IsForVisitor,
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
    }

    private static OfflineBadgeUploadResult Rejected(
        long sequence, string qrId, string errorCode, string message) =>
        new(sequence, qrId, OfflineBadgeUploadStatus.Rejected, errorCode, message);
}
