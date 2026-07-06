import 'package:flutter/material.dart';

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
    super.key,
  });

  final String title;

  final VoidCallback onBack;

  /// While true the back button is disabled (a request is in flight).
  final bool busy;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 56,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          Align(
            alignment: Alignment.centerLeft,
            child: Padding(
              padding: const EdgeInsets.only(left: 8),
              child: IconButton(
                key: const ValueKey<String>('accountBack'),
                onPressed: busy ? null : onBack,
                icon: const Icon(
                  Icons.arrow_back_ios_new,
                  color: Colors.white,
                  size: 20,
                  textDirection: TextDirection.ltr,
                ),
              ),
            ),
          ),
          Text(
            title,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 24,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }
}
