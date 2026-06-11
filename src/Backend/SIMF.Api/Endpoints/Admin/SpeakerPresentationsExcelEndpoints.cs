// Tests: SIMF.Api.Tests/SpeakerPresentationsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/speaker-presentations/export</c> — the D-356 grid export
/// for the speaker presentation-file list (P2.3 / D-228, FR-407). <b>Export only:</b>
/// no generic import is added (presentations are binary files uploaded one at a
/// time, not flat rows).
/// <para>
/// The presentations grid is <b>master-detail</b>: it always shows the files of one
/// selected speaker, listed via <see cref="IAdminSpeakerPresentationService.ListForSpeakerAsync"/>
/// (a per-speaker, non-paged GET — there is no <see cref="GridQuery"/> list endpoint
/// for it). So the export carries the chosen speaker id through
/// <c>Query.Filters["speakerId"]</c>; the CP page (mirroring the grid's "one speaker
/// at a time" contract) sets that filter on export. With no speaker id the export is
/// empty — the same as the grid before a speaker is picked.
/// </para>
/// Reuses the existing <see cref="PermissionCatalog.Speakers.Export"/> permission
/// (this page is gated by the Speakers.* surface, like its list/download/delete).
/// </summary>
public sealed class ExportSpeakerPresentationsEndpoint(
    IAdminSpeakerPresentationService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminSpeakerPresentationRow>(exporter)
{
    /// <summary>The grid query key carrying the selected speaker id (set by the CP page).</summary>
    public const string SpeakerFilterKey = "speakerId";

    protected override string RoutePath => "/admin/speaker-presentations/export";
    protected override string Permission => PermissionCatalog.Speakers.Export;
    protected override string SheetName => "SpeakerPresentations";
    protected override string FilePrefix => "simf-speaker-presentations";

    protected override IReadOnlyList<GridExcelColumn<AdminSpeakerPresentationRow>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminSpeakerPresentationRow>> _columns =
    [
        new("FileName", row => row.FileName),
        new("Session", row => row.SessionTitle),
        new("SessionArabic", row => row.SessionTitleArabic),
        new("ContentType", row => row.ContentType),
        new("SizeBytes", row => row.SizeBytes),
        new("CreatedAt", row => row.CreatedAt),
    ];

    // The presentations list has no GridQuery contract — it is keyed by speaker. The
    // base passes the request's Query through; we read the speaker id from its filters
    // and list that speaker's files (the same source the grid renders). No speaker id
    // => nothing to export.
    protected override async Task<IReadOnlyList<AdminSpeakerPresentationRow>> ListAsync(
        GridQuery query, CancellationToken ct)
    {
        if (!query.Filters.TryGetValue(SpeakerFilterKey, out var raw)
            || !Guid.TryParse(raw, out var speakerId))
        {
            return [];
        }
        return await service.ListForSpeakerAsync(speakerId, ct);
    }

    protected override Guid IdOf(AdminSpeakerPresentationRow row) => row.Id;
}
