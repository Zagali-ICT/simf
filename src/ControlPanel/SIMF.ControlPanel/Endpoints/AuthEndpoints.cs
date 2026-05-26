// Tests: SIMF.ControlPanel.Tests/SignInTicketStoreTests.cs (the ticket hand-off).
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SIMF.ApiClient;
using SIMF.ControlPanel;

using SIMF.Common.Enums;

namespace SIMF.ControlPanel.Endpoints;

/// <summary>
/// The HTTP endpoints that write and clear the authentication cookie — the
/// steps of the sign-in flow that need an HTTP request context rather than an
/// interactive circuit.
/// </summary>
internal static class AuthEndpoints
{
    /// <summary>
    /// Reads the role claims from a JWT access token without doing signature
    /// validation — the API already verified the token before issuing it; the
    /// CP only needs to copy the claim values across.
    /// </summary>
    private static IEnumerable<string> ExtractRoleClaims(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return [];
        }
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
            {
                return [];
            }
            var token = handler.ReadJwtToken(accessToken);
            // The API issues roles as ClaimTypes.Role; with the standard
            // claim-type mapping in JwtSecurityTokenHandler that arrives back
            // as the same long-form URI, so match on either form.
            return token.Claims
                .Where(claim => claim.Type == ClaimTypes.Role
                    || claim.Type == "role")
                .Select(claim => claim.Value)
                .ToArray();
        }
        catch (Exception)
        {
            // A malformed token can't yield roles — fall back to "no roles",
            // which means every role-gated page denies. The /account/profile
            // and dashboard are still reachable; the user can re-sign-in.
            return [];
        }
    }

    /// <summary>
    /// Reads named single-value claims (P11 — D-052) from the JWT —
    /// <c>account_state</c>, <c>user_type</c>. Same no-signature-validation
    /// shape as <see cref="ExtractRoleClaims"/>; the API already verified
    /// the token. Returns an empty dictionary on a malformed token.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ExtractJwtClaims(string accessToken)
    {
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(accessToken))
        {
            return pairs;
        }
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

    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        // Completes an interactive sign-in: redeems the one-time reference and
        // issues the authentication cookie, then sends the user to the shell.
        // Intentionally anonymous — it runs before the cookie exists; the
        // single-use, short-lived ticket reference is the control.
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
                    "Control Panel sign-in completion rejected — the ticket was unknown, "
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

            // The JWT carries the user's roles as ClaimTypes.Role; copy them
            // into the cookie principal so page-level [Authorize(Roles = …)]
            // checks pass for Administrator / future role gates. Without this,
            // the cookie has no role claims and every role-gated page kicks
            // the user back to /login (D-041, D-042).
            foreach (var role in ExtractRoleClaims(tokens.AccessToken))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // P11 — D-052: copy account_state + user_type from the JWT into
            // the cookie so layout-level guards + the state-banner pages
            // can read them without an API round-trip. The bilingual
            // rejection reason rides on the cookie too, sourced from the
            // sign-in response (the JWT itself does not carry it).
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

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // The API tokens are kept in the (encrypted) cookie for the module
            // pages that will call the API; the shell itself does not use them.
            // D-121 — also persist expires_at so the cookie-validate refresh
            // hook (SimfCookieRefreshHandler) knows when to rotate without
            // round-tripping the API on every request.
            var properties = new AuthenticationProperties { IsPersistent = true };
            SimfCookieRefreshHandler.StoreTokens(properties, tokens, DateTimeOffset.UtcNow);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
            logger.LogInformation("Control Panel sign-in completed for {UserId}.", tokens.User.Id);
            // P11 — D-052: route non-Approved users to the matching
            // state-banner page directly; the layout guard would do the
            // same on the next request but the explicit redirect avoids a
            // flash of the dashboard.
            return accountState switch
            {
                "PendingApproval" => Results.Redirect("/auth/pending"),
                "Rejected" => Results.Redirect("/auth/rejected"),
                _ => Results.Redirect("/"),
            };
        }).AllowAnonymous();

        // Signs out: ends the API session, then clears the cookie. POST and
        // authenticated-only — with the SameSite=Lax cookie a cross-site POST
        // carries no cookie, so a forged request is unauthenticated and
        // rejected; antiforgery is therefore not the gate here.
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
                        "Control Panel sign-out: the API session could not be ended for {UserId}.",
                        userId);
                }
            }

            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            logger.LogInformation("Control Panel sign-out for {UserId}.", userId);
            return Results.Redirect("/login");
        }).RequireAuthorization().DisableAntiforgery();
    }
}
