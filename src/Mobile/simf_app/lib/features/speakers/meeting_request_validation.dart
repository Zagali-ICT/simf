import 'package:simf_app/app/localization/app_l10n.dart';

/// The send-time precondition chain for the meeting-request sheet
/// (`MeetingRequestForm`) — the first unmet precondition's inline message, or
/// null when the request may go to the server.
///
/// Pure and target-type-agnostic, so it stays out of the sheet's State: the
/// sheet passes what it knows (a target is chosen, the trimmed subject, a slot
/// is chosen) rather than the values themselves.
///
/// [validateExtra] is passed as a callback, not as an already-computed message,
/// so the delegation sheet's عدد الحضور field is only validated once the target
/// and subject checks have passed — the order the sheet has always used.
///
/// G3 (owner 2026-07-30, supersedes D-767 R1) — a slot is ALWAYS required. The
/// subject-only bypass is gone: the server 409s a request against a target with
/// no free slot, so sending one could only ever fail. The send button is
/// disabled in that state, which leaves the last check here guarding the
/// picked-a-day-but-not-a-time case.
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
