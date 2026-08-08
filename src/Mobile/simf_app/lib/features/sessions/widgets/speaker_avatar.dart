import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The speaker's photo on a speaker card (frame 1060:12892): a 40×40 rounded
/// square with a beige hairline. Renders the uploaded SpeakerPhoto asset
/// (D-357), falling back to a navy person glyph while it loads or when the
/// speaker has no photo (the asset route 404s).
class SpeakerAvatar extends StatelessWidget {
  const SpeakerAvatar({required this.imageUrl});

  final String imageUrl;

  @override
  Widget build(BuildContext context) {
    const placeholder = ColoredBox(
      color: SimfTokens.navy,
      child: Center(
        child: Icon(Icons.person, size: 20, color: SimfTokens.beigeBorder),
      ),
    );
    return Container(
      width: SimfTokens.space10,
      height: SimfTokens.space10,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Image.network(
        imageUrl,
        fit: BoxFit.cover,
        gaplessPlayback: true,
        loadingBuilder: (context, child, progress) =>
            progress == null ? child : placeholder,
        errorBuilder: (context, error, stackTrace) => placeholder,
      ),
    );
  }
}
