// Tests: SIMF.Api.Tests/CommentsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.SessionComments.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/comments-moderation/export</c> — the D-356 grid export
/// for the audience-comments moderation desk (Mockup page 28). <b>Export only:</b>
/// comments are submitted by the audience and moderated in place, so there is no
/// import path. The moderation list is <b>session-scoped</b>
/// (<see cref="IAdminSessionCommentService.ListAsync"/> takes a session id), so
/// the CP passes the picked session id in <c>Query.Filters["sessionId"]</c> and
/// this subclass resolves it before delegating to the same service the list
/// endpoint uses; with no (or an unparseable) session id it exports nothing.
/// </summary>
public sealed class ExportCommentsEndpoint(IAdminSessionCommentService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<SessionCommentModerationRow>(exporter)
{
    /// <summary>The grid query filter key the CP uses to carry the picked session id.</summary>
    private const string SessionIdFilterKey = "sessionId";

    protected override string RoutePath => "/admin/comments-moderation/export";
    protected override string Permission => PermissionCatalog.Comments.Export;
    protected override string SheetName => "Comments";
    protected override string FilePrefix => "simf-comments";

    protected override IReadOnlyList<GridExcelColumn<SessionCommentModerationRow>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<SessionCommentModerationRow>> _columns =
    [
        new("Author", row => row.AuthorDisplayName),
        new("Email", row => row.AuthorEmail),
        new("Body", row => row.Body),
        new("Status", row => StatusText(row.Status)),
        new("AiVerdict", row => row.AiFilterVerdict),
        new("Created", row => row.CreatedAt),
    ];

    protected override async Task<IReadOnlyList<SessionCommentModerationRow>> ListAsync(
        GridQuery query, CancellationToken ct)
    {
        if (!query.Filters.TryGetValue(SessionIdFilterKey, out var raw)
            || !Guid.TryParse(raw, out var sessionId))
        {
            // The CP only enables Export once a session is picked; defend the
            // endpoint anyway — no session means nothing to export.
            return Array.Empty<SessionCommentModerationRow>();
        }

        // Export every status (Pending + Approved + Hidden) for the session,
        // mirroring the moderation list's default (status = null).
        return (await service.ListAsync(sessionId, status: null, query, ct)).Items;
    }

    protected override Guid IdOf(SessionCommentModerationRow row) => row.Id;

    // Resolve the moderation status enum to display text for the workbook cell.
    private static string StatusText(SessionCommentStatus status) => status switch
    {
        SessionCommentStatus.Approved => "Approved",
        SessionCommentStatus.Hidden => "Hidden",
        _ => "Pending",
    };
}
