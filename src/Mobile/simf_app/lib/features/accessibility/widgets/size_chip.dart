import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class SizeChip extends StatelessWidget {
  const SizeChip({
    required this.label,
    required this.selected,
    required this.onTap,
    super.key,
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
          height: SimfTokens.sizeChipHeight,
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
            // #16 — the two states are named tokens, not an inline style built
            // from tokens. Same values: selected = white/12/w600, unselected =
            // beige/12 at the theme's regular weight.
            style: selected
                ? SimfTokens.labelWhiteSemiboldSm
                : SimfTokens.labelBeigeSm,
          ),
        ),
      ),
    );
  }
}
