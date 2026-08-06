using System.Globalization;
using System.Resources;

namespace SIMF.Common.Resources.Enums;

/// <summary>Resource accessor for <c>NotificationKind</c> enum display names.</summary>
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
    public static string InvitationReceived => Get(nameof(InvitationReceived));
    public static string VipBroadcast => Get(nameof(VipBroadcast));
    public static string BookingConfirmed => Get(nameof(BookingConfirmed));
    public static string SessionReminder => Get(nameof(SessionReminder));
    public static string BookingRejected => Get(nameof(BookingRejected));
    public static string MeetingScheduled => Get(nameof(MeetingScheduled));
    public static string MeetingCancelled => Get(nameof(MeetingCancelled));
    public static string SessionRatingRequest => Get(nameof(SessionRatingRequest));
    public static string DayRatingRequest => Get(nameof(DayRatingRequest));
    public static string EventRatingRequest => Get(nameof(EventRatingRequest));
    public static string AppRatingRequest => Get(nameof(AppRatingRequest));
    public static string ExhibitionRatingRequest => Get(nameof(ExhibitionRatingRequest));
    public static string MeetingRequestConfirmed => Get(nameof(MeetingRequestConfirmed));
    public static string BookingReleased => Get(nameof(BookingReleased));
    public static string ParticipationDocumentDecided => Get(nameof(ParticipationDocumentDecided));
    public static string BadgeUpdateDecided => Get(nameof(BadgeUpdateDecided));
    public static string MeetingRequested => Get(nameof(MeetingRequested));
    public static string MeetingReminder => Get(nameof(MeetingReminder));
    public static string AdminAnnouncement => Get(nameof(AdminAnnouncement));
    public static string ExhibitorLeadCaptured => Get(nameof(ExhibitorLeadCaptured));
    public static string SessionCancelled => Get(nameof(SessionCancelled));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
