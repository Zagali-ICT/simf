// Tests: SIMF.Api.Tests/LogsEndpointsTests.cs (todo).
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Auditing;
using SIMF.Application.Logs;
using SIMF.Common;
using SIMF.Contracts.Logs;
using SIMF.Domain.Auditing;

using SIMF.Common.Enums;

namespace SIMF.Api.Endpoints.Admin.Logs;

/// <summary>
/// <c>GET /api/v1/admin/logs/list</c> — one envelope with every project's
/// log files, used to populate the CP /admin/logs dropdowns (P6). Administrator
/// role required.
/// </summary>
public sealed class ListLogsEndpoint(ILogFileService logs) : EndpointWithoutRequest<ApiResult<LogListResponse>>
{
    public override void Configure()
    {
        Get("/admin/logs/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "List per-project log files. Requires Administrator role.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await logs.ListAsync(ct);
        await Send.OkAsync(ApiResult<LogListResponse>.Ok(response), ct);
    }
}

/// <summary>
/// <c>GET /api/v1/admin/logs/tail?project=X&amp;file=Y&amp;lines=N</c> — the
/// last <c>lines</c> of a file, used by the CP page's polling loop. Audits one
/// <c>Admin.LogViewed</c> entry per request.
/// </summary>
public sealed class TailLogRequest
{
    public string Project { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int Lines { get; set; } = 500;
}

public sealed class TailLogEndpoint(ILogFileService logs, IAuditLog auditLog)
    : Endpoint<TailLogRequest, ApiResult<LogTailResponse>>
{
    public override void Configure()
    {
        Get("/admin/logs/tail");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return the last N lines of one log file. Requires Administrator role.");
    }

    public override async Task HandleAsync(TailLogRequest req, CancellationToken ct)
    {
        var tail = await logs.TailAsync(req.Project, req.File, req.Lines, ct);
        if (tail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AdminLogViewed,
                Outcome = AuditOutcome.Success,
                Detail = $"{tail.Project}/{tail.FileName}",
            },
            ct);

        await Send.OkAsync(ApiResult<LogTailResponse>.Ok(tail), ct);
    }
}

/// <summary>
/// <c>GET /api/v1/admin/logs/download?project=X&amp;file=Y</c> — streams the
/// full file as <c>text/plain</c> with a <c>Content-Disposition</c> attachment.
/// Audits one <c>Admin.LogDownloaded</c> entry.
/// </summary>
public sealed class DownloadLogRequest
{
    public string Project { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
}

public sealed class DownloadLogEndpoint(ILogFileService logs, IAuditLog auditLog)
    : Endpoint<DownloadLogRequest>
{
    public override void Configure()
    {
        Get("/admin/logs/download");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Download one full log file. Requires Administrator role.");
    }

    public override async Task HandleAsync(DownloadLogRequest req, CancellationToken ct)
    {
        var stream = logs.OpenRead(req.Project, req.File);
        if (stream is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AdminLogDownloaded,
                Outcome = AuditOutcome.Success,
                Detail = $"{req.Project}/{req.File}",
            },
            ct);

        var safeFileName = Path.GetFileName(req.File);
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{safeFileName}\"";
        await using (stream)
        {
            await Send.StreamAsync(stream, contentType: "text/plain", cancellation: ct);
        }
    }
}
