// Tests: SIMF.Api.Tests/InvitationsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/invitations/export</c> — the grid export for the
/// public-relations invitation desk. <b>Export only:</b> invitations are
/// created/edited from the CP forms, so no generic import endpoint is added here.
/// Columns mirror the grid; the recipient is rendered by English name (the
/// summary already carries both names) and the state enum is resolved to its
/// text. Lists via the SAME service method the list endpoint uses
/// (<see cref="IAdminInvitationService.ListAllAsync"/>).
/// </summary>
public sealed class ExportInvitationsEndpoint(IAdminInvitationService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminInvitationSummary>(exporter)
{
    protected override string RoutePath => "/admin/invitations/export";
    protected override string Permission => PermissionCatalog.Invitations.Export;
    protected override string SheetName => "Invitations";
    protected override string FilePrefix => "simf-invitations";

    protected override IReadOnlyList<GridExcelColumn<AdminInvitationSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminInvitationSummary>> _columns =
    [
        new("RecipientEnglishName", row => row.RecipientEnglishName),
        new("RecipientArabicName", row => row.RecipientArabicName),
        new("ProfileType", row => row.RecipientProfileTypeName ?? string.Empty),
        new("Email", row => row.RecipientEmail ?? string.Empty),
        new("State", row => row.State.ToString()),
        new("Notes", row => row.Notes ?? string.Empty),
        new("SentBy", row => row.SentByDisplayName),
        new("CreatedAt", row => row.CreatedAt),
        new("RespondedAt", row => row.RespondedAt.HasValue ? row.RespondedAt.Value : (DateTime?)null),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminInvitationSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminInvitationSummary row) => row.Id;
}
