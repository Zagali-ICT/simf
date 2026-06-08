// Mirrors SIMF.ControlPanel.Endpoints.AuthEndpoints; lands the sign-in
// cookie for visitors signing in to the public Website (decision D-046 c).
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SIMF.ApiClient;
using SIMF.Web;

using SIMF.Common.Enums;

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
            var payload = tickets.Redeem(reference);
            if (payload is null
                || string.IsNullOrEmpty(payload.Tokens.User.Email)
                || string.IsNullOrEmpty(payload.Tokens.User.DisplayName))
            {
                logger.LogWarning(
                    "Website sign-in completion rejected — the ticket was unknown, "
                    + "already used, expired or incomplete.");
                return Results.Redirect("/login");
            }
            var tokens = payload.Tokens;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, tokens.User.Id.ToString()),
                new(ClaimTypes.Email, tokens.User.Email),
                new(ClaimTypes.Name, tokens.User.DisplayName),
            };

            // P11 — D-052: copy account_state + user_type from the JWT
            // into the cookie so the Account-area pages can route + read
            // the rejection reason without an API call. The bilingual
            // reason rides on the cookie (sourced from the API's sign-in
            // response — the JWT itself does not carry it).
            var jwtClaims = ExtractJwtClaims(tokens.AccessToken);
            var accountState = payload.AccountState?.State
                ?? jwtClaims.GetValueOrDefault("account_state")
                ?? "Approved";
            claims.Add(new Claim("account_state", accountState));
            if (jwtClaims.TryGetValue("user_type", out var userType))
            {
                claims.Add(new Claim("user_type", userType));
            }
            if (payload.AccountState?.RejectionReason is { } reason)
            {
                claims.Add(new Claim("rejection_reason", reason));
            }
            if (payload.AccountState?.RejectionReasonArabic is { } reasonAr)
            {
                claims.Add(new Claim("rejection_reason_ar", reasonAr));
            }

            var identity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // API tokens travel in the (encrypted) cookie's stored tokens
            // so the user-profile page can call the API through the
            // /account/api/ proxy — the access token never reaches the
            // browser. D-121 — also persist expires_at so the cookie-validate
            // refresh hook (SimfCookieRefreshHandler) knows when to rotate.
            var properties = new AuthenticationProperties { IsPersistent = true };
            SimfCookieRefreshHandler.StoreTokens(properties, tokens, DateTimeOffset.UtcNow);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
            logger.LogInformation("Website sign-in completed for {UserId}.", tokens.User.Id);
            // P11 — D-052: route non-Approved users directly to the
            // matching state-banner page. The /account/user-profile page
            // also guards itself but the explicit redirect avoids a
            // flash of the profile form.
            return accountState switch
            {
                "PendingApproval" => Results.Redirect("/account/pending"),
                "Rejected" => Results.Redirect("/account/rejected"),
                _ => Results.Redirect("/account/profile"),
            };
        }).AllowAnonymous();

        // POST /auth/sign-out — ends the API session, then clears the
        // cookie. POST + authenticated-only — a cross-site forged POST
        // carries no cookie (SameSite=Lax) and so is rejected; antiforgery
        // is not the gate here, the SameSite policy is.
        routes.MapPost("/auth/sign-out",
            async (HttpContext http, SimfAuthClient api, CancellationToken ct) =>
        {
            var logger = AuthLog.Of(http);
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "(unknown)";

            var accessToken = await http.GetTokenAsync("access_token");
            if (accessToken is not null)
            {
                var result = await api.SignOutAsync(accessToken, ct);
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

    /// <summary>
    /// Reads named single-value claims (P11 — D-052) from the JWT —
    /// <c>account_state</c>, <c>user_type</c>. The API already verified
    /// the signature when minting the token; this routine never does
    /// crypto, only structural decoding.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ExtractJwtClaims(string accessToken)
    {
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(accessToken)) return pairs;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken)) return pairs;
            var token = handler.ReadJwtToken(accessToken);
            foreach (var name in new[] { "account_state", "user_type" })
            {
                var claim = token.Claims.FirstOrDefault(c => c.Type == name);
                if (claim is not null) pairs[name] = claim.Value;
            }
        }
        catch (Exception) { /* fall through; defaults apply */ }
        return pairs;
    }
}
