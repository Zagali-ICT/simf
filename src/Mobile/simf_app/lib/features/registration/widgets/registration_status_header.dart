import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// Back chevron (inline start) + centred title (Figma 1701:3794/3796). A plain
/// white chevron — the gate frame does not use the standard circled back box.
class RegistrationStatusHeader extends StatelessWidget {
  const RegistrationStatusHeader({
    required this.title,
    required this.onBack,
    super.key,
  });

  final String title;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    // LTR so the chevron sits at the physical left and the title stays centred,
    // matching the frame under Arabic/RTL.
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space2,
        ),
        child: Row(
          children: <Widget>[
            IconButton(
              onPressed: onBack,
              icon: const Icon(
                Icons.arrow_back_ios_new,
                color: Colors.white,
                size: 20,
              ),
              splashRadius: 22,
            ),
            Expanded(
              child: Text(
                title,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontSize: SimfTokens.textTitle,
                  fontWeight: FontWeight.w600,
                  color: Colors.white,
                ),
              ),
            ),
            const SizedBox(width: 48), // balances the back button; keeps title centred
          ],
        ),
      ),
    );
  }
}
