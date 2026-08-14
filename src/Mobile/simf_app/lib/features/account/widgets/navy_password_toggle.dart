import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/account/widgets/account_form_field.dart' show AccountPasswordField;

/// The show/hide eye toggle used inside the navy-family password fields
/// (reset-password / badge-activation) — the beige Material eye wired to the
/// caller's obscure state. Shared so the glyph + colour live in one place
/// (D-659); the card-family fields use [AccountPasswordField]'s own SVG eye.
class NavyPasswordToggle extends StatelessWidget {
  const NavyPasswordToggle({
    required this.obscure,
    required this.onToggle,
    super.key,
  });

  final bool obscure;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return IconButton(
      tooltip: obscure ? l10n.showPasswordTooltip : l10n.hidePasswordTooltip,
      icon: Icon(
        obscure ? Icons.visibility_off_outlined : Icons.visibility_outlined,
        size: SimfTokens.navyPasswordToggleSize,
        color: SimfTokens.beigeBorder,
      ),
      onPressed: onToggle,
    );
  }
}
