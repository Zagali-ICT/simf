import 'package:flutter/material.dart';
import '../theme/tokens.dart';

/// One media-center tab pill (frame node 1049:12640 / 1049:12642): a 48-high,
/// 8px-radius pill — active is solid gold with **white** text; inactive is a
/// transparent pill with a beige 0.2 hairline and beige text.
class MediaTab extends StatelessWidget {
  const MediaTab({required this.label, required this.active, this.onTap});

  final String label;
  final bool active;
  final VoidCallback? onTap;

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
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: SizedBox(
          height: SimfTokens.mediaTabHeight,
          child: Center(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
              child: Text(
                label,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                  // Figma 1049 — the active gold pill carries white text.
                  color: active ? SimfTokens.surface : SimfTokens.beigeBorder,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
