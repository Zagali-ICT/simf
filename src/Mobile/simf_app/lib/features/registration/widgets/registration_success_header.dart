import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The success-frame header band (Figma 505:1456): a back chevron pinned to the
/// inline-start with a centred title. Kept as a local header (not `SimfPageShell`)
/// because the frame's title is 24/w500 and the screen carries a decorative
/// sweep behind it — neither is a shell hook.
class RegistrationSuccessHeader extends StatelessWidget {
  const RegistrationSuccessHeader({
    required this.title,
    required this.onBack,
    super.key,
  });

  final String title;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 56,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          Align(
            alignment: Alignment.centerLeft,
            child: Padding(
              padding: const EdgeInsets.only(left: 8),
              child: IconButton(
                onPressed: onBack,
                icon: const Icon(
                  Icons.arrow_back_ios_new,
                  color: Colors.white,
                  size: 20,
                  textDirection: TextDirection.ltr,
                ),
              ),
            ),
          ),
          Text(
            title,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 24,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }
}
