import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The account sub-pages' header band (56-high): the white back chevron pinned
/// at the inline start over a centred white title. Shared by the forgot / reset
/// / email-verify / OTP / interests / biometric / badge screens so the one back
/// glyph and title metrics live in a single place (D-658) — before this they
/// each carried a local `_buildHeader` with three different back glyphs
/// (`arrow_back_ios_new`, `ic_back.svg`, `auth_back.svg`).
class AccountSubHeader extends StatelessWidget {
  const AccountSubHeader({
    required this.title,
    required this.onBack,
    this.busy = false,
    this.circular = false,
    super.key,
  });

  final String title;

  final VoidCallback onBack;

  /// While true the back button is disabled (a request is in flight).
  final bool busy;

  /// When true the back chevron sits inside a navy-deep circle — the badge
  /// scanner's over-camera style (D-659); every other sub-page uses the bare
  /// chevron (false).
  final bool circular;

  @override
  Widget build(BuildContext context) {
    final Widget backButton = IconButton(
      key: const ValueKey<String>('accountBack'),
      onPressed: busy ? null : onBack,
      tooltip: MaterialLocalizations.of(context).backButtonTooltip,
      icon: const Icon(
        Icons.arrow_back_ios_new,
        color: SimfTokens.surface,
        size: 20,
        textDirection: TextDirection.ltr,
      ),
    );
    return SizedBox(
      height: SimfTokens.accountSubHeaderHeight,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          Align(
            alignment: Alignment.centerLeft,
            child: Padding(
              padding: const EdgeInsets.only(left: SimfTokens.space2),
              child: circular
                  ? DecoratedBox(
                      decoration: const BoxDecoration(
                        color: SimfTokens.navyDeep,
                        shape: BoxShape.circle,
                      ),
                      child: backButton,
                    )
                  : backButton,
            ),
          ),
          Text(
            title,
            style: const TextStyle(
              color: SimfTokens.surface,
              fontSize: SimfTokens.text24,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }
}
