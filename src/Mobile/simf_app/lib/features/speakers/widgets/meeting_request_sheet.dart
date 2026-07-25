import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/utils/gregorian_month_names.dart';
import '../../../core/utils/weekday_names.dart';
import '../data/speaker_models.dart';
import '../data/speakers_repository.dart';
import 'meeting_slot_pickers.dart';
import 'speaker_option_tile.dart';

/// The meeting-request form (bottom sheet) — approved-account only (E2). The
/// beige "طلب مقابلة" sheet, Figma **1776:5036**: a gold drag handle, the
/// الموضوع subject field, the speaker's available days (اختيار التاريخ) with that
/// day's time slots (اختيار الوقت), then the gold "ارسال الطلب" button.
///
/// Two entry points share this one sheet:
/// - from a **speaker profile** — [speakerId] is set, so the speaker is fixed and
///   no picker is shown;
/// - from the **"طلب جديد"** on the requests list (اللقاءات الثنائية, 1408:9726) —
///   [speakerId] is **null**, so the sheet first shows a searchable speaker
///   picker (type-to-filter by name/rank, owner 2026-07-11).
///
/// D-709 (item 6, FDS-013 §15.4 GAP-4) — the date + time come from the speaker's
/// **real availability slots** (`GET /app/speakers/{id}/available-slots`), NOT a
/// free client-side grid; this **reverts D-703**. When the speaker has no windows
/// the sheet shows a clear "no slots" state and the request is sent subject-only
/// (the team then arranges a time). Booking a slot is VIP-gated by the server (a
/// 403 surfaces "VIP only").
class MeetingRequestSheet extends ConsumerStatefulWidget {
  const MeetingRequestSheet({
    required this.speakerId,
    required this.defaultName,
    required this.baseUrl,
    required this.l10n,
    super.key,
  });

  /// The speaker to meet, or **null** for the bilateral entry (show the picker).
  final String? speakerId;
  final String defaultName;

  /// The API base URL — used to build the speaker photo asset URL for the
  /// bilateral picker's identity rows (D-745). Unused on the profile flow (a
  /// fixed speaker, no picker), but always supplied so the constructor is uniform.
  final String baseUrl;
  final AppL10n l10n;

  @override
  ConsumerState<MeetingRequestSheet> createState() =>
      _MeetingRequestSheetState();
}

class _MeetingRequestSheetState extends ConsumerState<MeetingRequestSheet> {
  final TextEditingController _subject = TextEditingController();
  bool _submitting = false;
  // The speaker the request targets: the fixed [widget.speakerId] from a speaker
  // profile, or the one picked from the dropdown in the bilateral flow.
  String? _selectedSpeakerId;
  // The picker's speaker list (bilateral flow only; empty on the profile flow).
  List<SpeakerSummary> _speakers = const <SpeakerSummary>[];
  bool _speakersLoaded = false;
  // The picker's type-to-filter query (owner 2026-07-11) — matched against the
  // speaker name + rank, mirroring the speakers list search (908:1744).
  String _speakerQuery = '';
  // The chosen speaker's real availability slots (D-709), loaded once a speaker
  // is set. Empty (once loaded) ⇒ the "no slots" state.
  List<SpeakerSlot> _slots = const <SpeakerSlot>[];
  bool _slotsLoading = false;
  // The picked calendar day (one that has slots) and the picked slot within it.
  DateTime? _selectedDay;
  SpeakerSlot? _selectedSlot;

  @override
  void initState() {
    super.initState();
    _selectedSpeakerId = widget.speakerId;
    if (widget.speakerId == null) {
      // Bilateral entry (no fixed speaker) — load the speaker list for the picker.
      unawaited(_loadSpeakers());
    } else {
      unawaited(_loadSlots(widget.speakerId!));
    }
  }

  /// Bilateral flow — fetch the speakers for the dropdown.
  Future<void> _loadSpeakers() async {
    try {
      final speakers = await ref.read(speakersRepositoryProvider).getSpeakers();
      if (!mounted) {
        return;
      }
      setState(() {
        _speakers = speakers;
        _speakersLoaded = true;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() => _speakersLoaded = true);
    }
  }

  /// Load the chosen speaker's real availability slots (D-709). A failure is
  /// treated as "no slots" so the request can still be sent subject-only.
  Future<void> _loadSlots(String speakerId) async {
    setState(() {
      _slotsLoading = true;
      _slots = const <SpeakerSlot>[];
      _selectedDay = null;
      _selectedSlot = null;
    });
    try {
      final slots = await ref
          .read(speakersRepositoryProvider)
          .getAvailableSlots(speakerId);
      // Drop a stale response — in the bilateral flow the user may have switched
      // to another speaker while this load was in flight.
      if (!mounted || speakerId != _selectedSpeakerId) {
        return;
      }
      setState(() {
        _slots = slots;
        _slotsLoading = false;
      });
    } on ApiFailure {
      if (!mounted || speakerId != _selectedSpeakerId) {
        return;
      }
      setState(() => _slotsLoading = false);
    }
  }

  void _onSpeakerSelected(String? speakerId) {
    if (speakerId == null || speakerId == _selectedSpeakerId) {
      return;
    }
    setState(() => _selectedSpeakerId = speakerId);
    unawaited(_loadSlots(speakerId));
  }

  /// The distinct local days that carry at least one slot, in the order the
  /// endpoint returned them (it derives slots chronologically).
  List<DateTime> get _daysWithSlots {
    final days = <DateTime>[];
    for (final slot in _slots) {
      final local = slot.start.toLocal();
      final day = DateTime(local.year, local.month, local.day);
      if (!days.contains(day)) {
        days.add(day);
      }
    }
    return days;
  }

  /// The slots on a given local day, in the endpoint's (chronological) order.
  List<SpeakerSlot> _slotsForDay(DateTime day) => <SpeakerSlot>[
        for (final slot in _slots)
          if (_isSameDay(slot.start.toLocal(), day)) slot,
      ];

  static bool _isSameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;

  @override
  void dispose() {
    _subject.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final l10n = widget.l10n;
    // Owner: "no need for name" — the requester is the signed-in account, so we
    // submit its display name as the requesterName the backend contract requires.
    final name = widget.defaultName.trim();
    final speakerId = _selectedSpeakerId;
    if (speakerId == null) {
      // Bilateral flow with no speaker picked yet.
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.meetingSelectSpeakerFirst)),
      );
      return;
    }
    final subject = _subject.text.trim();
    if (subject.isEmpty) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(l10n.meetingRequestInvalid)));
      return;
    }
    // A slot is required only when the speaker actually offers slots; with no
    // slots the request goes subject-only and the team arranges a time.
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
      await ref.read(speakersRepositoryProvider).submitMeetingRequest(
            speakerId,
            requesterName: name,
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

  // A time-of-day as "10:00 ص" / "02:30 PM" (12-hour, Arabic ص/م — Figma
  // 1776:5078). No intl locale needed.
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
      return l10n.meetingVipOnly;
    }
    if (failure.httpStatus == 409) {
      return l10n.meetingRequestNotAllowed;
    }
    if (failure.httpStatus == 400) {
      return l10n.meetingRequestInvalid;
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
            l10n.requestMeeting, // طلب مقابلة
            textAlign: TextAlign.end,
            style: SimfTokens.labelInkSemiboldTitle,
          ),
          const SizedBox(height: SimfTokens.space4),
          // Bilateral entry (speakerId == null): the speaker picker. A speaker
          // profile fixes the speaker, so it shows no picker.
          if (widget.speakerId == null) ...<Widget>[
            _label(l10n.meetingSelectSpeakerLabel), // اختر المتحدث
            const SizedBox(height: SimfTokens.space2),
            _speakerPicker(l10n, isArabic),
            const SizedBox(height: SimfTokens.space4),
          ],
          // The subject + slots + send appear once a speaker is set (always on
          // the profile flow; after a pick on the bilateral flow).
          if (_selectedSpeakerId != null) ...<Widget>[
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

  /// The date + time section: a spinner while loading, a "no slots" hint when the
  /// speaker has no windows, else the day cards + the selected day's time chips.
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
      return <Widget>[_hint(l10n.meetingSlotNone)]; // لا توجد فترات متاحة
    }
    return <Widget>[
      _label(l10n.meetingChooseDateLabel), // اختر التاريخ
      const SizedBox(height: SimfTokens.space2),
      _dayCards(isArabic),
      const SizedBox(height: SimfTokens.space4),
      _label(l10n.meetingChooseTimeLabel), // اختر الوقت
      const SizedBox(height: SimfTokens.space2),
      if (_selectedDay == null)
        _hint(l10n.meetingChooseDateFirst)
      else
        _timeChips(isArabic),
    ];
  }

  /// A form field label — navy, 12px, at the inline start (right, RTL).
  Widget _label(String text) => Align(
        alignment: AlignmentDirectional.centerStart,
        child: Text(
          text,
          style: SimfTokens.labelNavyMediumSm,
        ),
      );

  /// A muted, inline-start hint line (no-slots notice / choose-a-date-first).
  Widget _hint(String text) => Align(
        alignment: AlignmentDirectional.centerStart,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: SimfTokens.space1),
          child: Text(
            text,
            style: SimfTokens.bodyGreySm,
          ),
        ),
      );

  /// The subject input — a white, beige-bordered field with the "اكتب الموضوع"
  /// hint (Figma 1776:5048).
  Widget _subjectField(AppL10n l10n) => TextField(
        key: const ValueKey<String>('meeting-subject'),
        controller: _subject,
        textAlign: TextAlign.start,
        maxLength: 1000,
        maxLines: 1,
        style: SimfTokens.bodyInputMd,
        decoration: InputDecoration(
          counterText: '',
          hintText: l10n.meetingSubjectHint, // اكتب الموضوع
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

  /// The bilateral speaker picker (owner 2026-07-11) — a selectable list of every
  /// speaker showing the same **photo + name + country flag + rank** identity the
  /// speakers list uses, instead of a bare name dropdown. Tapping a row selects
  /// that speaker (and loads their real availability slots). Shown only when
  /// [MeetingRequestSheet.speakerId] is null. The list is height-capped and scrolls
  /// internally so a long roster never pushes the subject/slots off-screen.
  Widget _speakerPicker(AppL10n l10n, bool isArabic) {
    if (!_speakersLoaded) {
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
    if (_speakers.isEmpty) {
      return _hint(l10n.meetingSelectSpeakerHint);
    }
    final matches = _filteredSpeakers(isArabic);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _speakerSearchField(l10n),
        const SizedBox(height: SimfTokens.space2),
        if (matches.isEmpty)
          _hint(l10n.speakersNoMatches) // لا نتائج مطابقة
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
                final speaker = matches[i];
                return SpeakerOptionTile(
                  speaker: speaker,
                  isArabic: isArabic,
                  baseUrl: widget.baseUrl,
                  selected: _selectedSpeakerId == speaker.id,
                  onTap:
                      _submitting ? null : () => _onSpeakerSelected(speaker.id),
                );
              },
            ),
          ),
      ],
    );
  }

  /// The picker's speakers after the type-to-filter query — matched against the
  /// name + rank, case-insensitive, mirroring the speakers list (908:1744) so
  /// search behaves identically wherever a speaker is chosen. The already-chosen
  /// speaker is always kept in the list even when it doesn't match the query, so
  /// the picker can never hide (or contradict) the target the form submits to.
  List<SpeakerSummary> _filteredSpeakers(bool isArabic) {
    final q = _speakerQuery.trim().toLowerCase();
    if (q.isEmpty) {
      return _speakers;
    }
    return _speakers.where((s) {
      if (s.id == _selectedSpeakerId) {
        return true;
      }
      final name = s.localizedName(isArabic).toLowerCase();
      final rank = (s.rank ?? '').toLowerCase();
      final rankArabic = (s.rankArabic ?? '').toLowerCase();
      return name.contains(q) || rank.contains(q) || rankArabic.contains(q);
    }).toList();
  }

  /// The picker's type-to-filter field (owner 2026-07-11) — the sheet's beige
  /// field look with a leading magnifier so a VIP can filter a long speaker
  /// roster instead of scrolling. Reuses the speakers-list search hint.
  Widget _speakerSearchField(AppL10n l10n) => TextField(
        key: const ValueKey<String>('meeting-speaker-search'),
        onChanged: (value) => setState(() => _speakerQuery = value),
        style: SimfTokens.bodyInputMd,
        decoration: InputDecoration(
          isDense: true,
          hintText: l10n.speakersSearchHint, // ما الذي تبحث عنه
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
          // OutlineInputBorder's default radius is circular-4 (==
          // SimfTokens.borderRadiusSmall), so it is left implicit — passing it
          // trips avoid_redundant_argument_values (as in lookup_search_sheet).
          enabledBorder: const OutlineInputBorder(
            borderSide: BorderSide(color: SimfTokens.beigeBorder),
          ),
          focusedBorder: const OutlineInputBorder(
            borderSide: BorderSide(color: SimfTokens.accent),
          ),
        ),
      );

  /// The horizontal row of the speaker's available day cards (Figma 1776:5052).
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
            key: ValueKey<String>('meeting-day-$i'),
            weekday: gregorianWeekdayName(day, isArabic),
            dayNumber: day.day,
            month: gregorianMonthName(day.month, isArabic),
            selected: _selectedDay != null && _isSameDay(_selectedDay!, day),
            onTap: () => setState(() {
              _selectedDay = day;
              _selectedSlot = null; // a new day → pick from its own slots
            }),
          );
        },
      ),
    );
  }

  /// Selected day's available slots as tappable time chips (Figma 1776:5076).
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
              key: ValueKey<String>('meeting-time-$i'),
              label: _formatTime(
                TimeOfDay.fromDateTime(slots[i].start.toLocal()),
                isArabic,
              ),
              selected: _selectedSlot == slots[i],
              onTap: () => setState(() => _selectedSlot = slots[i]),
            ),
        ],
      ),
    );
  }

  /// The full-width gold "ارسال الطلب" button (Figma 1776:5083).
  Widget _sendButton(AppL10n l10n) => Material(
        color: SimfTokens.accent,
        borderRadius: SimfTokens.borderRadiusSmall,
        child: InkWell(
          // Disabled while slots load so a fast tap can't submit subject-only
          // before we know whether the speaker offers windows.
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
