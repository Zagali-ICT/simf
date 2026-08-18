// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// seat layouts and reservations, meeting requests, availability windows
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.ControlPanel.Components.Assistant;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Email;
using SIMF.Contracts.Faq;
using SIMF.Contracts.Feedback;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.Media;
using SIMF.Contracts.Programme;
using SIMF.Contracts.Requests;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Regions;
using SIMF.Contracts.Reporting;
using SIMF.Contracts.Sessions;
using SIMF.Common.Enums;

namespace SIMF.ControlPanel.Endpoints;

internal static partial class AccountEndpoints
{
    private static void MapSeatingAndMeetings(IEndpointRouteBuilder group)
    {
        // CP UI for seat reservations.
        group.MapGet("/admin/halls/{hallId:guid}/seat-layout",
            async (Guid hallId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetHallSeatLayoutAsync(hallId, token));
        });

        group.MapPut("/admin/halls/{hallId:guid}/seat-layout",
            async (Guid hallId, SetHallSeatLayoutRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetHallSeatLayoutAsync(hallId, body, token));
        });

        // Remove a hall's seat layout (back to general admission).
        group.MapDelete("/admin/halls/{hallId:guid}/seat-layout",
            async (Guid hallId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteHallSeatLayoutAsync(hallId, token));
        });

        group.MapPost("/admin/sessions/{sessionId:guid}/seats/list",
            async (Guid sessionId, GridQuery body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionSeatReservationsAsync(
                sessionId, body, token));
        });

        group.MapPost("/admin/sessions/{sessionId:guid}/seats/reserve-row",
            async (Guid sessionId, AdminReserveRowRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.AdminReserveSessionRowAsync(
                sessionId, body, token));
        });

        group.MapPost("/admin/sessions/{sessionId:guid}/seats/reserve-seat",
            async (Guid sessionId, AdminReserveSeatRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.AdminReserveSessionSeatAsync(
                sessionId, body, token));
        });

        group.MapDelete("/admin/sessions/{sessionId:guid}/seats/{reservationId:guid}",
            async (Guid sessionId, Guid reservationId,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.AdminReleaseSessionSeatAsync(
                sessionId, reservationId, token));
        });

        // 2026-07-18 (live per-session hall view, CP page 2e) — the 4-state seat
        // map + everyone currently present in the hall, both API-side gated
        // Attendance.View.
        group.MapGet("/admin/sessions/{sessionId:guid}/seat-map",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAdminSessionSeatMapAsync(sessionId, token));
        });

        group.MapPost("/admin/sessions/{sessionId:guid}/present/list",
            async (Guid sessionId, GridQuery body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionPresentAttendeesAsync(
                sessionId, body, token));
        });

        // Speaker meeting requests BFF passthroughs.
        group.MapPost("/admin/speaker-meeting-requests/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAdminSpeakerMeetingRequestsAsync(body, token));
        });

        group.MapGet("/admin/speaker-meeting-requests/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAdminSpeakerMeetingRequestAsync(id, token));
        });

        group.MapPut("/admin/speaker-meeting-requests/{id:guid}/respond",
            async (Guid id, RespondToSpeakerMeetingRequestRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RespondToAdminSpeakerMeetingRequestAsync(
                id, body, token));
        });

        // Re-send the speaker confirmation links for an AwaitingSpeaker request.
        group.MapPost("/admin/speaker-meeting-requests/{id:guid}/resend-confirmation",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ResendSpeakerMeetingConfirmationAsync(id, token));
        });

        // Bi-Meeting rework — operator check-in of a confirmed speaker meeting → Done.
        group.MapPost("/admin/speaker-meeting-requests/{id:guid}/check-in",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CheckInSpeakerMeetingAsync(id, token));
        });

        // Reopen a Rejected / Cancelled request back to Pending.
        group.MapPost("/admin/speaker-meeting-requests/{id:guid}/reopen",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ReopenSpeakerMeetingRequestAsync(id, token));
        });

        // Participation-document request (الطلبات) BFF passthroughs.
        group.MapPost("/admin/document-requests/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAdminParticipationDocumentRequestsAsync(body, token));
        });

        group.MapGet("/admin/document-requests/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAdminParticipationDocumentRequestAsync(id, token));
        });

        group.MapPut("/admin/document-requests/{id:guid}/respond",
            async (Guid id, RespondToParticipationDocumentRequestRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RespondToAdminParticipationDocumentRequestAsync(
                id, body, token));
        });

        // Badge-update request (الطلبات) BFF passthroughs.
        group.MapPost("/admin/badge-requests/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAdminBadgeUpdateRequestsAsync(body, token));
        });

        group.MapGet("/admin/badge-requests/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAdminBadgeUpdateRequestAsync(id, token));
        });

        group.MapPut("/admin/badge-requests/{id:guid}/respond",
            async (Guid id, RespondToBadgeUpdateRequestRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RespondToAdminBadgeUpdateRequestAsync(
                id, body, token));
        });

        // Speaker availability windows passthroughs.
        group.MapGet("/admin/speakers/{speakerId:guid}/availability-windows",
            async (Guid speakerId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSpeakerAvailabilityWindowsAsync(speakerId, token));
        });
        group.MapPost("/admin/speakers/{speakerId:guid}/availability-windows",
            async (Guid speakerId, CreateSpeakerAvailabilityWindowRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateSpeakerAvailabilityWindowAsync(speakerId, body, token));
        });
        group.MapDelete("/admin/speaker-availability-windows/{windowId:guid}",
            async (Guid windowId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteSpeakerAvailabilityWindowAsync(windowId, token));
        });

        // Hall availability windows passthroughs.
        group.MapGet("/admin/halls/{hallId:guid}/availability-windows",
            async (Guid hallId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListHallAvailabilityWindowsAsync(hallId, token));
        });
        group.MapPost("/admin/halls/{hallId:guid}/availability-windows",
            async (Guid hallId, CreateHallAvailabilityWindowRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateHallAvailabilityWindowAsync(hallId, body, token));
        });
        group.MapDelete("/admin/hall-availability-windows/{windowId:guid}",
            async (Guid windowId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteHallAvailabilityWindowAsync(windowId, token));
        });
        // The hall's free meeting slots (read by the
        // speaker-meeting-request review modal before binding an accept to one).
        group.MapGet("/admin/halls/{hallId:guid}/available-slots",
            async (Guid hallId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetHallAvailableSlotsAsync(hallId, token));
        });

        // Delegation meeting requests BFF passthroughs.
        group.MapPost("/admin/delegation-meeting-requests/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAdminDelegationMeetingRequestsAsync(body, token));
        });
        group.MapGet("/admin/delegation-meeting-requests/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAdminDelegationMeetingRequestAsync(id, token));
        });
        group.MapPut("/admin/delegation-meeting-requests/{id:guid}/respond",
            async (Guid id, RespondToDelegationMeetingRequestRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RespondToAdminDelegationMeetingRequestAsync(
                id, body, token));
        });

        // Bi-Meeting rework — operator check-in of a confirmed delegation meeting → Done.
        group.MapPost("/admin/delegation-meeting-requests/{id:guid}/check-in",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CheckInDelegationMeetingAsync(id, token));
        });

        // Bi-Meeting rework — delegation availability windows (parity with the
        // speaker-availability passthroughs; keyed on the ISO-numeric country id).
        group.MapGet("/admin/countries/{countryId:int}/availability-windows",
            async (int countryId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListDelegationAvailabilityWindowsAsync(countryId, token));
        });
        group.MapPost("/admin/countries/{countryId:int}/availability-windows",
            async (int countryId, CreateDelegationAvailabilityWindowRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateDelegationAvailabilityWindowAsync(countryId, body, token));
        });
        group.MapDelete("/admin/delegation-availability-windows/{windowId:guid}",
            async (Guid windowId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteDelegationAvailabilityWindowAsync(windowId, token));
        });
    }
}
