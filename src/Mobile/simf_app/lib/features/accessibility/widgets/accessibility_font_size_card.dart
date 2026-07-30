import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/accessibility_controller.dart';

/// The "حجم الخط" card (frame 1116:16630): the label over a row of four pill
/// chips, the selected one filled gold.
class AccessibilityFontSizeCard extends StatelessWidget {
  const AccessibilityFontSizeCard({
    required this.value,
    required this.onChanged,
    super.key,
  });

  final AppTextSize value;
  final ValueChanged<AppTextSize> onChanged;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // Reading order (RTL) صغير · متوسط · كبير · أكبر.
    final options = <(AppTextSize, String)>[
      (AppTextSize.small, l10n.accessibilityTextSizeSmall),
      (AppTextSize.normal, l10n.accessibilityTextSizeDefault),
      (AppTextSize.large, l10n.accessibilityTextSizeLarge),
      (AppTextSize.extraLarge, l10n.accessibilityTextSizeExtraLarge),
    ];
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.accessibilityTextSizeLabel,
            textAlign: TextAlign.start,
            style: SimfTokens.labelWhiteMedium,
          ),
          const SizedBox(height: SimfTokens.space3),
          Row(
            children: <Widget>[
              for (final (index, (size, label)) in options.indexed) ...<Widget>[
                if (index > 0) const SizedBox(width: SimfTokens.space2),
                Expanded(
                  child: _SizeChip(
                    label: label,
                    selected: size == value,
                    onTap: () => onChanged(size),
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

/// One font-size pill: gold fill when [selected], else a beige-hairline outline.
class _SizeChip extends StatelessWidget {
  const _SizeChip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      selected: selected,
      label: label,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Container(
          height: 36,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected ? SimfTokens.accent : SimfTokens.transparent,
            border: selected
                ? null
                : Border.all(
                    color: SimfTokens.beigeBorder,
                    width: SimfTokens.hairline,
                  ),
            borderRadius: BorderRadius.circular(SimfTokens.radius),
          ),
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: selected ? SimfTokens.surface : SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              fontWeight: selected ? FontWeight.w600 : FontWeight.w400,
            ),
          ),
        ),
      ),
    );
  }
}
