import 'dart:typed_data';

import 'package:share_plus/share_plus.dart';

/// Web implementation of [shareTextContent].
///
/// There is no `dart:io` filesystem in the browser, so the raw [content] is
/// shared as text via the browser share API. [filename] and [mimeType] are not
/// used on web (no file is materialised); they are kept in the signature so the
/// native and web entry points are interchangeable.
Future<void> shareTextContent({
  required String content,
  required String filename,
  required String mimeType,
}) =>
    Share.share(content);

/// Web implementation of [shareBinaryContent] — shares the in-memory [bytes] via
/// the browser share API (no `dart:io` filesystem on web).
Future<void> shareBinaryContent({
  required List<int> bytes,
  required String filename,
  required String mimeType,
}) =>
    Share.shareXFiles(<XFile>[
      XFile.fromData(
        Uint8List.fromList(bytes),
        name: filename,
        mimeType: mimeType,
      ),
    ]);
