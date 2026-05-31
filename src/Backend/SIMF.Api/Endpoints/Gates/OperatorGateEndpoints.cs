// Tests: SIMF.Api.Tests/GateScanTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.AccessControl;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Gates;

namespace SIMF.Api.Endpoints.Gates;

public sealed class MyAssignmentsEndpoint(IGateOperatorService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<OperatorGateAssignment>>>
{
    public override void Configure()
    {
        Get("/gates/my-assignments");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount),
                 PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Operate));
        Tags("Gates");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var operatorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<IReadOnlyList<OperatorGateAssignment>>.Ok(
            await service.ListMyAssignmentsAsync(operatorId, ct)), ct);
    }
}

public sealed class PostScanRequest
{
    public Guid GateId { get; set; }
    public string Qr { get; set; } = string.Empty;
    public DateTimeOffset? ClientScannedAtUtc { get; set; }
    public string? IdempotencyKey { get; set; }
    public SIMF.Common.Enums.ScanSource Source { get; set; }
        = SIMF.Common.Enums.ScanSource.MobileApp;
}

public sealed class PostScanEndpoint(IGateOperatorService service)
    : Endpoint<PostScanRequest, ApiResult<GateScanResponse>>
{
    public override void Configure()
    {
        Post("/gates/{gateId:guid}/scans");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount),
                 PermissionCatalog.PolicyFor(PermissionCatalog.Gates.Operate));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Gates");
    }
    public override async Task HandleAsync(PostScanRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var operatorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

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
                ClientScannedAtUtc = req.ClientScannedAtUtc,
                IdempotencyKey = req.IdempotencyKey,
                Source = req.Source,
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
            case GateScanResultKind.GateInactive:
                throw new ApiException(ErrorCodes.GateInactive, 503,
                    "This gate is currently inactive.",
                    "هذه البوابة غير نشطة حالياً.");
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

// D-160 — `POST /gates/{gateId}/visitors/list` request. GateId binds
// from the route; the rest matches the SIMF.Contracts.Gates contract.
public sealed class PostGateVisitorsListRequest
{
    public Guid GateId { get; set; }
    public string? Cursor { get; set; }
    public int PageSize { get; set; }
    public SIMF.Common.Enums.ScanDirection? Direction { get; set; }
    public SIMF.Common.Enums.ScanOutcome? Outcome { get; set; }
    public DateTimeOffset? SinceUtc { get; set; }
    public DateTimeOffset? UntilUtc { get; set; }
}

public sealed class PostGateVisitorsListEndpoint(IGateOperatorService service)
    : Endpoint<PostGateVisitorsListRequest, ApiResult<GateVisitorsListResponse>>
{
    public override void Configure()
    {
        Post("/gates/{gateId:guid}/visitors/list");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount),
                 PermissionCatalog.PolicyFor(PermissionCatalog.Gates.ViewOwnReports));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Gates");
    }
    public override async Task HandleAsync(
        PostGateVisitorsListRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var operatorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var serviceRequest = new GateVisitorsListRequest
        {
            Cursor = req.Cursor,
            PageSize = req.PageSize,
            Direction = req.Direction,
            Outcome = req.Outcome,
            SinceUtc = req.SinceUtc,
            UntilUtc = req.UntilUtc,
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
        Get("/gates/my-reports/today");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount),
                 PermissionCatalog.PolicyFor(PermissionCatalog.Gates.ViewOwnReports));
        Tags("Gates");
    }
    public override async Task HandleAsync(MyDailyReportRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var operatorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<OperatorDailyReport>.Ok(
            await service.GetMyDailyReportAsync(operatorId, req.GateId, ct)), ct);
    }
}
