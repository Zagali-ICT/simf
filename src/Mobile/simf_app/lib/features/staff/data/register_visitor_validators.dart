import 'package:flutter/widgets.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/validation/phone_validation.dart';
import 'package:simf_app/core/validation/required_validation.dart';
import 'package:simf_app/core/validation/saudi_id_validation.dart';
import 'package:simf_app/features/staff/data/walk_in_field_errors.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_form_fields.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// The walk-in form's field rules, and the order they are read in.
///
/// The desk deliberately keeps its OWN validators rather than calling
/// `visitor_profile_validators.dart`: it echoes the server's per-field
/// rejections back into the form, so a valid-shaped value can still fail with
/// the server's reason (DEF-STF-003). Same shape rules — those still come from
/// `core/validation/*` — different error surfacing.
///
/// The live state each rule reads arrives as a callback rather than a value,
/// because the validators are bound to the inputs ONCE (in the screen's
/// `initState`) and then run on every keystroke. [l10n] in particular must be
/// resolved at validate time: the shared EN/ع pill switches language while the
/// form is on screen, and a captured [AppL10n] would freeze every message in
/// whichever language the form opened in.
class RegisterVisitorValidators {
  RegisterVisitorValidators({
    required this.l10n,
    required this.isSaudi,
    required this.docType,
    required this.serverErrors,
  });

  final AppL10n Function() l10n;
  final bool Function() isSaudi;
  final VisitorDocType Function() docType;
  final WalkInFieldErrors serverErrors;

  /// The form's inputs, each bound to the rule that governs it. There is only
  /// ever one correct wiring of these eight, so it lives here rather than being
  /// re-spelled by the screen that holds the fields.
  RegisterVisitorFormFields buildFields() => RegisterVisitorFormFields(
        validateArabicName: arabicName,
        validateEnglishName: englishName,
        validateEmail: email,
        validateJobTitle: jobTitle,
        validateJobTitleArabic: jobTitleArabic,
        validateNationalId: nationalId,
        validateDocumentNumber: documentNumber,
        validatePhone: phone,
      );

  String? notBlank(String? value) =>
      isBlank(value) ? l10n().requiredField : null;

  String? arabicName(String? value) =>
      notBlank(value) ?? serverErrors.messageFor('ArabicName', value);

  /// DisplayName is derived from this field (the request sends the English name
  /// as the display name), so its rejection lands here too.
  String? englishName(String? value) =>
      notBlank(value) ??
      serverErrors.messageFor('EnglishName', value) ??
      serverErrors.messageFor('DisplayName', value);

  String? email(String? value) => serverErrors.messageFor('Email', value);

  /// D-723 — required (matches the app self-registration form).
  String? jobTitle(String? value) =>
      notBlank(value) ?? serverErrors.messageFor('JobTitle', value);

  String? jobTitleArabic(String? value) =>
      serverErrors.messageFor('JobTitleArabic', value);

  // D-700 — mirror the self-service shape + Luhn checks client-side so staff
  // get instant feedback (the server already enforces the same via
  // AdminWalkInRegistrationRequestValidator). Empty keeps the "required"
  // message.
  String? nationalId(String? value) {
    final id = value?.trim() ?? '';
    if (id.isEmpty) {
      return notBlank(value);
    }
    if (!isValidNationalId(id)) {
      return l10n().nationalIdInvalid;
    }
    return serverErrors.messageFor('NationalId', value);
  }

  String? documentNumber(String? value) {
    final messages = l10n();
    final number = value?.trim() ?? '';
    if (number.isEmpty) {
      return notBlank(value);
    }
    if (docType() == VisitorDocType.iqama) {
      return isValidIqama(number)
          ? serverErrors.messageFor('IqamaNumber', value)
          : messages.iqamaInvalid;
    }
    return isValidPassport(number)
        ? serverErrors.messageFor('PassportNumber', value)
        : messages.passportInvalid;
  }

  /// Phone is required server-side (Saudi or international); validate inline
  /// like every other required field, with the same standard shapes as
  /// self-service.
  String? phone(String? value) {
    final messages = l10n();
    final saudi = isSaudi();
    final number = value?.trim() ?? '';
    if (number.isEmpty) {
      return messages.requiredField;
    }
    final valid = saudi
        ? isStandardSaudiMobile(number)
        : isStandardInternationalMobile(number);
    if (valid) {
      return serverErrors.messageFor(
        saudi ? 'SaudiMobile' : 'InternationalMobile',
        value,
      );
    }
    return saudi
        ? messages.saudiMobileInvalid
        : messages.internationalMobileInvalid;
  }

  /// 19l — the first invalid field after a blocked submit, so the screen can
  /// bring it into view. The order mirrors the on-screen order, so the operator
  /// lands on the top-most problem rather than an arbitrary one. Null when
  /// nothing is left to fix.
  GlobalKey? firstProblemAnchor(
    RegisterVisitorFormFields fields, {
    required String? profileTypeId,
    required String? nationalityCode,
    required String? organisationId,
  }) {
    if (profileTypeId == null) {
      return fields.profileTypeAnchor;
    }
    if (notBlank(fields.arabicName.text) != null) {
      return fields.arabicNameAnchor;
    }
    if (notBlank(fields.englishName.text) != null) {
      return fields.englishNameAnchor;
    }
    if (nationalityCode == null) {
      return fields.nationalityAnchor;
    }
    if (isSaudi()) {
      if (nationalId(fields.nationalId.text) != null) {
        return fields.documentAnchor;
      }
    } else if (documentNumber(fields.documentNumber.text) != null) {
      return fields.documentNumberAnchor;
    }
    if (notBlank(fields.jobTitle.text) != null) {
      return fields.jobTitleAnchor;
    }
    if (phone(fields.phone.text) != null) {
      return fields.phoneAnchor;
    }
    if (organisationId == null) {
      return fields.organisationAnchor;
    }
    return null;
  }
}
