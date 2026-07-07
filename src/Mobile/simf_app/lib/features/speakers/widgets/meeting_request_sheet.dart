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
/// light "طلب مقابلة" sheet (Figma 1776:4958): gold handle, subject field, a
/// row of day cards, then that day's time-slot chips, and a gold send button.
///
/// Two entry points share this one sheet:
/// - from a **speaker profile** — [speakerId] is set, the speaker is fixed and
///   no picker is shown (the original flow);
/// - from the **bilateral-meeting** tile (owner: VIP "اللقاءات الثنائية") —
///   [speakerId] is **null**, so the sheet shows a speaker **dropdown** to pick
///   one, then the same subject + slot form. The request itself is VIP-gated by
///   the server (a 403 surfaces the "VIP only" message) either way.
class MeetingRequestSheet extends ConsumerStatefulWidget {
  const MeetingRequestSheet({
    required this.speakerId,
    required this.defaultName,
    required this.l10n,
    super.key,
  });

  /// The speaker to meet, or **null** for the bilateral entry (show the picker).
  final String? speakerId;
  final String defaultName;
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
  // D-474/D-475 (#11) — the VIP availability-slot picker (optional: a picked slot
  // is the VIP flow; none keeps the legacy topic-only request).
  List<SpeakerSlot> _slots = const <SpeakerSlot>[];
  SpeakerSlot? _selectedSlot;
  // The picked calendar day (Figma 1701:7479 — date+time selection): narrows the
  // available slots to that day's times before one is chosen.
  DateTime? _selectedDate;
  bool _slotsLoaded = false;

  @override
  void initState() {
    super.initState();
    _selectedSpeakerId = widget.speakerId;
    if (widget.speakerId != null) {
      unawaited(_loadSlots());
    } else {
      // Bilateral entry — load the speaker list for the picker.
      unawaited(_loadSpeakers());
    }
  }

  Future<void> _loadSlots() async {
    final speakerId = _selectedSpeakerId;
    if (speakerId == null) {
      return;
    }
    try {
      final slots = await ref
          .read(speakersRepositoryProvider)
          .getAvailableSlots(speakerId);
      if (!mounted) {
        return;
      }
      setState(() {
        _slots = slots;
        _slotsLoaded = true;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      // No slots shown; the legacy topic-only request still works.
      setState(() => _slotsLoaded = true);
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

  /// Picking a speaker in the bilateral flow: switch target + reload that
  /// speaker's free slots (resetting any previous day/time pick).
  void _onSpeakerSelected(String? speakerId) {
    if (speakerId == null || speakerId == _selectedSpeakerId) {
      return;
    }
    setState(() {
      _selectedSpeakerId = speakerId;
      _slots = const <SpeakerSlot>[];
      _selectedDate = null;
      _selectedSlot = null;
      _slotsLoaded = false;
    });
    unawaited(_loadSlots());
  }

  @override
  void dispose() {
    _subject.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final l10n = widget.l10n;
    // Owner: "no need for name" — the requester is the signed-in account, so we
    // submit its display name as the requesterName the backend contract still
    // requires, instead of asking the user to type it.
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
    setState(() => _submitting = true);
    final navigator = Navigator.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(speakersRepositoryProvider).submitMeetingRequest(
            speakerId,
            requesterName: name,
            subject: subject,
            slotStartUtc: _selectedSlot?.startUtc,
            slotEndUtc: _selectedSlot?.endUtc,
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

  // The slot's local calendar day — the key that groups the available slots into
  // the date picker (the time picker then lists that day's slots).
  DateTime _dayOf(SpeakerSlot slot) {
    final s = slot.startUtc.toLocal();
    return DateTime(s.year, s.month, s.day);
  }

  // The distinct days that carry at least one free slot, ascending.
  List<DateTime> get _availableDays {
    final days = <DateTime>{for (final slot in _slots) _dayOf(slot)}.toList()
      ..sort();
    return days;
  }

  // The free slots on the selected day, ascending by start time.
  List<SpeakerSlot> get _slotsForSelectedDay {
    final day = _selectedDate;
    if (day == null) {
      return const <SpeakerSlot>[];
    }
    return _slots.where((s) => _dayOf(s) == day).toList()
      ..sort((a, b) => a.startUtc.compareTo(b.startUtc));
  }

  // The slot's local start time as "10:00 ص" / "02:30 PM" (12-hour, Arabic ص/م,
  // no intl locale needed — Figma 1776:5036).
  String _formatSlotTime(SpeakerSlot slot, bool isArabic) {
    final t = slot.startUtc.toLocal();
    final hour12 = t.hour % 12 == 0 ? 12 : t.hour % 12;
    final hh = hour12.toString().padLeft(2, '0');
    final mm = t.minute.toString().padLeft(2, '0');
    final meridiem =
        isArabic ? (t.hour >= 12 ? 'م' : 'ص') : (t.hour >= 12 ? 'PM' : 'AM');
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
          // The subject + slots + send appear once a speaker is set (always on
          // the profile flow; after a pick on the bilateral flow).
          if (_selectedSpeakerId != null) ...<Widget>[
            _label(l10n.meetingSubjectLabel), // الموضوع
            const SizedBox(height: SimfTokens.space2),
            _subjectField(l10n),
            const SizedBox(height: SimfTokens.space4),
            // The slot picker is sourced from the speaker's free slots so the
            // chosen slot matches a free one the server accepts (VIP-only;
            // optional — no pick keeps the legacy topic-only request).
            if (_slotsLoaded && _slots.isNotEmpty) ...<Widget>[
              _label(l10n.meetingChooseDateLabel), // اختر التاريخ
              const SizedBox(height: SimfTokens.space2),
              _dayCards(isArabic),
              const SizedBox(height: SimfTokens.space4),
              _label(l10n.meetingChooseTimeLabel), // اختر الوقت
              const SizedBox(height: SimfTokens.space2),
              if (_selectedDate == null)
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: Text(
                    l10n.meetingChooseDateFirst,
                    style: const TextStyle(
                      color: SimfTokens.greyText,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                )
              else
                _timeChips(isArabic),
              const SizedBox(height: SimfTokens.space5),
            ] else if (_slotsLoaded) ...<Widget>[
              Text(
                l10n.meetingSlotNone,
                style: const TextStyle(color: SimfTokens.greyText),
              ),
              const SizedBox(height: SimfTokens.space5),
            ],
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
  /// hint (Figma 1776:4967).
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

  /// The bilateral speaker picker — a beige-bordered dropdown of the speakers
  /// (Figma 1776:5035 flow). Shown only when [MeetingRequestSheet.speakerId] is
  /// null; picking one loads that speaker's slots.
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

  /// The horizontal row of day cards, one per day that carries a free slot.
  Widget _dayCards(bool isArabic) => SizedBox(
        height: 64,
        child: ListView.separated(
          scrollDirection: Axis.horizontal,
          itemCount: _availableDays.length,
          separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space2),
          itemBuilder: (context, i) {
            final day = _availableDays[i];
            return MeetingDayCard(
              key: ValueKey<String>('meeting-day-$i'),
              weekday: gregorianWeekdayName(day, isArabic),
              dayNumber: day.day,
              month: gregorianMonthName(day.month, isArabic),
              selected: _selectedDate == day,
              onTap: () => setState(() {
                _selectedDate = day;
                _selectedSlot = null; // reset the time when the day changes
              }),
            );
          },
        ),
      );

  /// The selected day's free slots as tappable time chips.
  Widget _timeChips(bool isArabic) {
    final slots = _slotsForSelectedDay;
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Wrap(
        spacing: SimfTokens.space2,
        runSpacing: SimfTokens.space2,
        children: <Widget>[
          for (var i = 0; i < slots.length; i++)
            MeetingTimeChip(
              key: ValueKey<String>('meeting-slot-$i'),
              label: _formatSlotTime(slots[i], isArabic),
              // Compare by start time (SpeakerSlot has no value equality) so the
              // highlight survives any future re-fetch of the slot list.
              selected: _selectedSlot?.startUtc == slots[i].startUtc,
              onTap: () => setState(() => _selectedSlot = slots[i]),
            ),
        ],
      ),
    );
  }

  /// The full-width gold "ارسال الطلب" button (Figma 1776:5001).
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
