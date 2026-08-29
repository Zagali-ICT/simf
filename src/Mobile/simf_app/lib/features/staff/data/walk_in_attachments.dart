import 'package:flutter/foundation.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/staff/data/staff_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The two optional images the desk can attach after the account is created.
enum WalkInAttachment { idDocument, photo }

/// An image the operator picked, waiting to be attached to the new visitor.
@immutable
class WalkInAttachmentFile {
  const WalkInAttachmentFile({required this.bytes, required this.filename});

  final Uint8List bytes;
  final String filename;
}

/// What the desk has picked so far. Deliberately mutable: the screen swaps a
/// picked file in — and back out on "Remove" — inside its own `setState`.
class WalkInAttachments {
  WalkInAttachmentFile? idDocument;
  WalkInAttachmentFile? photo;

  void clear() {
    idDocument = null;
    photo = null;
  }
  /// Assigns by slot. The screen used to branch on the enum itself; the mapping
  /// from slot to field belongs on the object that owns both.
  void set(WalkInAttachment which, WalkInAttachmentFile? file) {
    if (which == WalkInAttachment.idDocument) {
      idDocument = file;
    } else {
      photo = file;
    }
  }

}

/// Uploads the picked images against the new visitor's id and returns the ones
/// that did NOT land. An upload failure is non-fatal — the account already
/// exists — but it must never be swallowed: the operator has to know the
/// document is missing (DEF-STF-004). [only] narrows the attempt to a retry of
/// the attachments named in it.
Future<List<WalkInAttachment>> uploadWalkInAttachments(
  StaffRepository repo,
  WalkInAttachments attachments, {
  required String userId,
  Set<WalkInAttachment>? only,
}) async {
  if (userId.isEmpty) {
    return const <WalkInAttachment>[];
  }
  final failed = <WalkInAttachment>[];
  final idDocument = attachments.idDocument;
  if (idDocument != null &&
      (only?.contains(WalkInAttachment.idDocument) ?? true)) {
    try {
      await repo.uploadIdImage(
        userId: userId,
        bytes: idDocument.bytes,
        filename: idDocument.filename,
      );
    } on ApiFailure {
      failed.add(WalkInAttachment.idDocument);
    }
  }
  final photo = attachments.photo;
  if (photo != null && (only?.contains(WalkInAttachment.photo) ?? true)) {
    try {
      await repo.uploadAvatar(
        userId: userId,
        bytes: photo.bytes,
        filename: photo.filename,
      );
    } on ApiFailure {
      failed.add(WalkInAttachment.photo);
    }
  }
  return failed;
}

/// The attachment's name in the reader's language, for the retry dialog's list.
String walkInAttachmentLabel(AppL10n l10n, WalkInAttachment attachment) =>
    attachment == WalkInAttachment.idDocument
        ? l10n.staffAttachIdLabel
        : l10n.staffAttachPhotoLabel;
