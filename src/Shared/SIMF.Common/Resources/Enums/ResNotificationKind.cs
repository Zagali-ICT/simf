using System.Globalization;
using System.Resources;

namespace SIMF.Common.Resources.Enums;

/// <summary>D-110: resource accessor for <c>NotificationKind</c> enum display names.</summary>
public static class ResNotificationKind
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "SIMF.Common.Resources.Enums.ResNotificationKind",
            typeof(ResNotificationKind).Assembly);

    public static string CredentialEmailVerificationSent => Get(nameof(CredentialEmailVerificationSent));
    public static string CredentialEmailVerificationResent => Get(nameof(CredentialEmailVerificationResent));
    public static string CredentialSignInOtpSent => Get(nameof(CredentialSignInOtpSent));
    public static string CredentialPasswordResetRequested => Get(nameof(CredentialPasswordResetRequested));
    public static string AccountProfileSubmitted => Get(nameof(AccountProfileSubmitted));
    public static string AdminPendingVisitor => Get(nameof(AdminPendingVisitor));
    public static string AccountApproved => Get(nameof(AccountApproved));
    public static string AccountRejected => Get(nameof(AccountRejected));
    public static string AccountTwoFactorReset => Get(nameof(AccountTwoFactorReset));
    public static string AccountWelcome => Get(nameof(AccountWelcome));
    public static string AccountPasswordChanged => Get(nameof(AccountPasswordChanged));
    public static string AccountPasswordResetCompleted => Get(nameof(AccountPasswordResetCompleted));
    public static string AdminPendingApproval => Get(nameof(AdminPendingApproval));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
