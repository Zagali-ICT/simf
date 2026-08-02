using Microsoft.AspNetCore.Localization;

namespace SIMF.Web.Endpoints;

/// <summary>The endpoint that switches the interface language for the public Website.</summary>
internal static class CultureEndpoint
{
    private static readonly string[] Supported = ["en", "ar"];

    public static void MapCultureEndpoint(this IEndpointRouteBuilder routes)
    {
        // Sets the interface-language cookie and returns to the page the user
        // was on. A full navigation, so the whole document re-renders in the
        // new language and direction (LTR / RTL).
        routes.MapGet("/culture", (string culture, string? redirectUri, HttpContext http) =>
        {
            if (!Supported.Contains(culture))
            {
                culture = "en";
            }

            http.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddYears(1),
                    IsEssential = true,
                });

            var target = redirectUri;
            if (string.IsNullOrEmpty(target)
                || !Uri.IsWellFormedUriString(target, UriKind.Relative)
                || !target.StartsWith('/'))
            {
                target = "/";
            }
            return Results.LocalRedirect(target);
        });
    }
}
