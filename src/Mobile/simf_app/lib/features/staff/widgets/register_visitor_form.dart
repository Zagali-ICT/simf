import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/digit_normalization.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/core/validation/name_validation.dart';
import 'package:simf_app/core/widgets/simf_labeled_text_field.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/widgets/attachment_field.dart';
import 'package:simf_app/features/account/widgets/mobile_field.dart';
import 'package:simf_app/features/account/widgets/terms_and_next_buttons.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_form_fields.dart';
import 'package:simf_app/features/staff/widgets/staff_document_type_field.dart';
import 'package:simf_app/features/staff/widgets/staff_form_row.dart';
import 'package:simf_app/features/staff/widgets/staff_gender_field.dart';
import 'package:simf_app/features/staff/widgets/staff_lookup_field.dart';
import 'package:simf_app/features/staff/widgets/staff_register_card_header.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// The walk-in registration card: every field of the staff visitor form, laid
/// out two-up on a tablet and stacked on a phone. The screen keeps the state,
/// the network calls and the pickers; this only renders them, so a `setState`
/// here is impossible by construction.
class RegisterVisitorForm extends StatelessWidget {
  const RegisterVisitorForm({
    required this.fields,
    required this.wide,
    required this.profileTypes,
    required this.profileTypeId,
    required this.countries,
    required this.nationalityCode,
    required this.organisations,
    required this.organisationId,
    required this.gender,
    required this.docType,
    required this.isSaudi,
    required this.triedSubmit,
    required this.submitting,
    required this.idBytes,
    required this.idName,
    required this.photoBytes,
    required this.photoName,
    required this.onPickProfileType,
    required this.onPickNationality,
    required this.onPickOrganisation,
    required this.onGenderChanged,
    required this.onDocTypeChanged,
    required this.onAttachId,
    required this.onRemoveId,
    required this.onAttachPhoto,
    required this.onRemovePhoto,
    required this.onSubmit,
    super.key,
  });

  final RegisterVisitorFormFields fields;

  /// Tablet width: the fields pair up into two columns.
  final bool wide;

  final List<ProfileTypeItem> profileTypes;
  final String? profileTypeId;
  final List<CountryItem> countries;
  final String? nationalityCode;
  final List<OrganisationItem> organisations;
  final String? organisationId;

  final AppGender gender;
  final VisitorDocType docType;
  final bool isSaudi;

  /// Submit has been attempted, so the lookups that have no validator of their
  /// own may show their "required" message.
  final bool triedSubmit;
  final bool submitting;

  final Uint8List? idBytes;
  final String? idName;
  final Uint8List? photoBytes;
  final String? photoName;

  final VoidCallback onPickProfileType;
  final VoidCallback onPickNationality;
  final VoidCallback onPickOrganisation;
  final ValueChanged<AppGender> onGenderChanged;
  final ValueChanged<VisitorDocType> onDocTypeChanged;
  final VoidCallback onAttachId;
  final VoidCallback onRemoveId;
  final VoidCallback onAttachPhoto;
  final VoidCallback onRemovePhoto;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    const gap = SizedBox(height: SimfTokens.space4);
    final profileType =
        profileTypes.where((t) => t.id == profileTypeId).toList();
    final nationality =
        countries.where((c) => c.code == nationalityCode).toList();
    final organisation =
        organisations.where((o) => o.id == organisationId).toList();
    // DEF-STF-007 — when the lookup comes back EMPTY there is nothing to pick,
    // so submit could never pass and the operator had no correctable field.
    // The field now says so, and says what to do about it, instead of silently
    // blocking.
    final typesUnavailable = profileTypes.isEmpty;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const StaffRegisterCardHeader(),
        const SizedBox(height: SimfTokens.space6),
        // 19g — the walk-in classification is no longer silently pinned to the
        // row literally named "Normal": the operator picks from the
        // visitor-eligible types (seeded to "Normal" when it exists).
        StaffLookupField(
          key: fields.profileTypeAnchor,
          label: l10n.profileTypeLabel,
          fieldKey: 'staffProfileTypePicker',
          displayText: profileType.isNotEmpty
              ? (l10n.isArabic
                  ? profileType.first.nameArabic
                  : profileType.first.name)
              : (typesUnavailable
                  ? l10n.staffProfileTypeUnavailable
                  : l10n.profileTypeLabel),
          isPlaceholder: profileType.isEmpty,
          onTap: typesUnavailable ? null : onPickProfileType,
          errorText: typesUnavailable
              ? l10n.staffProfileTypeEmptyHelp
              : (triedSubmit && profileTypeId == null)
                  ? l10n.profileTypeRequired
                  : null,
        ),
        gap,
        StaffFormRow(
          wide: wide,
          start: _named(
            l10n.arabicNameLabel,
            KeyedSubtree(
              key: fields.arabicNameAnchor,
              child: SimfLabeledTextField(
                label: l10n.arabicNameLabel,
                controller: fields.arabicName,
                maxLength: FieldLimits.profileName,
                textDirection: TextDirection.rtl,
                // MERGE (BUG-019 rebuild + BUG-021): the rebuilt field keeps
                // the shared widget, but the character class is the widened
                // shared one so tashkeel/tatweel are no longer silently dropped
                // as you type.
                inputFormatters: <TextInputFormatter>[
                  FilteringTextInputFormatter.allow(arabicNameCharacters),
                ],
                validator: fields.validateArabicName,
              ),
            ),
          ),
          end: _named(
            l10n.englishNameLabel,
            KeyedSubtree(
              key: fields.englishNameAnchor,
              child: SimfLabeledTextField(
                label: l10n.englishNameLabel,
                controller: fields.englishName,
                maxLength: FieldLimits.profileName,
                textDirection: TextDirection.ltr,
                inputFormatters: <TextInputFormatter>[
                  FilteringTextInputFormatter.allow(RegExp(r'[A-Za-z\s]')),
                ],
                validator: fields.validateEnglishName,
              ),
            ),
          ),
        ),
        gap,
        StaffFormRow(
          wide: wide,
          start: StaffGenderField(
            gender: gender,
            onChanged: onGenderChanged,
          ),
          // 19j — the 57-country list gets the shared searchable picker,
          // exactly like Create-profile, instead of a raw Material dropdown.
          end: StaffLookupField(
            key: fields.nationalityAnchor,
            label: l10n.nationalityLabel,
            fieldKey: 'staffNationalityPicker',
            displayText: nationality.isNotEmpty
                ? (l10n.isArabic
                    ? nationality.first.nameArabic
                    : nationality.first.name)
                : l10n.nationalityLabel,
            isPlaceholder: nationality.isEmpty,
            onTap: onPickNationality,
            errorText: (triedSubmit && nationalityCode == null)
                ? l10n.nationalityRequired
                : null,
          ),
        ),
        gap,
        StaffFormRow(
          wide: wide,
          start: isSaudi
              ? _named(
                  l10n.nationalIdLabel,
                  KeyedSubtree(
                    key: fields.documentAnchor,
                    child: SimfLabeledTextField(
                      label: l10n.nationalIdLabel,
                      controller: fields.nationalId,
                      keyboardType: TextInputType.number,
                      maxLength: FieldLimits.nationalId,
                      // The id renders LTR (digits) even under Arabic —
                      // genuinely-LTR content, unlike the surrounding layout
                      // (19b).
                      textDirection: TextDirection.ltr,
                      inputFormatters: <TextInputFormatter>[
                        const WesternDigitsFormatter(),
                        FilteringTextInputFormatter.digitsOnly,
                      ],
                      validator: fields.validateNationalId,
                    ),
                  ),
                )
              : StaffDocumentTypeField(
                  key: fields.documentAnchor,
                  docType: docType,
                  onChanged: onDocTypeChanged,
                ),
          end: isSaudi
              ? null
              : _named(
                  l10n.documentNumberLabel,
                  KeyedSubtree(
                    key: fields.documentNumberAnchor,
                    child: SimfLabeledTextField(
                      label: l10n.documentNumberLabel,
                      controller: fields.documentNumber,
                      maxLength: docType == VisitorDocType.iqama ? 10 : 9,
                      textDirection: TextDirection.ltr,
                      inputFormatters: const <TextInputFormatter>[
                        WesternDigitsFormatter(),
                      ],
                      validator: fields.validateDocumentNumber,
                    ),
                  ),
                ),
        ),
        gap,
        StaffFormRow(
          wide: wide,
          start: _named(
            l10n.jobTitleLabel,
            KeyedSubtree(
              key: fields.jobTitleAnchor,
              child: SimfLabeledTextField(
                label: l10n.jobTitleLabel,
                controller: fields.jobTitle,
                maxLength: FieldLimits.fullName,
                textDirection: TextDirection.ltr,
                // D-723 — required (matches the app self-registration form).
                validator: fields.validateJobTitle,
              ),
            ),
          ),
          // Optional Arabic job title — the backend already carries
          // AdminWalkInRegistrationRequest.JobTitleArabic; capture it here too.
          end: _named(
            l10n.jobTitleArabicLabel,
            SimfLabeledTextField(
              label: l10n.jobTitleArabicLabel,
              controller: fields.jobTitleArabic,
              maxLength: FieldLimits.fullName,
              textDirection: TextDirection.rtl,
              validator: fields.validateJobTitleArabic,
            ),
          ),
        ),
        gap,
        StaffFormRow(
          wide: wide,
          // 19b — email and phone are genuinely LTR CONTENT, so the inputs stay
          // explicitly LTR while the layout follows the locale.
          start: _named(
            l10n.staffEmailLabel,
            SimfLabeledTextField(
              label: l10n.staffEmailLabel,
              controller: fields.email,
              maxLength: FieldLimits.email,
              keyboardType: TextInputType.emailAddress,
              textDirection: TextDirection.ltr,
              validator: fields.validateEmail,
            ),
          ),
          end: _named(
            l10n.staffPhoneLabel,
            KeyedSubtree(
              key: fields.phoneAnchor,
              child: MobileField(
                saudi: isSaudi,
                controller: fields.phone,
                validator: fields.validatePhone,
              ),
            ),
          ),
        ),
        gap,
        // 19j — the organisation list also moves to the shared searchable
        // picker; the type-to-filter sheet handles the 200 loaded rows.
        StaffLookupField(
          key: fields.organisationAnchor,
          label: l10n.staffOrganisationLabel,
          fieldKey: 'staffOrganisationPicker',
          displayText: organisation.isNotEmpty
              ? organisationDisplayName(organisation.first, l10n)
              : l10n.organisationSearchHint,
          isPlaceholder: organisation.isEmpty,
          onTap: onPickOrganisation,
          errorText: (triedSubmit && organisationId == null)
              ? l10n.organisationRequired
              : null,
        ),
        gap,
        StaffFormRow(
          wide: wide,
          start: Semantics(
            label: l10n.staffAttachIdLabel,
            child: AttachmentField(
              label: l10n.staffAttachIdLabel,
              // 19k — the long "ID / Iqama / passport" detail lives here now,
              // so the caption stays one line like every sibling.
              hintText: l10n.staffAttachIdHint,
              bytes: idBytes,
              round: false,
              attachLabel: l10n.staffAttachFile,
              attachIcon: Icons.add_circle_outline,
              onAttach: onAttachId,
              attachedName: idName ?? l10n.idImageAttachedLabel,
              actionLabel: l10n.removeLabel,
              onAction: onRemoveId,
            ),
          ),
          end: Semantics(
            label: l10n.staffAttachPhotoLabel,
            child: AttachmentField(
              label: l10n.staffAttachPhotoLabel,
              // Keeps the two attach boxes on the same baseline as the ID
              // field's hint line, and states the (true) optionality.
              hintText: l10n.staffAttachOptionalHint,
              bytes: photoBytes,
              round: true,
              attachLabel: l10n.staffAttachPhoto,
              attachIcon: Icons.photo_camera_outlined,
              onAttach: onAttachPhoto,
              attachedName: photoName ?? l10n.idImageAttachedLabel,
              actionLabel: l10n.removeLabel,
              onAction: onRemovePhoto,
            ),
          ),
        ),
        const SizedBox(height: SimfTokens.space6),
        TermsAndNextButtons(
          onNext: onSubmit,
          busy: submitting,
        ),
      ],
    );
  }
}

/// The organisation's name in the reader's language. Shared with the screen's
/// picker sheet so the selected row and the option that produced it read the
/// same.
String organisationDisplayName(OrganisationItem o, AppL10n l10n) =>
    l10n.isArabic ? o.nameAr : (o.nameEn ?? o.nameAr);

/// 19h — the shared field widgets render a visible caption but leave the input
/// itself unnamed for a screen reader; this names it.
Widget _named(String label, Widget field) =>
    Semantics(label: label, textField: true, child: field);
