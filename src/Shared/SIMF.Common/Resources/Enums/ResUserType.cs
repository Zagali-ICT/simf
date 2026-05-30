using System.Globalization;
using System.Resources;

namespace SIMF.Common.Resources.Enums;

/// <summary>D-110: resource accessor for <c>UserType</c> enum display names.</summary>
public static class ResUserType
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "SIMF.Common.Resources.Enums.ResUserType",
            typeof(ResUserType).Assembly);

    public static string Visitor => Get(nameof(Visitor));
    public static string Admin => Get(nameof(Admin));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
