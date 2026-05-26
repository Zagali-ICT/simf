using System.Globalization;
using System.Resources;

namespace SIMF.Common.Resources.Enums;

/// <summary>D-110: resource accessor for <c>AccountState</c> enum display names.</summary>
public static class ResAccountState
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "SIMF.Common.Resources.Enums.ResAccountState",
            typeof(ResAccountState).Assembly);

    public static string Registered => Get(nameof(Registered));
    public static string EmailVerified => Get(nameof(EmailVerified));
    public static string PendingApproval => Get(nameof(PendingApproval));
    public static string Approved => Get(nameof(Approved));
    public static string Rejected => Get(nameof(Rejected));
    public static string Disabled => Get(nameof(Disabled));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
