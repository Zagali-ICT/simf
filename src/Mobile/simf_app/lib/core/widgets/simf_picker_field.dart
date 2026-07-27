import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import 'simf_field_style.dart';

/// A tappable "open the searchable picker" field — the shared chrome (beige
/// field + down chevron) every lookup field uses (nationality, birth-region,
/// profile-type, plate-letter). [displayText] is the selected label or the
/// placeholder ([isPlaceholder] greys it). The picker logic stays in the screen
/// ([onTap] opens the sheet); this is pure display. [showChevron] false drops
/// the arrow (the narrow plate-letter boxes need the full width — D-459).
///
/// A null [onTap] renders the field un-tappable — for a lookup with nothing to
/// pick, where opening an empty sheet would only waste the user's time
/// (DEF-STF-007). Pair it with an [errorText] that says why.
class SimfPickerField extends StatelessWidget {
  const SimfPickerField({
    required this.fieldKey,
    required this.displayText,
    required this.isPlaceholder,
    required this.onTap,
    this.errorText,
    this.showChevron = true,
    super.key,
  });

  final String fieldKey;
  final String displayText;
  final bool isPlaceholder;
  final VoidCallback? onTap;
  final String? errorText;
  final bool showChevron;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      key: ValueKey<String>(fieldKey),
      onTap: onTap,
      borderRadius: SimfTokens.borderRadiusSmall,
      child: InputDecorator(
        decoration: simfFieldDecoration(
          errorText: errorText,
          suffixIcon: showChevron
              ? const Icon(
                  Icons.keyboard_arrow_down,
                  color: SimfTokens.greyText,
                )
              : null,
        ),
        child: Text(
          displayText,
          style: isPlaceholder
              ? simfInputStyle.copyWith(color: SimfTokens.greyText)
              : simfInputStyle,
          overflow: TextOverflow.ellipsis,
        ),
      ),
    );
  }
}
