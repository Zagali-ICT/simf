// Tests: SIMF.Api.Tests/GateScanTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.AccessControl;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Contracts.Gates;

namespace SIMF.Api.Endpoints.Gates;

public sealed class MyAssignmentsEndpoint(IGateOperatorService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<OperatorGateAssignment>>>
{
    public override void Configure()
    {
        Get("/app/gates/my-assignments");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount),
                 PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Operate));
        Tags("Gates");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var operatorId = User.ActorId();
        await Send.OkAsync(ApiResult<IReadOnlyList<OperatorGateAssignment>>.Ok(
            await service.ListMyAssignmentsAsync(operatorId, ct)), ct);
    }
}

/// <summary>
/// <c>GET /app/gates/offline-config</c>. The snapshot a scanner caches
/// so it can judge a badge with no network.
///
/// <para>Its own endpoint rather than a field on <c>my-assignments</c> because
/// it carries the badge key: a secret should be visible in the access log as its
/// own call, and be revocable without also breaking the assignment list.</para>
/// </summary>
public sealed class GateOfflineConfigEndpoint(IGateOperatorService service)
    : EndpointWithoutRequest<ApiResult<GateOfflineConfig>>
{
    public override void Configure()
    {
        Get("/app/gates/offline-config");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount),
                 PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Operate));
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Tags("Gates");
        Summary(summary => summary.Summary =
            "Offline scanning rules + badge key for this operator's gates.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var operatorId = User.ActorId();
        await Send.OkAsync(ApiResult<GateOfflineConfig>.Ok(
            await service.GetOfflineConfigAsync(operatorId, ct)), ct);
    }
}

public sealed class PostScanRequest
{
    public Guid GateId { get; set; }
    public string Qr { get; set; } = string.Empty;
    public DateTime? ClientScannedAt { get; set; }
    public string? IdempotencyKey { get; set; }
    public SIMF.Common.Enums.ScanSource Source { get; set; }
        = SIMF.Common.Enums.ScanSource.MobileApp;

    /// <summary>The operator's دخول/خروج choice (honoured only for a
    /// Both-mode gate; fixed In/Out gates ignore it). Null = server infers.</summary>
    public SIMF.Common.Enums.ScanDirection? Direction { get; set; }
}

public sealed class PostScanEndpoint(IGateOperatorService service)
    : Endpoint<PostScanRequest, ApiResult<GateScanResponse>>
{
    public override void Configure()
    {
        Post("/app/gates/{gateId:guid}/scans");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount),
                 PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Operate));
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Tags("Gates");
    }
    public override async Task HandleAsync(PostScanRequest req, CancellationToken ct)
    {
        var operatorId = User.ActorId();

        var headerKey = HttpContext.Request.Headers.TryGetValue(
            GateProtocol.IdempotencyKeyHeader, out var hk) ? hk.ToString() : null;
        var acceptLang = HttpContext.Request.Headers.AcceptLanguage.ToString();

        var context = new GateScanContext
        {
            GateId = req.GateId,
            OperatorUserId = operatorId,
            Request = new GateScanRequest
            {
                Qr = req.Qr,
                ClientScannedAt = req.ClientScannedAt,
                IdempotencyKey = req.IdempotencyKey,
                Source = req.Source,
                RequestedDirection = req.Direction,
            },
            CorrelationId = HttpContext.TraceIdentifier,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = HttpContext.Request.Headers.UserAgent.ToString(),
            AcceptLanguage = acceptLang.Contains("ar",
                StringComparison.OrdinalIgnoreCase) ? "ar" : "en",
            HeaderIdempotencyKey = headerKey,
        };

        var result = await service.RecordScanAsync(context, ct);

        switch (result.Kind)
        {
            case GateScanResultKind.GateNotFound:
                throw new ApiException(ErrorCodes.GateNotFound, 404,
                    "The gate was not found.",
                    "لم يتم العثور على البوابة.");
            case GateScanResultKind.NotAssigned:
                throw new ApiException(ErrorCodes.GateOperatorNotAssigned, 403,
                    "You are not assigned to this gate.",
                    "أنت غير معيّن لهذه البوابة.");
            // DEF-STF-008 — there is deliberately no GATE_INACTIVE (503) arm: an
            // inactive gate is a RECORDED denial at HTTP 200
            // (DenialReasonCode.GateInactiveAtScan), so the attempt still lands
            // in the append-only GateScan audit trail and the operator gets the
            // designed denial card instead of an envelope failure.
            case GateScanResultKind.IdempotencyConflict:
                throw new ApiException(ErrorCodes.IdempotencyKeyConflict, 409,
                    "An idempotency key was reused with a different payload.",
                    "تم إعادة استخدام مفتاح idempotency بحمولة مختلفة.");
            case GateScanResultKind.CircuitOpen:
                HttpContext.Response.Headers[GateProtocol.FailureCircuitHeader] =
                    GateProtocol.FailureCircuitOpenValue;
                throw new ApiException(ErrorCodes.GateFailureCircuitOpen, 429,
                    "This gate is temporarily refusing scans due to a failure-rate threshold.",
                    "هذه البوابة ترفض المسح مؤقتاً بسبب تجاوز عتبة معدل الفشل.");
        }

        if (result.IsIdempotentReplay)
        {
            HttpContext.Response.Headers[GateProtocol.IdempotentReplayHeader] = "true";
        }
        await Send.OkAsync(ApiResult<GateScanResponse>.Ok(result.Response), ct);
    }
}

// `POST /gates/{gateId}/visitors/list` request. GateId binds
// from the route; the rest matches the SIMF.Contracts.Gates contract.
public sealed class PostGateVisitorsListRequest
{
    public Guid GateId { get; set; }
    public string? Cursor { get; set; }
    public int PageSize { get; set; }
    public SIMF.Common.Enums.ScanDirection? Direction { get; set; }
    public SIMF.Common.Enums.ScanOutcome? Outcome { get; set; }
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
}

public sealed class PostGateVisitorsListEndpoint(IGateOperatorService service)
    : Endpoint<PostGateVisitorsListRequest, ApiResult<GateVisitorsListResponse>>
{
    public override void Configure()
    {
        Post("/app/gates/{gateId:guid}/visitors/list");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount),
                 PermissionCatalog.PolicyFor(PermissionCatalog.Gates.ViewOwnReports));
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Tags("Gates");
    }
    public override async Task HandleAsync(
        PostGateVisitorsListRequest req, CancellationToken ct)
    {
        var operatorId = User.ActorId();
        var serviceRequest = new GateVisitorsListRequest
        {
            Cursor = req.Cursor,
            PageSize = req.PageSize,
            Direction = req.Direction,
            Outcome = req.Outcome,
            Since = req.Since,
            Until = req.Until,
        };
        var result = await service.ListGateVisitorsAsync(
            operatorId, req.GateId, serviceRequest, ct);
        switch (result.Kind)
        {
            case GateVisitorsListResultKind.GateNotFound:
                throw new ApiException(ErrorCodes.GateNotFound, 404,
                    "The gate was not found.",
                    "لم يتم العثور على البوابة.");
            case GateVisitorsListResultKind.NotAssigned:
                throw new ApiException(ErrorCodes.GateOperatorNotAssigned, 403,
                    "You are not assigned to this gate.",
                    "أنت غير معيّن لهذه البوابة.");
        }
        await Send.OkAsync(ApiResult<GateVisitorsListResponse>.Ok(result.Response!), ct);
    }
}

public sealed class MyDailyReportRequest { public Guid? GateId { get; set; } }

public sealed class MyDailyReportEndpoint(IGateOperatorService service)
    : Endpoint<MyDailyReportRequest, ApiResult<OperatorDailyReport>>
{
    public override void Configure()
    {
        Get("/app/gates/my-reports/today");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount),
                 PermissionCatalog.PolicyFor(PermissionCatalog.Gates.ViewOwnReports));
        Tags("Gates");
    }
    public override async Task HandleAsync(MyDailyReportRequest req, CancellationToken ct)
    {
        var operatorId = User.ActorId();
        await Send.OkAsync(ApiResult<OperatorDailyReport>.Ok(
            await service.GetMyDailyReportAsync(operatorId, req.GateId, ct)), ct);
    }
}
