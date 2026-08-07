using System.Globalization;
using System.Resources;

namespace SIMF.Common.Resources.Enums;

/// <summary>Resource accessor for <c>SecondFactorKind</c> enum display names.</summary>
public static class ResSecondFactorKind
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "SIMF.Common.Resources.Enums.ResSecondFactorKind",
            typeof(ResSecondFactorKind).Assembly);

    public static string Totp => Get(nameof(Totp));
    public static string EmailOtp => Get(nameof(EmailOtp));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
