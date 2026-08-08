import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The "we sent a code to <address>" block above the OTP boxes: the muted
/// sentence over the recipient on a gold line, always LTR so an email or phone
/// number reads correctly under RTL.
///
/// Shared by every OTP screen (sign-up email verify, sign-in email OTP, change
/// email) — each had spelled the same two Texts out itself.
class OtpSentTo extends StatelessWidget {
  const OtpSentTo({
    required this.prefix,
    required this.recipient,
    this.fallback,
    super.key,
  });

  /// The muted lead-in ("أرسلنا رمزاً إلى").
  final String prefix;

  /// The address the code went to. When blank, [fallback] renders instead.
  final String? recipient;

  /// Shown when no address is carried — the generic sentence.
  final String? fallback;

  @override
  Widget build(BuildContext context) {
    final to = recipient?.trim() ?? '';
    if (to.isEmpty) {
      final body = fallback;
      if (body == null) {
        return const SizedBox.shrink();
      }
      return Text(
        body,
        textAlign: TextAlign.center,
        style: SimfTokens.bodyBeigeMd,
      );
    }
    return Column(
      children: <Widget>[
        Text(
          prefix,
          textAlign: TextAlign.center,
          style: SimfTokens.bodyBeigeMd,
        ),
        const SizedBox(height: SimfTokens.space2),
        Text(
          to,
          textAlign: TextAlign.center,
          textDirection: TextDirection.ltr,
          style: SimfTokens.labelGoldMedium,
        ),
      ],
    );
  }
}
