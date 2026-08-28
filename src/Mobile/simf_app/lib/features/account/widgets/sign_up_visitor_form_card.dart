import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/account/data/app_gender.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/sign_up_visitor_form.dart';
import 'package:simf_app/features/account/data/sign_up_visitor_lookups.dart';
import 'package:simf_app/features/account/widgets/beige_tabs.dart';
import 'package:simf_app/features/account/widgets/date_of_birth_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_face_photo_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_header_avatar.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_id_image_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_organisation_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_place_of_birth_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_plate_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_profile_type_field.dart';
import 'package:simf_app/features/account/widgets/terms_and_next_buttons.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_form_state.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';
import 'package:simf_app/features/visitor_profile/widgets/contact_section.dart';
import 'package:simf_app/features/visitor_profile/widgets/document_section.dart';
import 'package:simf_app/features/visitor_profile/widgets/identity_section.dart';
import 'package:simf_app/features/visitor_profile/widgets/nationality_section.dart';

/// The beige form card (Figma 168:2977) holding the whole create-profile form:
/// the card head, the visitor/other tabs and every section down to the terms
/// line and التالي.
///
/// The screen keeps the state, the network calls and the pickers; this only
/// renders them, so a `setState` here is impossible by construction — the same
/// split the walk-in desk uses between `RegisterVisitorScreen` and
/// `RegisterVisitorForm`. The two holders carry the values, which is what keeps
/// the argument list to the callbacks plus a handful of load flags.
class SignUpVisitorFormCard extends StatelessWidget {
  const SignUpVisitorFormCard({
    required this.form,
    required this.picks,
    required this.type,
    required this.initialOrganisations,
    required this.saveError,
    required this.saving,
    required this.onTypeChanged,
    required this.onRetryProfileTypes,
    required this.onProfileTypeChanged,
    required this.onGenderChanged,
    required this.onPickMobileCallingCode,
    required this.onOrganisationSelected,
    required this.onOrganisationCleared,
    required this.onPickNationality,
    required this.onDocTypeChanged,
    required this.onPickDateOfBirth,
    required this.onBirthRegionPicked,
    required this.onPlateChanged,
    required this.onAttachIdImage,
    required this.onRemoveIdImage,
    required this.onCaptureFacePhoto,
    required this.onNext,
    super.key,
  });

  final SignUpVisitorForm form;
  final VisitorProfileFormState picks;

  /// نوع التسجيل and the ProfileType lookup it filters. Under Visitor the
  /// picker is hidden and the type is locked (C5 — D-371).
  final SignUpVisitorTypeSelection type;

  /// The organisations fetched with the opening load, so the type-ahead has a
  /// list before the first keystroke.
  final List<OrganisationItem> initialOrganisations;

  /// D-684 — the profile is saved on THIS step, so a server error shows here.
  final String? saveError;
  final bool saving;

  final ValueChanged<bool> onTypeChanged;
  final VoidCallback onRetryProfileTypes;
  final ValueChanged<String?> onProfileTypeChanged;
  final ValueChanged<AppGender> onGenderChanged;

  /// Opens the country sheet for the calling code. The screen owns it like
  /// every other lookup on this form.
  final VoidCallback onPickMobileCallingCode;
  final ValueChanged<OrganisationItem> onOrganisationSelected;
  final VoidCallback onOrganisationCleared;
  final VoidCallback onPickNationality;
  final ValueChanged<VisitorDocType> onDocTypeChanged;
  final VoidCallback onPickDateOfBirth;
  final void Function(String code, String name) onBirthRegionPicked;
  final VoidCallback onPlateChanged;
  final VoidCallback onAttachIdImage;
  final VoidCallback onRemoveIdImage;
  final VoidCallback onCaptureFacePhoto;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    const gap = SizedBox(height: SimfTokens.space4);
    // A Material (not a decorated Container) so the ListTile/switch ink inside
    // the card renders above the beige fill.
    return Material(
      color: SimfTokens.cardBeige,
      borderRadius: SimfTokens.borderRadiusSmall,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            // Card head (Figma 522:2186): avatar badge + title.
            Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    l10n.createProfileTitle,
                    style: const TextStyle(
                      fontSize: SimfTokens.text24,
                      fontWeight: FontWeight.w600,
                      color: SimfTokens.headlineInk,
                    ),
                  ),
                ),
                // The captured face photo replaces the placeholder person icon
                // at the top once taken (owner follow-up).
                SignUpVisitorHeaderAvatar(bytes: form.faceImageBytes),
              ],
            ),
            const SizedBox(height: SimfTokens.space6),
            // نوع التسجيل (Visitor / Other) — beige tabs (Figma 505:1075).
            BeigeTabs(
              options: <String>[l10n.signUpTypeVisitor, l10n.signUpTypeOther],
              selectedIndex: type.isVisitor ? 0 : 1,
              onChanged: (index) => onTypeChanged(index == 0),
            ),
            const SizedBox(height: SimfTokens.space6),
            SignUpVisitorProfileTypeField(
              l10n: l10n,
              form: picks,
              isVisitorType: type.isVisitor,
              loading: type.loading,
              failed: type.failed,
              onRetry: onRetryProfileTypes,
              onChanged: onProfileTypeChanged,
            ),
            gap,
            IdentitySection(
              l10n: l10n,
              arabicName: form.arabicName,
              englishName: form.englishName,
              jobTitle: form.jobTitle,
              jobTitleArabic: form.jobTitleArabic,
              gender: picks.gender,
              onGenderChanged: onGenderChanged,
              organisationField: SignUpVisitorOrganisationField(
                l10n: l10n,
                initialResults: initialOrganisations,
                selectedId: picks.organisationId,
                selectedLabel: form.organisationLabel,
                showError: picks.triedSubmit && picks.organisationId == null,
                isOther: form.organisationIsOther,
                otherController: form.organisationOther,
                showOtherError: picks.triedSubmit &&
                    form.organisationIsOther &&
                    form.organisationOther.text.trim().isEmpty,
                onSelected: onOrganisationSelected,
                onCleared: onOrganisationCleared,
              ),
            ),
            gap,
            NationalitySection(
              l10n: l10n,
              countries: picks.countries,
              selectedCode: picks.nationalityCode,
              showError: picks.triedSubmit && picks.nationalityCode == null,
              onTap: onPickNationality,
            ),
            gap,
            // D-373 — the Saudi switch is gone: the nationality pick drives
            // national-ID vs iqama/passport (SA → national ID).
            DocumentSection(
              l10n: l10n,
              isSaudi: picks.isSaudi,
              docType: picks.docType,
              nationalId: form.nationalId,
              documentNumber: form.documentNumber,
              onDocTypeChanged: onDocTypeChanged,
            ),
            gap,
            ContactSection(
              l10n: l10n,
              isSaudi: picks.isSaudi,
              saudiMobile: form.saudiMobile,
              internationalMobile: form.internationalMobile,
              callingCode: form.mobileCallingCode,
              onPickCallingCode: onPickMobileCallingCode,
            ),
            gap,
            DateOfBirthField(
              displayValue: form.dateOfBirthDisplay,
              hasError: picks.triedSubmit && form.dateOfBirth == null,
              onTap: onPickDateOfBirth,
            ),
            gap,
            SignUpVisitorPlaceOfBirthField(
              l10n: l10n,
              isSaudi: picks.isSaudi,
              controller: form.placeOfBirth,
              regionCode: form.birthRegionCode,
              showError: picks.triedSubmit && form.birthRegionCode == null,
              onRegionPicked: onBirthRegionPicked,
            ),
            gap,
            // D-373 — the plate is the last input before the attach.
            SignUpVisitorPlateField(
              l10n: l10n,
              state: form.plate,
              onChanged: onPlateChanged,
            ),
            gap,
            SignUpVisitorIdImageField(
              l10n: l10n,
              bytes: form.idImageBytes,
              filename: form.idImageName,
              hasStoredImage: form.hasExistingIdImage,
              triedSubmit: picks.triedSubmit,
              onAttach: onAttachIdImage,
              onRemove: onRemoveIdImage,
            ),
            gap,
            SignUpVisitorFacePhotoField(
              l10n: l10n,
              bytes: form.faceImageBytes,
              gender: picks.gender,
              hasStoredAvatar: form.hasExistingAvatar,
              triedSubmit: picks.triedSubmit,
              onCapture: onCaptureFacePhoto,
            ),
            if (saveError != null) ...<Widget>[
              Text(
                saveError!,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: SimfTokens.danger,
                  fontSize: SimfTokens.signUpVisitorScreenFontSize,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: SimfTokens.space3),
            ],
            TermsAndNextButtons(onNext: onNext, busy: saving),
          ],
        ),
      ),
    );
  }
}
