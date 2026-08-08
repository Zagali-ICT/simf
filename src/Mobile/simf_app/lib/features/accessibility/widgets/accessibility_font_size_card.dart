import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/accessibility_controller.dart';
import 'size_chip.dart';

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
                  child: SizeChip(
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

