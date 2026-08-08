import 'dart:typed_data';

import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The card-head avatar: the captured face photo once taken (owner follow-up —
/// "show at top by replacing the icon with the real profile photo"), else the
/// placeholder person icon.
class SignUpVisitorHeaderAvatar extends StatelessWidget {
  const SignUpVisitorHeaderAvatar({required this.bytes, super.key});

  final Uint8List? bytes;

  @override
  Widget build(BuildContext context) {
    final data = bytes;
    if (data == null) {
      // The frame (168:2972) shows a navy-deep rounded box with a gold person
      // glyph, not a bare dark icon on the beige card (D-674).
      return Container(
        width: SimfTokens.space10,
        height: SimfTokens.space10,
        alignment: Alignment.center,
        decoration: const BoxDecoration(
          color: SimfTokens.navyDeep,
          borderRadius: SimfTokens.borderRadiusSmall,
        ),
        child: const Icon(
          Icons.account_circle_outlined,
          size: SimfTokens.signUpVisitorHeaderAvatarSize,
          color: SimfTokens.accent,
        ),
      );
    }
    return ClipOval(
      child: Image.memory(data, width: SimfTokens.space10, height: SimfTokens.space10, fit: BoxFit.cover),
    );
  }
}
