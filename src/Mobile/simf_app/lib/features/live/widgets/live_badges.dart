import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/localization/locale_controller.dart';
import '../../../app/theme/tokens.dart';

/// The red LIVE badge (frame 934:3609): a white pulse dot after the label.
class LiveBadge extends StatelessWidget {
  const LiveBadge({required this.label, super.key});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      // Frame 934:3609 — px10 py4 on a brick-red (#C0392B) pill, radius 6.
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.gap10,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.liveRed,
        borderRadius: BorderRadius.circular(SimfTokens.radius6),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        // The live label leads and the ~7px white pulse dot trails it (frame
        // 934:3609/3610); forced LTR keeps the dot on the trailing side. (The
        // frame's Latin "LIVE" is localized to مباشر via l10n.liveNowLabel.)
        textDirection: TextDirection.ltr,
        children: <Widget>[
          Text(
            label,
            // Frame 934:3611 — 12px Bold.
            style: SimfTokens.labelWhiteBoldSm,
          ),
          const SizedBox(width: SimfTokens.gap5),
          Container(
            width: 7,
            height: 7,
            decoration: const BoxDecoration(
              color: SimfTokens.surface,
              shape: BoxShape.circle,
            ),
          ),
        ],
      ),
    );
  }
}

/// The frame's caption-language chip (934:3450) on the player band, opposite the
/// LIVE badge. Shows the current UI language's native name + a globe glyph and
/// toggles the app language on tap (owner decision: wire to the app locale).
class LanguageChip extends ConsumerWidget {
  const LanguageChip({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return Material(
      // Frame 934:3604 — a translucent dark glassy pill with a gold hairline and
      // white text/icon (was a solid white chip with navy glyphs).
      color: SimfTokens.scrimBlack55,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(
          color: SimfTokens.accent,
          width: SimfTokens.hairline,
        ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        onTap: () =>
            unawaited(ref.read(localeControllerProvider.notifier).toggle()),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.gap10,
            vertical: SimfTokens.gap5,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                l10n.currentLanguageAutonym,
                style: SimfTokens.bodyWhiteRegularSm,
              ),
              const SizedBox(width: SimfTokens.gap5),
              const Icon(Icons.language, size: 14, color: SimfTokens.surface),
            ],
          ),
        ),
      ),
    );
  }
}
