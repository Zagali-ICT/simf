import 'dart:typed_data';
import 'dart:ui' show Rect;

import 'package:share_plus/share_plus.dart';

/// Web implementation of [shareTextContent].
///
/// There is no `dart:io` filesystem in the browser, so the raw [content] is
/// shared as text via the browser share API. [filename] and [mimeType] are not
/// used on web (no file is materialised); they are kept in the signature so the
/// native and web entry points are interchangeable.
///
/// [sharePositionOrigin] is unused on web (the browser share API is modal).
Future<void> shareTextContent({
  required String content,
  required String filename,
  required String mimeType,
  Rect? sharePositionOrigin,
}) =>
    Share.share(content);

/// Web implementation of [shareBinaryContent] — shares the in-memory [bytes]
/// via the browser share API (no `dart:io` filesystem on web).
///
/// [sharePositionOrigin] is unused on web (the browser share API is modal).
Future<void> shareBinaryContent({
  required List<int> bytes,
  required String filename,
  required String mimeType,
  Rect? sharePositionOrigin,
}) =>
    Share.shareXFiles(<XFile>[
      XFile.fromData(
        Uint8List.fromList(bytes),
        name: filename,
        mimeType: mimeType,
      ),
    ]);
