import 'package:flutter/material.dart';

import '../localization/app_l10n.dart';
import '../theme/tokens.dart';

/// The shared full-size image viewer (owner 2026-07-26 — "on-press on a logo
/// must show it in full size").
///
/// A full-screen navy route that paints the SAME [ImageProvider] the thumbnail
/// used — so an already-decoded logo/photo opens instantly from the image cache
/// and an authenticated (bearer-gated) image opens from its in-memory bytes
/// (D-422: never a bare `Image.network` for a bearer-gated asset). Pinch /
/// double-tap zooms via [InteractiveViewer]; the system back gesture, the
/// hardware back button and the gold close control all dismiss it.
///
/// Opened through [showSimfImageViewer]; never instantiated per page.
class SimfImageViewer extends StatelessWidget {
  const SimfImageViewer({
    required this.image,
    required this.label,
    super.key,
  });

  /// The provider the calling thumbnail rendered — reused verbatim so the
  /// viewer hits the same image-cache entry instead of re-fetching.
  final ImageProvider image;

  /// The accessible name of the picture (the speaker / sponsor / booth name).
  final String label;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navy,
      body: SafeArea(
        child: Stack(
          children: <Widget>[
            Positioned.fill(
              child: Semantics(
                image: true,
                label: label,
                child: InteractiveViewer(
                  minScale: 1,
                  maxScale: 5,
                  child: Center(
                    child: Image(
                      image: image,
                      fit: BoxFit.contain,
                      errorBuilder: (context, error, stackTrace) => Padding(
                        padding: const EdgeInsets.all(SimfTokens.space6),
                        child: Text(
                          l10n.imageViewerLoadFailed,
                          textAlign: TextAlign.center,
                          style: SimfTokens.bodyBeige,
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ),
            PositionedDirectional(
              top: SimfTokens.space2,
              end: SimfTokens.space2,
              child: Semantics(
                button: true,
                label: l10n.imageViewerCloseLabel,
                child: IconButton(
                  key: const ValueKey<String>('imageViewerClose'),
                  onPressed: () => Navigator.of(context).maybePop(),
                  tooltip: l10n.imageViewerCloseLabel,
                  icon: const Icon(Icons.close, color: SimfTokens.accent),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Opens [image] full size over the current page. Returns when the viewer is
/// dismissed. Uses the root navigator so the viewer covers the bottom nav.
Future<void> showSimfImageViewer(
  BuildContext context, {
  required ImageProvider image,
  required String label,
}) {
  return Navigator.of(context, rootNavigator: true).push<void>(
    MaterialPageRoute<void>(
      fullscreenDialog: true,
      builder: (_) => SimfImageViewer(image: image, label: label),
    ),
  );
}

/// The press-to-enlarge affordance: names [child] as a tappable picture and
/// opens [image] in [SimfImageViewer] when it is pressed. Lives here (not in
/// each box) so every enlargeable picture — a network logo, the authenticated
/// badge photo — carries the same semantics and hit behaviour.
class SimfTapToEnlarge extends StatelessWidget {
  const SimfTapToEnlarge({
    required this.image,
    required this.label,
    required this.child,
    super.key,
  });

  final ImageProvider image;

  /// The picture's accessible name; blank means "unnamed" (no label node).
  final String label;

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      image: true,
      label: label.isEmpty ? null : label,
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: () => showSimfImageViewer(context, image: image, label: label),
        child: child,
      ),
    );
  }
}
