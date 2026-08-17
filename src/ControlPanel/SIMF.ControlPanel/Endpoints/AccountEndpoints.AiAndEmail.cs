// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// AI prompts, invocations, the assistant, e-mail templates
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
    private static void MapAiAndEmail(IEndpointRouteBuilder group)
    {
        // AI module admin CRUD + invocations log.
        group.MapPost("/admin/ai/prompts/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAiPromptsAsync(body, token));
        });

        group.MapGet("/admin/ai/prompts/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAiPromptAsync(id, token));
        });

        group.MapPost("/admin/ai/prompts",
            async (CreateAiPromptRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateAiPromptAsync(body, token));
        });

        group.MapPut("/admin/ai/prompts/{id:guid}",
            async (Guid id, UpdateAiPromptRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateAiPromptAsync(id, body, token));
        });

        group.MapDelete("/admin/ai/prompts/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateAiPromptAsync(id, token));
        });

        group.MapPost("/admin/ai/prompts/{id:guid}/test",
            async (Guid id, TestAiPromptRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TestAiPromptAsync(id, body, token));
        });

        group.MapPost("/admin/ai/invocations/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAiInvocationsAsync(body, token));
        });

        // Append-only prompt version history (CP Phase-0 history modal), one
        // page at a time: the history grows by a row on every edit.
        group.MapPost("/admin/ai/prompts/{id:guid}/history/list",
            async (Guid id, GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAiPromptHistoryAsync(id, body, token));
        });

        // Full redacted invocation payload (CP Phase-0 detail modal).
        group.MapGet("/admin/ai/invocations/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAiInvocationAsync(id, token));
        });

        // CP Phase-1 — the AI dashboard 24h health aggregate.
        group.MapGet("/admin/ai/dashboard",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAiDashboardAsync(token));
        });

        // Control Panel operator assistant — the floating help chat. The browser
        // posts only the question; the CP builds the grounding page directory
        // server-side (filtered to the pages THIS operator may open) and the UI
        // locale, then forwards to the cp-assistant prompt. Building the directory
        // here — not in the browser — is what keeps the answer honest: a user
        // cannot widen it to pages they lack.
        group.MapPost("/admin/ai/assistant",
            async (CpAssistantRequest body, HttpContext http,
                   SimfAdminClient api, IStringLocalizer<Strings> localizer) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var permissions = http.User.FindAll(PermissionCatalog.ClaimType)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal);
            var hasAll = permissions.Contains(PermissionCatalog.Wildcard);
            var pages = CpAssistantDirectory.Build(permissions, hasAll, localizer);
            var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                ? "ar" : "en";
            return Forward(await api.AssistAsync(
                new CpAssistantRequest
                {
                    Question = body.Question ?? string.Empty,
                    Pages = pages,
                    Locale = locale,
                }, token));
        });

        // Transactional email-template editor (list / read / edit /
        // reset / preview). The {type} segment is the EmailTemplateType name.
        group.MapPost("/admin/email/templates/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListEmailTemplatesAsync(body, token));
        });

        group.MapGet("/admin/email/templates/{type}",
            async (string type, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetEmailTemplateAsync(type, token));
        });

        group.MapPut("/admin/email/templates/{type}",
            async (string type, UpdateEmailTemplateRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateEmailTemplateAsync(type, body, token));
        });

        group.MapPost("/admin/email/templates/{type}/reset",
            async (string type, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ResetEmailTemplateAsync(type, token));
        });

        group.MapPost("/admin/email/templates/{type}/preview",
            async (string type, PreviewEmailTemplateRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.PreviewEmailTemplateAsync(type, body, token));
        });
    }
}
