using System.Globalization;
using System.Resources;

namespace SIMF.Common.Resources.Enums;

/// <summary>D-110: resource accessor for <c>RowAuditOperation</c> enum display names.</summary>
public static class ResRowAuditOperation
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "SIMF.Common.Resources.Enums.ResRowAuditOperation",
            typeof(ResRowAuditOperation).Assembly);

    public static string Insert => Get(nameof(Insert));
    public static string Update => Get(nameof(Update));
    public static string Delete => Get(nameof(Delete));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
