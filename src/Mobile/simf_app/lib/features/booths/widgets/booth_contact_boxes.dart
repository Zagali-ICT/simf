import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import 'booth_contact_box.dart';

/// The booth-officer contact boxes (frame 922:2810): email + phone in two
/// bordered navy boxes side by side, each with a trailing glyph. D-432 — only
/// the boxes the wire actually carries are shown.
class BoothContactBoxes extends StatelessWidget {
  const BoothContactBoxes({this.email, this.phone, super.key});

  final String? email;
  final String? phone;

  @override
  Widget build(BuildContext context) {
    final hasEmail = (email ?? '').isNotEmpty;
    final hasPhone = (phone ?? '').isNotEmpty;
    return Row(
      children: <Widget>[
        if (hasEmail)
          Expanded(
            child: BoothContactBox(text: email!, icon: Icons.mail_outline),
          ),
        if (hasEmail && hasPhone) const SizedBox(width: SimfTokens.space4),
        if (hasPhone)
          Expanded(
            child: BoothContactBox(text: phone!, icon: Icons.call_outlined),
          ),
      ],
    );
  }
}
