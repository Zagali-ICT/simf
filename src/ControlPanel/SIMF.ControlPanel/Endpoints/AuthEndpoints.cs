using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SIMF.ApiClient;

namespace SIMF.ControlPanel.Endpoints;

/// <summary>
/// The HTTP endpoints that write and clear the authentication cookie — the
/// steps of the sign-in flow that need an HTTP request context rather than an
/// interactive circuit.
/// </summary>
internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        // Completes an interactive sign-in: redeems the one-time reference and
        // issues the authentication cookie, then sends the user to the shell.
        routes.MapGet("/auth/complete", async (
            string reference, SignInTicketStore tickets, HttpContext http) =>
        {
            var tokens = tickets.Redeem(reference);
            if (tokens is null)
            {
                return Results.Redirect("/login");
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, tokens.User.Id.ToString()),
                new(ClaimTypes.Email, tokens.User.Email),
                new(ClaimTypes.Name, tokens.User.DisplayName),
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // The API tokens are kept in the (encrypted) cookie for the module
            // pages that will call the API; the shell itself does not use them.
            var properties = new AuthenticationProperties { IsPersistent = true };
            properties.StoreTokens(
            [
                new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken },
                new AuthenticationToken { Name = "refresh_token", Value = tokens.RefreshToken },
            ]);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
            return Results.Redirect("/");
        });

        // Signs out: ends the API session, then clears the cookie. POST so a
        // cross-site GET cannot trigger it; antiforgery is disabled on this one
        // endpoint because the worst case is only a nuisance sign-out.
        routes.MapPost("/auth/sign-out", async (HttpContext http, SimfAuthClient api) =>
        {
            var accessToken = await http.GetTokenAsync("access_token");
            if (accessToken is not null)
            {
                await api.SignOutAsync(accessToken);
            }
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        }).DisableAntiforgery();
    }
}
