import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';

/// The underlined terms-agreement link (Figma 522:2179 — opens Page 009) above
/// the primary Next button that submits the step. [onNext] runs the screen's
/// validate-and-advance logic.
class TermsAndNextButtons extends StatelessWidget {
  const TermsAndNextButtons({
    required this.onNext,
    this.busy = false,
    super.key,
  });

  final VoidCallback onNext;

  /// While the profile is being saved (D-684 profile-first save) the button is
  /// disabled and shows a spinner so the step can't be double-submitted.
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Center(
          child: TextButton(
            onPressed: () => context.pushNamed(RouteNames.terms),
            style: TextButton.styleFrom(
              padding: EdgeInsets.zero,
              minimumSize: Size.zero,
              tapTargetSize: MaterialTapTargetSize.shrinkWrap,
              foregroundColor: SimfTokens.navy,
            ),
            child: Text(
              l10n.termsAgreeQuestion,
              style: const TextStyle(
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w600,
                decoration: TextDecoration.underline,
                // The frame underlines in navySurface, not the navy foreground
                // (owner: 0xFF102238, D-674).
                decorationColor: SimfTokens.navySurface,
              ),
            ),
          ),
        ),
        const SizedBox(height: SimfTokens.space6),
        // BUG-019 / 19h — the button loses its only label while the spinner is
        // up, so the accessible name is declared explicitly and always present.
        Semantics(
          button: true,
          label: l10n.nextLabel,
          child: FilledButton(
            onPressed: busy ? null : onNext,
            child: busy
                ? const SizedBox(
                    height: 20,
                    width: 20,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: SimfTokens.surface,
                    ),
                  )
                : Text(
                    l10n.nextLabel,
                    style: SimfTokens.titleBold,
                  ),
          ),
        ),
      ],
    );
  }
}
