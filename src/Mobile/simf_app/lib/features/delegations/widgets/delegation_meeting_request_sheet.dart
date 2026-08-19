import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_repository.dart';
import 'package:simf_app/features/delegations/widgets/delegation_attendee_count_field.dart';
import 'package:simf_app/features/delegations/widgets/delegation_option_tile.dart';
import 'package:simf_app/features/speakers/widgets/meeting_request_form.dart';
import 'package:simf_app/features/speakers/widgets/meeting_request_sheet.dart'
    show MeetingRequestSheet;
import 'package:simf_app/features/speakers/widgets/meeting_sheet_fields.dart';
import 'package:simf_app/features/speakers/widgets/meeting_slot_section.dart';
import 'package:simf_app/features/speakers/widgets/meeting_target_picker.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Bi-Meeting rework — the delegation-meeting request sheet (طلب اجتماع وفد),
/// mirroring the speaker [MeetingRequestSheet]. A delegate of one invited
/// country asks to meet another invited country's delegation. Everything the
/// two sheets share lives in [MeetingRequestForm]; this file is the delegation
/// half: the repository calls, the عدد الحضور field, the copy, and the failure
/// mapping. Two entry points share it:
/// - from a **tapped delegation card** — [country] is set (fixed target);
/// - from the **"طلب اجتماع وفد"** button on the Bi-Meeting page — [country] is
///   null, so a searchable delegation picker is shown first.
///
/// The date + time come from the target delegation's real availability slots
/// (`GET /app/countries/{id}/available-slots`). Eligibility
/// (AllowsDelegationMeeting) is enforced server-side — a 403 surfaces here.
class DelegationMeetingRequestSheet extends ConsumerStatefulWidget {
  const DelegationMeetingRequestSheet({
    required this.country,
    required this.l10n,
    super.key,
  });

  /// The delegation to meet, or **null** for the bilateral entry (show the
  /// picker).
  final DelegationItem? country;
  final AppL10n l10n;

  @override
  ConsumerState<DelegationMeetingRequestSheet> createState() =>
      _DelegationMeetingRequestSheetState();
}

class _DelegationMeetingRequestSheetState
    extends ConsumerState<DelegationMeetingRequestSheet> {
  final TextEditingController _attendees = TextEditingController(text: '1');

  @override
  void dispose() {
    _attendees.dispose();
    super.dispose();
  }

  String? _validateAttendees() {
    final attendees = int.tryParse(_attendees.text.trim()) ?? 0;
    return attendees < 1 ? widget.l10n.delegationAttendeeCountInvalid : null;
  }

  // A35 — the server's own bilingual message wins. The old map hard-coded one
  // client string per status, so a 409 surfaced the SPEAKER copy ("this
  // speaker is not accepting meeting requests") on a DELEGATION sheet, and
  // every distinct 400 (subject too long, bad attendee count, invalid slot,
  // own delegation) read as "this delegation is not available for meetings".
  // The envelope already carries the message in the active language
  // (`ApiFailure.message`); the l10n strings stay as the fallback for a
  // failure that never reached the server (network / timeout, httpStatus null).
  String _failureText(ApiFailure failure) {
    if (failure.httpStatus != null && failure.message.trim().isNotEmpty) {
      return failure.message;
    }
    return switch (failure.httpStatus) {
      403 => widget.l10n.delegationNotAllowed,
      400 => widget.l10n.delegationTargetNotInvited,
      _ => widget.l10n.meetingRequestFailed,
    };
  }

  @override
  Widget build(BuildContext context) {
    final l10n = widget.l10n;
    final repository = ref.watch(delegationsRepositoryProvider);
    return MeetingRequestForm<DelegationItem>(
      target: widget.country,
      targetId: (country) => '${country.countryId}',
      l10n: l10n,
      title: l10n.delegationRequestTitle, // طلب اجتماع وفد
      keyPrefix: 'delegation',
      searchFieldKey: const ValueKey<String>('delegation-search'),
      pickerLabel: l10n.delegationSelectCountryLabel, // اختر الوفد
      pickerEmptyHint: l10n.delegationNoneAvailable,
      pinSelectedInPicker: false,
      noTargetSelectedError: l10n.delegationSelectCountryFirst,
      extraFields: <Widget>[
        // عدد الحضور
        MeetingFieldLabel(text: l10n.delegationAttendeeCountLabel),
        const SizedBox(height: SimfTokens.space2),
        DelegationAttendeeCountField(
          controller: _attendees,
          hintText: l10n.delegationAttendeeCountHint,
        ),
        const SizedBox(height: SimfTokens.space4),
      ],
      validateExtra: _validateAttendees,
      loadTargets: () async => <MeetingTargetOption<DelegationItem>>[
        for (final delegation in (await repository.getDelegations()).items)
          _optionFor(delegation),
      ],
      loadSlots: (country) async => <MeetingSlot>[
        for (final slot
            in await repository.getAvailableSlots(country.countryId))
          MeetingSlot(start: slot.start, end: slot.end),
      ],
      submit: ({
        required target,
        required subject,
        required slotStart,
        required slotEnd,
      }) =>
          repository.submitMeetingRequest(
        targetCountryCode: target.countryCode,
        attendeeCount: int.tryParse(_attendees.text.trim()) ?? 0,
        subject: subject,
        slotStart: slotStart,
        slotEnd: slotEnd,
      ),
      failureText: _failureText,
    );
  }

  MeetingTargetOption<DelegationItem> _optionFor(DelegationItem delegation) =>
      MeetingTargetOption<DelegationItem>(
        id: '${delegation.countryId}',
        value: delegation,
        matches: delegation.matches,
        buildTile: ({required selected, required onTap}) =>
            DelegationOptionTile(
          delegation: delegation,
          isArabic: widget.l10n.isArabic,
          selected: selected,
          onTap: onTap,
        ),
      );
}
