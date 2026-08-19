import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:simf_app/core/validation/phone_validation.dart';
import 'package:simf_app/features/account/data/app_gender.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/sign_up_profile_draft.dart';
import 'package:simf_app/features/account/saudi_regions.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_plate_field.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_form_state.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// Everything the sign-up profile-data form (Page 007) collects that is NOT a
/// shared pick: the nine text controllers, the plate entry, the two images and
/// the values carried straight back out to the server untouched.
///
/// The shared picks — nationality, profile type, organisation, gender, document
/// type — stay in [VisitorProfileFormState], which the walk-in desk uses too.
/// This holder is the sign-up half, and it exists so the two mappings that read
/// and write ALL of it — [applyProfile] and [toRequest] — can live beside the
/// fields instead of as two 40-line methods on the screen's State, where they
/// could only be exercised by pumping the whole form. Same shape and the same
/// reason as [SignUpVisitorPlateState], one level down.
///
/// It owns the controllers, so whoever builds it calls [dispose].
class SignUpVisitorForm {
  final TextEditingController arabicName = TextEditingController();
  final TextEditingController englishName = TextEditingController();
  final TextEditingController jobTitle = TextEditingController();
  final TextEditingController jobTitleArabic = TextEditingController();
  final TextEditingController placeOfBirth = TextEditingController();
  final TextEditingController nationalId = TextEditingController();
  final TextEditingController documentNumber = TextEditingController();
  final TextEditingController saudiMobile = TextEditingController();
  final TextEditingController internationalMobile = TextEditingController();

  // C6 (D-459) — the three letter picks, the digits and the assembled plate
  // code, which are only ever useful together.
  final SignUpVisitorPlateState plate = SignUpVisitorPlateState();

  // D-469 — the selected Saudi region code (birth-location dropdown); null for
  // a non-Saudi (free-text place of birth) or an unmatched stored value.
  String? birthRegionCode;

  DateTime? dateOfBirth;

  /// The selected organisation's name in the reader's language. The id itself
  /// is a shared pick, but the label is not on it: the type-ahead reports the
  /// whole [OrganisationItem] once and the field has nothing to look it up in
  /// afterwards, so the two are set and cleared together.
  String? organisationLabel;

  // The ID DOCUMENT image (picked from the gallery) — mandatory for all.
  Uint8List? idImageBytes;
  String? idImageName;

  // The FACE photo (live capture, becoming the avatar) — mandatory for men,
  // optional for women. Shown at the top of the card once captured.
  Uint8List? faceImageBytes;
  String? faceImageName;

  /// True when the server already stores an ID document / a face photo for this
  /// profile (prefill), so a re-entry is not forced to re-pick either.
  bool hasExistingIdImage = false;
  bool hasExistingAvatar = false;

  /// Any interest ids already on the profile (pre-fill). Carried forward in the
  /// draft so the interests screen (Page 007‑01) pre-selects them on re-entry.
  List<String> existingInterestIds = const <String>[];

  /// "Show in Meet People Like You" visibility. The in-app opt-in was removed
  /// (owner 2026-07-24) — this now lives only in the CP; the value is loaded
  /// from the profile and carried forward unchanged so the app never clobbers
  /// the CP-set flag. Defaults to true for a brand-new "Other" registrant.
  bool showInMeetLikeYou = true;

  /// The on-screen date-of-birth text in Saudi civil order `dd-MM-yyyy` (owner:
  /// every displayed date uses dd-MM-yyyy), or an em dash when unset. The API
  /// payload keeps the ISO `yyyy-MM-dd` wire value; only the visible text
  /// differs.
  String get dateOfBirthDisplay {
    final date = dateOfBirth;
    if (date == null) {
      return '—';
    }
    return '${_pad(date.day, 2)}-${_pad(date.month, 2)}-${_pad(date.year, 4)}';
  }

  /// Pre-fills the form from an existing profile (empty on a first-time open).
  /// Guards every picker value against its lookup so a stale id never crashes a
  /// dropdown / leaves an unselectable chip.
  void applyProfile(
    UserProfileResponse profile,
    VisitorProfileFormState picks,
  ) {
    arabicName.text = profile.arabicName;
    englishName.text = profile.englishName;
    jobTitle.text = profile.jobTitle ?? '';
    jobTitleArabic.text = profile.jobTitleArabic ?? '';
    placeOfBirth.text = profile.placeOfBirth;
    birthRegionCode = regionByName(profile.placeOfBirth)?.code;
    nationalId.text = profile.nationalId ?? '';
    if ((profile.iqamaNumber ?? '').isNotEmpty) {
      picks.docType = VisitorDocType.iqama;
      documentNumber.text = profile.iqamaNumber!;
    } else if ((profile.passportNumber ?? '').isNotEmpty) {
      picks.docType = VisitorDocType.passport;
      documentNumber.text = profile.passportNumber!;
    }
    saudiMobile.text = profile.saudiMobile ?? '';
    internationalMobile.text = profile.internationalMobile ?? '';
    plate.setFromCode(profile.plateNumber);
    // D-373 defaults — Male and Saudi Arabia pre-selected on a first-time
    // (empty) profile; a saved profile keeps its own values.
    picks.gender = profile.gender == AppGender.unspecified
        ? AppGender.male
        : profile.gender;
    hasExistingIdImage = profile.hasIdImage;
    hasExistingAvatar = profile.hasAvatar;

    final code = profile.nationalityCode;
    picks.nationalityCode = picks.countries.any((c) => c.code == code)
        ? code
        : (picks.countries.any((c) => c.code == 'SA') ? 'SA' : null);

    final typeId = profile.profileTypeId;
    picks
      ..profileTypeId =
          picks.profileTypes.any((t) => t.id == typeId) ? typeId : null
      ..organisationId = profile.organisationId;
    organisationLabel = null;

    final storedBirthDate = profile.dateOfBirth ?? '';
    if (storedBirthDate.isNotEmpty) {
      dateOfBirth = DateTime.tryParse(storedBirthDate);
    }

    // Carried forward to the interests screen (Page 007‑01) for pre-selection.
    existingInterestIds = profile.interestIds;
    showInMeetLikeYou = profile.showInMeetLikeYou;
  }

  void setOrganisation(
    VisitorProfileFormState picks,
    OrganisationItem organisation, {
    required bool isArabic,
  }) {
    picks.organisationId = organisation.id;
    organisationLabel = isArabic
        ? organisation.nameAr
        : (organisation.nameEn ?? organisation.nameAr);
  }

  void clearOrganisation(VisitorProfileFormState picks) {
    picks.organisationId = null;
    organisationLabel = null;
  }

  /// Applies a nationality pick. Clearing the stale national-id / iqama input
  /// when the Saudi-ness flips keeps the derived document section consistent
  /// (D-373).
  void applyNationality(VisitorProfileFormState picks, String code) {
    final wasSaudi = picks.isSaudi;
    picks.nationalityCode = code;
    if (wasSaudi == picks.isSaudi) {
      return;
    }
    nationalId.clear();
    documentNumber.clear();
    // D-469 — the birth-location control flips with nationality: becoming
    // Saudi keeps the value only if it matches a region (else the picker
    // starts empty); leaving Saudi keeps it as free text.
    if (!picks.isSaudi) {
      return;
    }
    birthRegionCode = regionByName(placeOfBirth.text)?.code;
    if (birthRegionCode == null) {
      placeOfBirth.clear();
    }
  }

  /// Stores a birth-region pick. The code is kept as well as the displayed
  /// name so the field can be re-read in the other language when the user
  /// toggles it (D-469/D-470).
  void setBirthRegion(String code, String name) {
    birthRegionCode = code;
    placeOfBirth.text = name;
  }

  /// Stores / drops the picked ID document. The bytes and the file name travel
  /// together: the upload needs both, and half of a pick is not a pick.
  void setIdImage(Uint8List bytes, String filename) {
    idImageBytes = bytes;
    idImageName = filename;
  }

  void clearIdImage() {
    idImageBytes = null;
    idImageName = null;
  }

  /// Builds the request from the data fields. `interestIds` carries any
  /// existing picks (for pre-selection); the interests screen replaces it via
  /// `copyWith` before the save.
  UpsertUserProfileRequest toRequest(VisitorProfileFormState picks) {
    final isSaudi = picks.isSaudi;
    final birthDate = dateOfBirth;
    return UpsertUserProfileRequest(
      profileTypeId: picks.profileTypeId,
      interestIds: existingInterestIds,
      arabicName: arabicName.text.trim(),
      englishName: englishName.text.trim(),
      jobTitle: _emptyToNull(jobTitle.text),
      jobTitleArabic: _emptyToNull(jobTitleArabic.text),
      nationalityCode: picks.nationalityCode ?? '',
      dateOfBirth: birthDate == null
          ? null
          : '${_pad(birthDate.year, 4)}-${_pad(birthDate.month, 2)}'
              '-${_pad(birthDate.day, 2)}',
      placeOfBirth: placeOfBirth.text.trim(),
      isSaudi: isSaudi,
      nationalId: isSaudi ? _emptyToNull(nationalId.text) : null,
      iqamaNumber: !isSaudi && picks.docType == VisitorDocType.iqama
          ? _emptyToNull(documentNumber.text)
          : null,
      passportNumber: !isSaudi && picks.docType == VisitorDocType.passport
          ? _emptyToNull(documentNumber.text)
          : null,
      // Submit the canonical phone — Arabic digits folded, a leading `00`
      // rewritten to `+` — so the value matches the server's `+`-only shapes.
      saudiMobile:
          isSaudi ? _emptyToNull(normalizePhone(saudiMobile.text)) : null,
      internationalMobile: !isSaudi
          ? _emptyToNull(normalizePhone(internationalMobile.text))
          : null,
      plateNumber: _emptyToNull(plate.value),
      organisationId: picks.organisationId,
      gender: picks.gender,
      showInMeetLikeYou: showInMeetLikeYou,
    );
  }

  /// The same fields plus the picked images, for the interests screen (Page
  /// 007‑01) to finish and re-save.
  SignUpProfileDraft toDraft(VisitorProfileFormState picks) {
    return SignUpProfileDraft(
      request: toRequest(picks),
      idImageBytes: idImageBytes,
      idImageName: idImageName,
      faceImageBytes: faceImageBytes,
      faceImageName: faceImageName,
    );
  }

  void dispose() {
    arabicName.dispose();
    englishName.dispose();
    jobTitle.dispose();
    jobTitleArabic.dispose();
    placeOfBirth.dispose();
    nationalId.dispose();
    documentNumber.dispose();
    saudiMobile.dispose();
    internationalMobile.dispose();
    plate.dispose();
  }

  static String? _emptyToNull(String value) {
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  static String _pad(int value, int width) =>
      value.toString().padLeft(width, '0');
}
