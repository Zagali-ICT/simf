import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// The shared loading / error / empty triad every data screen composes.
/// Split out of `simf_page_shell.dart` (one widget group per file); that file
/// re-exports these, so every existing import keeps working.

/// The standard error surface: the message over a gold retry button — one
/// home for the retry chrome instead of a per-screen copy.
class SimfErrorState extends StatelessWidget {
  const SimfErrorState({
    required this.message,
    required this.retryLabel,
    required this.onRetry,
    super.key,
  });

  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.surface),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(retryLabel)),
          ],
        ),
      ),
    );
  }
}

/// The standard empty / pending surface: a muted icon over the message.
class SimfEmptyState extends StatelessWidget {
  const SimfEmptyState({required this.icon, required this.message, super.key});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, size: 56, color: SimfTokens.beigeBorder),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: SimfTokens.hintBeige,
            ),
          ],
        ),
      ),
    );
  }
}

/// The standard loading surface: the accent spinner, centered — one home for the
/// loader chrome so screens don't re-emit the raw indicator. Completes the
/// loading / error / empty triad with [SimfErrorState] and [SimfEmptyState].
class SimfLoadingState extends StatelessWidget {
  const SimfLoadingState({super.key});

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: CircularProgressIndicator(color: SimfTokens.accent),
    );
  }
}
