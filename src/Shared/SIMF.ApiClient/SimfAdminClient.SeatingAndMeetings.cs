// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// seat reservations, meeting requests, availability windows
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Requests;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Attendance;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.Email;
using SIMF.Contracts.Faq;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.Feedback;
using SIMF.Contracts.Logs;
using SIMF.Contracts.Media;
using SIMF.Contracts.Organization;
using SIMF.Contracts.Programme;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Regions;
using SIMF.Contracts.Sessions;
using SIMF.Contracts.Statistics;
using SIMF.Contracts.Configuration;
using SIMF.Contracts.Ops;
using SIMF.Contracts.Support;
using SIMF.Common.Enums;

namespace SIMF.ApiClient;

public sealed partial class SimfAdminClient
{
    // -- Seat reservations: the CP UI over the seat-plan API ---------------

    public Task<ApiCallResult<HallSeatLayoutSnapshot>> GetHallSeatLayoutAsync(
        Guid hallId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<HallSeatLayoutSnapshot>(
            HttpMethod.Get, $"halls/{hallId}/seat-layout", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<HallSeatLayoutSnapshot>> SetHallSeatLayoutAsync(
        Guid hallId, SetHallSeatLayoutRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<HallSeatLayoutSnapshot>(
            HttpMethod.Put, $"halls/{hallId}/seat-layout",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Remove the hall's seat layout (the hall reverts to general
    /// admission). Returns the now-empty snapshot.</summary>
    public Task<ApiCallResult<HallSeatLayoutSnapshot>> DeleteHallSeatLayoutAsync(
        Guid hallId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<HallSeatLayoutSnapshot>(
            HttpMethod.Delete, $"halls/{hallId}/seat-layout", content: null,
            accessToken, cancellationToken);

    /// <summary>The seat plan's active reservations in the ADMIN
    /// shape: each row names its holder and carries the real status + check-in flag.</summary>
    public Task<ApiCallResult<GridPage<SeatPlanCell>>> ListSessionSeatReservationsAsync(
        Guid sessionId, GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<SeatPlanCell>>(
            HttpMethod.Post, $"sessions/{sessionId}/seats/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> AdminReserveSessionRowAsync(
        Guid sessionId, AdminReserveRowRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"sessions/{sessionId}/seats/reserve-row",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> AdminReserveSessionSeatAsync(
        Guid sessionId, AdminReserveSeatRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"sessions/{sessionId}/seats/reserve-seat",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> AdminReleaseSessionSeatAsync(
        Guid sessionId, Guid reservationId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"sessions/{sessionId}/seats/{reservationId}",
            content: null, accessToken, cancellationToken);

    // The live per-session hall view: the session's 4-state
    // seat map (no "my seat" cell) and everyone currently present in the hall.
    // Both API-side gated Attendance.View.
    public Task<ApiCallResult<SessionSeatMap>> GetAdminSessionSeatMapAsync(
        Guid sessionId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SessionSeatMap>(
            HttpMethod.Get, $"sessions/{sessionId}/seat-map", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<SessionPresentAttendee>>> GetSessionPresentAttendeesAsync(
        Guid sessionId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<SessionPresentAttendee>>(
            HttpMethod.Get, $"sessions/{sessionId}/present", content: null,
            accessToken, cancellationToken);

    // -- Speaker meeting requests (SIMF.Contracts.Programme) -----------------

    public Task<ApiCallResult<GridPage<AdminSpeakerMeetingRequestRow>>> ListAdminSpeakerMeetingRequestsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSpeakerMeetingRequestRow>>(
            HttpMethod.Post, "speaker-meeting-requests/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerMeetingRequestDetail>> GetAdminSpeakerMeetingRequestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerMeetingRequestDetail>(
            HttpMethod.Get, $"speaker-meeting-requests/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerMeetingRequestDetail>> RespondToAdminSpeakerMeetingRequestAsync(
        Guid id, RespondToSpeakerMeetingRequestRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerMeetingRequestDetail>(
            HttpMethod.Put, $"speaker-meeting-requests/{id}/respond",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // Re-send the speaker's Approve/Reject confirmation links (AwaitingSpeaker only).
    public Task<ApiCallResult<bool>> ResendSpeakerMeetingConfirmationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"speaker-meeting-requests/{id}/resend-confirmation",
            content: null, accessToken, cancellationToken);

    // Bi-Meeting rework — an operator checks a confirmed speaker meeting in at the hall → Done.
    public Task<ApiCallResult<AdminSpeakerMeetingRequestDetail>> CheckInSpeakerMeetingAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerMeetingRequestDetail>(
            HttpMethod.Post, $"speaker-meeting-requests/{id}/check-in",
            content: null, accessToken, cancellationToken);

    // An admin reopens a Rejected / Cancelled request back to Pending.
    public Task<ApiCallResult<AdminSpeakerMeetingRequestDetail>> ReopenSpeakerMeetingRequestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerMeetingRequestDetail>(
            HttpMethod.Post, $"speaker-meeting-requests/{id}/reopen",
            content: null, accessToken, cancellationToken);

    // -- Participation-document + badge-update (الطلبات) request
    //    desks (SIMF.Contracts.Requests) -------------------------------------

    public Task<ApiCallResult<GridPage<AdminParticipationDocumentRequestRow>>> ListAdminParticipationDocumentRequestsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminParticipationDocumentRequestRow>>(
            HttpMethod.Post, "document-requests/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminParticipationDocumentRequestDetail>> GetAdminParticipationDocumentRequestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminParticipationDocumentRequestDetail>(
            HttpMethod.Get, $"document-requests/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminParticipationDocumentRequestDetail>> RespondToAdminParticipationDocumentRequestAsync(
        Guid id, RespondToParticipationDocumentRequestRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminParticipationDocumentRequestDetail>(
            HttpMethod.Put, $"document-requests/{id}/respond",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminBadgeUpdateRequestRow>>> ListAdminBadgeUpdateRequestsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminBadgeUpdateRequestRow>>(
            HttpMethod.Post, "badge-requests/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBadgeUpdateRequestDetail>> GetAdminBadgeUpdateRequestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBadgeUpdateRequestDetail>(
            HttpMethod.Get, $"badge-requests/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBadgeUpdateRequestDetail>> RespondToAdminBadgeUpdateRequestAsync(
        Guid id, RespondToBadgeUpdateRequestRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBadgeUpdateRequestDetail>(
            HttpMethod.Put, $"badge-requests/{id}/respond",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // Speaker availability windows.
    public Task<ApiCallResult<IReadOnlyList<AdminSpeakerAvailabilityWindow>>>
        ListSpeakerAvailabilityWindowsAsync(Guid speakerId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminSpeakerAvailabilityWindow>>(
            HttpMethod.Get, $"speakers/{speakerId}/availability-windows", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerAvailabilityWindow>>
        CreateSpeakerAvailabilityWindowAsync(Guid speakerId,
            CreateSpeakerAvailabilityWindowRequest request, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerAvailabilityWindow>(
            HttpMethod.Post, $"speakers/{speakerId}/availability-windows",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>>
        DeleteSpeakerAvailabilityWindowAsync(Guid windowId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"speaker-availability-windows/{windowId}", content: null,
            accessToken, cancellationToken);

    // The forum-day window (MIN/MAX over active ProgrammeDay.Date). The CP
    // meeting-scheduling pages read it to bound their date pickers to the event days.
    public Task<ApiCallResult<ForumWindowResponse>> GetForumWindowAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<ForumWindowResponse>(
            HttpMethod.Get, "programme/forum-window", content: null,
            accessToken, cancellationToken);

    // -- Hall availability windows ---------------------------------------------

    public Task<ApiCallResult<IReadOnlyList<AdminHallAvailabilityWindow>>>
        ListHallAvailabilityWindowsAsync(Guid hallId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminHallAvailabilityWindow>>(
            HttpMethod.Get, $"halls/{hallId}/availability-windows", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminHallAvailabilityWindow>>
        CreateHallAvailabilityWindowAsync(Guid hallId,
            CreateHallAvailabilityWindowRequest request, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminHallAvailabilityWindow>(
            HttpMethod.Post, $"halls/{hallId}/availability-windows",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>>
        DeleteHallAvailabilityWindowAsync(Guid windowId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"hall-availability-windows/{windowId}", content: null,
            accessToken, cancellationToken);

    // The hall's currently-free meeting slots (the
    // meeting-review flow reads these before binding an accepted request to one).
    public Task<ApiCallResult<IReadOnlyList<HallAvailableSlot>>>
        GetHallAvailableSlotsAsync(Guid hallId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<HallAvailableSlot>>(
            HttpMethod.Get, $"halls/{hallId}/available-slots", content: null,
            accessToken, cancellationToken);

    // -- Delegation meeting requests (SIMF.Contracts.Programme) ---------------

    public Task<ApiCallResult<GridPage<AdminDelegationMeetingRequestRow>>>
        ListAdminDelegationMeetingRequestsAsync(GridQuery query, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminDelegationMeetingRequestRow>>(
            HttpMethod.Post, "delegation-meeting-requests/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminDelegationMeetingRequestDetail>>
        GetAdminDelegationMeetingRequestAsync(Guid id, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminDelegationMeetingRequestDetail>(
            HttpMethod.Get, $"delegation-meeting-requests/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminDelegationMeetingRequestDetail>>
        RespondToAdminDelegationMeetingRequestAsync(
            Guid id, RespondToDelegationMeetingRequestRequest request, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminDelegationMeetingRequestDetail>(
            HttpMethod.Put, $"delegation-meeting-requests/{id}/respond",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // Bi-Meeting rework — an operator checks a confirmed delegation meeting in → Done.
    public Task<ApiCallResult<AdminDelegationMeetingRequestDetail>> CheckInDelegationMeetingAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminDelegationMeetingRequestDetail>(
            HttpMethod.Post, $"delegation-meeting-requests/{id}/check-in",
            content: null, accessToken, cancellationToken);

    // Bi-Meeting rework — delegation availability windows (parity with the speaker
    // stack): the team defines a country/delegation's free windows; the app offers
    // their slots. Keyed on the ISO-numeric CountryId (int), not a Guid.
    public Task<ApiCallResult<IReadOnlyList<AdminDelegationAvailabilityWindow>>>
        ListDelegationAvailabilityWindowsAsync(int countryId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminDelegationAvailabilityWindow>>(
            HttpMethod.Get, $"countries/{countryId}/availability-windows", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminDelegationAvailabilityWindow>>
        CreateDelegationAvailabilityWindowAsync(int countryId,
            CreateDelegationAvailabilityWindowRequest request, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminDelegationAvailabilityWindow>(
            HttpMethod.Post, $"countries/{countryId}/availability-windows",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>>
        DeleteDelegationAvailabilityWindowAsync(Guid windowId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"delegation-availability-windows/{windowId}", content: null,
            accessToken, cancellationToken);
}
