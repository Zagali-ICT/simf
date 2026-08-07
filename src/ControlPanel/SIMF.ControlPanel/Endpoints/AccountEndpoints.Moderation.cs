// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// session moderators, the question queue, AI session summaries
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
    private static void MapModeration(IEndpointRouteBuilder group)
    {
        // Session-question moderation BFF passthroughs.
        group.MapPost("/admin/session-moderators/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionModeratorsAsync(body, token));
        });
        // DEF-MOD-005 — the assign dialog's pickers (replaces two raw GUID boxes).
        group.MapGet("/admin/session-moderators/assign-options",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionModeratorAssignOptionsAsync(token));
        });
        group.MapPost("/admin/session-moderators",
            async (AssignSessionModeratorRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.AssignSessionModeratorAsync(body, token));
        });
        group.MapDelete("/admin/session-moderators/{sessionId:guid}/{userId:guid}",
            async (Guid sessionId, Guid userId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RevokeSessionModeratorAsync(sessionId, userId, token));
        });
        // Scientific-Committee Q&A queue passthroughs.
        group.MapGet("/admin/questions/queue",
            async (QuestionStatus? status, Guid? sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListQuestionQueueAsync(status, sessionId, token));
        });
        group.MapPut("/admin/questions/{questionId:guid}/approve",
            async (Guid questionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ApproveQuestionAsync(questionId, token));
        });
        group.MapPut("/admin/questions/{questionId:guid}/hide",
            async (Guid questionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.HideQuestionFromQueueAsync(questionId, token));
        });
        group.MapPut("/admin/questions/{questionId:guid}/escalate",
            async (Guid questionId, EscalateQuestionRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.EscalateQuestionAsync(questionId, body, token));
        });
        group.MapGet("/sessions/{sessionId:guid}/questions/moderate",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListModeratorQueueAsync(sessionId, token));
        });
        group.MapPut("/sessions/{sessionId:guid}/questions/{questionId:guid}/hide",
            async (Guid sessionId, Guid questionId,
                SIMF.Contracts.Sessions.SetQuestionHiddenRequest body,
                HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.HideQuestionAsync(sessionId, questionId, body.IsHidden, token));
        });
        group.MapPut("/sessions/{sessionId:guid}/questions/{questionId:guid}/push",
            async (Guid sessionId, Guid questionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.PushQuestionAsync(sessionId, questionId, token));
        });
        group.MapPut("/sessions/{sessionId:guid}/questions/reorder",
            async (Guid sessionId,
                SIMF.Contracts.Sessions.ReorderQuestionsRequest body,
                HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ReorderQuestionsAsync(
                sessionId, body.OrderedQuestionIds.ToList(), token));
        });

        // AI session-summary / محضر committee desk passthroughs.
        group.MapGet("/admin/session-summaries",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionSummariesAsync(token));
        });
        group.MapGet("/admin/session-summaries/{sessionId:guid}",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetSessionSummaryAsync(sessionId, token));
        });
        group.MapPost("/admin/session-summaries/{sessionId:guid}/generate",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GenerateSessionSummaryAsync(sessionId, token));
        });
        group.MapPut("/admin/session-summaries/{sessionId:guid}",
            async (Guid sessionId, SaveSessionSummaryRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SaveSessionSummaryAsync(sessionId, body, token));
        });
        group.MapPut("/admin/session-summaries/{sessionId:guid}/publish",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.PublishSessionSummaryAsync(sessionId, token));
        });
        group.MapPut("/admin/session-summaries/{sessionId:guid}/unpublish",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UnpublishSessionSummaryAsync(sessionId, token));
        });
        // The team review/approval workflow passthroughs.
        group.MapPut("/admin/session-summaries/{sessionId:guid}/submit-review",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SubmitSessionSummaryForReviewAsync(sessionId, token));
        });
        group.MapPut("/admin/session-summaries/{sessionId:guid}/approve",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ApproveSessionSummaryAsync(sessionId, token));
        });
        group.MapPut("/admin/session-summaries/{sessionId:guid}/return-to-draft",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ReturnSessionSummaryToDraftAsync(sessionId, token));
        });

        // Operator hall-door QR arrival passthrough.
        group.MapPost("/admin/sessions/{sessionId:guid}/arrivals",
            async (Guid sessionId, SIMF.Contracts.Sessions.RecordQrArrivalRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RecordQrArrivalAsync(sessionId, body, token));
        });

        // 2026-07-18: operator hall-door QR departure (check-out) passthrough.
        group.MapPost("/admin/sessions/{sessionId:guid}/departures",
            async (Guid sessionId, SIMF.Contracts.Sessions.RecordQrArrivalRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RecordQrDepartureAsync(sessionId, body, token));
        });
    }
}
