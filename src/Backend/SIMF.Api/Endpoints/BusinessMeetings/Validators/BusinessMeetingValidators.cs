using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.BusinessMeetings;

namespace SIMF.Api.Endpoints.BusinessMeetings.Validators;

/// <summary>A7 — FluentValidation for the BusinessMeetings-cluster string fields.
/// Each <c>MaximumLength</c> mirrors the EF <c>HasMaxLength</c> exactly (the
/// triple-lock, CLAUDE §7): MeetingTable.Code(16)/RowLabel(8),
/// HallAllocation.RowColumnSpec(512)/Notes(512), BusinessMeeting.Notes(1024)/
/// CancellationReason(512). Without these, an oversized string flows past the
/// endpoint (the service's <c>Trim</c> helper does no length check) into EF
/// <c>SaveChanges</c>, where SQL Server raises a truncation error → 500; the
/// validator turns that into a 400 at the edge.
///
/// <para>FastEndpoints binds a validator to the endpoint's request type, so the
/// create/update-table + create-allocation + cancel validators target the
/// route-derived DTOs the endpoints actually bind (a validator on the base
/// contract would not be auto-discovered for the route type — same reason as
/// <c>UpdateArchiveEditionRequestValidator</c>). Code/Capacity keep their existing
/// service-side guards (<c>NormaliseCode</c> 1–16, <c>ValidateCapacity</c> 2–100);
/// the <c>Code</c> rule here matches <c>NormaliseCode</c>'s bound so a bad code is
/// rejected uniformly at the edge.</para></summary>
public sealed class CreateMeetingTableRouteValidator : Validator<CreateMeetingTableRoute>
{
    public CreateMeetingTableRouteValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.RowLabel).MaximumLength(8);
    }
}

public sealed class UpdateMeetingTableRouteValidator : Validator<UpdateMeetingTableRoute>
{
    public UpdateMeetingTableRouteValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.RowLabel).MaximumLength(8);
    }
}

public sealed class CreateHallAllocationRouteValidator : Validator<CreateHallAllocationRoute>
{
    public CreateHallAllocationRouteValidator()
    {
        RuleFor(x => x.RowColumnSpec).MaximumLength(512);
        RuleFor(x => x.Notes).MaximumLength(512);
    }
}

public sealed class ScheduleMeetingRequestValidator : Validator<ScheduleMeetingRequest>
{
    public ScheduleMeetingRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(1024);
    }
}

public sealed class CancelBusinessMeetingRouteValidator : Validator<CancelBusinessMeetingRoute>
{
    public CancelBusinessMeetingRouteValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(512);
    }
}
