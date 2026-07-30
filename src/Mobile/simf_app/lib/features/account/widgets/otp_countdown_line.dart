import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The resend countdown under the OTP boxes (frame 758:2616): the muted prefix
/// followed by the remaining time in gold bold, as one rich line so the two
/// styles share a baseline.
///
/// Shared by the OTP screens, which each hand-rolled the same `Text.rich` — and
/// with it the same inline gold-bold `TextStyle`.
class OtpCountdownLine extends StatelessWidget {
  const OtpCountdownLine({
    required this.prefix,
    required this.remaining,
    super.key,
  });

  /// The muted lead-in ("إعادة الإرسال خلال").
  final String prefix;

  /// The formatted time left ("00:59").
  final String remaining;

  @override
  Widget build(BuildContext context) {
    return Text.rich(
      TextSpan(
        children: <InlineSpan>[
          TextSpan(text: '$prefix '),
          TextSpan(text: remaining, style: SimfTokens.labelGoldBoldInherit),
        ],
      ),
      style: SimfTokens.bodyBeigeMd,
    );
  }
}
