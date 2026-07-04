import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// Shared navy-deep card chrome for the contact sections (Figma card 1388:7774):
/// full-width, px-16 / py-8, 8-radius navy-deep fill.
class ContactCard extends StatelessWidget {
  const ContactCard({required this.child, super.key});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: child,
    );
  }
}

/// A contact-card heading (Figma 1388:7775): white 16px Medium, inline-start.
class ContactCardHeading extends StatelessWidget {
  const ContactCardHeading(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      textAlign: TextAlign.start,
      style: const TextStyle(
        color: Colors.white,
        fontSize: SimfTokens.textLg,
        fontWeight: FontWeight.w500,
      ),
    );
  }
}
