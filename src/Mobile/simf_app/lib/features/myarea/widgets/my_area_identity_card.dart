import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';

/// The My-Area identity card (frame node 512:2047): avatar 64, name +
/// tier·enrolled line + gold reference, and the bordered gold مشاركة button.
class MyAreaIdentityCard extends StatelessWidget {
  const MyAreaIdentityCard({
    required this.name,
    required this.line,
    this.reference,
    this.shareLabel,
    this.onShare,
    this.onAvatarTap,
    this.avatarTooltip,
    super.key,
  });

  final String name;
  final String line;
  final String? reference;
  final String? shareLabel;
  final VoidCallback? onShare;
  final VoidCallback? onAvatarTap;
  final String? avatarTooltip;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(color: SimfTokens.accent, width: 0.2),
      ),
      child: Row(
        children: <Widget>[
          _TappableAvatar(
            name: name,
            onTap: onAvatarTap,
            tooltip: avatarTooltip,
          ),
          const SizedBox(width: SimfTokens.space3),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  name,
                  style: const TextStyle(
                    color: SimfTokens.surface,
                    fontWeight: FontWeight.w600,
                    fontSize: SimfTokens.textTitle,
                  ),
                ),
                const SizedBox(height: SimfTokens.space2),
                Text(
                  line,
                  style: const TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
                if (reference != null) ...<Widget>[
                  const SizedBox(height: SimfTokens.space2),
                  Text(
                    reference!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    textDirection: TextDirection.ltr,
                    style: const TextStyle(
                      color: SimfTokens.accent,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ],
              ],
            ),
          ),
          if (onShare != null) ...<Widget>[
            const SizedBox(width: SimfTokens.space2),
            InkWell(
              onTap: onShare,
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              child: Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: SimfTokens.navy,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                  border: Border.all(color: SimfTokens.accent, width: 0.5),
                ),
                child: FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Padding(
                    padding: const EdgeInsets.all(SimfTokens.space1),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        const Icon(
                          Icons.share_outlined,
                          size: 18,
                          color: SimfTokens.accent,
                        ),
                        if (shareLabel != null)
                          Text(
                            shareLabel!,
                            style: const TextStyle(
                              color: SimfTokens.accent,
                              fontSize: SimfTokens.textSm,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// The profile avatar with a tap-to-change affordance (frame 213:963): the gold
/// rounded avatar plus a small camera badge at the corner. A null [onTap] (the
/// limited/pending view) renders a plain avatar with no camera badge.
class _TappableAvatar extends StatelessWidget {
  const _TappableAvatar({
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
                  border: Border.all(color: SimfTokens.navyDeep, width: 1.5),
                ),
                child: const Icon(
                  Icons.photo_camera_outlined,
                  size: 12,
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
