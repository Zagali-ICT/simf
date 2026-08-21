// Tests: SIMF.Api.Tests/ProgrammeSessionsTests.cs
// Tests: SIMF.Api.Tests/RecordedQuestionsTests.cs
// Tests: SIMF.Api.Tests/SessionSummaryTests.cs
using System.Globalization;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>The public, anonymous list of active programme sessions,
/// ordered by start time.
/// Optional <c>?day=yyyy-MM-dd</c> restricts to one event-local (+03:00)
/// calendar day (matching the day-grouped agenda) for the agenda's
/// Day 1/2/3 segmented control. Optional <c>?categoryId=</c> restricts
/// to one <c>SessionCategory</c> track. Mirrors the
/// <c>ListPublicDelegationsEndpoint</c> public-read shape.</summary>
public sealed class ListProgrammeSessionsRequest
{
    /// <summary>Optional event-local (+03:00) calendar day filter,
    /// <c>yyyy-MM-dd</c>. Omitted = the whole programme.</summary>
    public string? Day { get; set; }

    /// <summary>Optional server-side track filter: the id of a
    /// <c>SessionCategory</c> (the dynamic lookup). Omitted = every category.
    /// Combines with <see cref="Day"/> (AND). An unknown id returns an empty
    /// list rather than a 404, so the anonymous agenda is not a category-id
    /// oracle.
    /// <para>No public endpoint lists the categories: the lookup ships empty
    /// pending OI-2. Read an id off a session in this same list.</para></summary>
    public Guid? CategoryId { get; set; }
}

public sealed class ListProgrammeSessionsEndpoint(IProgrammeSessionService service)
    : Endpoint<ListProgrammeSessionsRequest, ApiResult<PublicSessions>>
{
    public override void Configure()
    {
        Get("/app/programme/sessions");
        AllowAnonymous();
        Tags("Public");
        // 45s output cache; varies by all query keys so each ?day= and each
        // ?categoryId= keeps a distinct entry (no-op under Testing).
        Options(b => b.CacheOutput("PublicRead"));
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
            await service.ListAsync(day, req.CategoryId, ct)), ct);
    }
}

/// <summary>The day-grouped public agenda ("تفاصيل اليوم"):
/// the active programme days (date + bilingual title + a has-logo flag),
/// each with its sessions. Anonymous; drives the app's day banner + day strip.</summary>
public sealed class ListProgrammeDaysEndpoint(IProgrammeSessionService service)
    : EndpointWithoutRequest<ApiResult<PublicProgrammeDays>>
{
    public override void Configure()
    {
        Get("/app/programme/days");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(ApiResult<PublicProgrammeDays>.Ok(
            await service.ListDaysAsync(ct)), ct);
    }
}

/// <summary>The public, anonymous session detail:
/// the full detail for one active session — bilingual title +
/// abstract, hall, time window, ordered themes + speakers, and a cheap
/// seat-availability summary. 404 when the session is missing or
/// soft-deleted (mirrors the seat-map / content public reads).</summary>
public sealed class GetProgrammeSessionRequest { public Guid Id { get; set; } }

public sealed class GetProgrammeSessionEndpoint(IProgrammeSessionService service)
    : Endpoint<GetProgrammeSessionRequest, ApiResult<PublicSessionDetail>>
{
    public override void Configure()
    {
        Get("/app/programme/sessions/{id:guid}");
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

/// <summary>The recorded Q&amp;A archive
/// for a published session — the questions that were actually asked on stage
/// (pushed to the speaker), attributed to the asker. With the two-path
/// Q&amp;A it filters on IsPushed, not Status==Approved (a live question now
/// auto-approves onto the desk, so Approved alone would leak un-asked questions).
/// Requires an approved (signed-in) account: attendee display names are not
/// exposed to anonymous callers. Returns an empty list when the session is not
/// active+published.</summary>
public sealed class ListRecordedQuestionsRequest { public Guid Id { get; set; } }

public sealed class ListRecordedQuestionsEndpoint(IProgrammeSessionService service)
    : Endpoint<ListRecordedQuestionsRequest, ApiResult<IReadOnlyList<PublicRecordedQuestion>>>
{
    public override void Configure()
    {
        Get("/app/programme/sessions/{id:guid}/recorded-questions");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Programme");
    }

    public override async Task HandleAsync(
        ListRecordedQuestionsRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<PublicRecordedQuestion>>.Ok(
            await service.ListRecordedQuestionsAsync(req.Id, ct)), ct);
}

/// <summary>The published AI session summary / محضر for one
/// session. Anonymous like the
/// session detail — published editorial content with no attendee PII (the
/// "speakers" line is curated public text). 404 when the session is missing /
/// soft-deleted or the Committee has not published a summary yet.</summary>
public sealed class GetSessionSummaryRequest { public Guid Id { get; set; } }

public sealed class GetSessionSummaryEndpoint(IProgrammeSessionService service)
    : Endpoint<GetSessionSummaryRequest, ApiResult<PublicSessionSummary>>
{
    public override void Configure()
    {
        Get("/app/programme/sessions/{id:guid}/summary");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(
        GetSessionSummaryRequest req, CancellationToken ct)
    {
        var summary = await service.GetSessionSummaryAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "No published summary was found for this session.",
                "لم يتم العثور على ملخّص منشور لهذه الجلسة.");
        await Send.OkAsync(ApiResult<PublicSessionSummary>.Ok(summary), ct);
    }
}

/// <summary>The team-approved محضر for the session's host / moderator
/// ("ready for المحاور"). Unlike the anonymous published read, this requires an
/// approved account and the caller must be the session's moderator or host (the
/// service enforces it, 403 otherwise); gated on the team approval, so the host
/// sees it before any public release. 404 when not yet approved.</summary>
public sealed class GetApprovedSessionSummaryForHostEndpoint(IProgrammeSessionService service)
    : Endpoint<GetSessionSummaryRequest, ApiResult<HostSessionSummary>>
{
    public override void Configure()
    {
        Get("/app/programme/sessions/{id:guid}/summary/approved");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Programme");
    }

    public override async Task HandleAsync(
        GetSessionSummaryRequest req, CancellationToken ct)
    {
        var callerId = User.ActorId();
        var summary = await service.GetApprovedSummaryForHostAsync(callerId, req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "No approved summary is available for this session yet.",
                "لا يوجد ملخّص معتمد لهذه الجلسة بعد.");
        await Send.OkAsync(ApiResult<HostSessionSummary>.Ok(summary), ct);
    }
}
