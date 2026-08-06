// Tests: SIMF.Api.Tests/VipRosterTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>GET /api/v1/admin/visitors/vip/roster</c>. The JSON feed of
/// the VVIP/VIP welcome roster the موج (Mawj) integration / technical teams
/// consume. Gated by the dedicated <see cref="PermissionCatalog.Visitors.ExportVip"/>
/// permission because it exposes VIP PII for external sharing.
///
/// <para><b>Distinct from the PR VIP guest-list export</b>
/// (<c>POST /admin/vips/export</c>, perm <c>Vips.Export</c>): that is the
/// public-relations "كبار الضيوف" list (basic profile columns). This roster adds
/// the موج welcome-message fields (Mawj id, honorific, preferred language, the
/// welcome-photo flag) for the integration. The two are complementary, not
/// duplicates.</para>
/// </summary>
public sealed class GetVipRosterEndpoint(IVipRosterService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<VipRosterRow>>>
{
    public override void Configure()
    {
        Get("/admin/visitors/vip/roster");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ExportVip), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "The VVIP/VIP welcome roster (موج) as JSON.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await service.GetRosterAsync(ct);
        await Send.OkAsync(ApiResult<IReadOnlyList<VipRosterRow>>.Ok(rows), ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/visitors/vip/roster/list</c>. One page of
/// the roster for the CP export grid (SimfDataGrid), with the standard
/// <see cref="GridQuery"/> applied in memory (the roster is small). Same
/// <see cref="PermissionCatalog.Visitors.ExportVip"/> gate.
/// </summary>
public sealed class ListVipRosterEndpoint(IVipRosterService service)
    : Endpoint<GridQuery, ApiResult<GridPage<VipRosterRow>>>
{
    public override void Configure()
    {
        Post("/admin/visitors/vip/roster/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ExportVip), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "One page of the VVIP/VIP welcome roster (موج) for the export grid.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await service.GetRosterPageAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<VipRosterRow>>.Ok(page), ct);
    }
}

/// <summary>
/// <c>GET /api/v1/admin/visitors/vip/roster/export?format=csv|xlsx</c>.
/// Streams the roster as a downloadable CSV (default) or Excel file for the موج
/// teams. Same <see cref="PermissionCatalog.Visitors.ExportVip"/> gate.
/// </summary>
public sealed class ExportVipRosterEndpoint(IVipRosterService service)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/admin/visitors/vip/roster/export");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ExportVip), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Download the VVIP/VIP welcome roster (موج) as CSV or Excel.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var formatRaw = (Query<string>("format", isRequired: false) ?? "csv").Trim().ToLowerInvariant();
        var format = formatRaw == "xlsx"
            ? VipRosterExportFormat.Xlsx
            : VipRosterExportFormat.Csv;

        var file = await service.ExportAsync(format, ct);
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{file.FileName}\"";
        await Send.BytesAsync(
            file.Content, file.FileName, contentType: file.ContentType, cancellation: ct);
    }
}
