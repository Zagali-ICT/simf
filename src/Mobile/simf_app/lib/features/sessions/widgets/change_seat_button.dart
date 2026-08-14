import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// B1 — the "change seat" action: a full-width gold-outlined button under the
/// frame's action row. Its visible label IS its accessible name, and the
/// leading icon is decorative (no `semanticLabel`), so a screen reader
/// announces exactly "تغيير المقعد" / "Change seat" once.
class ChangeSeatButton extends StatelessWidget {
  const ChangeSeatButton({required this.l10n, required this.onChangeSeat, super.key});

  final AppL10n l10n;
  final VoidCallback onChangeSeat;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      onPressed: onChangeSeat,
      style: OutlinedButton.styleFrom(
        minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
        foregroundColor: SimfTokens.surface,
        side: const BorderSide(color: SimfTokens.accent),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        ),
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space4,
          vertical: SimfTokens.space3,
        ),
      ),
      icon: const Icon(Icons.swap_horiz, size: SimfTokens.changeSeatButtonSize),
      label: Text(l10n.seatChangeCta, style: SimfTokens.labelSemiboldSm),
    );
  }
}
