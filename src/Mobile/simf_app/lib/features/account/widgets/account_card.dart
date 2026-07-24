import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The auth screens' content card: the beige rounded panel holding the sign-in
/// / sign-up form. Shared so the padding / colour / radius live in one place
/// instead of each screen rebuilding the `Container` decoration.
class AccountCard extends StatelessWidget {
  const AccountCard({required this.child, super.key});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space6),
      decoration: const BoxDecoration(
        color: SimfTokens.cardBeige,
        borderRadius: SimfTokens.borderRadiusSmall,
      ),
      child: child,
    );
  }
}
