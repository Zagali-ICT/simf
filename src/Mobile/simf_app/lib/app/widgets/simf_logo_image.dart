import 'package:flutter/material.dart';

import 'package:simf_app/app/widgets/simf_image_viewer.dart';

/// The one widget every logo / photo box in the app renders through (owner
/// 2026-07-26 — "make the logo fit its box in all logo views, and on-press show
/// it full size"; shared-widget rule: never a page-local copy).
///
/// Two things it guarantees everywhere:
///  * **Fit** — [fit] defaults to [BoxFit.contain], the correct fit for a brand
///    mark: the whole logo is shown inside its box, letterboxed rather than
///    cropped. Photographic subjects (a speaker portrait, a news hero) pass
///    [BoxFit.cover] explicitly so the frame stays filled.
///  * **Tap → full size** — tapping opens [SimfImageViewer] with the SAME
///    [ImageProvider] this box painted, so the full-resolution picture opens
///    from the image cache with pinch-zoom and a close control. Set
///    [enableFullScreen] to false for a box whose tap already means something
///    else (a list row that navigates, an avatar that changes the photo).
///
/// [placeholder] shows while the bytes are in flight; [onError] builds the
/// no-logo / failed-fetch state (defaulting to [placeholder]) and lets a caller
/// chain a second URL before its initials tile.
class SimfLogoImage extends StatelessWidget {
  const SimfLogoImage({
    required this.url,
    required this.placeholder,
    required this.semanticLabel,
    this.onError,
    this.fit = BoxFit.contain,
    this.width,
    this.height,
    this.cacheWidth,
    this.cacheHeight,
    this.enableFullScreen = true,
    super.key,
  });

  /// The image URL. Null / blank goes straight to [onError] without a fetch.
  final String? url;

  /// Shown while the image loads.
  final Widget placeholder;

  /// Built when [url] is blank or the fetch fails. Defaults to [placeholder].
  final Widget Function()? onError;

  /// The accessible name of the picture — also the viewer's title.
  final String semanticLabel;

  final BoxFit fit;
  final double? width;
  final double? height;

  /// Decode cap for the thumbnail (physical px). The full-size viewer always
  /// paints the uncapped provider.
  final int? cacheWidth;
  final int? cacheHeight;

  final bool enableFullScreen;

  @override
  Widget build(BuildContext context) {
    final source = url?.trim() ?? '';
    final fallback = onError ?? () => placeholder;
    if (source.isEmpty) {
      return fallback();
    }
    final full = NetworkImage(source);
    final Widget image = Image(
      image: _thumbnail(full),
      fit: fit,
      width: width,
      height: height,
      gaplessPlayback: true,
      loadingBuilder: (context, child, progress) =>
          progress == null ? child : placeholder,
      errorBuilder: (context, error, stackTrace) => fallback(),
    );
    if (!enableFullScreen) {
      return Semantics(image: true, label: semanticLabel, child: image);
    }
    return SimfTapToEnlarge(
      image: full,
      label: semanticLabel,
      child: image,
    );
  }

  /// The provider this BOX paints: [full] itself, or a decode-capped resize
  /// when the caller gave a cap. The full-size viewer always paints [full].
  ImageProvider _thumbnail(NetworkImage full) =>
      (cacheWidth == null && cacheHeight == null)
          ? full
          : ResizeImage(full, width: cacheWidth, height: cacheHeight);
}
