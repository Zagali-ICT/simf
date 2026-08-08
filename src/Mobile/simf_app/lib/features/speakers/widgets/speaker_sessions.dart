import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/route_names.dart';
import '../../../app/theme/app_assets.dart';
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
      style: SimfTokens.labelBeigeBoldXsTracked,
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
        pathParameters: <String, String>{RouteParams.sessionId: session.id},
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
                    style: SimfTokens.labelWhiteSemiboldSm,
                  ),
                  if (hall != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space1),
                    Text(
                      hall,
                      style: SimfTokens.bodyBeigeXs,
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            const SimfSvgIcon(
              AppAssets.icCaretLeft,
              color: SimfTokens.txtTertiary,
              size: SimfTokens.speakerSessionsSize,
            ),
          ],
        ),
      ),
    );
  }
}
