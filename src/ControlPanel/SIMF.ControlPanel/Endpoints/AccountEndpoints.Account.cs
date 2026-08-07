// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// profile, TOTP pairing, recovery codes, admin 2FA reset
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
    private static void MapAccount(IEndpointRouteBuilder group)
    {
        group.MapGet("/profile", async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetProfileAsync(token));
        });

        group.MapPost("/totp/setup", async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TotpSetupAsync(token));
        });

        // Returns the QR for the caller's CURRENT secret (no rotation).
        // Drives the /account/totp-pairing CP page used to re-pair a lost
        // authenticator without resetting the seeded super-admin's secret.
        group.MapGet("/totp/pairing", async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TotpPairingAsync(token));
        });

        // Verifies a code against the active secret without mutating state.
        group.MapPost("/totp/pairing/verify",
            async (TotpConfirmRequest body, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TotpPairingVerifyAsync(body, token));
        });

        group.MapPost("/totp/confirm",
            async (TotpConfirmRequest body, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TotpConfirmAsync(body, token));
        });

        group.MapPost("/totp/disable",
            async (TotpDisableRequest body, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TotpDisableAsync(body, token));
        });

        group.MapPost("/recovery-codes/regenerate",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RegenerateRecoveryCodesAsync(token));
        });

        group.MapPost("/admin/reset-2fa",
            async (AdminResetTwoFactorRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ResetTwoFactorAsync(body, token));
        });
    }
}
