import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

/// One horizontal share-action pill (frame 758:1283): the gold iconify glyph
/// beside its label on the shared card chrome, fixed to the frame's 48-px row.
class MyAreaShareTile extends StatelessWidget {
  const MyAreaShareTile({
    required this.label,
    required this.iconAsset,
    required this.onTap,
    super.key,
  });

  final String label;
  final String iconAsset;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      onTap: onTap,
      child: SizedBox(
        height: SimfTokens.myAreaRowsHeight,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space3),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              SimfSvgIcon(iconAsset,
                  size: SimfTokens.myAreaRowsSize, color: SimfTokens.accent,),
              const SizedBox(width: SimfTokens.space2),
              Flexible(
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: SimfTokens.labelWhiteSemiboldSm,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
