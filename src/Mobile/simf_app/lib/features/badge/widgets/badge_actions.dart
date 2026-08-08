import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/features/contacts/scan_contact_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// The role-based QR-page actions (D-426). A visitor reads another visitor's
/// shared contact (gold-filled "امسح لإضافة شخص") and shares their own (outlined);
/// an exhibitor scans visitor badges into My Visitors.
///
/// **DEF-EXH-005:** the branch keys off the signed-in [AppRole], not the
/// dashboard's `isVisitor` flag. `isVisitor` is false for EVERY partner profile
/// type (Staff, Moderator, Media, Sponsor, Exhibitor), so it showed the
/// exhibitor-only "مسح بطاقة زائر" button to all of them and the router
/// (`_routeRoles[106] = {exhibitor}`) then bounced them home — a visible dead
/// control. The role sets here mirror `_routeRoles`: routes 100..102 (the
/// contact actions) are attendee-only and route 106 is exhibitor-only, so a
/// Staff / Moderator badge now shows no action at all.
class BadgeActions extends StatelessWidget {
  const BadgeActions({required this.l10n, required this.role, super.key});

  final AppL10n l10n;

  /// The signed-in user's effective app role (`CurrentUser.effectiveAppRole` —
  /// a not-yet-approved account presents as [AppRole.guest], D-666).
  final AppRole role;

  @override
  Widget build(BuildContext context) {
    if (role == AppRole.exhibitor) {
      return _actionButton(
        icon: const SimfSvgIcon(
          AppAssets.badgeScan,
          size: SimfTokens.badgeActionsSize,
          color: SimfTokens.surface,
        ),
        label: l10n.badgeScanVisitor,
        onTap: () => context.pushNamed(RouteNames.scanVisitor),
      );
    }
    if (role == AppRole.visitor) {
      return Column(
        children: <Widget>[
          _actionButton(
            // Frame 758:1469 — the primary "امسح لإضافة شخص" action is the
            // gold-FILLED button; the share action below it stays outlined.
            filled: true,
            icon: const SimfSvgIcon(
              AppAssets.badgeScan,
              size: SimfTokens.badgeActionsSize,
              color: SimfTokens.surface,
            ),
            label: l10n.badgeAddPerson,
            // Open as a self-contained fullscreen modal (NOT a go_router route):
            // the modal closes reliably via Navigator.pop, sidestepping the
            // shell-route pop bug that left the scanner stuck (D-426).
            onTap: () => Navigator.of(context, rootNavigator: true).push<void>(
              MaterialPageRoute<void>(
                fullscreenDialog: true,
                builder: (_) => const ScanContactScreen(),
              ),
            ),
          ),
          const SizedBox(height: SimfTokens.space3),
          _actionButton(
            icon: const Icon(
              Icons.qr_code_2,
              size: SimfTokens.badgeActionsSize,
              color: SimfTokens.surface,
            ),
            label: l10n.shareMyContactTitle,
            onTap: () => context.pushNamed(RouteNames.shareMyContact),
          ),
        ],
      );
    }
    // Guest / Staff / Moderator — none of the badge actions are open to them.
    return const SizedBox.shrink();
  }

  /// One full-width QR-page action button (frame 758:1469). [filled] = the gold
  /// primary button (امسح لإضافة شخص); otherwise the gold-bordered outlined
  /// variant (the share action).
  Widget _actionButton({
    required Widget icon,
    required String label,
    required VoidCallback onTap,
    bool filled = false,
  }) {
    final labelText = Text(
      label,
      style: SimfTokens.labelWhiteBoldLg,
    );
    if (filled) {
      return FilledButton.icon(
        onPressed: onTap,
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
          backgroundColor: SimfTokens.accent,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
        ),
        icon: icon,
        label: labelText,
      );
    }
    return OutlinedButton.icon(
      onPressed: onTap,
      style: OutlinedButton.styleFrom(
        minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
        side: const BorderSide(color: SimfTokens.accent, width: 1),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        ),
      ),
      icon: icon,
      label: labelText,
    );
  }
}
