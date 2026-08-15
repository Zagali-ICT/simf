import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

/// The action row (frame 908:1733 / 908:1737): a gold-outlined "share location"
/// next to a gold-filled "guide me to my seat".
class SessionsActions extends StatelessWidget {
  const SessionsActions(
      {required this.l10n, required this.onNavigate, super.key, this.onShare,});

  final AppL10n l10n;
  final VoidCallback onNavigate;
  final VoidCallback? onShare;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        // RTL: the gold-filled "guide me" CTA sits at the inline-start (right),
        // the outlined "share location" at the inline-end (left) — frame
        // 908:1733 / 908:1737.
        Expanded(
          child: FilledButton.icon(
            onPressed: onNavigate,
            style: FilledButton.styleFrom(
              backgroundColor: SimfTokens.accent,
              foregroundColor: SimfTokens.surface,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
              padding: const EdgeInsets.symmetric(
                horizontal: SimfTokens.space4,
                vertical: SimfTokens.space3,
              ),
            ),
            icon: const SimfSvgIcon(
              AppAssets.icLocation,
              size: SimfTokens.sessionsActionsSize,
              color: SimfTokens.surface,
            ),
            label: Text(
              l10n.navigateToSeat,
              style: SimfTokens.labelSemiboldSm,
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: OutlinedButton.icon(
            onPressed: onShare,
            style: OutlinedButton.styleFrom(
              foregroundColor: SimfTokens.surface,
              side: const BorderSide(color: SimfTokens.accent),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
              padding: const EdgeInsets.symmetric(
                horizontal: SimfTokens.space4,
                vertical: SimfTokens.space3,
              ),
            ),
            icon: const Icon(Icons.share_outlined,
                size: SimfTokens.sessionsActionsSize,),
            label: Text(
              l10n.shareLocation,
              style: SimfTokens.labelSemiboldSm,
            ),
          ),
        ),
      ],
    );
  }
}
