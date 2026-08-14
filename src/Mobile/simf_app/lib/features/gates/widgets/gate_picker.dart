import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';

/// The assigned-gates dropdown (setup stage), keyed by `gateId`.
///
/// DEF-STF-006 — an INACTIVE gate is tagged (and dimmed) in the list. It used
/// to look exactly like a working one, so picking it turned every scan into a
/// red denial with nothing pointing at the gate.
class GatePicker extends StatelessWidget {
  const GatePicker({
    required this.isArabic,
    required this.gates,
    required this.gate,
    required this.onGate,
    super.key,
  });

  final bool isArabic;
  final List<OperatorGate> gates;
  final OperatorGate gate;
  final ValueChanged<OperatorGate> onGate;

  static String label(OperatorGate gate, AppL10n l10n,
      {required bool isArabic,}) {
    final name = gate.localizedName(isArabic: isArabic);
    return gate.isActive ? name : '$name — ${l10n.gateInactiveTag}';
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return DropdownButtonFormField<String>(
      initialValue: gate.gateId,
      isExpanded: true,
      dropdownColor: SimfTokens.navyDeep,
      icon: const Icon(Icons.keyboard_arrow_down, color: SimfTokens.surface),
      // Carry the brand font (FSAlbertArabic + Cairo) explicitly — a
      // DropdownButtonFormField's `style` does NOT inherit the theme font, so
      // the old hardcoded 'Inter' (superseded, D-454) or a bare style both
      // render Arabic gate names as tofu. Sourced from the theme constants.
      style: const TextStyle(
        color: SimfTokens.surface,
        fontFamily: SimfTheme.fontFamily,
        fontFamilyFallback: SimfTheme.fontFamilyFallback,
        fontSize: SimfTokens.textMd,
      ),
      decoration: InputDecoration(
        filled: true,
        fillColor: SimfTokens.navyDeep,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space4,
          vertical: SimfTokens.space2,
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          borderSide: const BorderSide(
            color: SimfTokens.accent,
            width: SimfTokens.hairlineBold,
          ),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          borderSide: const BorderSide(color: SimfTokens.accent),
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        ),
      ),
      items: <DropdownMenuItem<String>>[
        for (final g in gates)
          DropdownMenuItem<String>(
            value: g.gateId,
            child: Text(
              label(g, l10n, isArabic: isArabic),
              // `greyText` is for light surfaces; this list paints on navy, so
              // an inactive row dims to the 65%-white secondary token.
              style: g.isActive
                  ? null
                  : const TextStyle(color: SimfTokens.txtSecondary),
            ),
          ),
      ],
      onChanged: (id) {
        if (id == null) {
          return;
        }
        onGate(gates.firstWhere((g) => g.gateId == id, orElse: () => gate));
      },
    );
  }
}
