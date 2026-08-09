import 'package:simf_app/features/account/data/profile_models.dart';

/// Whether a visitor profile carries everything the server will require, field
/// by field.
///
/// These are the cross-field rules that a `Form`'s own validators cannot
/// express, because the controls they guard are NOT `FormField`s: pickers,
/// segmented tabs and image slots. They lived inline in the sign-up screen's
/// submit handler, which meant the answer to "what makes a profile complete?"
/// could only be read by scrolling a 105-line method, and could not be tested
/// without driving the whole form.
///
/// Each rule carries the decision that introduced it. They are stated
/// positively (valid when true) so a caller reads a checklist rather than a
/// chain of negations.
class VisitorProfileCompleteness {
  const VisitorProfileCompleteness._();

  /// D-221 - الجهة. The organisation is required; the server enforces it too.
  static bool organisation(String? organisationId) => organisationId != null;

  /// D-373 - nationality drives the document section and is required server
  /// side. The picker is not a `FormField`, so its inline error cannot gate
  /// submit on its own.
  static bool nationality(String? nationalityCode) => nationalityCode != null;

  /// D-723 - place of birth is required. A non-Saudi types it into a text field
  /// that the form validator already covers; a Saudi picks a region, and that
  /// picker is not a `FormField`, so its gate lives here.
  static bool placeOfBirth({
    required bool isSaudi,
    required String? birthRegionCode,
  }) =>
      !isSaudi || birthRegionCode != null;

  /// Date of birth is a picker, not a `FormField`.
  static bool dateOfBirth(DateTime? value) => value != null;

  /// D-471 - the profile-type picker is searchable rather than a `FormField`,
  /// and it is only REQUIRED when it is actually on screen: never when the
  /// registrant is Visitor-locked, and never while the lookup is loading,
  /// failed or empty (L-6). Gating on a hidden control would block submit with
  /// no visible cause.
  static bool profileType({
    required bool isVisitorType,
    required bool loading,
    required bool failed,
    required bool hasItems,
    required String? profileTypeId,
  }) {
    final bool pickerShown = !isVisitorType && !loading && !failed && hasItems;
    return !pickerShown || profileTypeId != null;
  }

  /// The ID DOCUMENT is mandatory for every registrant. Either a fresh pick or
  /// an already-stored image satisfies it, so a returning visitor is not asked
  /// to re-upload.
  static bool idImage({
    required bool hasPickedImage,
    required bool hasStoredImage,
  }) =>
      hasPickedImage || hasStoredImage;

  /// The FACE photo is mandatory for men and optional for women (the two-photo
  /// split). As with the ID document, a stored photo counts.
  static bool facePhoto({
    required AppGender gender,
    required bool hasPickedImage,
    required bool hasStoredImage,
  }) =>
      gender != AppGender.male || hasPickedImage || hasStoredImage;
}
