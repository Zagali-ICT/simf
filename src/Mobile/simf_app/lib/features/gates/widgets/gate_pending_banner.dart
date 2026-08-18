import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// Slim "N waiting to sync" banner above the active scanning stages while the
/// offline backlog is non-empty (G-4); collapses to nothing when it drains.
class GatePendingBanner extends StatelessWidget {
  const GatePendingBanner({
    required this.pending,
    required this.child,
    super.key,
  });

  final int pending;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (pending == 0) {
      return child;
    }
    final l10n = AppL10n.of(context);
    return Column(
      children: <Widget>[
        Container(
          width: double.infinity,
          color: SimfTokens.navyDeep,
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space4,
            vertical: SimfTokens.space2,
          ),
          child: Row(
            children: <Widget>[
              const Icon(
                Icons.sync,
                color: SimfTokens.accent,
                size: SimfTokens.gateScanScreenSizeSm,
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: Text(
                  l10n.gatePendingSync(pending),
                  style: SimfTokens.labelBeigeSm,
                ),
              ),
            ],
          ),
        ),
        Expanded(child: child),
      ],
    );
  }
}
