import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../core/widgets/simf_radio_pill.dart';
import '../data/profile_models.dart';

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
    return Row(
      children: <Widget>[
        Expanded(
          child: SimfRadioPill(
            label: l10n.genderMale,
            selected: gender == AppGender.male,
            onTap: () => onChanged(AppGender.male),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: SimfRadioPill(
            label: l10n.genderFemale,
            selected: gender == AppGender.female,
            onTap: () => onChanged(AppGender.female),
          ),
        ),
      ],
    );
  }
}
