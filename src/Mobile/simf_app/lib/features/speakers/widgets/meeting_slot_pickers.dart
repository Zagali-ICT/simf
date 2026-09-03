import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// One white day card in the meeting-request date picker (Figma 1776:4975):
/// weekday over the bold day number over the month; gold-filled when selected.
class MeetingDayCard extends StatelessWidget {
  const MeetingDayCard({
    required this.weekday,
    required this.dayNumber,
    required this.month,
    required this.selected,
    required this.onTap,
    super.key,
  });

  final String weekday;
  final int dayNumber;
  final String month;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final labelColor = selected ? SimfTokens.navy : SimfTokens.greyText;
    return Material(
      color: selected ? SimfTokens.accent : SimfTokens.surface,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Container(
          width: SimfTokens.dayCardWidth,
          height: meetingDayCardHeight(context),
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            border: Border.all(
              color: selected ? SimfTokens.accent : SimfTokens.beigeBorder,
            ),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              FittedBox(
                fit: BoxFit.scaleDown,
                child: Text(
                  weekday,
                  style: TextStyle(
                    color: labelColor,
                    fontSize: SimfTokens.textSm,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ),
              Text(
                '$dayNumber',
                style: TextStyle(
                  color: selected ? SimfTokens.navy : SimfTokens.navyDeep,
                  fontSize: SimfTokens.textTitle, // 18
                  fontWeight: FontWeight.w700,
                ),
              ),
              FittedBox(
                fit: BoxFit.scaleDown,
                child: Text(
                  month,
                  style: TextStyle(
                    color: labelColor,
                    fontSize: SimfTokens.textSm,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// One time-slot chip in the meeting-request time picker (Figma 1776:5036): a
/// white pill with the slot's start time; gold-filled when selected.
class MeetingTimeChip extends StatelessWidget {
  const MeetingTimeChip({
    required this.label,
    required this.selected,
    required this.onTap,
    super.key,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected ? SimfTokens.accent : SimfTokens.surface,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space4,
            vertical: SimfTokens.space2,
          ),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            border: Border.all(
              color: selected ? SimfTokens.accent : SimfTokens.beigeBorder,
            ),
          ),
          child: Text(
            label,
            style: TextStyle(
              color: selected ? SimfTokens.navy : SimfTokens.headlineInk,
              fontSize: SimfTokens.textMd,
              fontWeight: FontWeight.w500,
            ),
          ),
        ),
      ),
    );
  }
}

/// The day card's height, grown with the reader's text scale.
///
/// Three text lines sat in a hard 64pt box: about 60pt at scale 1.0, ~69 at
/// "Large" and ~78 at "Extra large", so it overflowed. The two `FittedBox`es
/// could not save it — a `Column` hands a non-flex child unbounded main-axis
/// constraints, so each reports its natural height and scales nothing.

/// The horizontal list hosting these cards must use the SAME value for its
/// viewport, which is why this is a shared function, not a constant per file.
double meetingDayCardHeight(BuildContext context) =>
    MediaQuery.textScalerOf(context).scale(SimfTokens.dayCardHeight);
