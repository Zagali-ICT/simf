import 'package:flutter/material.dart';
import '../theme/tokens.dart';
import 'simf_logo.dart';

/// The default avatar when a user has no photo (or the photo fails to load):
/// the SIMF brand mark on a navy box. The owner chose the logo over a cultural
/// figure (D-402); the logo art is white, so the box stays dark (navyDeep) for
/// contrast on every surface — the navy scaffold, the my-area card, and the
/// gold badge strip alike.
class AvatarFallback extends StatelessWidget {
  const AvatarFallback({required this.size});

  final double size;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.navyDeep,
      child: Padding(
        padding: EdgeInsets.all(size * 0.18),
        child: SimfLogo(size: size * 0.64),
      ),
    );
  }
}
