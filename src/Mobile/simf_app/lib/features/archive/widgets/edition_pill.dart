import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class EditionPill extends StatelessWidget {
  const EditionPill({
    required this.label,
    required this.active,
    required this.onTap,
  });

  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: active ? SimfTokens.accent : SimfTokens.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: active
            ? BorderSide.none
            : const BorderSide(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
      ),
      child: InkWell(
        onTap: active ? null : onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Container(
          height: SimfTokens.controlHeight,
          alignment: Alignment.center,
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: active
                ? SimfTokens.labelWhiteSemiboldSm
                : SimfTokens.labelBeigeSemiboldSm,
          ),
        ),
      ),
    );
  }
}
