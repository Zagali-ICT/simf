import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';
import '../data/request_models.dart';

/// The top action row (Figma 1408:9736): two equal buttons — "طلب جديد" (opens
/// the new-request sheet, beige-outlined) and "السجل" (the gold-filled "all/log"
/// view; tapping it clears any status filter). Accepted stays filterable via the
/// "مقبول" status chip below (D-592: the app's extra "المقبولة" button dropped to
/// match the frame).
class RequestActionRow extends StatelessWidget {
  const RequestActionRow({
    required this.l10n,
    required this.filter,
    required this.onNew,
    required this.onSelect,
    super.key,
  });

  final AppL10n l10n;
  final AppRequestStatus? filter;
  final VoidCallback onNew;
  final ValueChanged<AppRequestStatus?> onSelect;

  @override
  Widget build(BuildContext context) {
    // The Figma lays this row left→right (طلب جديد · السجل) — a fixed LTR control
    // row in the mock — so force LTR to match it exactly rather than letting the
    // RTL shell mirror it.
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Row(
        children: <Widget>[
          Expanded(
            child: RequestActionButton(
              label: l10n.requestNew,
              asset: 'assets/icons/request_new.svg',
              active: false,
              onTap: onNew,
            ),
          ),
          const SizedBox(width: SimfTokens.space4),
          Expanded(
            child: RequestActionButton(
              label: l10n.requestsTabLog,
              asset: 'assets/icons/request_log.svg',
              active: filter == null,
              onTap: () => onSelect(null),
            ),
          ),
        ],
      ),
    );
  }
}

/// One equal-width pill in the [RequestActionRow]: gold-filled when [active], else
/// a beige-hairline outline. Label SemiBold-12 over a 14px trailing Figma glyph.
class RequestActionButton extends StatelessWidget {
  const RequestActionButton({
    required this.label,
    required this.asset,
    required this.active,
    required this.onTap,
    super.key,
  });

  final String label;
  final String asset;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final Color fg = active ? Colors.white : SimfTokens.beigeBorder;
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        padding: const EdgeInsets.all(SimfTokens.space2),
        decoration: BoxDecoration(
          color: active ? SimfTokens.accent : Colors.transparent,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          border: active
              ? null
              : Border.all(
                  color: SimfTokens.beigeBorder,
                  width: SimfTokens.hairline,
                ),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Flexible(
              child: Text(
                label,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: fg,
                  fontSize: SimfTokens.textSm, // 12
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space1),
            SimfSvgIcon(asset, size: 14, color: fg),
          ],
        ),
      ),
    );
  }
}
