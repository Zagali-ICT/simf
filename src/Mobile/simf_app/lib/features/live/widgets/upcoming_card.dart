import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/live/data/live_models.dart';
import 'package:simf_app/features/live/widgets/time_chip.dart';

/// One "الجلسات القادمة" card (frame 934:3621/3630): the session title with a
/// gold HH:mm time chip at the inline-end.
class UpcomingCard extends StatelessWidget {
  const UpcomingCard(
      {required this.session, required this.isArabic, super.key,});

  final UpcomingSession session;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      child: Padding(
        // Frame 934:3621 — px8 / py16 on the radius-4 navy card.
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space2,
          vertical: SimfTokens.space4,
        ),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Text(
                session.localizedTitle(isArabic: isArabic),
                textAlign: TextAlign.start,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                // Frame 934:3626 — the upcoming-session title is 14px Bold.
                style: SimfTokens.labelWhiteBoldMd,
              ),
            ),
            const SizedBox(width: SimfTokens.space3),
            TimeChip(time: session.start),
          ],
        ),
      ),
    );
  }
}
