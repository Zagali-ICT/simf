// Tests: SIMF.Api.Tests/QuestionQueueExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/questions/export</c> — the D-356 grid export for the
/// Scientific-Committee central Q&amp;A queue (P3.3, Mockup page 26). <b>Export
/// only:</b> questions are submitted by the audience and moderated in place
/// (approve / hide / escalate), so there is no import path. The CP queue page is
/// <b>not</b> scoped by a parent picked on the page — it lists the full
/// cross-session Pending queue — so the export delegates to the same service the
/// list endpoint uses with the same defaults (status = Pending, all sessions).
/// </summary>
public sealed class ExportQuestionQueueEndpoint(ISessionQuestionCommitteeService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<SessionQuestionQueueRow>(exporter)
{
    protected override string RoutePath => "/admin/questions/export";
    protected override string Permission => PermissionCatalog.Questions.Export;
    protected override string SheetName => "Questions";
    protected override string FilePrefix => "simf-questions";

    protected override IReadOnlyList<GridExcelColumn<SessionQuestionQueueRow>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<SessionQuestionQueueRow>> _columns =
    [
        new("Session", row => row.SessionTitle),
        new("Question", row => row.QuestionText),
        new("Submitter", row => row.SubmittedByDisplayName),
        new("Email", row => row.SubmittedByEmail),
        new("Phase", row => PhaseText(row.Phase)),
        new("Status", row => StatusText(row.Status)),
        new("AiVerdict", row => row.AiFilterVerdict),
        new("AssignedToRole", row => row.AssignedToRole),
        new("Created", row => row.CreatedAt),
    ];

    protected override async Task<IReadOnlyList<SessionQuestionQueueRow>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        // Mirror the CP queue's load: the default Pending queue across all
        // sessions (the page passes no status / session filter). The grid pages /
        // filters / sorts this list client-side, so there is no server GridQuery
        // to forward.
        await service.ListQueueAsync(status: null, sessionId: null, ct);

    protected override Guid IdOf(SessionQuestionQueueRow row) => row.Id;

    // Resolve the pipeline enums to display text for the workbook cells.
    private static string PhaseText(QuestionPhase phase) => phase switch
    {
        QuestionPhase.Pre => "Pre",
        _ => "Live",
    };

    private static string StatusText(QuestionStatus status) => status switch
    {
        QuestionStatus.Approved => "Approved",
        QuestionStatus.Hidden => "Hidden",
        // DEF-MOD-001 — the moderator's persisted "answered on stage" state.
        QuestionStatus.Answered => "Answered",
        _ => "Pending",
    };
}
