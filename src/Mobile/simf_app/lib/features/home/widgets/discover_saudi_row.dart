import 'dart:async';

import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/confirm_external_link.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../core/env/build_config.dart';

/// The روح السعودية discover row, shared by the guest + signed-in layouts —
/// opens the configured Visit-Saudi link externally.
class DiscoverSaudiRow extends StatelessWidget {
  const DiscoverSaudiRow({required this.l10n, this.outlined = false, super.key});

  final AppL10n l10n;

  /// The guest home uses the **outlined** badge (gold hairline + gold "KSA"),
  /// matching frame 758:2910; the signed-in home keeps the filled gold badge.
  final bool outlined;

  @override
  Widget build(BuildContext context) {
    return SimfListRow(
      title: l10n.discoverSaudiTitle,
      subtitle: l10n.discoverSaudiSubtitle,
      badgeOutlined: outlined,
      // Guest = outlined gold "KSA" (758:2910); signed-in = filled gold
      // "السعودية" SemiBold (758:1280/1281).
      badge: Text(
        outlined ? l10n.discoverSaudiBadgeShort : l10n.discoverSaudiBadge,
        textAlign: TextAlign.center,
        style: TextStyle(
          color: outlined ? SimfTokens.accent : SimfTokens.surface,
          fontSize: SimfTokens.textMd,
          fontWeight: outlined ? FontWeight.w800 : FontWeight.w600,
        ),
      ),
      onTap: () => unawaited(
        confirmThenLaunchExternal(context, BuildConfig.visitSaudiUrl),
      ),
    );
  }
}
