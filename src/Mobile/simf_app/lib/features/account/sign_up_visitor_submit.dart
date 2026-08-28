/// The Next gate and the save it guards, for the sign-up profile-data form.
///
/// Both were inline in the screen's 105-line `_next`, which is why neither
/// could be read or tested without driving the whole form. The screen keeps
/// what only a widget can do — the snack bar, the busy flag and the push to the
/// interests screen; this file keeps the rules and the call order.
library;

import 'package:flutter/foundation.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/features/account/data/app_gender.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/data/sign_up_visitor_form.dart';
import 'package:simf_app/features/account/data/sign_up_visitor_lookups.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_completeness.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_form_state.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Whether every control the `Form` itself cannot validate is filled in.
///
/// The per-field rules and the decisions behind them live in
/// [VisitorProfileCompleteness]; this states which of them Page 007 applies,
/// and it is deliberately separate from the `Form.validate()` the caller runs
/// alongside it.
bool isSignUpVisitorComplete({
  required SignUpVisitorForm form,
  required VisitorProfileFormState picks,
  required SignUpVisitorTypeSelection type,
}) {
  return VisitorProfileCompleteness.dateOfBirth(form.dateOfBirth) &&
      VisitorProfileCompleteness.organisation(
        picks.organisationId,
        isOther: form.organisationIsOther,
        otherName: form.organisationOther.text,
      ) &&
      VisitorProfileCompleteness.nationality(picks.nationalityCode) &&
      VisitorProfileCompleteness.placeOfBirth(
        isSaudi: picks.isSaudi,
        birthRegionCode: form.birthRegionCode,
      ) &&
      VisitorProfileCompleteness.profileType(
        isVisitorType: type.isVisitor,
        loading: type.loading,
        failed: type.failed,
        hasItems: picks.profileTypes.isNotEmpty,
        profileTypeId: picks.profileTypeId,
      ) &&
      VisitorProfileCompleteness.idImage(
        hasPickedImage: form.idImageBytes != null,
        hasStoredImage: form.hasExistingIdImage,
      ) &&
      VisitorProfileCompleteness.facePhoto(
        gender: picks.gender,
        hasPickedImage: form.faceImageBytes != null,
        hasStoredImage: form.hasExistingAvatar,
      );
}

/// How [saveSignUpVisitorProfile] ended.
enum SignUpVisitorSaveOutcome {
  /// The profile is on the server; the caller may carry the draft forward.
  saved,

  /// A step failed and [SignUpVisitorSaveResult.message] says why, in the
  /// reader's language.
  failed,

  /// The screen went away mid-sequence. Nothing to show, nobody to show it to.
  abandoned,
}

/// The outcome of the profile-first save, with the message to display when it
/// failed.
@immutable
class SignUpVisitorSaveResult {
  const SignUpVisitorSaveResult(this.outcome, [this.message]);

  final SignUpVisitorSaveOutcome outcome;
  final String? message;
}

/// Uploads the images and saves the profile, in that order.
///
/// The order is a server requirement, not a preference: the profile save is
/// rejected when no ID document is stored (everyone) or, for a male, no face
/// photo — so the images must land BEFORE it. A failed MANDATORY upload stops
/// the sequence with a clear message; a failed avatar upload for a woman is
/// optional and falls through to the save.
///
/// The save happens here rather than two screens later on interests (D-684,
/// profile-first) so a server rejection — the name in particular — surfaces on
/// the form that produced it.
///
/// [stillMounted] is checked at exactly the points the screen used to check it,
/// so an unmount mid-sequence still abandons instead of running on.
/// [onAvatarUploaded] busts the cached avatar; the caller owns the `Ref`.
Future<SignUpVisitorSaveResult> saveSignUpVisitorProfile({
  required ProfileRepository repository,
  required SignUpVisitorForm form,
  required VisitorProfileFormState picks,
  required AppL10n l10n,
  required bool Function() stillMounted,
  required VoidCallback onAvatarUploaded,
}) async {
  const abandoned =
      SignUpVisitorSaveResult(SignUpVisitorSaveOutcome.abandoned);

  final idBytes = form.idImageBytes;
  final idName = form.idImageName;
  if (idBytes != null && idName != null) {
    try {
      await repository.uploadIdImage(bytes: idBytes, filename: idName);
    } on ApiFailure {
      if (!stillMounted()) {
        return abandoned;
      }
      return SignUpVisitorSaveResult(
        SignUpVisitorSaveOutcome.failed,
        l10n.idImageUploadFailed,
      );
    }
  }

  final faceBytes = form.faceImageBytes;
  final faceName = form.faceImageName;
  if (faceBytes != null && faceName != null) {
    try {
      await repository.uploadAvatar(bytes: faceBytes, filename: faceName);
      onAvatarUploaded();
    } on ApiFailure {
      if (!stillMounted()) {
        return abandoned;
      }
      if (picks.gender == AppGender.male) {
        return SignUpVisitorSaveResult(
          SignUpVisitorSaveOutcome.failed,
          l10n.facePhotoUploadFailed,
        );
      }
      // Optional for women — fall through and save.
    }
  }

  try {
    await repository.upsertMyProfile(form.toRequest(picks));
  } on ApiFailure catch (failure) {
    if (!stillMounted()) {
      return abandoned;
    }
    return SignUpVisitorSaveResult(
      SignUpVisitorSaveOutcome.failed,
      failure.localizedMessage(l10n),
    );
  }

  if (!stillMounted()) {
    return abandoned;
  }
  return const SignUpVisitorSaveResult(SignUpVisitorSaveOutcome.saved);
}
