import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../app/widgets/simf_svg_icon.dart';
import '../data/speaker_models.dart';

/// A small caps section heading (beige, 10px Bold, tracked) — used for the
/// speaker's "sessions" list header.
class SpeakerSectionHeading extends StatelessWidget {
  const SpeakerSectionHeading(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: SimfTokens.beigeBorder,
        fontWeight: FontWeight.w700,
        fontSize: SimfTokens.textXs,
        letterSpacing: 0.8,
      ),
    );
  }
}

/// One of the speaker's sessions — kept on the navy card chrome (the frame's
/// minimal content stops at the bio, but the screen's behaviour does not).
/// Tapping opens the session detail (#17).
class SpeakerSessionRow extends StatelessWidget {
  const SpeakerSessionRow({
    required this.session,
    required this.isArabic,
    super.key,
  });

  final SpeakerSession session;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final hall = session.localizedHall(isArabic);
    return SimfCard(
      onTap: () => context.pushNamed(
        RouteNames.sessionDetail,
        pathParameters: <String, String>{'sessionId': session.id},
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Row(
          children: <Widget>[
            const Icon(Icons.event_note_outlined, color: SimfTokens.accent),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    session.localizedTitle(isArabic),
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                  if (hall != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space1),
                    Text(
                      hall,
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textXs,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            const SimfSvgIcon(
              'assets/icons/ic_caret_left.svg',
              color: SimfTokens.txtTertiary,
              size: 18,
            ),
          ],
        ),
      ),
    );
  }
}
