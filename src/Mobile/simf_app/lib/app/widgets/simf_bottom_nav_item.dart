import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

/// One destination: the exact iconify glyph (inactive `#5E584B`, active gold),
/// plus the gold label **below it only when active** (the KSA nav shows a
/// single label under the current tab; frame 758:1476).
class SimfBottomNavItem extends StatelessWidget {
  const SimfBottomNavItem({
    required this.tab,
    required this.current,
    required this.iconAsset,
    required this.label,
    required this.onTap,
    super.key,
  });

  final SimfTab tab;
  final SimfTab? current;
  final String iconAsset;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final active = tab == current;
    final color = active ? SimfTokens.goldSoft : SimfTokens.navInactive;
    return Expanded(
      child: Semantics(
        button: true,
        selected: active,
        label: label,
        child: InkWell(
          onTap: active ? null : onTap,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              SimfSvgIcon(iconAsset,
                  size: SimfTokens.simfBottomNavItemSize, color: color,),
              if (active) ...<Widget>[
                const SizedBox(height: SimfTokens.space1),
                Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: SimfTokens.goldSoft,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
