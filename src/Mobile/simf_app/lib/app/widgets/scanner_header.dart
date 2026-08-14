import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The scanner's navy header band (56-high): a circular navy-deep back button
/// at the inline start over a centred white title — mirrors `AccountSubHeader`
/// with `circular: true` (kept local so `app/` doesn't depend on `features/`).
class ScannerHeader extends StatelessWidget {
  const ScannerHeader({required this.title, required this.onBack, super.key});

  final String title;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: SimfTokens.scannerHeaderHeight,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          Align(
            alignment: Alignment.centerLeft,
            child: Padding(
              padding: const EdgeInsets.only(left: 8),
              child: DecoratedBox(
                decoration: const BoxDecoration(
                  color: SimfTokens.navyDeep,
                  shape: BoxShape.circle,
                ),
                child: IconButton(
                  key: const ValueKey<String>('scannerBack'),
                  onPressed: onBack,
                  // Icon-only, over a live camera preview — the one control on
                  // the scanner a user must be able to find without reading the
                  // frame. Same defect and fix as SimfFormScaffold above and
                  // SimfCircledBackButton (DEF-SWEEP-003).
                  tooltip: MaterialLocalizations.of(context).backButtonTooltip,
                  icon: const Icon(
                    Icons.arrow_back_ios_new,
                    color: SimfTokens.surface,
                    size: SimfTokens.scannerHeaderSize,
                    textDirection: TextDirection.ltr,
                  ),
                ),
              ),
            ),
          ),
          Text(
            title,
            style: const TextStyle(
              color: SimfTokens.surface,
              fontSize: SimfTokens.scannerHeaderFontSize,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }
}
