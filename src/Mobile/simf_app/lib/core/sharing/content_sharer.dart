import 'content_sharer_web.dart'
    if (dart.library.io) 'content_sharer_io.dart' as platform;

/// Shares text [content] with the OS share sheet, cross-platform.
///
/// On a platform with a filesystem (Android / iOS / desktop) it writes
/// [content] to a temp file named [filename] of [mimeType] and shares the file,
/// so the OS offers add-to-contact / add-to-calendar rather than plain text. On
/// web there is no `dart:io` filesystem, so it shares the raw text via the
/// browser share API. This is the single share path for the app's vCard/ICS
/// exports (My-Area + Contacts), so the three call sites stay `dart:io`-free and
/// the project compiles for web.
Future<void> shareTextContent({
  required String content,
  required String filename,
  required String mimeType,
}) {
  return platform.shareTextContent(
    content: content,
    filename: filename,
    mimeType: mimeType,
  );
}

/// Shares binary [bytes] (a downloaded file) with the OS share sheet,
/// cross-platform — the تحميل path on the session-presentations screen
/// (Figma 1388:7621). On a filesystem platform it writes [bytes] to a temp file
/// named [filename] of [mimeType] and shares the file (the OS sheet offers
/// Save-to-Files / Open); on web it shares the in-memory bytes via the browser
/// share API. Keeps every call site `dart:io`-free so the project compiles for
/// web.
Future<void> shareBinaryContent({
  required List<int> bytes,
  required String filename,
  required String mimeType,
}) {
  return platform.shareBinaryContent(
    bytes: bytes,
    filename: filename,
    mimeType: mimeType,
  );
}
