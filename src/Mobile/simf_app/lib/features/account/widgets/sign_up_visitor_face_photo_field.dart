import 'dart:typed_data';

import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/account/data/app_gender.dart';
import 'package:simf_app/features/account/widgets/attachment_field.dart';

/// "Face photo" — the live face capture (→ the profile avatar) on the sign-up
/// profile step. Mandatory for men, optional for women. Once captured the face
/// shows at the top of the card and this row confirms it with a Retake.
///
/// The male-**required** hint stays hidden until a blocked Next (then danger
/// red, D-674); the women-**optional** hint is informational, so it stays
/// visible in grey.
///
/// The capture itself is NOT started here: the screen owns [onCapture], which
/// pushes the guided liveness page (D-662/D-694). Keeping the navigation on the
/// screen leaves the face-capture flow one path, not two.
class SignUpVisitorFacePhotoField extends StatelessWidget {
  const SignUpVisitorFacePhotoField({
    required this.l10n,
    required this.bytes,
    required this.gender,
    required this.hasStoredAvatar,
    required this.triedSubmit,
    required this.onCapture,
    super.key,
  });

  final AppL10n l10n;
  final Uint8List? bytes;
  final AppGender gender;

  /// True when the server already stores a face photo (avatar) for this
  /// profile.
  final bool hasStoredAvatar;

  final bool triedSubmit;
  final VoidCallback onCapture;

  @override
  Widget build(BuildContext context) {
    final data = bytes;
    final maleNeedsFace =
        gender == AppGender.male && data == null && !hasStoredAvatar;
    final showOptionalHint =
        data == null && !hasStoredAvatar && !maleNeedsFace;
    final showRequiredHint = triedSubmit && maleNeedsFace;
    return AttachmentField(
      label: l10n.facePhotoLabel,
      hintText: showRequiredHint
          ? l10n.facePhotoRequiredForMen
          : (showOptionalHint ? l10n.facePhotoOptionalForWomen : null),
      hintDanger: showRequiredHint,
      bytes: data,
      round: true,
      attachLabel: l10n.facePhotoCaptureLabel,
      attachIcon: Icons.photo_camera_outlined,
      onAttach: onCapture,
      attachedName: l10n.facePhotoCaptured,
      actionLabel: l10n.retakeLabel,
      onAction: onCapture,
    );
  }
}
