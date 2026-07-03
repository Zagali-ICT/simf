import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The black surface shown when there is no live stream but a recording exists —
/// keeps the frame's black player band, with a recording note instead of a feed.
class RecordingSurface extends StatelessWidget {
  const RecordingSurface({required this.message, super.key});

  final String message;

  @override
  Widget build(BuildContext context) {
    return _MessageSurface(
      icon: Icons.video_library_outlined,
      message: message,
    );
  }
}

/// The black surface shown when the session is neither live nor recorded.
class NotLiveSurface extends StatelessWidget {
  const NotLiveSurface({required this.message, super.key});

  final String message;

  @override
  Widget build(BuildContext context) {
    return _MessageSurface(
      icon: Icons.live_tv_outlined,
      message: message,
    );
  }
}

/// The black player-band placeholder for the non-live states (recording /
/// not-live) — keeps the frame's full-bleed black band, centring an icon +
/// message where the feed would play.
class _MessageSurface extends StatelessWidget {
  const _MessageSurface({required this.icon, required this.message});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Colors.black,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: AspectRatio(
          aspectRatio: 16 / 9,
          child: DecoratedBox(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(SimfTokens.radius),
              border: Border.all(color: SimfTokens.beigeBorder, width: 0.2),
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
                      style: const TextStyle(color: SimfTokens.beigeBorder),
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
