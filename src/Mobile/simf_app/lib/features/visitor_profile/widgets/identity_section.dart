import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/core/validation/name_validation.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/core/widgets/simf_labeled_text_field.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/widgets/gender_pills_field.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// Who the visitor is: the bilingual name pair, gender, and the bilingual job
/// title. Both registration surfaces collect exactly these.
///
/// The controllers are OWNED BY THE CALLER, not created here. A
/// `TextEditingController` must be disposed by whoever made it, and the two
/// screens already create and dispose their own; taking that over would move
/// disposal responsibility into a widget that gets rebuilt.
///
/// Each script-restricted field blocks the other script AT THE KEYSTROKE rather
/// than only failing validation, so a field can never hold mixed text: Arabic
/// name and Arabic job title accept Arabic letters and spaces, their English
/// counterparts accept Latin.
class IdentitySection extends StatelessWidget {
  const IdentitySection({
    required this.l10n,
    required this.arabicName,
    required this.englishName,
    required this.jobTitle,
    required this.jobTitleArabic,
    required this.gender,
    required this.onGenderChanged,
    this.organisationField,
    super.key,
  });

  final AppL10n l10n;
  final TextEditingController arabicName;
  final TextEditingController englishName;
  final TextEditingController jobTitle;
  final TextEditingController jobTitleArabic;
  final AppGender gender;
  final ValueChanged<AppGender> onGenderChanged;

  /// The organisation picker, which sits between gender and job title on
  /// sign-up. It stays a caller-supplied widget because the two surfaces build
  /// it differently: sign-up type-aheads against the API, and the walk-in desk
  /// has its own arrangement. Null omits the slot entirely.
  final Widget? organisationField;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfLabeledTextField(
          label: l10n.arabicNameLabel,
          controller: arabicName,
          maxLength: FieldLimits.email,
          // Arabic letters + spaces only - block other scripts at the
          // keystroke so the field can never hold mixed text.
          inputFormatters: <TextInputFormatter>[
            FilteringTextInputFormatter.allow(arabicNameCharacters),
          ],
          validator: (v) => validateArabicName(v, l10n),
        ),
        const SizedBox(height: SimfTokens.space4),
        SimfLabeledTextField(
          label: l10n.englishNameLabel,
          controller: englishName,
          maxLength: FieldLimits.email,
          textDirection: TextDirection.ltr,
          // Latin letters + spaces only.
          inputFormatters: <TextInputFormatter>[
            FilteringTextInputFormatter.allow(RegExp(r'[A-Za-z\s]')),
          ],
          validator: (v) => validateEnglishName(v, l10n),
        ),
        const SizedBox(height: SimfTokens.space4),
        SimfFieldLabel(l10n.genderLabel),
        const SizedBox(height: SimfTokens.space2),
        GenderPillsField(gender: gender, onChanged: onGenderChanged),
        if (organisationField != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space4),
          organisationField!,
        ],
        const SizedBox(height: SimfTokens.space4),
        SimfLabeledTextField(
          label: l10n.jobTitleLabel,
          controller: jobTitle,
          maxLength: FieldLimits.fullName,
          textDirection: TextDirection.ltr,
          // Latin letters + spaces only - mirror the English name field so the
          // English job title can never hold Arabic.
          inputFormatters: <TextInputFormatter>[
            FilteringTextInputFormatter.allow(RegExp(r'[A-Za-z\s]')),
          ],
          // D-723 - required (only the plate number stays optional).
          validator: (String? v) =>
              (v == null || v.trim().isEmpty) ? l10n.jobTitleRequired : null,
        ),
        const SizedBox(height: SimfTokens.space4),
        // Optional Arabic job title - the backend + CP already carry
        // UserProfile.JobTitleArabic; captured here too (server validates only
        // when present).
        SimfLabeledTextField(
          label: l10n.jobTitleArabicLabel,
          controller: jobTitleArabic,
          maxLength: FieldLimits.fullName,
          textDirection: TextDirection.rtl,
          // Arabic letters + spaces only - mirror the Arabic name field so the
          // Arabic job title can never hold Latin text.
          inputFormatters: <TextInputFormatter>[
            FilteringTextInputFormatter.allow(arabicNameCharacters),
          ],
        ),
      ],
    );
  }
}
