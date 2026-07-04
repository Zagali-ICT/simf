import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

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
          Expanded(child: _ContactBox(text: email!, icon: Icons.mail_outline)),
        if (hasEmail && hasPhone) const SizedBox(width: SimfTokens.space4),
        if (hasPhone)
          Expanded(child: _ContactBox(text: phone!, icon: Icons.call_outlined)),
      ],
    );
  }
}

class _ContactBox extends StatelessWidget {
  const _ContactBox({required this.text, required this.icon});

  final String text;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 44,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              textDirection: TextDirection.ltr,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textXs,
              ),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Icon(icon, size: 16, color: SimfTokens.beigeBorder),
        ],
      ),
    );
  }
}
