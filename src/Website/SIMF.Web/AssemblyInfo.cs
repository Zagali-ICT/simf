using System.Runtime.CompilerServices;

// Exposes SIMF.Web's internal surface to the test project so the bUnit page
// tests and the site-content mapper tests can exercise the internal helpers
// directly (SiteContentEndpoints, PublicEditions, CultureEndpoint,
// AppRenderMode). InternalsVisibleTo is assembly-wide; it does NOT expose
// private members. Test-enablement only, no runtime behaviour change, and the
// test assembly is non-packable so nothing ships.
//
// It has its own file for a reason: it used to live in
// SimfCookieRefreshHandler.cs, and deleting that file along with the Website's
// login/account area silently took the test project's access with it.
[assembly: InternalsVisibleTo("SIMF.Web.Tests")]
