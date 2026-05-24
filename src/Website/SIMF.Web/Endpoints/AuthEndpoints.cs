// Mirrors SIMF.ControlPanel.Endpoints.AuthEndpoints; lands the sign-in
// cookie for visitors signing in to the public Website (decision D-046 c).
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SIMF.ApiClient;

namespace SIMF.Web.Endpoints;

/// <summary>
/// HTTP endpoints that write and clear the Website authentication cookie.
/// The sign-in completion is reached from the Blazor circuit through a
/// one-time ticket (<see cref="SignInTicketStore"/>) — the circuit cannot
/// write a cookie itself.
/// </summary>
internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        // /auth/complete?reference={ticket} — redeems the ticket and issues
        // the authentication cookie, then lands the user on the
        // /account/profile page. Anonymous (the cookie is not yet
        // written; the short-lived single-use ticket is the control).
        routes.MapGet("/auth/complete", async (
            string reference, SignInTicketStore tickets, HttpContext http) =>
        {
            var logger = AuthLog.Of(http);
            var tokens = tickets.Redeem(reference);
            if (tokens is null
                || string.IsNullOrEmpty(tokens.User.Email)
                || string.IsNullOrEmpty(tokens.User.DisplayName))
            {
                logger.LogWarning(
                    "Website sign-in completion rejected — the ticket was unknown, "
                    + "already used, expired or incomplete.");
                return Results.Redirect("/login");
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, tokens.User.Id.ToString()),
                new(ClaimTypes.Email, tokens.User.Email),
                new(ClaimTypes.Name, tokens.User.DisplayName),
            };

            var identity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // API tokens travel in the (encrypted) cookie's stored tokens
            // so the user-profile page can call the API through the
            // /account/api/ proxy — the access token never reaches the
            // browser.
            var properties = new AuthenticationProperties { IsPersistent = true };
            properties.StoreTokens(
            [
                new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken },
                new AuthenticationToken { Name = "refresh_token", Value = tokens.RefreshToken },
            ]);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
            logger.LogInformation("Website sign-in completed for {UserId}.", tokens.User.Id);
            return Results.Redirect("/account/profile");
        }).AllowAnonymous();

        // POST /auth/sign-out — ends the API session, then clears the
        // cookie. POST + authenticated-only — a cross-site forged POST
        // carries no cookie (SameSite=Lax) and so is rejected; antiforgery
        // is not the gate here, the SameSite policy is.
        routes.MapPost("/auth/sign-out", async (HttpContext http, SimfAuthClient api) =>
        {
            var logger = AuthLog.Of(http);
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "(unknown)";

            var accessToken = await http.GetTokenAsync("access_token");
            if (accessToken is not null)
            {
                var result = await api.SignOutAsync(accessToken);
                if (!result.Success)
                {
                    logger.LogWarning(
                        "Website sign-out: the API session could not be ended for {UserId}.",
                        userId);
                }
            }

            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            logger.LogInformation("Website sign-out for {UserId}.", userId);
            return Results.Redirect("/login");
        }).RequireAuthorization().DisableAntiforgery();
    }
}
