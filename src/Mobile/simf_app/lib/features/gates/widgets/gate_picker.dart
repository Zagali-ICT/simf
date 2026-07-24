import 'package:flutter/material.dart';

import '../../../app/theme/app_theme.dart';
import '../../../app/theme/tokens.dart';
import '../data/gate_models.dart';

/// The assigned-gates dropdown (setup stage), keyed by `gateId`.
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

  @override
  Widget build(BuildContext context) {
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
            child: Text(g.localizedName(isArabic)),
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
