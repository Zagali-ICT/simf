import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The registration card's head — title then the visitor glyph, laid out by
/// Directionality (19b) rather than a hardcoded direction + TextAlign.end.
class StaffRegisterCardHeader extends StatelessWidget {
  const StaffRegisterCardHeader({super.key});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            AppL10n.of(context).staffRegisterVisitorTitle,
            style: const TextStyle(
              fontSize: SimfTokens.text24,
              fontWeight: FontWeight.w600,
              color: SimfTokens.headlineInk,
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Container(
          width: SimfTokens.controlHeight,
          height: SimfTokens.controlHeight,
          decoration: const BoxDecoration(
            color: SimfTokens.navyDeep,
            borderRadius: SimfTokens.borderRadiusSmall,
          ),
          child: const Icon(
            Icons.person_outline,
            color: SimfTokens.accent,
            size: SimfTokens.metaIconBox,
          ),
        ),
      ],
    );
  }
}
