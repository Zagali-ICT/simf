import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/utils/gregorian_month_names.dart';
import 'package:simf_app/core/utils/local_days.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/core/utils/weekday_names.dart';
import 'package:simf_app/features/speakers/widgets/meeting_sheet_fields.dart';
import 'package:simf_app/features/speakers/widgets/meeting_slot_pickers.dart';

/// One free slot the meeting target offers, as the sheet needs it. Both
/// `SpeakerSlot` and `DelegationSlot` map onto this so the one sheet can render
/// either; neither carries anything else the form uses.
@immutable
class MeetingSlot {
  const MeetingSlot({required this.start, required this.end});

  final DateTime start;
  final DateTime end;
}

/// The sheet's date + time section: a spinner while loading, a load-error +
/// Retry when the fetch failed (G3), a "no slots" hint when the target really
/// has no free slot, else the day cards (Figma 1776:5052) + the selected day's
/// time chips (1776:5076).
class MeetingSlotSection extends StatelessWidget {
  const MeetingSlotSection({
    required this.slots,
    required this.loading,
    required this.loadFailed,
    required this.selectedDay,
    required this.selectedSlot,
    required this.isArabic,
    required this.l10n,
    required this.keyPrefix,
    required this.onRetry,
    required this.onDaySelected,
    required this.onSlotSelected,
    super.key,
  });

  final List<MeetingSlot> slots;
  final bool loading;

  /// G3 — the slot fetch FAILED (network / server), which is NOT the same as
  /// the target having no availability. Kept apart so the sheet can offer a
  /// retry instead of telling the user something untrue and locking the send
  /// button.
  final bool loadFailed;
  final DateTime? selectedDay;
  final MeetingSlot? selectedSlot;
  final bool isArabic;
  final AppL10n l10n;

  /// `meeting` or `delegation` — the widget-key namespace the E2E catalogue and
  /// the sheet tests drive the day / time / retry controls by.
  final String keyPrefix;
  final VoidCallback onRetry;
  final ValueChanged<DateTime> onDaySelected;
  final ValueChanged<MeetingSlot> onSlotSelected;

  /// The distinct local days that carry at least one slot. The endpoint
  /// derives slots chronologically, so the shared helper's ascending order is
  /// the endpoint's order; it is also correct if that ever stops holding.
  List<DateTime> get _daysWithSlots =>
      distinctLocalDays(slots, (slot) => saudiOf(slot.start));

  /// The slots on a given local day, in the endpoint's (chronological) order.
  List<MeetingSlot> _slotsForDay(DateTime day) => <MeetingSlot>[
        for (final slot in slots)
          if (sameLocalDay(saudiOf(slot.start), day)) slot,
      ];

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return const MeetingSheetSpinner();
    }
    if (loadFailed) {
      return MeetingSlotsRetry(
        retryKey: ValueKey<String>('$keyPrefix-slots-retry'),
        l10n: l10n,
        onRetry: onRetry,
      );
    }
    if (slots.isEmpty) {
      // لا توجد فترات متاحة
      return MeetingFieldHint(text: l10n.meetingSlotNone);
    }
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        MeetingFieldLabel(text: l10n.meetingChooseDateLabel), // اختر التاريخ
        const SizedBox(height: SimfTokens.space2),
        _dayCards(context),
        const SizedBox(height: SimfTokens.space4),
        MeetingFieldLabel(text: l10n.meetingChooseTimeLabel), // اختر الوقت
        const SizedBox(height: SimfTokens.space2),
        if (selectedDay == null)
          MeetingFieldHint(text: l10n.meetingChooseDateFirst)
        else
          _timeChips(),
      ],
    );
  }

  Widget _dayCards(BuildContext context) {
    final days = _daysWithSlots;
    return SizedBox(
      height: meetingDayCardHeight(context),
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: days.length,
        separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space2),
        itemBuilder: (context, i) {
          final day = days[i];
          return MeetingDayCard(
            key: ValueKey<String>('$keyPrefix-day-$i'),
            weekday: gregorianWeekdayName(day, isArabic: isArabic),
            dayNumber: day.day,
            month: gregorianMonthName(day.month, isArabic: isArabic),
            selected: selectedDay != null && sameLocalDay(selectedDay!, day),
            onTap: () => onDaySelected(day),
          );
        },
      ),
    );
  }

  Widget _timeChips() {
    final daySlots = _slotsForDay(selectedDay!);
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Wrap(
        spacing: SimfTokens.space2,
        runSpacing: SimfTokens.space2,
        children: <Widget>[
          for (var i = 0; i < daySlots.length; i++)
            MeetingTimeChip(
              key: ValueKey<String>('$keyPrefix-time-$i'),
              label: formatDateTime12h(
                saudiOf(daySlots[i].start),
                isArabic: isArabic,
              ),
              selected: selectedSlot == daySlots[i],
              onTap: () => onSlotSelected(daySlots[i]),
            ),
        ],
      ),
    );
  }
}

/// G3 — the slot fetch failed: say so and offer a Retry. Deliberately
/// different copy from [AppL10n.meetingSlotNone] so a network blip is never
/// read as "this speaker/delegation has no availability", which would be a lie
/// the user cannot act on.
class MeetingSlotsRetry extends StatelessWidget {
  const MeetingSlotsRetry({
    required this.retryKey,
    required this.l10n,
    required this.onRetry,
    super.key,
  });

  final Key retryKey;
  final AppL10n l10n;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          MeetingFieldHint(text: l10n.lookupLoadError), // تعذر تحميل القائمة.
          Align(
            alignment: AlignmentDirectional.centerStart,
            child: TextButton(
              key: retryKey,
              onPressed: onRetry,
              child: Text(l10n.retryLabel, style: SimfTokens.labelNavyMediumSm),
            ),
          ),
        ],
      );
}
