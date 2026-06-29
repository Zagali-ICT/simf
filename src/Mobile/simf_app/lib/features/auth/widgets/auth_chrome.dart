import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

final ButtonStyle authSubmitButtonStyle = FilledButton.styleFrom(
  backgroundColor: SimfTokens.accent,
  disabledBackgroundColor: SimfTokens.accent.withValues(alpha: 0.5),
  minimumSize: const Size.fromHeight(48),
  shape: const RoundedRectangleBorder(
    borderRadius: SimfTokens.borderRadiusSmall,
  ),
);

/// The KSA card's gold submit button with the busy spinner — shared by the
/// forgot/reset screens so the spinner/typography stays in one place.
class AuthSubmitButton extends StatelessWidget {
  const AuthSubmitButton({
    required this.label,
    required this.busy,
    required this.onPressed,
    super.key,
  });

  final String label;
  final bool busy;

  /// Null disables the button (the busy spinner still shows when [busy]).
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      onPressed: onPressed,
      style: authSubmitButtonStyle,
      child: busy
          ? const SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: Colors.white,
              ),
            )
          : Text(
              label,
              style: const TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w700,
                color: Colors.white,
              ),
            ),
    );
  }
}

