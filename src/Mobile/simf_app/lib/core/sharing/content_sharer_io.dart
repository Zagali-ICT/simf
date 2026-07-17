import 'dart:io';
import 'dart:ui' show Rect;

import 'package:share_plus/share_plus.dart';

/// Native (Android / iOS / desktop) implementation of [shareTextContent].
///
/// Writes the content to a real temp path (no `path_provider` needed) and
/// shares that file so the OS offers add-to-calendar / add-to-contact rather
/// than plain text.
///
/// [sharePositionOrigin] is passed through to [Share.shareXFiles] — required
/// on iPad / Mac Catalyst for the popover anchor.
Future<void> shareTextContent({
  required String content,
  required String filename,
  required String mimeType,
  Rect? sharePositionOrigin,
}) async {
  final dir = await Directory.systemTemp.createTemp('simf_share');
  final file = File('${dir.path}/$filename');
  await file.writeAsString(content);
  await Share.shareXFiles(
    <XFile>[XFile(file.path, mimeType: mimeType)],
    sharePositionOrigin: sharePositionOrigin,
  );
}

/// Native (Android / iOS / desktop) implementation of [shareBinaryContent] —
/// writes the downloaded [bytes] to a real temp file and shares it so the OS
/// sheet offers Save-to-Files / Open (the تحميل path, Figma 1388:7621).
///
/// [sharePositionOrigin] is passed through to [Share.shareXFiles] — required
/// on iPad / Mac Catalyst for the popover anchor.
Future<void> shareBinaryContent({
  required List<int> bytes,
  required String filename,
  required String mimeType,
  Rect? sharePositionOrigin,
}) async {
  final dir = await Directory.systemTemp.createTemp('simf_share');
  final file = File('${dir.path}/$filename');
  await file.writeAsBytes(bytes);
  await Share.shareXFiles(
    <XFile>[XFile(file.path, mimeType: mimeType)],
    sharePositionOrigin: sharePositionOrigin,
  );
}
