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

/// The meeting-request form (bottom sheet) — approved-account only (E2). The
/// beige "طلب مقابلة" sheet, Figma **1776:5036**: a gold drag handle, the
/// الموضوع subject field, a row of upcoming day cards (اختيار التاريخ), standard
/// time chips (اختيار الوقت), then the gold "ارسال الطلب" button.
///
/// Two entry points share this one sheet:
/// - from a **speaker profile** — [speakerId] is set, so the speaker is fixed and
///   no picker is shown;
/// - from the **"طلب جديد"** on the requests list (اللقاءات الثنائية, 1408:9726) —
///   [speakerId] is **null**, so the sheet first shows a speaker **dropdown**.
///
/// The date + time are a free client-side pick (owner 2026-07-08): the next seven
/// days as cards and standard hourly times as chips — not tied to a speaker's
/// availability slots — so the sheet always renders the full Figma layout. The
/// request itself is VIP-gated by the server (a 403 surfaces "VIP only").
class MeetingRequestSheet extends ConsumerStatefulWidget {
  const MeetingRequestSheet({
    required this.speakerId,
    required this.defaultName,
    required this.l10n,
    this.now,
    super.key,
  });

  /// The speaker to meet, or **null** for the bilateral entry (show the picker).
  final String? speakerId;
  final String defaultName;
  final AppL10n l10n;

  /// The clock for the day cards; defaults to live. Injected by tests so the
  /// generated dates are deterministic.
  final DateTime? now;

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
  // The picked calendar day + time of day (a free pick — Figma 1776:5052/5076).
  DateTime? _selectedDay;
  TimeOfDay? _selectedTime;

  // Standard meeting hours as time chips (Figma 1776:5076): 09:00 ص → 05:00 م.
  static const List<TimeOfDay> _timeOptions = <TimeOfDay>[
    TimeOfDay(hour: 9, minute: 0),
    TimeOfDay(hour: 10, minute: 0),
    TimeOfDay(hour: 11, minute: 0),
    TimeOfDay(hour: 12, minute: 0),
    TimeOfDay(hour: 13, minute: 0),
    TimeOfDay(hour: 14, minute: 0),
    TimeOfDay(hour: 15, minute: 0),
    TimeOfDay(hour: 16, minute: 0),
    TimeOfDay(hour: 17, minute: 0),
  ];

  @override
  void initState() {
    super.initState();
    _selectedSpeakerId = widget.speakerId;
    // Bilateral entry (no fixed speaker) — load the speaker list for the picker.
    if (widget.speakerId == null) {
      unawaited(_loadSpeakers());
    }
  }

  /// The next seven selectable days from today (Figma 1776:5052 — day cards).
  List<DateTime> get _days {
    final base = widget.now ?? DateTime.now();
    final today = DateTime(base.year, base.month, base.day);
    return <DateTime>[
      for (var i = 0; i < 7; i++) today.add(Duration(days: i)),
    ];
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

  void _onSpeakerSelected(String? speakerId) {
    if (speakerId == null || speakerId == _selectedSpeakerId) {
      return;
    }
    setState(() => _selectedSpeakerId = speakerId);
  }

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
    final day = _selectedDay;
    final time = _selectedTime;
    if (day == null || time == null) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(l10n.meetingPickDateTime)));
      return;
    }
    final startLocal =
        DateTime(day.year, day.month, day.day, time.hour, time.minute);
    setState(() => _submitting = true);
    final navigator = Navigator.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(speakersRepositoryProvider).submitMeetingRequest(
            speakerId,
            requesterName: name,
            subject: subject,
            slotStartUtc: startLocal.toUtc(),
            slotEndUtc: startLocal.add(const Duration(hours: 1)).toUtc(),
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
            style: const TextStyle(
              color: SimfTokens.headlineInk,
              fontWeight: FontWeight.w600,
              fontSize: SimfTokens.textTitle, // 18
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          // Bilateral entry (speakerId == null): the speaker picker. A speaker
          // profile fixes the speaker, so it shows no picker.
          if (widget.speakerId == null) ...<Widget>[
            _label(l10n.meetingSelectSpeakerLabel), // اختر المتحدث
            const SizedBox(height: SimfTokens.space2),
            _speakerDropdown(l10n, isArabic),
            const SizedBox(height: SimfTokens.space4),
          ],
          // The subject + date + time + send appear once a speaker is set (always
          // on the profile flow; after a pick on the bilateral flow).
          if (_selectedSpeakerId != null) ...<Widget>[
            _label(l10n.meetingSubjectLabel), // الموضوع
            const SizedBox(height: SimfTokens.space2),
            _subjectField(l10n),
            const SizedBox(height: SimfTokens.space4),
            _label(l10n.meetingChooseDateLabel), // اختر التاريخ
            const SizedBox(height: SimfTokens.space2),
            _dayCards(isArabic),
            const SizedBox(height: SimfTokens.space4),
            _label(l10n.meetingChooseTimeLabel), // اختر الوقت
            const SizedBox(height: SimfTokens.space2),
            _timeChips(isArabic),
            const SizedBox(height: SimfTokens.space5),
            _sendButton(l10n),
          ],
        ],
      ),
    );
  }

  /// A form field label — navy, 12px, at the inline start (right, RTL).
  Widget _label(String text) => Align(
        alignment: AlignmentDirectional.centerStart,
        child: Text(
          text,
          style: const TextStyle(
            color: SimfTokens.navy,
            fontSize: SimfTokens.textSm, // 12
            fontWeight: FontWeight.w500,
          ),
        ),
      );

  /// The subject input — a white, beige-bordered field with the "اكتب الموضوع"
  /// hint (Figma 1776:5048).
  Widget _subjectField(AppL10n l10n) => TextField(
        controller: _subject,
        textAlign: TextAlign.start,
        maxLength: 1000,
        maxLines: 1,
        style: const TextStyle(
          color: SimfTokens.inputInk,
          fontSize: SimfTokens.textMd,
        ),
        decoration: InputDecoration(
          counterText: '',
          hintText: l10n.meetingSubjectHint, // اكتب الموضوع
          hintStyle: const TextStyle(
            color: SimfTokens.greyText,
            fontSize: SimfTokens.textMd,
          ),
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

  /// The bilateral speaker picker — a beige-bordered dropdown of the speakers.
  /// Shown only when [MeetingRequestSheet.speakerId] is null.
  Widget _speakerDropdown(AppL10n l10n, bool isArabic) {
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
    return DropdownButtonFormField<String>(
      initialValue: _selectedSpeakerId,
      isExpanded: true,
      icon: const Icon(Icons.expand_more, color: SimfTokens.greyText),
      dropdownColor: SimfTokens.surface,
      style: const TextStyle(
        color: SimfTokens.inputInk,
        fontSize: SimfTokens.textMd,
      ),
      hint: Text(
        l10n.meetingSelectSpeakerHint, // اختر المتحدث…
        style: const TextStyle(
          color: SimfTokens.greyText,
          fontSize: SimfTokens.textMd,
        ),
      ),
      decoration: const InputDecoration(
        filled: true,
        fillColor: SimfTokens.surface,
        contentPadding: EdgeInsets.symmetric(
          horizontal: SimfTokens.space4,
          vertical: SimfTokens.space2,
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: SimfTokens.borderRadiusSmall,
          borderSide: BorderSide(color: SimfTokens.beigeBorder),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: SimfTokens.borderRadiusSmall,
          borderSide: BorderSide(color: SimfTokens.accent),
        ),
        border: OutlineInputBorder(
          borderRadius: SimfTokens.borderRadiusSmall,
        ),
      ),
      items: <DropdownMenuItem<String>>[
        for (final s in _speakers)
          DropdownMenuItem<String>(
            value: s.id,
            child: Text(
              s.localizedName(isArabic),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
      ],
      onChanged: _submitting ? null : _onSpeakerSelected,
    );
  }

  /// The horizontal row of the next seven day cards (Figma 1776:5052).
  Widget _dayCards(bool isArabic) => SizedBox(
        height: 64,
        child: ListView.separated(
          scrollDirection: Axis.horizontal,
          itemCount: _days.length,
          separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space2),
          itemBuilder: (context, i) {
            final day = _days[i];
            return MeetingDayCard(
              key: ValueKey<String>('meeting-day-$i'),
              weekday: gregorianWeekdayName(day, isArabic),
              dayNumber: day.day,
              month: gregorianMonthName(day.month, isArabic),
              selected: _selectedDay == day,
              onTap: () => setState(() => _selectedDay = day),
            );
          },
        ),
      );

  /// The standard meeting times as tappable chips (Figma 1776:5076).
  Widget _timeChips(bool isArabic) => Align(
        alignment: AlignmentDirectional.centerStart,
        child: Wrap(
          spacing: SimfTokens.space2,
          runSpacing: SimfTokens.space2,
          children: <Widget>[
            for (var i = 0; i < _timeOptions.length; i++)
              MeetingTimeChip(
                key: ValueKey<String>('meeting-time-$i'),
                label: _formatTime(_timeOptions[i], isArabic),
                selected: _selectedTime == _timeOptions[i],
                onTap: () => setState(() => _selectedTime = _timeOptions[i]),
              ),
          ],
        ),
      );

  /// The full-width gold "ارسال الطلب" button (Figma 1776:5083).
  Widget _sendButton(AppL10n l10n) => Material(
        color: SimfTokens.accent,
        borderRadius: SimfTokens.borderRadiusSmall,
        child: InkWell(
          onTap: _submitting ? null : () => unawaited(_submit()),
          borderRadius: SimfTokens.borderRadiusSmall,
          child: Container(
            height: 48,
            alignment: Alignment.center,
            child: Text(
              _submitting ? l10n.loadingLabel : l10n.meetingSendButton,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textLg, // 16
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ),
      );
}
