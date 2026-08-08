import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_radio_pill.dart';
import 'package:simf_app/features/account/data/profile_models.dart';

/// Gender as the design's two radio pills (Figma 522:2150). The screen owns the
/// [gender] value; [onChanged] reports the pick.
class GenderPillsField extends StatelessWidget {
  const GenderPillsField({
    required this.gender,
    required this.onChanged,
    super.key,
  });

  final AppGender gender;
  final ValueChanged<AppGender> onChanged;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // D-698 (owner) — the gold ring leads and the label follows; forcing RTL
    // keeps the ring on the right (leading edge) in both Arabic and English.
    return Row(
      children: <Widget>[
        Expanded(
          child: SimfRadioPill(
            label: l10n.genderMale,
            selected: gender == AppGender.male,
            onTap: () => onChanged(AppGender.male),
            textDirection: TextDirection.rtl,
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: SimfRadioPill(
            label: l10n.genderFemale,
            selected: gender == AppGender.female,
            onTap: () => onChanged(AppGender.female),
            textDirection: TextDirection.rtl,
          ),
        ),
      ],
    );
  }
}
