import 'dart:io';

import 'package:share_plus/share_plus.dart';

/// Native (Android / iOS / desktop) implementation of [shareTextContent].
///
/// Writes the content to a real temp path (no `path_provider` needed) and
/// shares that file so the OS offers add-to-calendar / add-to-contact rather
/// than plain text.
Future<void> shareTextContent({
  required String content,
  required String filename,
  required String mimeType,
}) async {
  final dir = await Directory.systemTemp.createTemp('simf_share');
  final file = File('${dir.path}/$filename');
  await file.writeAsString(content);
  await Share.shareXFiles(<XFile>[XFile(file.path, mimeType: mimeType)]);
}
