using System.Globalization;
using System.Resources;

namespace SIMF.Common.Resources.Enums;

/// <summary>Resource accessor for <c>AccountCodePurpose</c> enum display names.</summary>
public static class ResAccountCodePurpose
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "SIMF.Common.Resources.Enums.ResAccountCodePurpose",
            typeof(ResAccountCodePurpose).Assembly);

    public static string EmailVerification => Get(nameof(EmailVerification));
    public static string PasswordReset => Get(nameof(PasswordReset));
    public static string SignInOtp => Get(nameof(SignInOtp));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
