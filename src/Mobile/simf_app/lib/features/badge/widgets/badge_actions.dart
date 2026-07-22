import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/route_names.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';
import '../../contacts/scan_contact_screen.dart';

/// The role-based QR-page actions (D-426). A visitor reads another visitor's
/// shared contact (gold-filled "امسح لإضافة شخص") and shares their own (outlined);
/// an exhibitor ("Other") scans visitor badges into My Visitors.
class BadgeActions extends StatelessWidget {
  const BadgeActions({required this.l10n, required this.isVisitor, super.key});

  final AppL10n l10n;
  final bool isVisitor;

  @override
  Widget build(BuildContext context) {
    if (isVisitor) {
      return Column(
        children: <Widget>[
          _actionButton(
            // Frame 758:1469 — the primary "امسح لإضافة شخص" action is the
            // gold-FILLED button; the share action below it stays outlined.
            filled: true,
            icon: const SimfSvgIcon(
              AppAssets.badgeScan,
              size: 24,
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
              size: 24,
              color: SimfTokens.surface,
            ),
            label: l10n.shareMyContactTitle,
            onTap: () => context.pushNamed(RouteNames.shareMyContact),
          ),
        ],
      );
    }
    return _actionButton(
      icon: const SimfSvgIcon(
        AppAssets.badgeScan,
        size: 24,
        color: SimfTokens.surface,
      ),
      label: l10n.badgeScanVisitor,
      onTap: () => context.pushNamed(RouteNames.scanVisitor),
    );
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
