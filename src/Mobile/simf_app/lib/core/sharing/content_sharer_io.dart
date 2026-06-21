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
  // A11-3 (NCA Secure App-Dev Standard) — the shared vCard/ICS carries the
  // user's own contact PII. Purge leftover share temp files from previous
  // shares so the PII never lingers across sessions. (We can't delete the
  // *current* file the instant the share sheet closes, because the receiving
  // app — e.g. Contacts — may read it a moment later.)
  await _purgeOldShareTemp();

  final dir = await Directory.systemTemp.createTemp('simf_share');
  final file = File('${dir.path}/$filename');
  await file.writeAsString(content);
  await Share.shareXFiles(<XFile>[XFile(file.path, mimeType: mimeType)]);
}

/// Best-effort removal of temp directories left by earlier shares.
Future<void> _purgeOldShareTemp() async {
  try {
    await for (final entity in Directory.systemTemp.list()) {
      final name = entity.path.split(Platform.pathSeparator).last;
      if (entity is Directory && name.startsWith('simf_share')) {
        try {
          await entity.delete(recursive: true);
        } catch (_) {
          // Still held by a receiving app — skip it.
        }
      }
    }
  } catch (_) {
    // Listing the temp dir failed — nothing to clean up.
  }
}
