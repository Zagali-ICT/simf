import 'package:flutter/material.dart' show BuildContext, Rect, RenderBox;

import 'package:simf_app/core/sharing/content_sharer_web.dart'
    if (dart.library.io) 'content_sharer_io.dart' as platform;

/// Shares text [content] with the OS share sheet, cross-platform.
///
/// On a platform with a filesystem (Android / iOS / desktop) it writes
/// [content] to a temp file named [filename] of [mimeType] and shares the file,
/// so the OS offers add-to-contact / add-to-calendar rather than plain text. On
/// web there is no `dart:io` filesystem, so it shares the raw text via the
/// browser share API. This is the single share path for the app's vCard/ICS
/// exports (My-Area + Contacts), so the three call sites stay `dart:io`-free
/// and the project compiles for web.
///
/// [sharePositionOrigin] is required on iPad / Mac Catalyst — a non-empty
/// [Rect] within the app's view bounds that anchors the popover. On iPhone and
/// Android it is ignored.
Future<void> shareTextContent({
  required String content,
  required String filename,
  required String mimeType,
  Rect? sharePositionOrigin,
}) {
  return platform.shareTextContent(
    content: content,
    filename: filename,
    mimeType: mimeType,
    sharePositionOrigin: sharePositionOrigin,
  );
}

/// Shares binary [bytes] (a downloaded file) with the OS share sheet,
/// cross-platform — the تحميل path on the session-presentations screen
/// (Figma 1388:7621). On a filesystem platform it writes [bytes] to a temp file
/// named [filename] of [mimeType] and shares the file (the OS sheet offers
/// Save-to-Files / Open); on web it shares the in-memory bytes via the browser
/// share API. Keeps every call site `dart:io`-free so the project compiles for
/// web.
///
/// [sharePositionOrigin] is required on iPad / Mac Catalyst — a non-empty
/// [Rect] within the app's view bounds that anchors the popover. On iPhone and
/// Android it is ignored.
Future<void> shareBinaryContent({
  required List<int> bytes,
  required String filename,
  required String mimeType,
  Rect? sharePositionOrigin,
}) {
  return platform.shareBinaryContent(
    bytes: bytes,
    filename: filename,
    mimeType: mimeType,
    sharePositionOrigin: sharePositionOrigin,
  );
}

/// Returns a sensible [Rect] from [context] for anchoring the iPad / Mac
/// Catalyst share-sheet popover. Returns null when [context] has no render
/// object, in which case the caller may omit `sharePositionOrigin` (the share
/// sheet still works on iPhone + Android where a popover is not used).
///
/// The returned rect sits at the bottom-centre of the widget so the popover
/// appears near where the user tapped.
Rect? shareOriginFromContext(BuildContext context) {
  final box = context.findRenderObject() as RenderBox?;
  if (box == null || !box.hasSize) return null;
  final w = box.size.width;
  final h = box.size.height;
  return Rect.fromLTWH(w / 2 - 72, h - 120, 144, 48);
}
