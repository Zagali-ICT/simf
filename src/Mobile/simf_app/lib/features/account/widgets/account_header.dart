import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_logo.dart';

/// The auth screens' brand header: the [SimfLogo] beside the forum name,
/// centred (the logo at the inline start — the right under RTL), matching the
/// Figma auth frames' logo + title row.
class AccountHeader extends StatelessWidget {
  const AccountHeader({required this.title, super.key});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        const SimfLogo(size: SimfTokens.accountHeaderSize),
        const SizedBox(width: SimfTokens.space4),
        Flexible(
          child: Text(
            title,
            style: const TextStyle(
              fontSize: SimfTokens.text24,
              fontWeight: FontWeight.w500,
              color: SimfTokens.surface,
            ),
          ),
        ),
      ],
    );
  }
}
