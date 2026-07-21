import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';

/// A labeled checkbox row for the app's light forms: a 19x19 gold-active box
/// (matching the sign-up terms / remember-me control) with a tappable label that
/// toggles it. Shared so a visibility/consent toggle looks the same everywhere
/// rather than a per-screen `CheckboxListTile` copy.
class SimfCheckboxTile extends StatelessWidget {
  const SimfCheckboxTile({
    required this.value,
    required this.onChanged,
    required this.label,
    this.enabled = true,
    this.labelColor = SimfTokens.headlineInk,
    super.key,
  });

  final bool value;
  final ValueChanged<bool> onChanged;
  final String label;
  final bool enabled;

  /// Label colour — defaults to the light-form ink; pass a light token on a
  /// dark (navy) surface.
  final Color labelColor;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        SizedBox(
          width: 19,
          height: 19,
          child: Checkbox(
            value: value,
            onChanged: enabled ? (v) => onChanged(v ?? false) : null,
            activeColor: SimfTokens.accent,
            side: const BorderSide(color: SimfTokens.greyText, width: 1.5),
            shape: const RoundedRectangleBorder(
              borderRadius: SimfTokens.borderRadiusSmall,
            ),
            materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: GestureDetector(
            onTap: enabled ? () => onChanged(!value) : null,
            behavior: HitTestBehavior.opaque,
            child: Text(
              label,
              style: TextStyle(
                fontSize: 14,
                color: labelColor,
              ),
            ),
          ),
        ),
      ],
    );
  }
}
