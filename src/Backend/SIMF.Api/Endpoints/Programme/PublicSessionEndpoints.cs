// Tests: SIMF.Api.Tests/ProgrammeSessionsTests.cs
// Tests: SIMF.Api.Tests/RecordedQuestionsTests.cs (P3.4 — D-235)
using System.Globalization;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>D-199 (gap doc G3, Mockup page 16 "Agenda") — public,
/// anonymous list of active programme sessions, ordered by start time.
/// Optional <c>?day=yyyy-MM-dd</c> restricts to one calendar day (UTC)
/// for the agenda's Day 1/2/3 segmented control. Mirrors the
/// <c>ListPublicDelegationsEndpoint</c> public-read shape.</summary>
public sealed class ListProgrammeSessionsRequest
{
    /// <summary>Optional UTC calendar day filter, <c>yyyy-MM-dd</c>.
    /// Omitted = the whole programme.</summary>
    public string? Day { get; set; }
}

public sealed class ListProgrammeSessionsEndpoint(IProgrammeSessionService service)
    : Endpoint<ListProgrammeSessionsRequest, ApiResult<PublicSessions>>
{
    public override void Configure()
    {
        Get("/programme/sessions");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(
        ListProgrammeSessionsRequest req, CancellationToken ct)
    {
        DateOnly? day = null;
        if (!string.IsNullOrWhiteSpace(req.Day))
        {
            if (!DateOnly.TryParseExact(
                    req.Day.Trim(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var parsed))
            {
                throw new ApiException(
                    ErrorCodes.SessionInvalid, 400,
                    "The 'day' filter must be a date in yyyy-MM-dd format.",
                    "يجب أن يكون مرشّح 'day' تاريخاً بالصيغة yyyy-MM-dd.");
            }
            day = parsed;
        }

        await Send.OkAsync(ApiResult<PublicSessions>.Ok(
            await service.ListAsync(day, ct)), ct);
    }
}

/// <summary>D-199 (gap doc G3, Mockup page 17 "Session detail") — public,
/// anonymous full detail for one active session: bilingual title +
/// abstract, hall, time window, ordered themes + speakers, and a cheap
/// seat-availability summary. 404 when the session is missing or
/// soft-deleted (mirrors the seat-map / content public reads).</summary>
public sealed class GetProgrammeSessionRequest { public Guid Id { get; set; } }

public sealed class GetProgrammeSessionEndpoint(IProgrammeSessionService service)
    : Endpoint<GetProgrammeSessionRequest, ApiResult<PublicSessionDetail>>
{
    public override void Configure()
    {
        Get("/programme/sessions/{id:guid}");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(
        GetProgrammeSessionRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");
        await Send.OkAsync(ApiResult<PublicSessionDetail>.Ok(detail), ct);
    }
}

/// <summary>P3.4 — D-235 (Completion Programme §5.4): the recorded Q&amp;A archive
/// for a published session — the Committee-approved questions attributed to the
/// asker. Requires an approved (signed-in) account: attendee display names are
/// not exposed to anonymous callers. Returns an empty list when the session is
/// not active+published.</summary>
public sealed class ListRecordedQuestionsRequest { public Guid Id { get; set; } }

public sealed class ListRecordedQuestionsEndpoint(IProgrammeSessionService service)
    : Endpoint<ListRecordedQuestionsRequest, ApiResult<IReadOnlyList<PublicRecordedQuestion>>>
{
    public override void Configure()
    {
        Get("/programme/sessions/{id:guid}/recorded-questions");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Programme");
    }

    public override async Task HandleAsync(
        ListRecordedQuestionsRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<PublicRecordedQuestion>>.Ok(
            await service.ListRecordedQuestionsAsync(req.Id, ct)), ct);
}
