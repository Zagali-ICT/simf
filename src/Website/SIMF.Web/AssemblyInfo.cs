using System.Runtime.CompilerServices;

// D-194 — expose SIMF.Web's internal surface to the test project so the bUnit
// page tests and the site-content mapper tests can exercise the internal
// helpers directly (SiteContentEndpoints, PublicEditions, CultureEndpoint,
// AppRenderMode). InternalsVisibleTo is assembly-wide; it does NOT expose
// private members. Test-enablement only, no runtime behaviour change, and the
// test assembly is non-packable so nothing ships.
//
// D-774 — this attribute previously lived in SimfCookieRefreshHandler.cs, which
// was deleted with the Website login/account area. It now has its own file so a
// future deletion cannot silently take the test project's access with it.
[assembly: InternalsVisibleTo("SIMF.Web.Tests")]
