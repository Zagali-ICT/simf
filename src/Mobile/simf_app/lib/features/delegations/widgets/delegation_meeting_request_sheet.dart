import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/utils/gregorian_month_names.dart';
import 'package:simf_app/core/utils/local_days.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/core/utils/weekday_names.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_repository.dart';
import 'package:simf_app/features/delegations/widgets/delegation_option_tile.dart';
import 'package:simf_app/features/speakers/widgets/meeting_request_sheet.dart'
    show MeetingRequestSheet;
import 'package:simf_app/features/speakers/widgets/meeting_slot_pickers.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Bi-Meeting rework — the delegation-meeting request sheet (طلب اجتماع وفد),
/// mirroring the speaker [MeetingRequestSheet]. A delegate of one invited
/// country asks to meet another invited country's delegation. Two entry points
/// share it:
/// - from a **tapped delegation card** — [country] is set (fixed target);
/// - from the **"طلب اجتماع وفد"** button on the Bi-Meeting page — [country] is
///   null, so a searchable delegation picker is shown first.
///
/// The date + time come from the target delegation's real availability slots
/// (`GET /app/countries/{id}/available-slots`). Eligibility
/// (AllowsDelegationMeeting) is enforced server-side — a 403 surfaces here.
///
/// G3 (owner 2026-07-30, supersedes D-767 R1) — with **no free slot** the
/// request can no longer be sent subject-only: the sheet shows the "no slots"
/// notice and the send button is disabled (the API 409s
/// `DELEGATION_MEETING_NO_AVAILABILITY`). A **failed** slot fetch is a separate
/// state — it shows a load error + Retry, so a transient network failure never
/// masquerades as "this delegation has no time".
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
  final TextEditingController _subject = TextEditingController();
  final TextEditingController _attendees = TextEditingController(text: '1');
  bool _submitting = false;
  // R0 (D-767) — validation / API-failure feedback shown INLINE inside the sheet.
  // A ScaffoldMessenger snackbar fired while this modal bottom sheet is open
  // renders behind the sheet and is invisible; inline text stays on screen.
  String? _error;

  // The delegation the request targets: the fixed [widget.country], or the one
  // picked from the list in the bilateral flow.
  DelegationItem? _selected;
  // The picker's delegation list (bilateral flow only).
  List<DelegationItem> _delegations = const <DelegationItem>[];
  bool _delegationsLoaded = false;
  String _query = '';

  // The chosen delegation's real availability slots, loaded once a target is
  // set.
  List<DelegationSlot> _slots = const <DelegationSlot>[];
  bool _slotsLoading = false;
  // G3 — the slot fetch FAILED (network / server), which is NOT the same as the
  // delegation having no availability. Kept apart so the sheet can offer a
  // retry instead of telling the user something untrue and locking the send
  // button.
  bool _slotsError = false;
  DateTime? _selectedDay;
  DelegationSlot? _selectedSlot;

  @override
  void initState() {
    super.initState();
    _selected = widget.country;
    if (widget.country == null) {
      unawaited(_loadDelegations());
    } else {
      unawaited(_loadSlots(widget.country!.countryId));
    }
  }

  @override
  void dispose() {
    _subject.dispose();
    _attendees.dispose();
    super.dispose();
  }

  /// Bilateral flow — fetch the invited delegations for the picker.
  Future<void> _loadDelegations() async {
    try {
      final delegations =
          await ref.read(delegationsRepositoryProvider).getDelegations();
      if (!mounted) {
        return;
      }
      setState(() {
        _delegations = delegations.items;
        _delegationsLoaded = true;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() => _delegationsLoaded = true);
    }
  }

  /// Load the chosen delegation's real availability slots. G3 — a failure is NO
  /// LONGER folded into "no slots": now that an empty list disables sending, a
  /// swallowed network error would tell the user the delegation has no
  /// availability and leave them stuck, so it gets its own state + a Retry.
  Future<void> _loadSlots(int countryId) async {
    setState(() {
      _slotsLoading = true;
      _slotsError = false;
      _slots = const <DelegationSlot>[];
      _selectedDay = null;
      _selectedSlot = null;
    });
    try {
      final slots = await ref
          .read(delegationsRepositoryProvider)
          .getAvailableSlots(countryId);
      // Drop a stale response — the user may have switched target while in
      // flight.
      if (!mounted || countryId != _selected?.countryId) {
        return;
      }
      setState(() {
        _slots = slots;
        _slotsLoading = false;
      });
    } on ApiFailure {
      if (!mounted || countryId != _selected?.countryId) {
        return;
      }
      setState(() {
        _slotsLoading = false;
        _slotsError = true;
      });
    }
  }

  void _onCountrySelected(DelegationItem country) {
    if (country.countryId == _selected?.countryId) {
      return;
    }
    setState(() => _selected = country);
    unawaited(_loadSlots(country.countryId));
  }

  /// The distinct local days that carry at least one slot. The endpoint
  /// derives slots chronologically, so the shared helper's ascending order is
  /// the endpoint's order; it is also correct if that ever stops holding.
  List<DateTime> get _daysWithSlots =>
      distinctLocalDays(_slots, (slot) => saudiOf(slot.start));

  /// The slots on a given local day, in the endpoint's (chronological) order.
  List<DelegationSlot> _slotsForDay(DateTime day) => <DelegationSlot>[
        for (final slot in _slots)
          if (sameLocalDay(saudiOf(slot.start), day)) slot,
      ];

  Future<void> _submit() async {
    final l10n = widget.l10n;
    final target = _selected;
    if (target == null) {
      setState(() => _error = l10n.delegationSelectCountryFirst);
      return;
    }
    final subject = _subject.text.trim();
    if (subject.isEmpty) {
      setState(() => _error = l10n.meetingRequestInvalid);
      return;
    }
    final attendees = int.tryParse(_attendees.text.trim()) ?? 0;
    if (attendees < 1) {
      setState(() => _error = l10n.delegationAttendeeCountInvalid);
      return;
    }
    // G3 — a slot is now ALWAYS required. The subject-only bypass is gone: the
    // server 409s a request against a delegation with no free slot, so sending
    // one could only ever fail. The send button is disabled in that state; this
    // is the guard for the picked-a-day-but-not-a-time case.
    final slot = _selectedSlot;
    if (slot == null) {
      setState(() => _error = l10n.meetingPickDateTime);
      return;
    }
    final slotStart = slot.start;
    final slotEnd = slot.end;
    // R0 — clear the inline error and submit. Feedback stays inside the sheet.
    setState(() {
      _submitting = true;
      _error = null;
    });
    final navigator = Navigator.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(delegationsRepositoryProvider).submitMeetingRequest(
            targetCountryCode: target.countryCode,
            attendeeCount: attendees,
            subject: subject,
            slotStart: slotStart,
            slotEnd: slotEnd,
          );
      if (!mounted) {
        return;
      }
      // Success pops the sheet first, so this toast is visible (not occluded).
      navigator.pop();
      messenger.showSnackBar(SnackBar(content: Text(l10n.meetingRequestSent)));
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _submitting = false;
        _error = _failureText(failure, l10n);
      });
    }
  }

  // A35 — the server's own bilingual message wins. The old map hard-coded one
  // client string per status, so a 409 surfaced the SPEAKER copy ("this
  // speaker is not accepting meeting requests") on a DELEGATION sheet, and
  // every distinct 400 (subject too long, bad attendee count, invalid slot,
  // own delegation) read as "this delegation is not available for meetings".
  // The envelope already carries the message in the active language
  // (`ApiFailure.message`); the l10n strings stay as the fallback for a
  // failure that never reached the server (network / timeout, httpStatus null).
  String _failureText(ApiFailure failure, AppL10n l10n) {
    if (failure.httpStatus != null && failure.message.trim().isNotEmpty) {
      return failure.message;
    }
    return switch (failure.httpStatus) {
      403 => l10n.delegationNotAllowed,
      400 => l10n.delegationTargetNotInvited,
      _ => l10n.meetingRequestFailed,
    };
  }

  @override
  Widget build(BuildContext context) {
    final l10n = widget.l10n;
    final isArabic = l10n.isArabic;
    return SingleChildScrollView(
      padding: EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space3,
        SimfTokens.space4,
        MediaQuery.of(context).viewInsets.bottom + SimfTokens.space6,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Center(
            child: Container(
              width: SimfTokens.delegationMeetingRequestSheetWidthMd,
              height: SimfTokens.delegationMeetingRequestSheetHeightSm,
              decoration: BoxDecoration(
                color: SimfTokens.accent,
                borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
              ),
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          Text(
            l10n.delegationRequestTitle, // طلب اجتماع وفد
            textAlign: TextAlign.end,
            style: SimfTokens.labelInkSemiboldTitle,
          ),
          const SizedBox(height: SimfTokens.space4),
          if (widget.country == null) ...<Widget>[
            _label(l10n.delegationSelectCountryLabel), // اختر الوفد
            const SizedBox(height: SimfTokens.space2),
            _delegationPicker(l10n, isArabic),
            const SizedBox(height: SimfTokens.space4),
          ],
          if (_selected != null) ...<Widget>[
            _label(l10n.delegationAttendeeCountLabel), // عدد الحضور
            const SizedBox(height: SimfTokens.space2),
            _attendeesField(l10n),
            const SizedBox(height: SimfTokens.space4),
            _label(l10n.meetingSubjectLabel), // الموضوع
            const SizedBox(height: SimfTokens.space2),
            _subjectField(l10n),
            const SizedBox(height: SimfTokens.space4),
            ..._slotSection(l10n, isArabic),
            if (_error != null) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              Align(
                alignment: AlignmentDirectional.centerStart,
                child: Text(_error!, style: SimfTokens.bodyDanger),
              ),
            ],
            const SizedBox(height: SimfTokens.space5),
            _sendButton(l10n),
          ],
        ],
      ),
    );
  }

  List<Widget> _slotSection(AppL10n l10n, bool isArabic) {
    if (_slotsLoading) {
      return const <Widget>[
        Align(
          alignment: AlignmentDirectional.centerStart,
          child: Padding(
            padding: EdgeInsets.symmetric(vertical: SimfTokens.space2),
            child: SizedBox(
              width: SimfTokens.space5,
              height: SimfTokens.space5,
              child: CircularProgressIndicator(
                strokeWidth:
                    SimfTokens.delegationMeetingRequestSheetStrokeWidth,
                color: SimfTokens.accent,
              ),
            ),
          ),
        ),
      ];
    }
    if (_slotsError) {
      return <Widget>[_slotsRetry(l10n)];
    }
    if (_slots.isEmpty) {
      return <Widget>[_hint(l10n.meetingSlotNone)];
    }
    return <Widget>[
      _label(l10n.meetingChooseDateLabel),
      const SizedBox(height: SimfTokens.space2),
      _dayCards(isArabic),
      const SizedBox(height: SimfTokens.space4),
      _label(l10n.meetingChooseTimeLabel),
      const SizedBox(height: SimfTokens.space2),
      if (_selectedDay == null)
        _hint(l10n.meetingChooseDateFirst)
      else
        _timeChips(isArabic),
    ];
  }

  Widget _label(String text) => Align(
        alignment: AlignmentDirectional.centerStart,
        child: Text(text, style: SimfTokens.labelNavyMediumSm),
      );

  Widget _hint(String text) => Align(
        alignment: AlignmentDirectional.centerStart,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: SimfTokens.space1),
          child: Text(text, style: SimfTokens.bodyGreySm),
        ),
      );

  /// G3 — the slot fetch failed: say so and offer a Retry. Deliberately
  /// different copy from [AppL10n.meetingSlotNone] so a network blip is never
  /// read as "this delegation has no availability", which would be a lie the
  /// user cannot act on.
  Widget _slotsRetry(AppL10n l10n) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _hint(l10n.lookupLoadError), // تعذر تحميل القائمة.
          Align(
            alignment: AlignmentDirectional.centerStart,
            child: TextButton(
              key: const ValueKey<String>('delegation-slots-retry'),
              onPressed: _retrySlots,
              child: Text(l10n.retryLabel, style: SimfTokens.labelNavyMediumSm),
            ),
          ),
        ],
      );

  void _retrySlots() {
    final target = _selected;
    if (target == null) {
      return;
    }
    unawaited(_loadSlots(target.countryId));
  }

  /// The عدد الحضور (attendee count) input — a small digits-only field.
  Widget _attendeesField(AppL10n l10n) => TextField(
        key: const ValueKey<String>('delegation-attendees'),
        controller: _attendees,
        keyboardType: TextInputType.number,
        inputFormatters: <TextInputFormatter>[
          FilteringTextInputFormatter.digitsOnly,
          LengthLimitingTextInputFormatter(4),
        ],
        style: SimfTokens.bodyInputMd,
        decoration: InputDecoration(
          hintText: l10n.delegationAttendeeCountHint,
          hintStyle: SimfTokens.bodyGreyMd,
          filled: true,
          fillColor: SimfTokens.surface,
          contentPadding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space4,
            vertical: SimfTokens.space3,
          ),
          enabledBorder: const OutlineInputBorder(
            borderSide: BorderSide(color: SimfTokens.beigeBorder),
          ),
          focusedBorder: const OutlineInputBorder(
            borderSide: BorderSide(color: SimfTokens.accent),
          ),
          border: const OutlineInputBorder(),
        ),
      );

  Widget _subjectField(AppL10n l10n) => TextField(
        key: const ValueKey<String>('delegation-subject'),
        controller: _subject,
        maxLength: FieldLimits.meetingRequestMessage,
        style: SimfTokens.bodyInputMd,
        decoration: InputDecoration(
          counterText: '',
          hintText: l10n.meetingSubjectHint,
          hintStyle: SimfTokens.bodyGreyMd,
          filled: true,
          fillColor: SimfTokens.surface,
          contentPadding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space4,
            vertical: SimfTokens.space3,
          ),
          enabledBorder: const OutlineInputBorder(
            borderSide: BorderSide(color: SimfTokens.beigeBorder),
          ),
          focusedBorder: const OutlineInputBorder(
            borderSide: BorderSide(color: SimfTokens.accent),
          ),
          border: const OutlineInputBorder(),
        ),
      );

  /// The bilateral delegation picker — a searchable list of invited delegations
  /// (flag + country name + member count). Shown only when `country` is null.
  Widget _delegationPicker(AppL10n l10n, bool isArabic) {
    if (!_delegationsLoaded) {
      return const Align(
        alignment: AlignmentDirectional.centerStart,
        child: Padding(
          padding: EdgeInsets.symmetric(vertical: SimfTokens.space2),
          child: SizedBox(
            width: SimfTokens.space5,
            height: SimfTokens.space5,
            child: CircularProgressIndicator(
              strokeWidth: SimfTokens.delegationMeetingRequestSheetStrokeWidth,
              color: SimfTokens.accent,
            ),
          ),
        ),
      );
    }
    if (_delegations.isEmpty) {
      return _hint(l10n.delegationNoneAvailable);
    }
    final matches =
        _delegations.where((d) => d.matches(_query)).toList(growable: false);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _searchField(l10n),
        const SizedBox(height: SimfTokens.space2),
        if (matches.isEmpty)
          _hint(l10n.speakersNoMatches)
        else
          ConstrainedBox(
            constraints: const BoxConstraints(
                maxHeight: SimfTokens.delegationMeetingRequestSheetMaxHeight,),
            child: ListView.separated(
              shrinkWrap: true,
              padding: EdgeInsets.zero,
              itemCount: matches.length,
              separatorBuilder: (_, __) =>
                  const SizedBox(height: SimfTokens.space2),
              itemBuilder: (context, i) {
                final d = matches[i];
                return DelegationOptionTile(
                  delegation: d,
                  isArabic: isArabic,
                  selected: _selected?.countryId == d.countryId,
                  onTap: _submitting ? null : () => _onCountrySelected(d),
                );
              },
            ),
          ),
      ],
    );
  }

  Widget _searchField(AppL10n l10n) => TextField(
        key: const ValueKey<String>('delegation-search'),
        onChanged: (value) => setState(() => _query = value),
        style: SimfTokens.bodyInputMd,
        decoration: InputDecoration(
          isDense: true,
          hintText: l10n.speakersSearchHint,
          hintStyle: SimfTokens.bodyGreyMd,
          prefixIcon: const Icon(
            Icons.search,
            color: SimfTokens.greyText,
            size: SimfTokens.delegationMeetingRequestSheetSize,
          ),
          filled: true,
          fillColor: SimfTokens.surface,
          contentPadding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space3,
            vertical: SimfTokens.space3,
          ),
          enabledBorder: const OutlineInputBorder(
            borderSide: BorderSide(color: SimfTokens.beigeBorder),
          ),
          focusedBorder: const OutlineInputBorder(
            borderSide: BorderSide(color: SimfTokens.accent),
          ),
        ),
      );

  Widget _dayCards(bool isArabic) {
    final days = _daysWithSlots;
    return SizedBox(
      height: SimfTokens.dayCardHeight,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: days.length,
        separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space2),
        itemBuilder: (context, i) {
          final day = days[i];
          return MeetingDayCard(
            key: ValueKey<String>('delegation-day-$i'),
            weekday: gregorianWeekdayName(day, isArabic: isArabic),
            dayNumber: day.day,
            month: gregorianMonthName(day.month, isArabic: isArabic),
            selected: _selectedDay != null && sameLocalDay(_selectedDay!, day),
            onTap: () => setState(() {
              _selectedDay = day;
              _selectedSlot = null;
            }),
          );
        },
      ),
    );
  }

  Widget _timeChips(bool isArabic) {
    final slots = _slotsForDay(_selectedDay!);
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Wrap(
        spacing: SimfTokens.space2,
        runSpacing: SimfTokens.space2,
        children: <Widget>[
          for (var i = 0; i < slots.length; i++)
            MeetingTimeChip(
              key: ValueKey<String>('delegation-time-$i'),
              label: formatDateTime12h(
                saudiOf(slots[i].start),
                isArabic: isArabic,
              ),
              selected: _selectedSlot == slots[i],
              onTap: () => setState(() => _selectedSlot = slots[i]),
            ),
        ],
      ),
    );
  }

  /// G3 — disabled while the slots load, and disabled once they are loaded and
  /// EMPTY (no free slot ⇒ the server would 409), so the user is never invited
  /// to send a request that cannot succeed. A failed fetch ([_slotsError]) also
  /// leaves it disabled — the Retry in the slot section is the way forward
  /// there.
  Widget _sendButton(AppL10n l10n) {
    final enabled = !_submitting && !_slotsLoading && _slots.isNotEmpty;
    return Opacity(
      opacity: enabled ? 1 : SimfTokens.opacityDisabled,
      child: Material(
        color: SimfTokens.accent,
        borderRadius: SimfTokens.borderRadiusSmall,
        child: InkWell(
          onTap: enabled ? () => unawaited(_submit()) : null,
          borderRadius: SimfTokens.borderRadiusSmall,
          child: Container(
            height: SimfTokens.controlHeight,
            alignment: Alignment.center,
            child: Text(
              _submitting ? l10n.loadingLabel : l10n.meetingSendButton,
              style: SimfTokens.labelWhiteBoldLg,
            ),
          ),
        ),
      ),
    );
  }
}
