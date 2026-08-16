import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

/// One feed-toggle pill — the gold/navy view-pill language shared with the
/// sessions screen: active = solid gold, inactive = bordered navy card.
class TogglePill extends StatelessWidget {
  const TogglePill({
    required this.label,
    required this.active,
    required this.onTap,
    super.key,
    this.icon,
  });

  final String label;
  final bool active;
  final VoidCallback onTap;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      onTap: active ? null : onTap,
      color: active ? SimfTokens.accent : SimfTokens.navyDeep,
      borderColor: active ? SimfTokens.accent : SimfTokens.beigeBorder,
      child: SizedBox(
        height: SimfTokens.tapTarget,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            if (icon != null) ...<Widget>[
              Icon(icon,
                  size: SimfTokens.togglePillSize, color: SimfTokens.surface,),
              const SizedBox(width: SimfTokens.space1),
            ],
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
    );
  }
}
