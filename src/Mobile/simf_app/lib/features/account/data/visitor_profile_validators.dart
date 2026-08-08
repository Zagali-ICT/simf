import '../../../app/localization/app_l10n.dart';
import '../../../core/validation/name_validation.dart';
import '../../../core/validation/phone_validation.dart';
import '../../../core/validation/plate_validation.dart';
import '../../../core/validation/saudi_id_validation.dart';

/// Which identity document a non-Saudi visitor is registering with.
///
/// One definition for both registration surfaces. It was declared privately and
/// identically in `sign_up_visitor_screen.dart` and
/// `staff/register_visitor_screen.dart`, so adding a third document type meant
/// finding both.
enum VisitorDocType { iqama, passport }

/// Field validators for the visitor profile form.
///
/// These were members of the sign-up screen's State, which meant they could
/// only be read by scrolling a 1393-line file and could not be unit-tested
/// without pumping a widget. Nothing here touches screen state: each takes its
/// [AppL10n] explicitly, so they are pure functions of their input.
///
/// The shape rules themselves stay in `core/validation/*`; these add the order
/// of checks and the per-field message, mirroring
/// `UpsertUserProfileRequestValidator` on the server.
///
/// The staff walk-in desk deliberately keeps its OWN validators rather than
/// calling these. They look like duplicates and are not: the desk echoes
/// per-field server errors back into the form, so a valid-shaped value can
/// still fail with the server's reason. Same rules, different error surfacing.
/// Merging them would drop that echo and hide walk-in registration failures.
///
/// A name must be non-empty, use only [lettersOnly] script, and carry at least
/// two parts. [lettersOnlyMsg] is the per-script message, so Arabic and English
/// share one implementation.
String? validateName(
  String? value,
  AppL10n l10n,
  RegExp lettersOnly,
  String lettersOnlyMsg,
) {
  final name = value?.trim() ?? '';
  if (name.isEmpty) {
    return l10n.requiredField;
  }
  if (!isNameLettersOnly(name, lettersOnly)) {
    return lettersOnlyMsg;
  }
  if (!hasFullNameParts(name)) {
    return l10n.fullNameParts;
  }
  return null;
}

String? validateArabicName(String? value, AppL10n l10n) => validateName(
      value,
      l10n,
      arabicNameLettersOnly,
      l10n.arabicNameLettersOnly,
    );

String? validateEnglishName(String? value, AppL10n l10n) => validateName(
      value,
      l10n,
      englishNameLettersOnly,
      l10n.englishNameLettersOnly,
    );

String? validateNationalId(String? value, AppL10n l10n) {
  final id = value?.trim() ?? '';
  return isValidNationalId(id) ? null : l10n.nationalIdInvalid;
}

/// The document number's shape depends on which document it is, which is why
/// [docType] is a parameter rather than something read off the screen.
String? validateDocumentNumber(
  String? value,
  AppL10n l10n,
  VisitorDocType docType,
) {
  final number = value?.trim() ?? '';
  if (number.isEmpty) {
    return l10n.documentRequired;
  }
  if (docType == VisitorDocType.iqama) {
    return isValidIqama(number) ? null : l10n.iqamaInvalid;
  }
  return isValidPassport(number) ? null : l10n.passportInvalid;
}

/// C4 (D-371) - the standard shapes live in `phone_validation.dart`, mirroring
/// `UpsertUserProfileRequestValidator` exactly.
///
/// D-723 - mobile is required; only the plate number stays optional.
String? validateSaudiMobile(String? value, AppL10n l10n) {
  final phone = value?.trim() ?? '';
  if (phone.isEmpty) {
    return l10n.mobileRequired;
  }
  return isStandardSaudiMobile(phone) ? null : l10n.saudiMobileInvalid;
}

String? validateInternationalMobile(String? value, AppL10n l10n) {
  final phone = value?.trim() ?? '';
  if (phone.isEmpty) {
    return l10n.mobileRequired;
  }
  return isStandardInternationalMobile(phone)
      ? null
      : l10n.internationalMobileInvalid;
}

/// The plate is the one optional field: empty passes.
String? validatePlate(String? value, AppL10n l10n) {
  final plate = value?.trim() ?? '';
  if (plate.isEmpty) {
    return null;
  }
  return isStandardPlateNumber(plate) ? null : l10n.plateNumberInvalid;
}
