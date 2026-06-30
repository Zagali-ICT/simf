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
      return const Icon(
        Icons.account_circle_outlined,
        size: 40,
        color: SimfTokens.headlineInk,
      );
    }
    return ClipOval(
      child: Image.memory(data, width: 40, height: 40, fit: BoxFit.cover),
    );
  }
}
