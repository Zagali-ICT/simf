import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The two success-frame actions: the gold حالة التسجيل primary (→ status) and
/// the outlined الانتقال للرئيسية secondary (→ home). Figma 505:1525+.
class RegistrationSuccessActions extends StatelessWidget {
  const RegistrationSuccessActions({
    required this.statusLabel,
    required this.homeLabel,
    required this.onStatus,
    required this.onHome,
    super.key,
  });

  final String statusLabel;
  final String homeLabel;
  final VoidCallback onStatus;
  final VoidCallback onHome;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        FilledButton(
          onPressed: onStatus,
          child: Text(
            statusLabel,
            style: SimfTokens.titleBold,
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        OutlinedButton(
          onPressed: onHome,
          style: OutlinedButton.styleFrom(
            side: const BorderSide(color: SimfTokens.accent),
            foregroundColor: SimfTokens.surface,
            minimumSize: const Size.fromHeight(48),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
          ),
          child: Text(
            homeLabel,
            style: SimfTokens.titleBold,
          ),
        ),
      ],
    );
  }
}
