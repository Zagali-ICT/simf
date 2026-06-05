namespace SIMF.Common;

/// <summary>
/// The CMS content-block keys for the public marketing landing's hero text.
/// Shared so the Website's <c>/content/site</c> proxy (which requests + maps
/// them) and the seeder (which seeds them) cannot drift on the key strings — a
/// mismatch would silently drop the hero to its built-in defaults. Keys are
/// lowercase to match the CMS service's key normalisation.
/// </summary>
public static class LandingHeroContentKeys
{
    public const string TitleStart = "hero.titlestart";
    public const string TitleHighlight = "hero.titlehighlight";
    public const string TitleEnd = "hero.titleend";
    public const string Tagline = "hero.tagline";
    public const string MetaDate = "hero.metadate";
    public const string MetaVenue = "hero.metavenue";
    public const string CtaSecondary = "hero.ctasecondary";

    /// <summary>All hero keys, in landing display order.</summary>
    public static readonly string[] All =
    [
        TitleStart, TitleHighlight, TitleEnd, Tagline, MetaDate, MetaVenue, CtaSecondary,
    ];
}
