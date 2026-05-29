// Tests: SIMF.Api.Tests/GateScanTests.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Common.Enums;
using SIMF.Contracts.Gates;
using SIMF.Domain.AccessControl;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>
/// Operator surface + 13-step constraint engine (SIMF-FDS-003 §5.6.1). Every
/// recorded denial emits exactly one <see cref="DenialReasonCode"/> per the
/// table in §5.6.1; HTTP-level errors (404 / 403 / 409 / 429 / 503) ride on
/// <see cref="GateScanResult.Kind"/> for the endpoint to translate.
/// </summary>
internal sealed class GateOperatorService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IQrResolver qrResolver,
    IGateConfigCache configCache,
    IGateFailureCircuit failureCircuit,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<GateOperatorService> logger) : IGateOperatorService
{
    private const string ArabicLanguageCode = "ar";
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromHours(24);

    public async Task<IReadOnlyList<OperatorGateAssignment>> ListMyAssignmentsAsync(
        Guid operatorUserId, CancellationToken cancellationToken = default)
    {
        var rows = await appDbContext.GateAssignments.AsNoTracking()
            .Where(a => a.UserId == operatorUserId && a.IsActive)
            .Join(appDbContext.Gates.AsNoTracking(),
                a => a.GateId, g => g.Id,
                (a, g) => new { g.Id, g.Code, g.Name, g.NameArabic, g.DirectionMode, g.IsActive })
            .OrderBy(g => g.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(g => new OperatorGateAssignment(
            g.Id, g.Code, g.Name, g.NameArabic, g.DirectionMode, g.IsActive)).ToList();
    }

    public async Task<GateScanResult> RecordScanAsync(
        GateScanContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = await configCache.GetAsync(context.GateId, cancellationToken);
        if (snapshot is null)
        {
            return Routing(GateScanResultKind.GateNotFound, "GATE_NOT_FOUND");
        }
        if (!snapshot.AssignedOperatorUserIds.Contains(context.OperatorUserId))
        {
            return Routing(GateScanResultKind.NotAssigned, "GATE_OPERATOR_NOT_ASSIGNED");
        }
        if (failureCircuit.IsOpen(context.GateId))
        {
            return Routing(GateScanResultKind.CircuitOpen, "GATE_FAILURE_CIRCUIT_OPEN");
        }

        // Idempotency-key precedence (SIMF-API-GATES-001 §9): header wins.
        var idempotencyKey = !string.IsNullOrWhiteSpace(context.HeaderIdempotencyKey)
            ? context.HeaderIdempotencyKey
            : context.Request.IdempotencyKey;
        string? requestHash = null;
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            requestHash = HashRequest(context.GateId, context.Request, idempotencyKey);
            var replay = await TryReplayAsync(
                idempotencyKey, context.GateId, requestHash,
                context.AcceptLanguage, cancellationToken);
            if (replay is not null) { return replay; }
        }

        var qr = QrId.Normalise(context.Request.Qr ?? string.Empty);
        var resolution = await qrResolver.ResolveAsync(qr, cancellationToken);
        if (resolution is null)
        {
            return await RecordDenialAsync(
                context, qrIdAtScan: qr, denialCtx: DenialContext.Empty,
                direction: ColdStartDirection(snapshot),
                reason: DenialReasonCode.QrUnknown,
                requestHash, idempotencyKey, cancellationToken);
        }

        var denialCtx = DenialContext.From(resolution);
        var coldStart = ColdStartDirection(snapshot);

        // Steps 5–9: per-row predicate → denial reason, ordered. Step 9.5
        // (time-window) and step 11.5 (booking-required) are reserved hooks
        // for later increments — no rows here today.
        var simpleChecks = new (bool failed, DenialReasonCode reason)[]
        {
            (!snapshot.IsActive,                              DenialReasonCode.GateInactiveAtScan),
            (resolution.AccountState != AccountState.Approved
                && resolution.AccountState != AccountState.Disabled,
                                                              DenialReasonCode.HolderNotApproved),
            (resolution.AccountState == AccountState.Disabled, DenialReasonCode.HolderDisabled),
            (resolution.IsLockedOut,                          DenialReasonCode.HolderLocked),
            (resolution.ProfileTypeId is not null && !resolution.ProfileTypeActive,
                                                              DenialReasonCode.ProfileTypeInactive),
        };
        foreach (var (failed, reason) in simpleChecks)
        {
            if (!failed) { continue; }
            return await RecordDenialAsync(context, qrIdAtScan: qr, denialCtx,
                direction: coldStart, reason: reason,
                requestHash, idempotencyKey, cancellationToken);
        }

        // Step 12 — 5-second duplicate-absorption window keyed (GateId,
        // UserProfileId). Run BEFORE the allow-list and direction queries so
        // an absorbed duplicate skips both. The single query returns enough
        // to satisfy both the duplicate path and the direction inference.
        var windowCutoff = timeProvider.GetUtcNow() - DuplicateWindow;
        var lastAllowed = await appDbContext.GateScans.AsNoTracking()
            .Where(s => s.GateId == context.GateId
                     && s.UserProfileId == resolution.UserProfileId
                     && s.Outcome == ScanOutcome.Allowed)
            .OrderByDescending(s => s.ScannedAtUtc)
            .Select(s => new { s.Id, s.Direction, s.ScannedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);
        if (lastAllowed is not null && lastAllowed.ScannedAtUtc >= windowCutoff)
        {
            var replay = new GateScanResponse(
                lastAllowed.Id, ScanOutcome.Allowed,
                lastAllowed.Direction, lastAllowed.ScannedAtUtc,
                denialCtx.ToProfile(), null, null);
            return new GateScanResult(GateScanResultKind.Recorded, replay, false, null);
        }
        var direction = InferDirection(snapshot.DirectionMode, lastAllowed?.Direction);

        // Step 11 — allow-list. Empty raw list = pass; filtered-empty (L-15)
        // denies all.
        if (snapshot.AllowedProfileTypeIdsRaw.Count > 0
            && (resolution.ProfileTypeId is null
                || !snapshot.AllowedProfileTypeIdsFiltered.Contains(resolution.ProfileTypeId.Value)))
        {
            return await RecordDenialAsync(context, qrIdAtScan: qr, denialCtx,
                direction: direction, reason: DenialReasonCode.ProfileTypeNotAllowed,
                requestHash, idempotencyKey, cancellationToken);
        }

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

    public async Task<GateVisitorsListResult> ListGateVisitorsAsync(
        Guid operatorUserId, Guid gateId, GateVisitorsListRequest request,
        CancellationToken cancellationToken = default)
    {
        // Authority — same pattern as RecordScanAsync: gate must exist and
        // the operator must be assigned to it.
        var snapshot = await configCache.GetAsync(gateId, cancellationToken);
        if (snapshot is null)
        {
            return new GateVisitorsListResult(GateVisitorsListResultKind.GateNotFound, null);
        }
        if (!snapshot.AssignedOperatorUserIds.Contains(operatorUserId))
        {
            return new GateVisitorsListResult(GateVisitorsListResultKind.NotAssigned, null);
        }

        var pageSize = Math.Clamp(request.PageSize > 0 ? request.PageSize : 50, 1, 200);
        var afterId = DecodeCursor(request.Cursor);
        // Default the outcome to Allowed when not specified — that's the
        // "who's currently inside" use case the staff app actually wants.
        var outcome = request.Outcome ?? ScanOutcome.Allowed;

        var query = appDbContext.GateScans.AsNoTracking()
            .Where(s => s.GateId == gateId);
        if (afterId is { } cursorAfter)
        {
            query = query.Where(s => s.Id > cursorAfter);
        }
        query = query.Where(s => s.Outcome == outcome);
        if (request.Direction is { } dir)
        {
            query = query.Where(s => s.Direction == dir);
        }
        if (request.SinceUtc is { } since)
        {
            query = query.Where(s => s.ScannedAtUtc >= since);
        }
        if (request.UntilUtc is { } until)
        {
            query = query.Where(s => s.ScannedAtUtc < until);
        }

        var items = await query
            .OrderBy(s => s.Id)
            .Take(pageSize)
            .Select(s => new GateVisitorListItem(
                s.Id, s.ScannedAtUtc, s.Direction, s.Outcome,
                s.UserProfileId, s.QrIdAtScan,
                // D-158 snapshot columns — no cross-DB JOIN.
                s.ScannedDisplayName, s.ScannedProfileTypeName,
                s.DenialReasonCode))
            .ToListAsync(cancellationToken);

        var nextCursor = items.Count == pageSize
            ? EncodeCursor(items[^1].ScanId)
            : null;

        return new GateVisitorsListResult(
            GateVisitorsListResultKind.Ok,
            new GateVisitorsListResponse(items, nextCursor, timeProvider.GetUtcNow()));
    }

    // D-160 — opaque cursor encoding for the gate-visitors list. Single
    // long-valued cursor (lastSeenScanId); base64 over a tiny JSON blob
    // so the wire format can grow without breaking older clients.
    private static string EncodeCursor(long lastSeenScanId)
    {
        var json = JsonSerializer.Serialize(new { lastId = lastSeenScanId });
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    private static long? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) { return null; }
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.TryGetProperty("lastId", out var prop)
                && prop.TryGetInt64(out var id))
            {
                return id;
            }
        }
        catch
        {
            // A malformed cursor is treated as "no cursor" — the staff app
            // will see the first page again rather than a 400, which is
            // friendlier under the operator-poll loop.
        }
        return null;
    }

    // ---- internals ----

    private GateScanResult Routing(GateScanResultKind kind, string code) =>
        new(kind, EmptyResponse(timeProvider.GetUtcNow()), false, code);

    private static GateScanResponse EmptyResponse(DateTimeOffset scannedAtUtc) =>
        new(0, ScanOutcome.Denied, ScanDirection.CheckIn, scannedAtUtc, null, null, null);

    private static ScanDirection ColdStartDirection(GateConfigSnapshot snapshot) =>
        snapshot.DirectionMode == DirectionMode.Out
            ? ScanDirection.CheckOut : ScanDirection.CheckIn;

    private static ScanDirection InferDirection(DirectionMode mode, ScanDirection? lastDirection)
    {
        if (mode == DirectionMode.In) { return ScanDirection.CheckIn; }
        if (mode == DirectionMode.Out) { return ScanDirection.CheckOut; }
        return lastDirection switch
        {
            ScanDirection.CheckIn => ScanDirection.CheckOut,
            ScanDirection.CheckOut => ScanDirection.CheckIn,
            _ => ScanDirection.CheckIn,
        };
    }

    private async Task<GateScanResult?> TryReplayAsync(
        string idempotencyKey, Guid gateId, string requestHash,
        string? acceptLanguage, CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - IdempotencyRetention;
        var prior = await appDbContext.ScanIdempotencies.AsNoTracking()
            .Where(r => r.Key == idempotencyKey && r.GateId == gateId && r.StoredAt >= cutoff)
            .SingleOrDefaultAsync(cancellationToken);
        if (prior is null) { return null; }
        if (!string.Equals(prior.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Routing(GateScanResultKind.IdempotencyConflict, "IDEMPOTENCY_KEY_CONFLICT");
        }
        var replay = await LoadReplayAsync(prior, acceptLanguage, cancellationToken);
        return new GateScanResult(GateScanResultKind.Recorded, replay, true, null);
    }

    private async Task<GateScanResult> RecordAllowedAsync(
        GateScanContext context, string qr, QrResolution resolution,
        ScanDirection direction, string? requestHash, string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var scan = BuildScan(context, qr, ScanOutcome.Allowed,
            denialReason: null, userProfileId: resolution.UserProfileId,
            direction: direction, now: now, idempotencyKey: idempotencyKey,
            scannedDisplayName: resolution.DisplayName,
            scannedProfileTypeName: resolution.ProfileTypeName);
        appDbContext.GateScans.Add(scan);

        var response = new GateScanResponse(
            0, ScanOutcome.Allowed, direction, now,
            new GateScanUserProfile(
                resolution.UserProfileId, resolution.DisplayName,
                resolution.DisplayNameArabic, resolution.ProfileTypeId,
                resolution.ProfileTypeName, resolution.ProfileTypePageColor),
            null, null);

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && requestHash is not null)
        {
            appDbContext.ScanIdempotencies.Add(new ScanIdempotency
            {
                Key = idempotencyKey,
                GateId = context.GateId,
                RequestHash = requestHash,
                ResponseHash = HashResponse(response),
                ScanId = null,  // back-filled after SaveChanges below
                StoredAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        // ScanId is now populated by the IDENTITY column. Back-fill the
        // idempotency row in the same transaction window.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await appDbContext.ScanIdempotencies
                .Where(r => r.Key == idempotencyKey && r.GateId == context.GateId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ScanId, scan.Id),
                    cancellationToken);
        }

        failureCircuit.RecordAllowed(context.GateId);
        logger.LogInformation(
            "Allowed scan {ScanId} on gate {GateId} for visitor {ProfileId}",
            scan.Id, context.GateId, resolution.UserProfileId);
        return new GateScanResult(GateScanResultKind.Recorded,
            response with { ScanId = scan.Id }, false, null);
    }

    private async Task<GateScanResult> RecordDenialAsync(
        GateScanContext context,
        string qrIdAtScan,
        DenialContext denialCtx,
        ScanDirection direction,
        DenialReasonCode reason,
        string? requestHash, string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var scan = BuildScan(context, qrIdAtScan, ScanOutcome.Denied,
            denialReason: reason, userProfileId: denialCtx.UserProfileId,
            direction: direction, now: now, idempotencyKey: idempotencyKey,
            scannedDisplayName: denialCtx.DisplayName,
            scannedProfileTypeName: denialCtx.ProfileTypeName);
        appDbContext.GateScans.Add(scan);

        var message = MessageFor(reason, context.AcceptLanguage);
        var response = new GateScanResponse(
            0, ScanOutcome.Denied, direction, now,
            denialCtx.ToProfile(),
            reason, message);

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && requestHash is not null)
        {
            appDbContext.ScanIdempotencies.Add(new ScanIdempotency
            {
                Key = idempotencyKey,
                GateId = context.GateId,
                RequestHash = requestHash,
                ResponseHash = HashResponse(response),
                ScanId = null,
                StoredAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await appDbContext.ScanIdempotencies
                .Where(r => r.Key == idempotencyKey && r.GateId == context.GateId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ScanId, scan.Id),
                    cancellationToken);
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.GateScanDenied,
            Outcome = AuditOutcome.Failure,
            ActorUserId = context.OperatorUserId,
            Detail = $"gateId={context.GateId}; reason={reason}; scanId={scan.Id}; corr={context.CorrelationId}",
        }, cancellationToken);
        await failureCircuit.RecordDenialAsync(context.GateId, cancellationToken);

        return new GateScanResult(GateScanResultKind.Recorded,
            response with { ScanId = scan.Id }, false, null);
    }

    private static GateScan BuildScan(
        GateScanContext context, string qrIdAtScan, ScanOutcome outcome,
        DenialReasonCode? denialReason, Guid? userProfileId, ScanDirection direction,
        DateTimeOffset now, string? idempotencyKey,
        string? scannedDisplayName, string? scannedProfileTypeName) =>
        new()
        {
            GateId = context.GateId,
            UserProfileId = userProfileId,
            // D-157 — snapshot the visitor identity at scan time so the
            // log row survives even if the linked Identity-DB row is
            // later deleted or renamed.
            ScannedDisplayName = scannedDisplayName,
            ScannedProfileTypeName = scannedProfileTypeName,
            QrIdAtScan = qrIdAtScan,
            Direction = direction,
            Outcome = outcome,
            DenialReasonCode = denialReason,
            ScannedAtUtc = now,
            ClientScannedAtUtc = context.Request.ClientScannedAtUtc,
            ScannedByUserId = context.OperatorUserId,
            Source = context.Request.Source,
            CorrelationId = context.CorrelationId,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent,
            IdempotencyKey = idempotencyKey,
        };

    private async Task<GateScanResponse> LoadReplayAsync(
        ScanIdempotency prior, string? acceptLanguage,
        CancellationToken cancellationToken)
    {
        if (prior.ScanId is null) { return EmptyResponse(prior.StoredAt); }
        var scan = await appDbContext.GateScans.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == prior.ScanId.Value, cancellationToken);
        if (scan is null) { return EmptyResponse(prior.StoredAt); }

        GateScanUserProfile? profile = null;
        if (scan.UserProfileId is { } pid)
        {
            profile = await (
                from row in identityDbContext.UserProfiles.AsNoTracking()
                join user in identityDbContext.Users.AsNoTracking() on row.UserId equals user.Id
                where row.Id == pid
                select new GateScanUserProfile(
                    row.Id, user.DisplayName ?? string.Empty, row.ArabicName,
                    row.ProfileTypeId,
                    row.ProfileType != null ? row.ProfileType.Name : null,
                    row.ProfileType != null ? row.ProfileType.PageColor : null))
                .SingleOrDefaultAsync(cancellationToken);
        }

        string? message = scan.DenialReasonCode is { } reason
            ? MessageFor(reason, acceptLanguage) : null;
        return new GateScanResponse(
            scan.Id, scan.Outcome, scan.Direction, scan.ScannedAtUtc,
            profile, scan.DenialReasonCode, message);
    }

    private static string MessageFor(DenialReasonCode reason, string? acceptLanguage)
    {
        var (en, ar) = DenialMessages(reason);
        return string.Equals(acceptLanguage, ArabicLanguageCode, StringComparison.OrdinalIgnoreCase)
            ? ar : en;
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

    private static string HashRequest(Guid gateId, GateScanRequest request, string idempotencyKey) =>
        OpaqueToken.Hash($"{gateId:N}|{idempotencyKey}|{request.Qr}|{request.Source}");

    private static string HashResponse(GateScanResponse response) =>
        OpaqueToken.Hash(JsonSerializer.Serialize(response));

    /// <summary>The bundle of holder-derived display fields a denial response
    /// echoes back. Built once from a <see cref="QrResolution"/>; passed to
    /// every denial path so the response payload is consistent.</summary>
    private readonly struct DenialContext(
        Guid? userProfileId, string? displayName, string? displayNameArabic,
        Guid? profileTypeId, string? profileTypeName, string? profileTypePageColor)
    {
        public static readonly DenialContext Empty = default;

        public Guid? UserProfileId { get; } = userProfileId;
        public string? DisplayName { get; } = displayName;
        public string? DisplayNameArabic { get; } = displayNameArabic;
        public Guid? ProfileTypeId { get; } = profileTypeId;
        public string? ProfileTypeName { get; } = profileTypeName;
        public string? ProfileTypePageColor { get; } = profileTypePageColor;

        public static DenialContext From(QrResolution r) =>
            new(r.UserProfileId, r.DisplayName, r.DisplayNameArabic,
                r.ProfileTypeId, r.ProfileTypeName, r.ProfileTypePageColor);

        public GateScanUserProfile? ToProfile() =>
            UserProfileId is null
                ? null
                : new GateScanUserProfile(
                    UserProfileId.Value, DisplayName ?? string.Empty,
                    DisplayNameArabic ?? string.Empty, ProfileTypeId,
                    ProfileTypeName, ProfileTypePageColor);
    }
}
