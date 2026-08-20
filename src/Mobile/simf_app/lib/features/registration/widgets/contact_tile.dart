import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// One bordered contact tile (Figma 522:2223). Inert (null [onTap]) until its
/// BuildConfig contact value is supplied (D-369).
class ContactTile extends StatelessWidget {
  const ContactTile({required this.icon, super.key, this.onTap});

  final IconData icon;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius10),
      child: Container(
        height: SimfTokens.contactTileHeight,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          border: Border.all(
              color: SimfTokens.tileBorderNavy,
              width: SimfTokens.contactTileBorderWidth,),
          borderRadius: BorderRadius.circular(SimfTokens.radius10),
        ),
        child: Icon(icon,
            color: SimfTokens.surface, size: SimfTokens.contactTileSize,),
      ),
    );
  }
}
