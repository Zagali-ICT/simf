import 'package:simf_app/app/localization/app_l10n.dart';

/// The send-time precondition chain for `MeetingRequestForm` — the first unmet
/// precondition's inline message, or null when the request may be sent.
///
/// [validateExtra] is a callback rather than an already-computed message so the
/// delegation sheet's عدد الحضور field is only validated after the target and
/// subject checks pass.
///
/// G3 (owner 2026-07-30, supersedes D-767 R1) — a slot is ALWAYS required: the
/// server 409s a request against a target with no free slot.
String? meetingRequestError({
  required AppL10n l10n,
  required bool hasTarget,
  required String noTargetSelectedError,
  required String subject,
  required String? Function()? validateExtra,
  required bool hasSlot,
}) {
  if (!hasTarget) {
    return noTargetSelectedError;
  }
  if (subject.isEmpty) {
    return l10n.meetingRequestInvalid;
  }
  final extraError = validateExtra?.call();
  if (extraError != null) {
    return extraError;
  }
  return hasSlot ? null : l10n.meetingPickDateTime;
}
