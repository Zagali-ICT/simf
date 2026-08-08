import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';

/// The profile avatar with a tap-to-change affordance (frame 213:963): the gold
/// rounded avatar plus a small camera badge at the corner. A null [onTap] (the
/// limited/pending view) renders a plain avatar with no camera badge.
class TappableAvatar extends StatelessWidget {
  const TappableAvatar({
    required this.name,
    this.onTap,
    this.tooltip,
  });

  final String name;
  final VoidCallback? onTap;
  final String? tooltip;

  @override
  Widget build(BuildContext context) {
    final avatar = SimfAvatar(name: name, currentUser: true, size: 64);
    if (onTap == null) {
      return avatar;
    }
    return Semantics(
      button: true,
      label: tooltip,
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: Stack(
          clipBehavior: Clip.none,
          children: <Widget>[
            avatar,
            Positioned.directional(
              textDirection: Directionality.of(context),
              end: -2,
              bottom: -2,
              child: Container(
                padding: const EdgeInsets.all(SimfTokens.space1),
                decoration: BoxDecoration(
                  color: SimfTokens.accent,
                  shape: BoxShape.circle,
                  border: Border.all(color: SimfTokens.navyDeep, width: SimfTokens.tappableAvatarWidth),
                ),
                child: const Icon(
                  Icons.photo_camera_outlined,
                  size: SimfTokens.tappableAvatarSize,
                  color: SimfTokens.navy,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
