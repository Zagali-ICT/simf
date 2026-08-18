import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/widgets/meeting_request_form.dart';
import 'package:simf_app/features/speakers/widgets/meeting_slot_section.dart';
import 'package:simf_app/features/speakers/widgets/meeting_target_picker.dart';
import 'package:simf_app/features/speakers/widgets/speaker_option_tile.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The speaker meeting-request sheet (طلب مقابلة, Figma **1776:5036**) —
/// approved-account only (E2). Everything it shares with the delegation sheet
/// lives in [MeetingRequestForm]; this file is the speaker half: the
/// repository calls, the copy, and the 403/409 mapping.
///
/// Two entry points share it:
/// - from a **speaker profile** — [speakerId] is set, so the speaker is fixed
///   and no picker is shown;
/// - from the **"طلب جديد"** on the requests list (اللقاءات الثنائية,
///   1408:9726) — [speakerId] is **null**, so the sheet first shows a
///   searchable speaker picker (type-to-filter by name/rank, owner 2026-07-11).
///
/// D-709 (item 6, FDS-013 §15.4 GAP-4) — the date + time come from the
/// speaker's **real availability slots**
/// (`GET /app/speakers/{id}/available-slots`), NOT a free client-side grid;
/// this **reverts D-703**. Sending a request is gated by the server on the
/// per-user `allowsSpeakerMeeting` flag (D-760, replacing the VIP-tier gate);
/// a 403 surfaces `meetingNotEnabled`.
class MeetingRequestSheet extends ConsumerWidget {
  const MeetingRequestSheet({
    required this.speakerId,
    required this.defaultName,
    required this.baseUrl,
    required this.l10n,
    super.key,
  });

  /// The speaker to meet, or **null** for the bilateral entry (show the
  /// picker).
  final String? speakerId;
  final String defaultName;

  /// The API base URL — used to build the speaker photo asset URL for the
  /// bilateral picker's identity rows (D-745). Unused on the profile flow (a
  /// fixed speaker, no picker), but always supplied so the constructor is
  /// uniform.
  final String baseUrl;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final repository = ref.watch(speakersRepositoryProvider);
    return MeetingRequestForm<String>(
      target: speakerId,
      targetId: (id) => id,
      l10n: l10n,
      title: l10n.requestMeeting, // طلب مقابلة
      keyPrefix: 'meeting',
      searchFieldKey: const ValueKey<String>('meeting-speaker-search'),
      pickerLabel: l10n.meetingSelectSpeakerLabel, // اختر المتحدث
      pickerEmptyHint: l10n.meetingSelectSpeakerHint,
      // The already-chosen speaker stays in the list even when it does not
      // match the query, so the picker can never hide (or contradict) the
      // target the form submits to.
      pinSelectedInPicker: true,
      noTargetSelectedError: l10n.meetingSelectSpeakerFirst,
      loadTargets: () async => <MeetingTargetOption<String>>[
        for (final speaker in await repository.getSpeakers())
          _optionFor(speaker),
      ],
      loadSlots: (id) async => <MeetingSlot>[
        for (final slot in await repository.getAvailableSlots(id))
          MeetingSlot(start: slot.start, end: slot.end),
      ],
      submit: ({
        required target,
        required subject,
        required slotStart,
        required slotEnd,
      }) =>
          repository.submitMeetingRequest(
        target,
        // Owner: "no need for name" — the requester is the signed-in account,
        // so we submit its display name as the requesterName the backend
        // contract requires.
        requesterName: defaultName.trim(),
        subject: subject,
        slotStart: slotStart,
        slotEnd: slotEnd,
      ),
      failureText: _failureText,
    );
  }

  MeetingTargetOption<String> _optionFor(SpeakerSummary speaker) =>
      MeetingTargetOption<String>(
        id: speaker.id,
        value: speaker.id,
        // Matched against the name + rank, case-insensitive, mirroring the
        // speakers list (908:1744) so search behaves identically wherever a
        // speaker is chosen.
        matches: (query) => speaker.matches(query, isArabic: l10n.isArabic),
        buildTile: ({required selected, required onTap}) => SpeakerOptionTile(
          speaker: speaker,
          isArabic: l10n.isArabic,
          baseUrl: baseUrl,
          selected: selected,
          onTap: onTap,
        ),
      );

  /// The inline error for a failed submit.
  ///
  /// QA A26 — this used to map EVERY 409 onto "this speaker does not accept
  /// meeting requests", so a duplicate-pending or a slot-already-taken conflict
  /// surfaced a flatly wrong reason and the correct bilingual text the API had
  /// already picked for the active locale was thrown away. Every status except
  /// 403 now defers to the shared [ApiFailureL10n] mapper, which returns the
  /// envelope's own message and still localizes the network / timeout codes.
  ///
  /// QA A28 — a 403 keeps its own app copy because the server's forbidden text
  /// is generic: eligibility is the per-user `AllowsSpeakerMeeting` flag (no
  /// longer the VIP tier), and only the app can say what the user should do.
  String _failureText(ApiFailure failure) {
    if (failure.httpStatus == 403) {
      return l10n.meetingNotEnabled;
    }
    return failure.localizedMessage(l10n);
  }
}
