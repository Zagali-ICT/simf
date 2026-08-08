import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The black player-band placeholder for the non-live states (recording /
/// not-live) — keeps the frame's full-bleed black band, centring an icon +
/// message where the feed would play.
class MessageSurface extends StatelessWidget {
  const MessageSurface({required this.icon, required this.message});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.black,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: AspectRatio(
          aspectRatio: SimfTokens.videoAspectRatio,
          child: DecoratedBox(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(SimfTokens.radius),
              border: Border.all(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
            ),
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space4),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Icon(icon, size: 40, color: SimfTokens.beigeBorder),
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      message,
                      textAlign: TextAlign.center,
                      style: SimfTokens.hintBeige,
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
