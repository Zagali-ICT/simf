import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/widgets/gender_pills_field.dart';

/// Captioned gender pills, as the walk-in form lays them out.
class StaffGenderField extends StatelessWidget {
  const StaffGenderField({
    required this.gender,
    required this.onChanged,
    super.key,
  });

  final AppGender gender;
  final ValueChanged<AppGender> onChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(AppL10n.of(context).genderLabel),
        const SizedBox(height: SimfTokens.space2),
        GenderPillsField(gender: gender, onChanged: onChanged),
      ],
    );
  }
}
