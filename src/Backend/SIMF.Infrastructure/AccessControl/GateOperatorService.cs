// Tests: SIMF.Api.Tests/Gates/GateScanTests.cs (full constraint-engine matrix)
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common.Enums;
using SIMF.Contracts.Gates;
using SIMF.Domain.AccessControl;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>
/// D-148 — operator surface + 13-step constraint engine
/// (SIMF-FDS-003 §5.6.1). Every recorded denial emits exactly one
/// <see cref="DenialReasonCode"/> per the table in §5.6.1; HTTP-level
/// errors (404 / 403 / 409 / 429 / 503) ride on <see cref="GateScanResult.Kind"/>
/// for the endpoint to translate.
/// </summary>
internal sealed class GateOperatorService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IQrResolver qrResolver,
    IGateConfigCache configCache,
    IScanIdempotencyStore idempotencyStore,
    IGateFailureCircuit failureCircuit,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<GateOperatorService> logger) : IGateOperatorService
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);

    public async Task<IReadOnlyList<OperatorGateAssignment>> ListMyAssignmentsAsync(
        Guid operatorUserId, CancellationToken cancellationToken = default)
    {
        var assignments = await appDbContext.GateAssignments.AsNoTracking()
            .Where(a => a.UserId == operatorUserId && a.IsActive)
            .Join(appDbContext.Gates.AsNoTracking(),
                a => a.GateId, g => g.Id,
                (a, g) => new { g.Id, g.Code, g.Name, g.NameArabic, g.DirectionMode, g.IsActive })
            .OrderBy(g => g.Code)
            .ToListAsync(cancellationToken);

        return assignments
            .Select(g => new OperatorGateAssignment(
                g.Id, g.Code, g.Name, g.NameArabic, g.DirectionMode, g.IsActive))
            .ToList();
    }

    public async Task<GateScanResult> RecordScanAsync(
        GateScanContext context, CancellationToken cancellationToken = default)
    {
        // Step 1 — authentication is verified at the HTTP layer; if we are
        // here the caller is authenticated. No engine check.

        // Step 2 — load the gate config snapshot via the cache. A missing
        // snapshot is GateNotFound (HTTP 404). Operator-assignment check
        // rides on the snapshot too.
        var snapshot = await configCache.GetAsync(context.GateId, cancellationToken);
        if (snapshot is null)
        {
            return new GateScanResult(GateScanResultKind.GateNotFound,
                EmptyResponse(timeProvider.GetUtcNow()), false, "GATE_NOT_FOUND");
        }
        if (!snapshot.AssignedOperatorUserIds.Contains(context.OperatorUserId))
        {
            return new GateScanResult(GateScanResultKind.NotAssigned,
                EmptyResponse(timeProvider.GetUtcNow()), false, "GATE_OPERATOR_NOT_ASSIGNED");
        }

        // Failure-rate circuit short-circuit (SIMF-FDS-003 §5.6.6).
        if (failureCircuit.IsOpen(context.GateId))
        {
            return new GateScanResult(GateScanResultKind.CircuitOpen,
                EmptyResponse(timeProvider.GetUtcNow()), false, "GATE_FAILURE_CIRCUIT_OPEN");
        }

        // Idempotency-key precedence (SIMF-API-GATES-001 §9): header wins
        // over body when both are present.
        var idempotencyKey = !string.IsNullOrWhiteSpace(context.HeaderIdempotencyKey)
            ? context.HeaderIdempotencyKey
            : context.Request.IdempotencyKey;

        string? requestHash = null;
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            requestHash = HashRequest(context.GateId, context.Request, idempotencyKey);
            var prior = await idempotencyStore.TryGetAsync(
                idempotencyKey, context.GateId, cancellationToken);
            if (prior is not null)
            {
                if (!string.Equals(prior.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    return new GateScanResult(GateScanResultKind.IdempotencyConflict,
                        EmptyResponse(timeProvider.GetUtcNow()), false,
                        "IDEMPOTENCY_KEY_CONFLICT");
                }
                // Same key + same payload → replay the original.
                var replay = await LoadReplayAsync(prior, context.AcceptLanguage, cancellationToken);
                return new GateScanResult(GateScanResultKind.Recorded, replay, true, null);
            }
        }

        // Step 3 — resolve the QR. Null → record QR_UNKNOWN.
        var qr = (context.Request.Qr ?? string.Empty).Trim();
        var resolution = await qrResolver.ResolveAsync(qr, cancellationToken);
        if (resolution is null)
        {
            return await RecordDenialAsync(context, qrIdAtScan: qr,
                userProfileId: null,
                profileDisplayName: null, profileDisplayNameArabic: null,
                profileTypeId: null, profileTypeName: null, profileTypePageColor: null,
                direction: ResolveColdStartDirection(snapshot),
                reason: DenialReasonCode.QrUnknown,
                requestHash, idempotencyKey,
                cancellationToken);
        }

        // Step 5 — gate active (snapshot reflects current DB state up to the TTL).
        if (!snapshot.IsActive)
        {
            return await RecordDenialAsync(context, qrIdAtScan: qr,
                userProfileId: resolution.UserProfileId,
                profileDisplayName: resolution.DisplayName,
                profileDisplayNameArabic: resolution.DisplayNameArabic,
                profileTypeId: resolution.ProfileTypeId,
                profileTypeName: resolution.ProfileTypeName,
                profileTypePageColor: resolution.ProfileTypePageColor,
                direction: ResolveColdStartDirection(snapshot),
                reason: DenialReasonCode.GateInactiveAtScan,
                requestHash, idempotencyKey,
                cancellationToken);
        }

        // Step 6 — holder approved.
        if (resolution.AccountState != AccountState.Approved
            && resolution.AccountState != AccountState.Disabled)
        {
            return await RecordDenialAsync(context, qrIdAtScan: qr,
                userProfileId: resolution.UserProfileId,
                profileDisplayName: resolution.DisplayName,
                profileDisplayNameArabic: resolution.DisplayNameArabic,
                profileTypeId: resolution.ProfileTypeId,
                profileTypeName: resolution.ProfileTypeName,
                profileTypePageColor: resolution.ProfileTypePageColor,
                direction: ResolveColdStartDirection(snapshot),
                reason: DenialReasonCode.HolderNotApproved,
                requestHash, idempotencyKey,
                cancellationToken);
        }

        // Step 7 — not disabled.
        if (resolution.AccountState == AccountState.Disabled)
        {
            return await RecordDenialAsync(context, qrIdAtScan: qr,
                userProfileId: resolution.UserProfileId,
                profileDisplayName: resolution.DisplayName,
                profileDisplayNameArabic: resolution.DisplayNameArabic,
                profileTypeId: resolution.ProfileTypeId,
                profileTypeName: resolution.ProfileTypeName,
                profileTypePageColor: resolution.ProfileTypePageColor,
                direction: ResolveColdStartDirection(snapshot),
                reason: DenialReasonCode.HolderDisabled,
                requestHash, idempotencyKey,
                cancellationToken);
        }

        // Step 8 — not locked.
        if (resolution.IsLockedOut)
        {
            return await RecordDenialAsync(context, qrIdAtScan: qr,
                userProfileId: resolution.UserProfileId,
                profileDisplayName: resolution.DisplayName,
                profileDisplayNameArabic: resolution.DisplayNameArabic,
                profileTypeId: resolution.ProfileTypeId,
                profileTypeName: resolution.ProfileTypeName,
                profileTypePageColor: resolution.ProfileTypePageColor,
                direction: ResolveColdStartDirection(snapshot),
                reason: DenialReasonCode.HolderLocked,
                requestHash, idempotencyKey,
                cancellationToken);
        }

        // Step 9 — ProfileType active.
        if (resolution.ProfileTypeId is not null && !resolution.ProfileTypeActive)
        {
            return await RecordDenialAsync(context, qrIdAtScan: qr,
                userProfileId: resolution.UserProfileId,
                profileDisplayName: resolution.DisplayName,
                profileDisplayNameArabic: resolution.DisplayNameArabic,
                profileTypeId: resolution.ProfileTypeId,
                profileTypeName: resolution.ProfileTypeName,
                profileTypePageColor: resolution.ProfileTypePageColor,
                direction: ResolveColdStartDirection(snapshot),
                reason: DenialReasonCode.ProfileTypeInactive,
                requestHash, idempotencyKey,
                cancellationToken);
        }

        // Step 9.5 — RESERVED hook (time-window). No-op in this increment.

        // Step 10 — direction resolution.
        var direction = await ResolveDirectionAsync(
            context.GateId, resolution.UserProfileId, snapshot.DirectionMode,
            cancellationToken);

        // Step 11 — allow-list check. Empty raw list = pass (general gate).
        // Non-empty raw list = filtered allow-list must contain the holder's
        // ProfileTypeId; filtered-empty (L-15 safe default) denies all.
        if (snapshot.AllowedProfileTypeIdsRaw.Count > 0
            && (resolution.ProfileTypeId is null
                || !snapshot.AllowedProfileTypeIdsFiltered.Contains(resolution.ProfileTypeId.Value)))
        {
            return await RecordDenialAsync(context, qrIdAtScan: qr,
                userProfileId: resolution.UserProfileId,
                profileDisplayName: resolution.DisplayName,
                profileDisplayNameArabic: resolution.DisplayNameArabic,
                profileTypeId: resolution.ProfileTypeId,
                profileTypeName: resolution.ProfileTypeName,
                profileTypePageColor: resolution.ProfileTypePageColor,
                direction: direction,
                reason: DenialReasonCode.ProfileTypeNotAllowed,
                requestHash, idempotencyKey,
                cancellationToken);
        }

        // Step 11.5 — RESERVED hook (booking-required). No-op in this increment.

        // Step 12 — 5-second duplicate-absorption window keyed (GateId, UserProfileId).
        var windowCutoff = timeProvider.GetUtcNow() - DuplicateWindow;
        var priorAllowed = await appDbContext.GateScans.AsNoTracking()
            .Where(s => s.GateId == context.GateId
                     && s.UserProfileId == resolution.UserProfileId
                     && s.Outcome == ScanOutcome.Allowed
                     && s.ScannedAtUtc >= windowCutoff)
            .OrderByDescending(s => s.ScannedAtUtc)
            .Select(s => new { s.Id, s.Direction, s.ScannedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);
        if (priorAllowed is not null)
        {
            // Duplicate absorbed — return the original outcome without inserting.
            var response = new GateScanResponse(
                priorAllowed.Id, ScanOutcome.Allowed,
                priorAllowed.Direction, priorAllowed.ScannedAtUtc,
                new GateScanUserProfile(
                    resolution.UserProfileId, resolution.DisplayName,
                    resolution.DisplayNameArabic, resolution.ProfileTypeId,
                    resolution.ProfileTypeName, resolution.ProfileTypePageColor),
                null, null);
            return new GateScanResult(GateScanResultKind.Recorded, response, false, null);
        }

        // Step 13 — persist allowed scan.
        return await RecordAllowedAsync(context, qr, resolution, direction,
            requestHash, idempotencyKey, cancellationToken);
    }

    public async Task<OperatorDailyReport> GetMyDailyReportAsync(
        Guid operatorUserId, Guid? gateId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var fromUtc = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var toUtc = fromUtc.AddDays(1).AddTicks(-1);

        var query = appDbContext.GateScans.AsNoTracking()
            .Where(s => s.ScannedByUserId == operatorUserId
                     && s.ScannedAtUtc >= fromUtc && s.ScannedAtUtc <= toUtc);
        if (gateId is { } gid) { query = query.Where(s => s.GateId == gid); }

        var rows = await query
            .OrderByDescending(s => s.ScannedAtUtc)
            .Take(500)
            .Select(s => new
            {
                s.Id, s.ScannedAtUtc, s.Outcome, s.Direction,
                s.UserProfileId, s.DenialReasonCode,
            })
            .ToListAsync(cancellationToken);

        var profileIds = rows
            .Where(r => r.UserProfileId != null)
            .Select(r => r.UserProfileId!.Value)
            .Distinct().ToList();
        var displayNames = profileIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await (
                from profile in identityDbContext.UserProfiles.AsNoTracking()
                join user in identityDbContext.Users.AsNoTracking() on profile.UserId equals user.Id
                where profileIds.Contains(profile.Id)
                select new { profile.Id, Name = user.DisplayName ?? string.Empty })
              .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var allowed = rows.Count(r => r.Outcome == ScanOutcome.Allowed);
        var denied = rows.Count - allowed;
        var denialBuckets = rows
            .Where(r => r.Outcome == ScanOutcome.Denied && r.DenialReasonCode is not null)
            .GroupBy(r => r.DenialReasonCode!.Value)
            .Select(g => new OperatorDenialBucket(g.Key.ToString(), g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

        var rowDtos = rows.Select(r => new OperatorScanRow(
            r.Id, r.ScannedAtUtc, r.Outcome, r.Direction,
            r.UserProfileId is { } pid && displayNames.TryGetValue(pid, out var name) ? name : null,
            r.DenialReasonCode)).ToList();

        return new OperatorDailyReport(
            operatorUserId, fromUtc, toUtc,
            new OperatorDailyReportTotals(allowed, denied),
            denialBuckets, rowDtos);
    }

    // ---- internals ----

    private static GateScanResponse EmptyResponse(DateTimeOffset scannedAtUtc) =>
        new(0, ScanOutcome.Denied, ScanDirection.CheckIn, scannedAtUtc, null, null, null);

    private static ScanDirection ResolveColdStartDirection(GateConfigSnapshot snapshot) =>
        snapshot.DirectionMode switch
        {
            DirectionMode.Out => ScanDirection.CheckOut,
            _ => ScanDirection.CheckIn,
        };

    private async Task<ScanDirection> ResolveDirectionAsync(
        Guid gateId, Guid userProfileId, DirectionMode mode, CancellationToken cancellationToken)
    {
        if (mode == DirectionMode.In) { return ScanDirection.CheckIn; }
        if (mode == DirectionMode.Out) { return ScanDirection.CheckOut; }

        var last = await appDbContext.GateScans.AsNoTracking()
            .Where(s => s.GateId == gateId
                     && s.UserProfileId == userProfileId
                     && s.Outcome == ScanOutcome.Allowed)
            .OrderByDescending(s => s.ScannedAtUtc)
            .Select(s => (ScanDirection?)s.Direction)
            .FirstOrDefaultAsync(cancellationToken);

        return last switch
        {
            ScanDirection.CheckIn => ScanDirection.CheckOut,
            ScanDirection.CheckOut => ScanDirection.CheckIn,
            _ => ScanDirection.CheckIn,
        };
    }

    private async Task<GateScanResult> RecordAllowedAsync(
        GateScanContext context, string qr, QrResolution resolution,
        ScanDirection direction, string? requestHash, string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var scan = new GateScan
        {
            GateId = context.GateId,
            UserProfileId = resolution.UserProfileId,
            QrIdAtScan = qr,
            Direction = direction,
            Outcome = ScanOutcome.Allowed,
            DenialReasonCode = null,
            ScannedAtUtc = now,
            ClientScannedAtUtc = context.Request.ClientScannedAtUtc,
            ScannedByUserId = context.OperatorUserId,
            Source = context.Request.Source,
            CorrelationId = context.CorrelationId,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent,
            IdempotencyKey = idempotencyKey,
        };
        appDbContext.GateScans.Add(scan);
        await appDbContext.SaveChangesAsync(cancellationToken);

        var response = new GateScanResponse(
            scan.Id, ScanOutcome.Allowed, direction, now,
            new GateScanUserProfile(
                resolution.UserProfileId, resolution.DisplayName,
                resolution.DisplayNameArabic, resolution.ProfileTypeId,
                resolution.ProfileTypeName, resolution.ProfileTypePageColor),
            null, null);

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && requestHash is not null)
        {
            await idempotencyStore.PersistAsync(
                idempotencyKey, context.GateId,
                requestHash, HashResponse(response),
                scan.Id, cancellationToken);
        }

        failureCircuit.RecordAllowed(context.GateId);
        logger.LogInformation(
            "Allowed scan {ScanId} on gate {GateId} for visitor {ProfileId}",
            scan.Id, context.GateId, resolution.UserProfileId);

        return new GateScanResult(GateScanResultKind.Recorded, response, false, null);
    }

    private async Task<GateScanResult> RecordDenialAsync(
        GateScanContext context,
        string qrIdAtScan,
        Guid? userProfileId,
        string? profileDisplayName, string? profileDisplayNameArabic,
        Guid? profileTypeId, string? profileTypeName, string? profileTypePageColor,
        ScanDirection direction,
        DenialReasonCode reason,
        string? requestHash, string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var scan = new GateScan
        {
            GateId = context.GateId,
            UserProfileId = userProfileId,
            QrIdAtScan = qrIdAtScan,
            Direction = direction,
            Outcome = ScanOutcome.Denied,
            DenialReasonCode = reason,
            ScannedAtUtc = now,
            ClientScannedAtUtc = context.Request.ClientScannedAtUtc,
            ScannedByUserId = context.OperatorUserId,
            Source = context.Request.Source,
            CorrelationId = context.CorrelationId,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent,
            IdempotencyKey = idempotencyKey,
        };
        appDbContext.GateScans.Add(scan);
        await appDbContext.SaveChangesAsync(cancellationToken);

        var (messageEn, messageAr) = DenialMessages(reason);
        var message = string.Equals(context.AcceptLanguage, "ar", StringComparison.OrdinalIgnoreCase)
            ? messageAr : messageEn;

        var response = new GateScanResponse(
            scan.Id, ScanOutcome.Denied, direction, now,
            userProfileId is null ? null
                : new GateScanUserProfile(
                    userProfileId.Value, profileDisplayName ?? string.Empty,
                    profileDisplayNameArabic ?? string.Empty, profileTypeId,
                    profileTypeName, profileTypePageColor),
            reason, message);

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && requestHash is not null)
        {
            await idempotencyStore.PersistAsync(
                idempotencyKey, context.GateId,
                requestHash, HashResponse(response),
                scan.Id, cancellationToken);
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.GateScanDenied,
            Outcome = AuditOutcome.Failure,
            ActorUserId = context.OperatorUserId,
            Detail = $"gateId={context.GateId}; reason={reason}; scanId={scan.Id}; corr={context.CorrelationId}",
        }, cancellationToken);

        await failureCircuit.RecordDenialAsync(context.GateId, cancellationToken);

        return new GateScanResult(GateScanResultKind.Recorded, response, false, null);
    }

    private async Task<GateScanResponse> LoadReplayAsync(
        ScanIdempotencyRecord prior, string? acceptLanguage,
        CancellationToken cancellationToken)
    {
        if (prior.ScanId is null)
        {
            return EmptyResponse(prior.StoredAt);
        }
        var scan = await appDbContext.GateScans.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == prior.ScanId.Value, cancellationToken);
        if (scan is null)
        {
            return EmptyResponse(prior.StoredAt);
        }

        GateScanUserProfile? profile = null;
        if (scan.UserProfileId is { } pid)
        {
            var profileRow = await (
                from row in identityDbContext.UserProfiles.AsNoTracking()
                join user in identityDbContext.Users.AsNoTracking() on row.UserId equals user.Id
                where row.Id == pid
                select new
                {
                    Id = row.Id,
                    DisplayName = user.DisplayName ?? string.Empty,
                    DisplayNameArabic = row.ArabicName,
                    ProfileTypeId = row.ProfileTypeId,
                    ProfileTypeName = row.ProfileType != null ? row.ProfileType.Name : null,
                    ProfileTypePageColor = row.ProfileType != null ? row.ProfileType.PageColor : null,
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (profileRow is not null)
            {
                profile = new GateScanUserProfile(
                    profileRow.Id, profileRow.DisplayName, profileRow.DisplayNameArabic,
                    profileRow.ProfileTypeId, profileRow.ProfileTypeName,
                    profileRow.ProfileTypePageColor);
            }
        }

        string? message = null;
        if (scan.DenialReasonCode is { } reason)
        {
            var (messageEn, messageAr) = DenialMessages(reason);
            message = string.Equals(acceptLanguage, "ar", StringComparison.OrdinalIgnoreCase)
                ? messageAr : messageEn;
        }

        return new GateScanResponse(
            scan.Id, scan.Outcome, scan.Direction, scan.ScannedAtUtc,
            profile, scan.DenialReasonCode, message);
    }

    private static (string en, string ar) DenialMessages(DenialReasonCode reason) =>
        reason switch
        {
            DenialReasonCode.QrUnknown =>
                ("This QR code is not recognised.",
                 "هذا الرمز غير معروف."),
            DenialReasonCode.GateInactiveAtScan =>
                ("This gate is currently inactive.",
                 "هذه البوابة غير نشطة حالياً."),
            DenialReasonCode.HolderNotApproved =>
                ("This visitor's account has not been approved.",
                 "لم يتم اعتماد حساب هذا الزائر."),
            DenialReasonCode.HolderDisabled =>
                ("This visitor's account is disabled.",
                 "حساب هذا الزائر معطّل."),
            DenialReasonCode.HolderLocked =>
                ("This visitor's account is locked.",
                 "حساب هذا الزائر مقفل."),
            DenialReasonCode.ProfileTypeInactive =>
                ("This visitor's profile type is no longer active.",
                 "نوع ملف هذا الزائر لم يعد نشطاً."),
            DenialReasonCode.OutsideTimeWindow =>
                ("This gate is closed at this time.",
                 "هذه البوابة مغلقة في هذا الوقت."),
            DenialReasonCode.ProfileTypeNotAllowed =>
                ("This gate is not open to this visitor's profile type.",
                 "هذه البوابة ليست مفتوحة لنوع ملف هذا الزائر."),
            DenialReasonCode.BookingRequiredMissing =>
                ("A booking is required for this gate.",
                 "يتطلب هذه البوابة وجود حجز مسبق."),
            _ => ("This scan was denied.", "تم رفض هذا المسح."),
        };

    private static string HashRequest(Guid gateId, GateScanRequest request, string idempotencyKey)
    {
        var payload = $"{gateId:N}|{idempotencyKey}|{request.Qr}|{request.Source}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static string HashResponse(GateScanResponse response) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(response)));
}
