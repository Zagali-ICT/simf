import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_radio_pill.dart';

/// B7 — the "إلى من؟" recipient choice (D-174: the mockup's two pills,
/// المتحدث / المضيف). The wire field and the moderator + committee queues have
/// carried `Recipient` since D-169, but the screen hardcoded Speaker, so every
/// question ever submitted read `recipient = 0` and the Host half of the enum
/// — plus these three strings — was dead. Built from the shared
/// [SimfRadioPill], not a page-local pill.
class SendQuestionRecipientPicker extends StatelessWidget {
  const SendQuestionRecipientPicker({
    required this.label,
    required this.speakerLabel,
    required this.hostLabel,
    required this.hostSelected,
    required this.onSpeaker,
    required this.onHost,
    super.key,
  });

  final String label;
  final String speakerLabel;
  final String hostLabel;

  /// True when the Host pill is the current choice; false = Speaker (the
  /// default, preserving the shipped behaviour for a user who never taps).
  final bool hostSelected;
  final VoidCallback onSpeaker;
  final VoidCallback onHost;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // TextAlign.start = right under RTL, matching the composer's label.
        Text(
          label,
          textAlign: TextAlign.start,
          style: SimfTokens.labelWhiteMediumLg,
        ),
        const SizedBox(height: SimfTokens.space2),
        Row(
          children: <Widget>[
            Expanded(
              child: SimfRadioPill(
                label: speakerLabel,
                selected: !hostSelected,
                onTap: onSpeaker,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: SimfRadioPill(
                label: hostLabel,
                selected: hostSelected,
                onTap: onHost,
              ),
            ),
          ],
        ),
      ],
    );
  }
}
