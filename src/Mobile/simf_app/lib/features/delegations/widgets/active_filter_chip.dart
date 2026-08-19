import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class ActiveFilterChip extends StatelessWidget {
  const ActiveFilterChip({
    required this.country,
    required this.clearLabel,
    required this.onClear,
    super.key,
  });

  final String country;
  final String clearLabel;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: onClear,
        child: Container(
          decoration: BoxDecoration(
            color: SimfTokens.goldFill6,
            border: Border.all(color: SimfTokens.goldBorder15),
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
          padding: const EdgeInsetsDirectional.only(
            start: SimfTokens.space3,
            end: SimfTokens.space2,
            top: SimfTokens.space2,
            bottom: SimfTokens.space2,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Flexible(
                child: Text(
                  country,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: SimfTokens.labelGoldSemiboldSm,
                ),
              ),
              const SizedBox(width: SimfTokens.space1),
              // Semantics label so the tap target reads as "clear filter" to
              // assistive tech, not just a bare glyph.
              Semantics(
                button: true,
                label: clearLabel,
                child: const Icon(
                  Icons.close,
                  size: SimfTokens.activeFilterChipSize,
                  color: SimfTokens.accent,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
