import 'dart:typed_data';

import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/account/widgets/attachment_field.dart';

/// "Upload ID" — the mandatory ID-document attachment on the sign-up profile
/// step. The document is picked from the gallery (a national-ID / Iqama /
/// passport scan), so there is no live-camera or face-detection step here.
///
/// The "required" hint stays hidden until a blocked Next, then shows in danger
/// red — like the text-field validators, not surfaced up-front in grey (D-674).
class SignUpVisitorIdImageField extends StatelessWidget {
  const SignUpVisitorIdImageField({
    required this.l10n,
    required this.bytes,
    required this.filename,
    required this.hasStoredImage,
    required this.triedSubmit,
    required this.onAttach,
    required this.onRemove,
    super.key,
  });

  final AppL10n l10n;
  final Uint8List? bytes;
  final String? filename;

  /// True when the server already stores an ID document for this profile, so a
  /// re-entry is not forced to re-pick one.
  final bool hasStoredImage;

  final bool triedSubmit;
  final VoidCallback onAttach;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final needsImage = bytes == null && !hasStoredImage;
    return AttachmentField(
      label: l10n.attachmentsLabel,
      hintText: (triedSubmit && needsImage) ? l10n.idImageRequired : null,
      hintDanger: true,
      bytes: bytes,
      round: false,
      attachLabel: l10n.attachFileLabel,
      attachIcon: Icons.add_circle_outline,
      onAttach: onAttach,
      attachedName: filename ?? l10n.idImageAttachedLabel,
      actionLabel: l10n.removeLabel,
      onAction: onRemove,
    );
  }
}
