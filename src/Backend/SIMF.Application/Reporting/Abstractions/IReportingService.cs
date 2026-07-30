using SIMF.Contracts.Reporting;

namespace SIMF.Application.Reporting.Abstractions;

/// <summary>
/// The Control Panel reporting module: server-paged, date-ranged, read-only
/// views over records the other contexts own. Reporting owns no data of its own
/// and never writes.
///
/// <para>Each report has a list method and an export method. The export returns
/// the XLSX bytes for the <b>whole filtered set</b>, not the visible page, so a
/// report someone paged through still exports completely.</para>
///
/// <para>Date ranges arrive as Saudi calendar dates and are inclusive on both
/// ends; the implementation resolves them to UTC instants.</para>
/// </summary>
public interface IReportingService
{
    Task<ReportPage<AttendanceReportRow>> GetAttendanceAsync(
        ReportQuery query, CancellationToken cancellationToken = default);

    Task<byte[]> ExportAttendanceAsync(
        ReportQuery query, CancellationToken cancellationToken = default);

    Task<ReportPage<RegistrationReportRow>> GetRegistrationsAsync(
        ReportQuery query, CancellationToken cancellationToken = default);

    Task<byte[]> ExportRegistrationsAsync(
        ReportQuery query, CancellationToken cancellationToken = default);

    Task<ReportPage<GateActivityReportRow>> GetGateActivityAsync(
        ReportQuery query, CancellationToken cancellationToken = default);

    Task<byte[]> ExportGateActivityAsync(
        ReportQuery query, CancellationToken cancellationToken = default);
}
