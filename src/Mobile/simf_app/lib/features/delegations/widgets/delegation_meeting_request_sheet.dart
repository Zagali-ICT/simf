import 'dart:async';
import '../../../core/utils/saudi_time.dart';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/utils/gregorian_month_names.dart';
import '../../../core/utils/weekday_names.dart';
import '../../speakers/widgets/meeting_slot_pickers.dart';
import '../data/delegation_models.dart';
import '../data/delegations_repository.dart';

/// Bi-Meeting rework — the delegation-meeting request sheet (طلب اجتماع وفد),
/// mirroring the speaker [MeetingRequestSheet]. A delegate of one invited country
/// asks to meet another invited country's delegation. Two entry points share it:
/// - from a **tapped delegation card** — [country] is set (fixed target);
/// - from the **"طلب اجتماع وفد"** button on the Bi-Meeting page — [country] is
///   null, so a searchable delegation picker is shown first.
///
/// The date + time come from the target delegation's real availability slots
/// (`GET /app/countries/{id}/available-slots`). With no windows the sheet sends
/// the request subject-only (the team arranges a time). Eligibility
/// (AllowsDelegationMeeting) is enforced server-side — a 403 surfaces here.
class DelegationMeetingRequestSheet extends ConsumerStatefulWidget {
  const DelegationMeetingRequestSheet({
    required this.country,
    required this.l10n,
    super.key,
  });

  /// The delegation to meet, or **null** for the bilateral entry (show the picker).
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

  // The delegation the request targets: the fixed [widget.country], or the one
  // picked from the list in the bilateral flow.
  DelegationItem? _selected;
  // The picker's delegation list (bilateral flow only).
  List<DelegationItem> _delegations = const <DelegationItem>[];
  bool _delegationsLoaded = false;
  String _query = '';

  // The chosen delegation's real availability slots, loaded once a target is set.
  List<DelegationSlot> _slots = const <DelegationSlot>[];
  bool _slotsLoading = false;
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

  /// Load the chosen delegation's real availability slots. A failure is treated as
  /// "no slots" so the request can still be sent subject-only.
  Future<void> _loadSlots(int countryId) async {
    setState(() {
      _slotsLoading = true;
      _slots = const <DelegationSlot>[];
      _selectedDay = null;
      _selectedSlot = null;
    });
    try {
      final slots = await ref
          .read(delegationsRepositoryProvider)
          .getAvailableSlots(countryId);
      // Drop a stale response — the user may have switched target while in flight.
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
      setState(() => _slotsLoading = false);
    }
  }

  void _onCountrySelected(DelegationItem country) {
    if (country.countryId == _selected?.countryId) {
      return;
    }
    setState(() => _selected = country);
    unawaited(_loadSlots(country.countryId));
  }

  /// The distinct local days that carry at least one slot, in endpoint order.
  List<DateTime> get _daysWithSlots {
    final days = <DateTime>[];
    for (final slot in _slots) {
      final local = saudiOf(slot.start);
      final day = DateTime(local.year, local.month, local.day);
      if (!days.contains(day)) {
        days.add(day);
      }
    }
    return days;
  }

  List<DelegationSlot> _slotsForDay(DateTime day) => <DelegationSlot>[
        for (final slot in _slots)
          if (_isSameDay(saudiOf(slot.start), day)) slot,
      ];

  static bool _isSameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;

  Future<void> _submit() async {
    final l10n = widget.l10n;
    final target = _selected;
    if (target == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.delegationSelectCountryFirst)),
      );
      return;
    }
    final subject = _subject.text.trim();
    if (subject.isEmpty) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(l10n.meetingRequestInvalid)));
      return;
    }
    final attendees = int.tryParse(_attendees.text.trim()) ?? 0;
    if (attendees < 1) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.delegationAttendeeCountInvalid)),
      );
      return;
    }
    // A slot is required only when the delegation actually offers slots.
    DateTime? slotStart;
    DateTime? slotEnd;
    if (_slots.isNotEmpty) {
      final slot = _selectedSlot;
      if (slot == null) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(l10n.meetingPickDateTime)));
        return;
      }
      slotStart = slot.start;
      slotEnd = slot.end;
    }
    setState(() => _submitting = true);
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
      navigator.pop();
      messenger.showSnackBar(SnackBar(content: Text(l10n.meetingRequestSent)));
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() => _submitting = false);
      messenger.showSnackBar(
        SnackBar(content: Text(_failureText(failure, l10n))),
      );
    }
  }

  // A time-of-day as "10:00 ص" / "02:30 PM" — matches the speaker sheet.
  String _formatTime(TimeOfDay time, bool isArabic) {
    final hour12 = time.hour % 12 == 0 ? 12 : time.hour % 12;
    final hh = hour12.toString().padLeft(2, '0');
    final mm = time.minute.toString().padLeft(2, '0');
    final meridiem = isArabic
        ? (time.hour >= 12 ? 'م' : 'ص')
        : (time.hour >= 12 ? 'PM' : 'AM');
    return '$hh:$mm $meridiem';
  }

  String _failureText(ApiFailure failure, AppL10n l10n) {
    if (failure.httpStatus == 403) {
      return l10n.delegationNotAllowed;
    }
    if (failure.httpStatus == 400) {
      return l10n.delegationTargetNotInvited;
    }
    if (failure.httpStatus == 409) {
      return l10n.meetingRequestNotAllowed;
    }
    return l10n.meetingRequestFailed;
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
              width: 80,
              height: 5,
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
              width: 20,
              height: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: SimfTokens.accent,
              ),
            ),
          ),
        ),
      ];
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

  /// The عدد الحضور (attendee count) input — a small digits-only field.
  Widget _attendeesField(AppL10n l10n) => TextField(
        key: const ValueKey<String>('delegation-attendees'),
        controller: _attendees,
        textAlign: TextAlign.start,
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
            borderRadius: SimfTokens.borderRadiusSmall,
            borderSide: BorderSide(color: SimfTokens.beigeBorder),
          ),
          focusedBorder: const OutlineInputBorder(
            borderRadius: SimfTokens.borderRadiusSmall,
            borderSide: BorderSide(color: SimfTokens.accent),
          ),
          border: const OutlineInputBorder(
            borderRadius: SimfTokens.borderRadiusSmall,
          ),
        ),
      );

  Widget _subjectField(AppL10n l10n) => TextField(
        key: const ValueKey<String>('delegation-subject'),
        controller: _subject,
        textAlign: TextAlign.start,
        maxLength: 1000,
        maxLines: 1,
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
            borderRadius: SimfTokens.borderRadiusSmall,
            borderSide: BorderSide(color: SimfTokens.beigeBorder),
          ),
          focusedBorder: const OutlineInputBorder(
            borderRadius: SimfTokens.borderRadiusSmall,
            borderSide: BorderSide(color: SimfTokens.accent),
          ),
          border: const OutlineInputBorder(
            borderRadius: SimfTokens.borderRadiusSmall,
          ),
        ),
      );

  /// The bilateral delegation picker — a searchable list of invited delegations
  /// (flag + country name + member count). Shown only when [country] is null.
  Widget _delegationPicker(AppL10n l10n, bool isArabic) {
    if (!_delegationsLoaded) {
      return const Align(
        alignment: AlignmentDirectional.centerStart,
        child: Padding(
          padding: EdgeInsets.symmetric(vertical: SimfTokens.space2),
          child: SizedBox(
            width: 20,
            height: 20,
            child: CircularProgressIndicator(
              strokeWidth: 2,
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
            constraints: const BoxConstraints(maxHeight: 264),
            child: ListView.separated(
              shrinkWrap: true,
              padding: EdgeInsets.zero,
              itemCount: matches.length,
              separatorBuilder: (_, __) =>
                  const SizedBox(height: SimfTokens.space2),
              itemBuilder: (context, i) {
                final d = matches[i];
                return _DelegationOptionTile(
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
            size: 18,
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
            weekday: gregorianWeekdayName(day, isArabic),
            dayNumber: day.day,
            month: gregorianMonthName(day.month, isArabic),
            selected: _selectedDay != null && _isSameDay(_selectedDay!, day),
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
              label: _formatTime(
                TimeOfDay.fromDateTime(saudiOf(slots[i].start)),
                isArabic,
              ),
              selected: _selectedSlot == slots[i],
              onTap: () => setState(() => _selectedSlot = slots[i]),
            ),
        ],
      ),
    );
  }

  Widget _sendButton(AppL10n l10n) => Material(
        color: SimfTokens.accent,
        borderRadius: SimfTokens.borderRadiusSmall,
        child: InkWell(
          onTap: (_submitting || _slotsLoading)
              ? null
              : () => unawaited(_submit()),
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
      );
}

/// One selectable delegation row in the picker — flag + localized country name +
/// member count, with a selected (gold) outline. Mirrors [SpeakerOptionTile]'s
/// role for the speaker picker.
class _DelegationOptionTile extends StatelessWidget {
  const _DelegationOptionTile({
    required this.delegation,
    required this.isArabic,
    required this.selected,
    required this.onTap,
  });

  final DelegationItem delegation;
  final bool isArabic;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.surface,
      borderRadius: SimfTokens.borderRadiusSmall,
      child: InkWell(
        onTap: onTap,
        borderRadius: SimfTokens.borderRadiusSmall,
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space3,
            vertical: SimfTokens.space3,
          ),
          decoration: BoxDecoration(
            borderRadius: SimfTokens.borderRadiusSmall,
            border: Border.all(
              color: selected ? SimfTokens.accent : SimfTokens.beigeBorder,
              width: selected ? 2 : 1,
            ),
          ),
          child: Row(
            children: <Widget>[
              Text(
                delegation.flagEmoji,
                style: const TextStyle(fontSize: 22),
              ),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Text(
                  delegation.localizedCountry(isArabic),
                  style: SimfTokens.labelNavyMediumSm,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Text(
                '${delegation.memberCount}',
                style: SimfTokens.bodyGreySm,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
