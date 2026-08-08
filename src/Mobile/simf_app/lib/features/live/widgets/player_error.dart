import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../../app/theme/tokens.dart';

/// Shown when a live feed fails to load — a terminal error surface with a Retry
/// that re-binds the player (Page_025 L-7), instead of an endless spinner.
class PlayerError extends StatelessWidget {
  const PlayerError({
    required this.message,
    required this.retryLabel,
    required this.onRetry,
  });

  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return AspectRatio(
      aspectRatio: SimfTokens.videoAspectRatio,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: SimfTokens.black,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
        ),
        // Centred + scrollable so the icon + message + button never overflow
        // the fixed 16:9 box on a short / landscape viewport (RenderFlex).
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(SimfTokens.space3),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                const Icon(
                  Icons.error_outline,
                  size: 36,
                  color: SimfTokens.beigeBorder,
                ),
                const SizedBox(height: SimfTokens.space2),
                Text(
                  message,
                  textAlign: TextAlign.center,
                  style: SimfTokens.hintBeige,
                ),
                const SizedBox(height: SimfTokens.space3),
                FilledButton(onPressed: onRetry, child: Text(retryLabel)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
