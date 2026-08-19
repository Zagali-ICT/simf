import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/staff/data/staff_repository.dart';
import 'package:simf_app/features/staff/data/walk_in_attachments.dart';
import 'package:simf_app/features/staff/widgets/staff_upload_failed_dialog.dart';

/// Tells the operator exactly which attachment did not land and lets them retry
/// the UPLOAD against the already-created visitor, so the person is never
/// registered a second time (DEF-STF-004).
///
/// Keeps asking until there is nothing left to resolve: every attachment in
/// [failed] uploaded, or the operator chose to skip. Each round re-sends only
/// the files still outstanding.
///
/// Returns once the operator is done with the attachments — it deliberately
/// does not announce the registration itself, because the caller owns that
/// message and the form reset that follows it.
Future<void> resolveFailedWalkInUploads({
  required BuildContext context,
  required ScaffoldMessengerState messenger,
  required StaffRepository repo,
  required WalkInAttachments attachments,
  required String userId,
  required List<WalkInAttachment> failed,
}) async {
  final l10n = AppL10n.of(context);
  var pending = failed;
  while (pending.isNotEmpty) {
    if (!context.mounted) {
      return;
    }
    final again = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (_) => StaffUploadFailedDialog(
        pendingLabels: <String>[
          for (final attachment in pending)
            walkInAttachmentLabel(l10n, attachment),
        ],
      ),
    );
    if (!context.mounted || again != true) {
      return;
    }
    pending = await uploadWalkInAttachments(
      repo,
      attachments,
      userId: userId,
      only: pending.toSet(),
    );
    if (!context.mounted) {
      return;
    }
    if (pending.isEmpty) {
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.staffUploadRetrySuccess)));
    }
  }
}
