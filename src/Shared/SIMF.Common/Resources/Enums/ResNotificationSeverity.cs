using System.Globalization;
using System.Resources;

namespace SIMF.Common.Resources.Enums;

/// <summary>Resource accessor for <c>NotificationSeverity</c> enum display names.</summary>
public static class ResNotificationSeverity
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "SIMF.Common.Resources.Enums.ResNotificationSeverity",
            typeof(ResNotificationSeverity).Assembly);

    public static string Info => Get(nameof(Info));
    public static string Success => Get(nameof(Success));
    public static string Warning => Get(nameof(Warning));
    public static string Error => Get(nameof(Error));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
