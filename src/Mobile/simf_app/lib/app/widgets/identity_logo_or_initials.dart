import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The identity cell's leading square: the entity logo, falling back to
/// its initials. The two widgets share one size and are used only by each
/// other, so they live together rather than in two files that must agree.
/// The 44px identity square (Figma identity cell).
const double _size = 44;

/// The leading tile: the entity logo/photo on a navy rounded-square when
/// [imageUrl] is set (falling back to the gold initials on load error / 404),
/// otherwise the gold initials avatar directly.
class IdentityLogoOrInitials extends StatelessWidget {
  const IdentityLogoOrInitials(
      {required this.imageUrl, required this.initials, super.key,});

  final String? imageUrl;
  final String initials;

  @override
  Widget build(BuildContext context) {
    final url = imageUrl;
    final avatar = IdentityInitialsAvatar(initials: initials);
    if (url == null || url.isEmpty) {
      return avatar;
    }
    return ClipRRect(
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Image.network(
        url,
        width: _size,
        height: _size,
        fit: BoxFit.cover,
        gaplessPlayback: true,
        loadingBuilder: (context, child, progress) =>
            progress == null ? child : avatar,
        errorBuilder: (context, error, stackTrace) => avatar,
      ),
    );
  }
}

/// The gold rounded-square initials avatar (matches the meet card's avatar).
class IdentityInitialsAvatar extends StatelessWidget {
  const IdentityInitialsAvatar({required this.initials, super.key});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: _size,
      height: _size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        initials,
        textDirection: TextDirection.ltr,
        style: SimfTokens.labelWhiteBoldMd,
      ),
    );
  }
}
