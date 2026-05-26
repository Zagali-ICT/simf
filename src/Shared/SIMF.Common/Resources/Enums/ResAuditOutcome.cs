using System.Globalization;
using System.Resources;

namespace SIMF.Common.Resources.Enums;

/// <summary>D-110: resource accessor for <c>AuditOutcome</c> enum display names.</summary>
public static class ResAuditOutcome
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "SIMF.Common.Resources.Enums.ResAuditOutcome",
            typeof(ResAuditOutcome).Assembly);

    public static string Success => Get(nameof(Success));
    public static string Failure => Get(nameof(Failure));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
