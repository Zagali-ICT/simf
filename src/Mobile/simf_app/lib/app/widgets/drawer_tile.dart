import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// One drawer row in the navy KSA styling: a white leading icon over a white
/// title (owner 2026-07-07: main-menu nav icons are white, not gold).
class DrawerTile extends StatelessWidget {
  const DrawerTile({required this.icon, required this.title, super.key,
    this.onTap,
  });

  final IconData icon;
  final String title;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    // No inline style on the title (#16): the app theme's `listTileTheme`
    // already sets `textColor: SimfTokens.surface`, so the colour-only override
    // that used to sit here was a duplicate of the token it re-stated.
    return ListTile(
      leading: Icon(icon, color: SimfTokens.surface),
      title: Text(title),
      onTap: onTap,
    );
  }
}
