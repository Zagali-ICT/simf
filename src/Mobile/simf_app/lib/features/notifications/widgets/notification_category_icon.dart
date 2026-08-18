import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/notifications/data/notification_models.dart';

/// The solid colour-coded circular category mark. Per Figma 758:2491 the icon
/// is styled **per notification kind** (a decorative colour + glyph), not per
/// severity — a gold ticket for an approved account, a green check for a
/// session reminder, a green card for a confirmed meeting, and a coral mark for
/// a VIP invitation. Kinds the frame does not document are grouped by meaning
/// (positive = green, alert/negative = coral, info/account = gold); any unknown
/// / future kind falls back to its severity colour so the wire stays
/// forward-safe.
class NotificationCategoryIcon extends StatelessWidget {
  const NotificationCategoryIcon({
    required this.kind,
    required this.severity,
    super.key,
  });

  final String kind;
  final NotificationSeverity severity;

  @override
  Widget build(BuildContext context) {
    final (color, icon) = _styleFor(kind, severity);
    return Container(
      width: SimfTokens.space10,
      height: SimfTokens.space10,
      decoration: BoxDecoration(color: color, shape: BoxShape.circle),
      child: Icon(icon,
          size: SimfTokens.notificationCategoryIconSize,
          color: SimfTokens.surface,),
    );
  }

  /// (colour, glyph) for a notification kind (`NotificationKind` enum names,
  /// D-053). The four kinds the frame (758:2491) documents are matched exactly
  /// on colour; their glyphs match too, except the VIP card — the mockup shows
  /// a ✕ (close-circle) on a *positive* VIP invitation, which reads as an
  /// error, so a star is used instead (the one deliberate deviation; swap to
  /// `Icons.cancel_rounded` to mirror the mockup literally).
  static (Color, IconData) _styleFor(
    String kind,
    NotificationSeverity severity,
  ) {
    switch (kind) {
      // ---- Figma 758:2491 documented kinds (exact colour) ----
      case 'AccountApproved': // gold ticket — "your badge is ready"
        return (SimfTokens.accent, Icons.confirmation_number_rounded);
      case 'SessionReminder': // green check
        return (SimfTokens.notifGreen, Icons.check_circle_rounded);
      case 'MeetingScheduled': // green card
        return (SimfTokens.notifGreen, Icons.credit_card_rounded);
      case 'InvitationReceived': // coral — VIP special (star, see note)
      case 'VipBroadcast':
        return (SimfTokens.notifCoral, Icons.star_rounded);
      case 'BookingConfirmed':
        return (SimfTokens.notifGreen, Icons.event_available_rounded);
      case 'AccountWelcome':
        return (SimfTokens.notifGreen, Icons.celebration_rounded);
      case 'AccountPasswordChanged':
      case 'AccountPasswordResetCompleted':
        return (SimfTokens.notifGreen, Icons.lock_reset_rounded);
      case 'BookingRejected':
      case 'MeetingCancelled':
        return (SimfTokens.notifCoral, Icons.event_busy_rounded);
      case 'AccountRejected':
        return (SimfTokens.notifCoral, Icons.cancel_rounded);
      case 'AccountProfileSubmitted':
      case 'AdminPendingVisitor':
      case 'AdminPendingApproval':
        return (SimfTokens.accent, Icons.how_to_reg_rounded);
      case 'AccountTwoFactorReset':
        return (SimfTokens.accent, Icons.shield_rounded);
      // A biometric credential was bound to the account. Grouped with the
      // credential-info kinds rather than the green "completions", because the
      // row exists for the case where the owner did NOT do it: an enrolled
      // device key outlives a session revoke, so this is the notice that has to
      // read as something to check, not something to celebrate.
      case 'DeviceKeyEnrolled':
        return (SimfTokens.accent, Icons.fingerprint_rounded);
      case 'CredentialEmailVerificationSent':
      case 'CredentialEmailVerificationResent':
        return (SimfTokens.accent, Icons.mark_email_unread_rounded);
      case 'CredentialSignInOtpSent':
        return (SimfTokens.accent, Icons.password_rounded);
      case 'CredentialPasswordResetRequested':
        return (SimfTokens.accent, Icons.lock_open_rounded);
      // Rating prompts — session / day / event / app / exhibition (D-678).
      case 'SessionRatingRequest':
      case 'DayRatingRequest':
      case 'EventRatingRequest':
      case 'AppRatingRequest':
      case 'ExhibitionRatingRequest':
        return (SimfTokens.accent, Icons.star_outline_rounded);
      default:
        return _severityStyle(severity);
    }
  }

  /// Fallback for an unknown / future kind — the original severity-driven style.
  static (Color, IconData) _severityStyle(NotificationSeverity severity) =>
      switch (severity) {
        NotificationSeverity.success => (
            SimfTokens.success,
            Icons.check_rounded,
          ),
        NotificationSeverity.error => (SimfTokens.danger, Icons.close_rounded),
        NotificationSeverity.warning => (
            SimfTokens.accent,
            Icons.priority_high_rounded,
          ),
        NotificationSeverity.info => (
            SimfTokens.accent,
            Icons.mail_outline_rounded,
          ),
      };
}
