import 'package:flutter/material.dart';

import '../../core/country_flag.dart';
import '../theme/tokens.dart';
import 'simf_page_shell.dart';
import 'simf_svg_icon.dart';

/// A shared identity row (Build #13): the navy [SimfCard] chrome carrying — in
/// RTL — a 44x44 logo/photo tile (or a gold initials avatar) at the inline start,
/// the white [title] (with an optional inline country flag) over the beige
/// [subtitle], and a gold caret at the inline end when [onTap] is set.
///
/// Built for the "Meet People Like You" partner directory (speakers, sponsors,
/// booth companies, opted-in members) so every kind renders through one cell
/// rather than a per-kind copy. When [onTap] is null the row is non-tappable and
/// shows no caret (an opted-in person has no detail screen — their data is on the
/// card).
class SimfIdentityCell extends StatelessWidget {
  const SimfIdentityCell({
    required this.title,
    this.subtitle,
    this.imageUrl,
    this.countryId,
    this.onTap,
    super.key,
  });

  final String title;

  /// The secondary line (rank / tier tagline / sector / job title). Hidden when
  /// null or blank.
  final String? subtitle;

  /// The logo/photo URL. Null or empty renders the gold initials avatar instead
  /// (person rows, or an entity with no uploaded logo).
  final String? imageUrl;

  /// Optional ISO numeric country id — renders the flag glyph inline after the
  /// title (matching the speakers list), never a badge on the avatar.
  final int? countryId;

  /// The tap handler. Null renders a non-tappable row with no caret.
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final flag = countryFlagEmoji(countryId);
    final sub = subtitle?.trim() ?? '';
    // Caret points forward in the reading direction: flip the RTL-drawn glyph
    // horizontally when the app is in English (LTR).
    final flip = Directionality.of(context) == TextDirection.ltr;

    return SimfCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Row(
          children: <Widget>[
            _LogoOrInitials(imageUrl: imageUrl, initials: _initialsFrom(title)),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      Flexible(
                        child: Text(
                          title,
                          textAlign: TextAlign.start,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            color: SimfTokens.surface,
                            fontWeight: FontWeight.w600,
                            fontSize: SimfTokens.textLg,
                          ),
                        ),
                      ),
                      if (flag != null) ...<Widget>[
                        const SizedBox(width: SimfTokens.space2),
                        Text(
                          flag,
                          textDirection: TextDirection.ltr,
                          style: const TextStyle(
                            fontSize: SimfTokens.textSm,
                            height: 1,
                          ),
                        ),
                      ],
                    ],
                  ),
                  if (sub.isNotEmpty) ...<Widget>[
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      sub,
                      textAlign: TextAlign.start,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            if (onTap != null) ...<Widget>[
              const SizedBox(width: SimfTokens.space2),
              Transform.flip(
                flipX: flip,
                child: const SimfSvgIcon(
                  'assets/icons/ic_back.svg',
                  size: 20,
                  color: SimfTokens.accent,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// First letters of the first two name words, dot-joined ("س.أ") — a single
/// letter for a one-word name.
String _initialsFrom(String name) {
  final parts =
      name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
  if (parts.isEmpty) {
    return '?';
  }
  if (parts.length == 1) {
    return parts.first.substring(0, 1);
  }
  return '${parts.first.substring(0, 1)}.${parts[1].substring(0, 1)}';
}

/// The leading tile: the entity logo/photo on a navy rounded-square when
/// [imageUrl] is set (falling back to the gold initials on load error / 404),
/// otherwise the gold initials avatar directly.
class _LogoOrInitials extends StatelessWidget {
  const _LogoOrInitials({required this.imageUrl, required this.initials});

  final String? imageUrl;
  final String initials;

  static const double _size = 44;

  @override
  Widget build(BuildContext context) {
    final url = imageUrl;
    final avatar = _InitialsAvatar(initials: initials);
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
class _InitialsAvatar extends StatelessWidget {
  const _InitialsAvatar({required this.initials});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: _LogoOrInitials._size,
      height: _LogoOrInitials._size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        initials,
        textDirection: TextDirection.ltr,
        style: const TextStyle(
          color: SimfTokens.surface,
          fontSize: SimfTokens.textMd,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}
