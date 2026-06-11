// Tests: SIMF.Api.Tests/BookingsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.SeatReservations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/bookings/export</c> — the D-356 grid export for the
/// P2.2 (D-227) booking approval queue (FDS-005 §5.2). <b>Export only:</b>
/// bookings are created by visitors in the app and approved/rejected from the
/// CP — there is no bulk import, so no generic import endpoint is added here.
/// Columns mirror the CP grid (session + start + seat + attendee + booked-at);
/// the seat is split into its row label + number and the seat-kind enum is
/// resolved to its text. Lists via the SAME service method the list endpoint
/// uses (<see cref="ISeatReservationService.ListPendingBookingsAsync"/>), and
/// honours a selected-ids export via the booking's <c>ReservationId</c>.
/// </summary>
public sealed class ExportBookingsEndpoint(ISeatReservationService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<BookingQueueRow>(exporter)
{
    protected override string RoutePath => "/admin/bookings/export";
    protected override string Permission => PermissionCatalog.Bookings.Export;
    protected override string SheetName => "Bookings";
    protected override string FilePrefix => "simf-bookings";

    protected override IReadOnlyList<GridExcelColumn<BookingQueueRow>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<BookingQueueRow>> _columns =
    [
        new("SessionTitle", row => row.SessionTitle),
        new("SessionTitleArabic", row => row.SessionTitleArabic),
        new("SessionStart", row => row.SessionStartUtc.UtcDateTime),
        new("Row", row => row.RowLabel),
        new("Seat", row => row.SeatNumber),
        new("Kind", row => row.Kind.ToString()),
        new("Attendee", row => row.AttendeeName),
        new("BookedAt", row => row.CreatedAt.UtcDateTime),
    ];

    protected override async Task<IReadOnlyList<BookingQueueRow>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListPendingBookingsAsync(query, ct)).Items;

    protected override Guid IdOf(BookingQueueRow row) => row.ReservationId;
}
