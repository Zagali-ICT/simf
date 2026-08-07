using System.Globalization;
using System.Resources;

namespace SIMF.Common.Resources.Enums;

/// <summary>Resource accessor for <c>SignInAudience</c> enum display names.</summary>
public static class ResSignInAudience
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "SIMF.Common.Resources.Enums.ResSignInAudience",
            typeof(ResSignInAudience).Assembly);

    public static string Web => Get(nameof(Web));
    public static string Cp => Get(nameof(Cp));
    public static string App => Get(nameof(App));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
