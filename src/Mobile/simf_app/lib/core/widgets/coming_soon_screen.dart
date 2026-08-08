import 'package:flutter/material.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';

/// Placeholder screen shown for any route that has not yet been built.
///
/// Used in Phase 1 for every non-auth route; replaced in Phase 3 by the
/// matching `mkp_*` screen.
class ComingSoonScreen extends StatelessWidget {
  const ComingSoonScreen({
    required this.screenNumber,
    required this.screenLabelAr,
    required this.screenLabelEn,
    super.key,
  });

  /// The mockup screen number (1..41).
  final int screenNumber;
  final String screenLabelAr;
  final String screenLabelEn;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final label = l10n.isArabic ? screenLabelAr : screenLabelEn;
    return Scaffold(
      appBar: AppBar(
        title: Text(label),
      ),
      body: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Container(
                width: SimfTokens.comingSoonScreenWidth,
                height: SimfTokens.comingSoonScreenHeight,
                decoration: const BoxDecoration(
                  color: SimfTokens.accent,
                  shape: BoxShape.circle,
                ),
                alignment: Alignment.center,
                child: Text(
                  screenNumber.toString().padLeft(2, '0'),
                  style: const TextStyle(
                    color: SimfTokens.navy,
                    fontWeight: FontWeight.w700,
                    fontSize: SimfTokens.textXl,
                  ),
                ),
              ),
              const SizedBox(height: SimfTokens.space4),
              Text(
                l10n.comingSoonTitle,
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: SimfTokens.space2),
              Text(
                l10n.comingSoonBody,
                style: Theme.of(context).textTheme.bodyMedium,
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
