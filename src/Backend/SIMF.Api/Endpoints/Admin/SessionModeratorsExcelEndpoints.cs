// Tests: SIMF.Api.Tests/SessionModeratorsExcelTests.cs
using System.Globalization;
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/session-moderators/export</c> — the D-356 grid export
/// for the per-session moderator-grants desk (D-169, gap doc G6). <b>Export only:</b>
/// grants are created in place via assign/revoke (composite key SessionId+UserId),
/// so there is no import path. The list is <b>not parent-scoped</b> — it delegates
/// to the same <see cref="IAdminSessionModeratorService.ListAllAsync"/> the list
/// endpoint uses, so the whole filtered set (or the selected rows) is exported.
/// </summary>
public sealed class ExportSessionModeratorsEndpoint(IAdminSessionModeratorService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminSessionModeratorRow>(exporter)
{
    protected override string RoutePath => "/admin/session-moderators/export";
    protected override string Permission => PermissionCatalog.SessionModerators.Export;
    protected override string SheetName => "SessionModerators";
    protected override string FilePrefix => "simf-session-moderators";

    protected override IReadOnlyList<GridExcelColumn<AdminSessionModeratorRow>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminSessionModeratorRow>> _columns =
    [
        new("SessionCode", row => row.SessionCode),
        new("SessionTitle", row => row.SessionTitle),
        new("SessionTitleArabic", row => row.SessionTitleArabic),
        new("Moderator", row => row.ModeratorDisplayName),
        new("Email", row => row.ModeratorEmail),
        new("AssignedBy", row => row.AssignedByDisplayName),
        new("AssignedAt", row => row.AssignedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)),
    ];

    protected override async Task<IReadOnlyList<AdminSessionModeratorRow>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    // Composite-key grant (SessionId, UserId); the moderator's UserId is the
    // grid's selectable identity for a selected-rows export.
    protected override Guid IdOf(AdminSessionModeratorRow row) => row.UserId;
}
