import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_form_scaffold.dart';
import '../../core/errors/api_error_l10n.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/validation/digit_normalization.dart';
import '../../core/validation/name_validation.dart';
import '../../core/validation/phone_validation.dart';
import '../../core/validation/plate_validation.dart';
import '../../core/validation/saudi_id_validation.dart';
import '../../core/widgets/simf_auth_sweep.dart';
import '../../core/widgets/simf_field_label.dart';
import '../../core/widgets/simf_field_style.dart';
import '../../core/widgets/simf_labeled_text_field.dart';
import '../../core/widgets/simf_picker_field.dart';
import '../myarea/identity_verification_screen.dart' show CapturedSelfie;
import 'data/profile_models.dart';
import 'data/profile_repository.dart';
import 'data/region_repository.dart';
import 'saudi_regions.dart';
import 'widgets/attachment_field.dart';
import 'widgets/beige_tabs.dart';
import 'widgets/date_of_birth_field.dart';
import 'widgets/gender_pills_field.dart';
import 'widgets/lookup_search_sheet.dart';
import 'widgets/mobile_field.dart';
import 'widgets/sign_up_visitor_header_avatar.dart';
import 'widgets/terms_and_next_buttons.dart';

/// Page 007 — إنشاء ملف شخصى · Sign up — profile **data**. The KSA-Project
/// Figma design (node 168:2972 — D-368): the login-style navy header (logo +
/// forum name, back chevron + the wired globe language toggle) over the beige
/// card holding the whole form — login-style bordered fields, the beige
/// segmented tabs (visitor/other + document type), gender radio pills, the
/// attach box, the underlined terms link and the gold التالي. The previous
/// screen is parked in `_legacy_mockup/`.
///
/// Contract unchanged (D-332): loads the existing profile + the three lookups
/// concurrently; the visitor/other tab is a client-only `?isVisitor=` filter
/// over the ProfileType picker; **Next** carries the collected data (+ the
/// optional ID image) forward as a [SignUpProfileDraft] to the interests
/// screen, which fires the single profile save. **No API write happens here.**
/// Design deltas (recorded in D-368): the frame's "رقم اللوحة (اختياري)" has
/// no backend field and is NOT rendered; date of birth, place of birth and
/// the Saudi national-ID path are kept (API-required) in the same styling
/// even though the frame omits them.
///
/// Clean-code pass — D-546 (2026-06-30). Decomposed to the per-page DoD
/// (golden `sign_up_visitor_168-2972.png`); see
/// `docs/pages/mobile/sign-up-visitor/README.md`.
class SignUpVisitorScreen extends ConsumerStatefulWidget {
  const SignUpVisitorScreen({super.key});

  @override
  ConsumerState<SignUpVisitorScreen> createState() =>
      _SignUpVisitorScreenState();
}

/// Which identity document a non-Saudi registrant supplies (the validator
/// accepts an Iqama **or** a passport — Page_007 L-4).
enum _DocType { iqama, passport }

class _SignUpVisitorScreenState extends ConsumerState<SignUpVisitorScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();

  final TextEditingController _arabicName = TextEditingController();
  final TextEditingController _englishName = TextEditingController();
  final TextEditingController _jobTitle = TextEditingController();
  final TextEditingController _jobTitleArabic = TextEditingController();
  final TextEditingController _placeOfBirth = TextEditingController();
  // D-469 — the selected Saudi region code (birth-location dropdown); null for a
  // non-Saudi (free-text place of birth) or an unmatched stored value.
  String? _birthRegionCode;
  final TextEditingController _nationalId = TextEditingController();
  final TextEditingController _documentNumber = TextEditingController();
  final TextEditingController _saudiMobile = TextEditingController();
  final TextEditingController _internationalMobile = TextEditingController();
  final TextEditingController _plate = TextEditingController();
  // C6 (D-459) — the plate letter picks (Latin codes) + the digits field. The
  // assembled value is mirrored into [_plate] so the submit/prefill path that
  // reads `_plate.text` is unchanged.
  String? _plateLetter1;
  String? _plateLetter2;
  String? _plateLetter3;
  // D-471 fix — a plate is valid in either order (letters-then-digits or
  // digits-then-letters) and the canonical code PRESERVES that order. Remember a
  // digits-first stored plate so prefill→re-sync doesn't silently reorder it
  // (e.g. "1234ABJ" must not be rewritten to "ABJ1234").
  bool _plateDigitsFirst = false;
  final TextEditingController _plateDigits = TextEditingController();
  final TextEditingController _organisationSearch = TextEditingController();

  /// نوع التسجيل: Visitor (true) / Other (false) — the `ProfileType.IsForVisitor`
  /// filter (D-332). Client-only; not persisted.
  bool _isVisitorType = true;
  // D-373 — Saudi-ness derives from the nationality pick (the explicit
  // switch was removed): SA → national-ID field, else Iqama/Passport.
  bool get _isSaudi => _nationalityCode == 'SA';
  _DocType _docType = _DocType.iqama;
  DateTime? _dateOfBirth;
  AppGender _gender = AppGender.unspecified;
  String? _nationalityCode;
  String? _profileTypeId;
  String? _organisationId;
  String? _organisationLabel;

  /// Any interest ids already on the profile (pre-fill). Carried forward in the
  /// draft so the interests screen (Page 007‑01) pre-selects them on re-entry.
  List<String> _existingInterestIds = const <String>[];

  List<CountryItem> _countries = const <CountryItem>[];
  List<ProfileTypeItem> _profileTypes = const <ProfileTypeItem>[];
  List<OrganisationItem> _organisationResults = const <OrganisationItem>[];

  // D-375 — API-fed pickers always surface their fetch state (owner rule:
  // every dropdown loaded from the API shows loading, and a failure is a
  // visible retry — never a silently missing/empty control).
  bool _profileTypesLoading = false;
  bool _profileTypesFailed = false;
  bool _organisationSearching = false;
  bool _organisationSearchFailed = false;

  Timer? _organisationDebounce;

  // The ID DOCUMENT image (picked from the gallery) — mandatory for all.
  Uint8List? _idImageBytes;
  String? _idImageName;
  // True when the server already stores an ID document for this profile
  // (prefill), so a re-entry is not forced to re-pick it.
  bool _hasExistingIdImage = false;

  // The FACE photo (live capture → the avatar) — mandatory for men, optional
  // for women. Shown at the top of the card once captured.
  Uint8List? _faceImageBytes;
  String? _faceImageName;
  // True when the server already stores a face photo (avatar) for this profile.
  bool _hasExistingAvatar = false;

  bool _loading = true;
  String? _loadError;
  bool _triedSubmit = false;
  // D-684 — the profile is saved on THIS step now (profile-first), so any server
  // error (e.g. the name) surfaces here, not two screens later on interests.
  bool _saving = false;
  String? _saveError;

  /// "Show in Meet People Like You" visibility. The in-app opt-in was removed
  /// (owner 2026-07-24) — this now lives only in the CP; the value is loaded
  /// from the profile and carried forward unchanged so the app never clobbers
  /// the CP-set flag. Defaults to true for a brand-new "Other" registrant.
  bool _showInMeetLikeYou = true;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  @override
  void dispose() {
    _organisationDebounce?.cancel();
    _arabicName.dispose();
    _englishName.dispose();
    _jobTitle.dispose();
    _jobTitleArabic.dispose();
    _placeOfBirth.dispose();
    _nationalId.dispose();
    _documentNumber.dispose();
    _saudiMobile.dispose();
    _internationalMobile.dispose();
    _plate.dispose();
    _plateDigits.dispose();
    _organisationSearch.dispose();
    super.dispose();
  }

  // ---- Loading -------------------------------------------------------------

  Future<void> _load() async {
    final repo = ref.read(profileRepositoryProvider);
    setState(() {
      _loading = true;
      _loadError = null;
    });
    try {
      final results = await Future.wait(<Future<Object>>[
        repo.getMyProfile(),
        repo.getCountries(),
        repo.getProfileTypes(isVisitor: _isVisitorType),
        repo.searchOrganisations(top: 20),
      ]);
      if (!mounted) {
        return;
      }
      setState(() {
        _countries = results[1] as List<CountryItem>;
        _profileTypes = results[2] as List<ProfileTypeItem>;
        _organisationResults = results[3] as List<OrganisationItem>;
        _applyProfile(results[0] as UserProfileResponse);
        _lockVisitorProfileType();
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      final l10n = AppL10n.of(context);
      setState(() {
        _loadError = failure.localizedMessage(l10n);
        _loading = false;
      });
    }
  }

  /// Pre-fills the form from an existing profile (empty on a first-time open).
  /// Guards every picker value against its lookup so a stale id never crashes a
  /// dropdown / leaves an unselectable chip.
  void _applyProfile(UserProfileResponse profile) {
    _arabicName.text = profile.arabicName;
    _englishName.text = profile.englishName;
    _jobTitle.text = profile.jobTitle ?? '';
    _jobTitleArabic.text = profile.jobTitleArabic ?? '';
    _placeOfBirth.text = profile.placeOfBirth;
    _birthRegionCode = regionByName(profile.placeOfBirth)?.code;
    _nationalId.text = profile.nationalId ?? '';
    if ((profile.iqamaNumber ?? '').isNotEmpty) {
      _docType = _DocType.iqama;
      _documentNumber.text = profile.iqamaNumber!;
    } else if ((profile.passportNumber ?? '').isNotEmpty) {
      _docType = _DocType.passport;
      _documentNumber.text = profile.passportNumber!;
    }
    _saudiMobile.text = profile.saudiMobile ?? '';
    _internationalMobile.text = profile.internationalMobile ?? '';
    _setPlateFromCode(profile.plateNumber);
    // D-373 defaults — Male and Saudi Arabia pre-selected on a first-time
    // (empty) profile; a saved profile keeps its own values.
    _gender = profile.gender == AppGender.unspecified
        ? AppGender.male
        : profile.gender;
    _hasExistingIdImage = profile.hasIdImage;
    _hasExistingAvatar = profile.hasAvatar;

    final code = profile.nationalityCode;
    _nationalityCode = _countries.any((c) => c.code == code)
        ? code
        : (_countries.any((c) => c.code == 'SA') ? 'SA' : null);

    final typeId = profile.profileTypeId;
    _profileTypeId = _profileTypes.any((t) => t.id == typeId) ? typeId : null;

    _organisationId = profile.organisationId;
    _organisationLabel = null;

    if ((profile.dateOfBirth ?? '').isNotEmpty) {
      _dateOfBirth = DateTime.tryParse(profile.dateOfBirth!);
    }

    // Carried forward to the interests screen (Page 007‑01) for pre-selection.
    _existingInterestIds = profile.interestIds;
    _showInMeetLikeYou = profile.showInMeetLikeYou;
  }

  // ---- نوع التسجيل (Visitor / Other) ---------------------------------------

  /// C5 (D-371) — under the Visitor tab the profile type is locked to the
  /// single seeded **"Normal" (عادي)** type: no picker is shown and the id is
  /// auto-assigned (overriding any prefill — an admin-assigned tier still wins
  /// server-side via the D-190 precedence). Falls back to the only row when
  /// the lookup has exactly one; an empty lookup leaves null (admin assigns).
  void _lockVisitorProfileType() {
    if (!_isVisitorType) {
      return;
    }
    final normal =
        _profileTypes.where((t) => t.name == 'Normal').toList();
    if (normal.isNotEmpty) {
      _profileTypeId = normal.first.id;
    } else if (_profileTypes.length == 1) {
      _profileTypeId = _profileTypes.first.id;
    } else {
      _profileTypeId = null;
    }
  }

  /// Switching the type re-filters the ProfileType picker (`?isVisitor=`) and
  /// drops any now-invalid ProfileType selection (Page_007 L-3). Under
  /// Visitor the picker is hidden and the type auto-locks to Normal (C5).
  Future<void> _onTypeChanged(bool isVisitor) async {
    if (isVisitor == _isVisitorType) {
      return;
    }
    setState(() {
      _isVisitorType = isVisitor;
      _profileTypeId = null;
      _profileTypes = const <ProfileTypeItem>[];
    });
    await _fetchProfileTypes();
  }

  /// D-375 — the picker fetch with a visible state machine: loading spinner
  /// while in flight, inline retry on failure. Pre-D-375 a failure here
  /// silently hid the الفئة (category) field (the owner-reported "removed list").
  Future<void> _fetchProfileTypes() async {
    setState(() {
      _profileTypesLoading = true;
      _profileTypesFailed = false;
    });
    final repo = ref.read(profileRepositoryProvider);
    try {
      final types = await repo.getProfileTypes(isVisitor: _isVisitorType);
      if (!mounted) {
        return;
      }
      setState(() {
        _profileTypes = types;
        _profileTypesLoading = false;
        _lockVisitorProfileType();
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _profileTypesLoading = false;
        _profileTypesFailed = true;
      });
    }
  }

  // ---- Organisation typeahead ---------------------------------------------

  void _onOrganisationSearchChanged(String value) {
    _organisationDebounce?.cancel();
    _organisationDebounce = Timer(
      const Duration(milliseconds: 350),
      () => unawaited(_runOrganisationSearch(value)),
    );
  }

  Future<void> _runOrganisationSearch(String value) async {
    // D-375 — the typeahead surfaces its fetch state: a spinner while in
    // flight and a visible retry on failure (a failed search previously
    // read as "no matches", which is misleading).
    setState(() {
      _organisationSearching = true;
      _organisationSearchFailed = false;
    });
    final repo = ref.read(profileRepositoryProvider);
    try {
      final results = await repo.searchOrganisations(search: value, top: 20);
      if (!mounted) {
        return;
      }
      setState(() {
        _organisationResults = results;
        _organisationSearching = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _organisationSearching = false;
        _organisationSearchFailed = true;
      });
    }
  }

  void _selectOrganisation(OrganisationItem organisation, AppL10n l10n) {
    setState(() {
      _organisationId = organisation.id;
      _organisationLabel = l10n.isArabic
          ? organisation.nameAr
          : (organisation.nameEn ?? organisation.nameAr);
      _organisationResults = const <OrganisationItem>[];
    });
  }

  void _clearOrganisation() {
    setState(() {
      _organisationId = null;
      _organisationLabel = null;
      _organisationSearch.clear();
    });
  }

  // ---- Date of birth + ID image -------------------------------------------

  Future<void> _pickDateOfBirth() async {
    final now = DateTime.now();
    final earliest = DateTime(now.year - 120);
    final latest = DateTime(now.year - 18, now.month, now.day);
    final picked = await showDatePicker(
      context: context,
      initialDate: _dateOfBirth ?? latest,
      firstDate: earliest,
      lastDate: latest,
    );
    if (picked != null && mounted) {
      setState(() => _dateOfBirth = picked);
    }
  }

  /// "Upload ID" — the ID DOCUMENT is picked from the gallery/library (a
  /// national-ID / Iqama / passport scan), so there is no live-camera or
  /// face-detection step here. Mandatory for every registrant; the Next gate
  /// reports a missing image.
  Future<void> _pickIdImage() async {
    try {
      final file = await ImagePicker().pickImage(source: ImageSource.gallery);
      if (file == null) {
        return;
      }
      final bytes = await file.readAsBytes();
      if (!mounted) {
        return;
      }
      setState(() {
        _idImageBytes = bytes;
        _idImageName = file.name;
      });
    } catch (_) {
      // The gallery is unavailable — the required-ID gate on Next reports it.
    }
  }

  /// "Face photo" — the live face capture (→ the profile avatar). Owner
  /// directive: reuse the existing **face-detection page** (the guided
  /// liveness screen, `identityVerification`, Page 103) the My-Area avatar
  /// already uses and that runs reliably — NOT a direct camera picker. That
  /// page owns the camera-permission request and the on-device face + liveness
  /// check (live-only, no gallery fallback — D-662); the returned selfie
  /// becomes the avatar and is shown at the top of the card immediately.
  /// Mandatory for men, optional for women. Route 103 is universal-auth (D-694)
  /// so a pending sign-up account reaches it instead of bouncing home.
  Future<void> _pickFacePhoto() async {
    final selfie = await context
        .pushNamed<CapturedSelfie>(RouteNames.identityVerification);
    if (selfie == null || !mounted) {
      return;
    }
    setState(() {
      _faceImageBytes = selfie.bytes;
      _faceImageName = selfie.filename;
    });
  }

  void _removeIdImage() {
    setState(() {
      _idImageBytes = null;
      _idImageName = null;
    });
  }

  // ---- Next (carry the draft to the interests screen) ----------------------

  /// Validates the data fields, uploads the images and **saves the profile here**
  /// (D-684, profile-first) so any server error — the name in particular —
  /// surfaces on THIS screen, not two steps later on interests. Only on a clean
  /// save does it carry the [SignUpProfileDraft] to the interests screen
  /// (Page 007‑01), where the interests are added in a second save.
  Future<void> _next() async {
    if (_saving) {
      return;
    }
    setState(() => _triedSubmit = true);
    final formValid = _formKey.currentState?.validate() ?? false;
    final dateOfBirthValid = _dateOfBirth != null;
    // B3 — D-221 (الجهة): organisation is required (server enforces it too).
    final organisationValid = _organisationId != null;
    // D-373 — nationality drives the document section and is required server-
    // side; the picker is not a FormField, so its inline error (line ~985)
    // must also gate Next, otherwise an empty code reaches the server (400).
    final nationalityValid = _nationalityCode != null;
    // D-723 — place of birth is required. Non-Saudi uses the free-text field
    // (caught by the form validator); Saudi uses the region picker (not a
    // FormField), so its required gate lives here.
    final placeOfBirthValid = !_isSaudi || _birthRegionCode != null;
    // D-471 — the profile-type picker is now a searchable field, not a FormField,
    // so its required gate (only when the "Other" picker is actually shown — never
    // when Visitor-locked, loading, failed or empty, per L-6) lives here.
    final profileTypePickerShown = !_isVisitorType &&
        !_profileTypesLoading &&
        !_profileTypesFailed &&
        _profileTypes.isNotEmpty;
    final profileTypeValid = !profileTypePickerShown || _profileTypeId != null;
    // Two-photo split — the ID DOCUMENT is mandatory for every registrant; the
    // FACE photo is mandatory for men and optional for women. Either a fresh
    // pick or an already-stored image satisfies each.
    final idImageValid = _idImageBytes != null || _hasExistingIdImage;
    final faceImageValid = _gender != AppGender.male ||
        _faceImageBytes != null ||
        _hasExistingAvatar;
    if (!formValid ||
        !dateOfBirthValid ||
        !organisationValid ||
        !nationalityValid ||
        !placeOfBirthValid ||
        !profileTypeValid ||
        !idImageValid ||
        !faceImageValid) {
      // D-434 — surface a clear message instead of failing silently, so the
      // user notices the highlighted missing items (e.g. the male ID photo).
      setState(() {});
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(content: Text(AppL10n.of(context).completeProfilePrompt)),
        );
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() {
      _saving = true;
      _saveError = null;
    });
    final repo = ref.read(profileRepositoryProvider);
    try {
      // The server rejects a profile with no stored ID document (everyone) or,
      // for a male, no stored face photo — so the images must land BEFORE the
      // profile save. A failed mandatory upload blocks with a clear message.
      final idBytes = _idImageBytes;
      final idName = _idImageName;
      if (idBytes != null && idName != null) {
        try {
          await repo.uploadIdImage(bytes: idBytes, filename: idName);
        } on ApiFailure {
          if (!mounted) return;
          setState(() => _saveError = l10n.idImageUploadFailed);
          return;
        }
      }
      final faceBytes = _faceImageBytes;
      final faceName = _faceImageName;
      if (faceBytes != null && faceName != null) {
        try {
          await repo.uploadAvatar(bytes: faceBytes, filename: faceName);
          ref.read(avatarBustProvider.notifier).state++;
        } on ApiFailure {
          if (!mounted) return;
          if (_gender == AppGender.male) {
            setState(() => _saveError = l10n.facePhotoUploadFailed);
            return;
          }
          // Optional for women — fall through and save.
        }
      }

      // Save the profile fields NOW — the server validates the name (etc.) and
      // any error is shown on this screen (interests are added in a 2nd save).
      await repo.upsertMyProfile(_buildRequest());
      if (!mounted) return;
      final draft = SignUpProfileDraft(
        request: _buildRequest(),
        idImageBytes: _idImageBytes,
        idImageName: _idImageName,
        faceImageBytes: _faceImageBytes,
        faceImageName: _faceImageName,
      );
      context.pushNamed(RouteNames.signUpInterests, extra: draft);
    } on ApiFailure catch (failure) {
      if (!mounted) return;
      setState(() => _saveError = failure.localizedMessage(l10n));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  /// Builds the request from the data fields. `interestIds` carries any existing
  /// picks (for pre-selection); the interests screen replaces it via `copyWith`
  /// before the save.
  UpsertUserProfileRequest _buildRequest() {
    final isSaudi = _isSaudi;
    return UpsertUserProfileRequest(
      profileTypeId: _profileTypeId,
      interestIds: _existingInterestIds,
      arabicName: _arabicName.text.trim(),
      englishName: _englishName.text.trim(),
      jobTitle: _emptyToNull(_jobTitle.text),
      jobTitleArabic: _emptyToNull(_jobTitleArabic.text),
      nationalityCode: _nationalityCode ?? '',
      dateOfBirth: _dateOfBirth == null ? null : _formatDate(_dateOfBirth!),
      placeOfBirth: _placeOfBirth.text.trim(),
      isSaudi: isSaudi,
      nationalId: isSaudi ? _emptyToNull(_nationalId.text) : null,
      iqamaNumber: !isSaudi && _docType == _DocType.iqama
          ? _emptyToNull(_documentNumber.text)
          : null,
      passportNumber: !isSaudi && _docType == _DocType.passport
          ? _emptyToNull(_documentNumber.text)
          : null,
      // Submit the canonical phone — Arabic digits folded, a leading `00`
      // rewritten to `+` — so the value matches the server's `+`-only shapes.
      saudiMobile:
          isSaudi ? _emptyToNull(normalizePhone(_saudiMobile.text)) : null,
      internationalMobile: !isSaudi
          ? _emptyToNull(normalizePhone(_internationalMobile.text))
          : null,
      plateNumber: _emptyToNull(_plate.text),
      organisationId: _organisationId,
      gender: _gender,
      showInMeetLikeYou: _showInMeetLikeYou,
    );
  }

  // ---- Validators (client mirror of UpsertUserProfileRequestValidator) -----

  /// Shared name rule for both scripts (mirror UpsertUserProfileRequestValidator):
  /// required, [lettersOnly] only, and 2–4 whitespace-separated parts. The pure
  /// shape rules live in `core/validation/name_validation.dart`; this keeps the
  /// per-script l10n message ([lettersOnlyMsg]) and the order of checks.
  String? _validateName(String? value, RegExp lettersOnly, String lettersOnlyMsg) {
    final l10n = AppL10n.of(context);
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

  String? _validateArabicName(String? value) => _validateName(
        value,
        arabicNameLettersOnly,
        AppL10n.of(context).arabicNameLettersOnly,
      );

  String? _validateEnglishName(String? value) => _validateName(
        value,
        englishNameLettersOnly,
        AppL10n.of(context).englishNameLettersOnly,
      );

  String? _validateNationalId(String? value) {
    final id = value?.trim() ?? '';
    return isValidNationalId(id)
        ? null
        : AppL10n.of(context).nationalIdInvalid;
  }

  String? _validateDocumentNumber(String? value) {
    final l10n = AppL10n.of(context);
    final number = value?.trim() ?? '';
    if (number.isEmpty) {
      return l10n.documentRequired;
    }
    if (_docType == _DocType.iqama) {
      return isValidIqama(number) ? null : l10n.iqamaInvalid;
    }
    return isValidPassport(number) ? null : l10n.passportInvalid;
  }

  /// C4 (D-371) — the standard shapes live in `phone_validation.dart`,
  /// mirroring `UpsertUserProfileRequestValidator` exactly.
  String? _validateSaudiMobile(String? value) {
    final phone = value?.trim() ?? '';
    // D-723 — mobile is required (only the plate number stays optional).
    if (phone.isEmpty) {
      return AppL10n.of(context).mobileRequired;
    }
    return isStandardSaudiMobile(phone)
        ? null
        : AppL10n.of(context).saudiMobileInvalid;
  }

  String? _validateInternationalMobile(String? value) {
    final phone = value?.trim() ?? '';
    // D-723 — mobile is required (only the plate number stays optional).
    if (phone.isEmpty) {
      return AppL10n.of(context).mobileRequired;
    }
    return isStandardInternationalMobile(phone)
        ? null
        : AppL10n.of(context).internationalMobileInvalid;
  }

  /// C6 (D-371) — optional plate; Saudi standard when filled (the shape
  /// lives in `plate_validation.dart`, mirroring the server).
  String? _validatePlate(String? value) {
    final plate = value?.trim() ?? '';
    if (plate.isEmpty) {
      return null;
    }
    return isStandardPlateNumber(plate)
        ? null
        : AppL10n.of(context).plateNumberInvalid;
  }

  void _back() {
    if (context.canPop()) {
      context.pop();
      return;
    }
    context.go('/');
  }

  // ---- Build ---------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // D-547 — watch the region lookup here so the place-of-birth picker rebuilds
    // when the API list lands (the picker reads it via ref.read in its handler,
    // so build must own the dependency for the closed-field name to refresh).
    ref.watch(regionsProvider);
    return SimfFormScaffold(
      pinnedHeader: true,
      onBack: _back,
      // The profile screen's sweep sits at the top-right, not the auth default.
      sweep: const SimfAuthSweep(top: -180, left: null, right: -40),
      child: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_loadError != null) {
      return _buildLoadError(l10n);
    }
    // The beige form card (Figma 168:2977) holding the whole form.
    return Form(
      key: _formKey,
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          0,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        // §13.7 — form content caps at the 560 form width (MaxWidthBody), so it
        // fills a phone but doesn't stretch edge-to-edge on a tablet.
        child: MaxWidthBody(
          maxWidth: 560,
          // A Material (not a decorated Container) so the ListTile/switch
          // ink inside the card renders above the beige fill.
          child: Material(
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
                      // The captured face photo replaces the placeholder
                      // person icon at the top once taken (owner follow-up).
                      SignUpVisitorHeaderAvatar(bytes: _faceImageBytes),
                    ],
                  ),
                  const SizedBox(height: SimfTokens.space6),
                  // نوع التسجيل (Visitor / Other) — beige tabs (Figma 505:1075).
                  BeigeTabs(
                    options: <String>[
                      l10n.signUpTypeVisitor,
                      l10n.signUpTypeOther,
                    ],
                    selectedIndex: _isVisitorType ? 0 : 1,
                    onChanged: (index) =>
                        unawaited(_onTypeChanged(index == 0)),
                  ),
                  const SizedBox(height: SimfTokens.space6),
                  _buildProfileTypeField(l10n),
                  const SizedBox(height: SimfTokens.space4),
                  SimfLabeledTextField(
                    label: l10n.arabicNameLabel,
                    controller: _arabicName,
                    maxLength: 50,
                    // Arabic letters + spaces only — block other scripts at
                    // the keystroke so the field can never hold mixed text.
                    inputFormatters: <TextInputFormatter>[
                      FilteringTextInputFormatter.allow(RegExp(r'[ء-ي\s]')),
                    ],
                    validator: _validateArabicName,
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  SimfLabeledTextField(
                    label: l10n.englishNameLabel,
                    controller: _englishName,
                    maxLength: 50,
                    textDirection: TextDirection.ltr,
                    // Latin letters + spaces only.
                    inputFormatters: <TextInputFormatter>[
                      FilteringTextInputFormatter.allow(RegExp(r'[A-Za-z\s]')),
                    ],
                    validator: _validateEnglishName,
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  SimfFieldLabel(l10n.genderLabel),
                  const SizedBox(height: SimfTokens.space2),
                  GenderPillsField(
                    gender: _gender,
                    onChanged: (value) =>
                        setState(() => _gender = value),
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  _buildOrganisationField(l10n),
                  const SizedBox(height: SimfTokens.space4),
                  SimfLabeledTextField(
                    label: l10n.jobTitleLabel,
                    controller: _jobTitle,
                    maxLength: 100,
                    textDirection: TextDirection.ltr,
                    // Latin letters + spaces only — mirror the English name
                    // field so the English job title can never hold Arabic.
                    inputFormatters: <TextInputFormatter>[
                      FilteringTextInputFormatter.allow(RegExp(r'[A-Za-z\s]')),
                    ],
                    // D-723 — required (only the plate number stays optional).
                    validator: (String? v) => (v == null || v.trim().isEmpty)
                        ? l10n.jobTitleRequired
                        : null,
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  // Optional Arabic job title — the backend + CP already
                  // carry UserProfile.JobTitleArabic; captured here too
                  // (server validates only when present).
                  SimfLabeledTextField(
                    label: l10n.jobTitleArabicLabel,
                    controller: _jobTitleArabic,
                    maxLength: 100,
                    textDirection: TextDirection.rtl,
                    // Arabic letters + spaces only — mirror the Arabic name
                    // field so the Arabic job title can never hold Latin text.
                    inputFormatters: <TextInputFormatter>[
                      FilteringTextInputFormatter.allow(RegExp(r'[ء-ي\s]')),
                    ],
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  _buildNationalityField(l10n),
                  const SizedBox(height: SimfTokens.space4),
                  // D-373 — the Saudi switch is gone: the nationality pick
                  // drives national-ID vs iqama/passport (SA → national ID).
                  ..._buildDocumentFields(l10n),
                  const SizedBox(height: SimfTokens.space4),
                  MobileField(
                    saudi: _isSaudi,
                    controller:
                        _isSaudi ? _saudiMobile : _internationalMobile,
                    validator: _isSaudi
                        ? _validateSaudiMobile
                        : _validateInternationalMobile,
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  DateOfBirthField(
                    displayValue: _dateOfBirth == null
                        ? '—'
                        : _formatDateDisplay(_dateOfBirth!),
                    hasError: _triedSubmit && _dateOfBirth == null,
                    onTap: () => unawaited(_pickDateOfBirth()),
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  _buildPlaceOfBirthField(l10n),
                  const SizedBox(height: SimfTokens.space4),
                  // D-373 — the plate is the last input before the attach.
                  _buildPlateField(l10n),
                  const SizedBox(height: SimfTokens.space4),
                  _buildIdImageField(l10n),
                  const SizedBox(height: SimfTokens.space4),
                  _buildFacePhotoField(l10n),
                  if (_saveError != null) ...<Widget>[
                    Text(
                      _saveError!,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        color: SimfTokens.danger,
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: SimfTokens.space3),
                  ],
                  TermsAndNextButtons(onNext: _next, busy: _saving),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildLoadError(AppL10n l10n) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              l10n.profileLoadError,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.txtSecondary),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(
              onPressed: () => unawaited(_load()),
              child: Text(l10n.retryLabel),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildProfileTypeField(AppL10n l10n) {
    // C5 (D-371) — Visitor is locked to "Normal": no picker rendered, the
    // id is auto-assigned by _lockVisitorProfileType.
    if (_isVisitorType) {
      return const SizedBox.shrink();
    }
    // D-375 — under "Other" the field is ALWAYS visible: loading, inline
    // retry on failure/empty, or the loaded dropdown. Never silently hidden.
    if (_profileTypesLoading) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          SimfFieldLabel(l10n.profileTypeLabel),
          const SizedBox(height: SimfTokens.space2),
          InputDecorator(
            decoration: simfFieldDecoration(),
            child: Row(
              children: <Widget>[
                const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: SimfTokens.accent,
                  ),
                ),
                const SizedBox(width: SimfTokens.space3),
                Text(
                  l10n.loadingLabel,
                  style: const TextStyle(
                    color: SimfTokens.greyText,
                    fontSize: SimfTokens.textMd,
                  ),
                ),
              ],
            ),
          ),
        ],
      );
    }
    if (_profileTypesFailed || _profileTypes.isEmpty) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          SimfFieldLabel(l10n.profileTypeLabel),
          const SizedBox(height: SimfTokens.space2),
          InputDecorator(
            decoration: simfFieldDecoration(),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    l10n.lookupLoadError,
                    style: const TextStyle(
                      color: SimfTokens.danger,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ),
                TextButton(
                  key: const ValueKey<String>('profileTypeRetry'),
                  onPressed: () => unawaited(_fetchProfileTypes()),
                  style:
                      TextButton.styleFrom(foregroundColor: SimfTokens.accent),
                  child: Text(l10n.retryLabel),
                ),
              ],
            ),
          ),
        ],
      );
    }
    // D-722 (owner batch item 3) — profile types are few, so this field is a
    // simple dropdown/select instead of the shared full-screen searchable sheet
    // (nationality / birth-region / plate keep the sheet — those lists are long).
    // Still not gated by the form validator; the required check stays in _next()
    // (the value can be null until the user picks). Mirrors the register-visitor
    // dropdowns' idiom (explicit style + chevron, initialValue + onChanged).
    final showError = _triedSubmit && _profileTypeId == null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.profileTypeLabel),
        const SizedBox(height: SimfTokens.space2),
        DropdownButtonFormField<String>(
          key: const ValueKey<String>('profileTypeDropdown'),
          initialValue: _profileTypeId,
          isExpanded: true,
          style: simfInputStyle,
          icon: const Icon(
            Icons.keyboard_arrow_down,
            color: SimfTokens.inputInk,
          ),
          decoration: simfFieldDecoration(
            errorText: showError ? l10n.profileTypeRequired : null,
          ).copyWith(
            // A DropdownButton's dense content floor is a fixed 24px (vs a text
            // field's ~21px line box), so with the shared 15px vertical inset
            // this field renders ~4px taller than the sibling standard fields.
            // Trim the vertical inset so the field lands at their 50px height
            // (24 + 2*13); the 14px horizontal inset is the shared field inset.
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 14,
              vertical: 13,
            ),
          ),
          dropdownColor: SimfTokens.surface,
          hint: Text(
            l10n.profileTypeLabel,
            style: simfInputStyle.copyWith(color: SimfTokens.greyText),
          ),
          items: <DropdownMenuItem<String>>[
            for (final ProfileTypeItem t in _profileTypes)
              DropdownMenuItem<String>(
                value: t.id,
                child: Text(
                  l10n.isArabic ? t.nameArabic : t.name,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
          ],
          onChanged: (String? id) => setState(() => _profileTypeId = id),
        ),
      ],
    );
  }

  /// Opens the shared searchable picker sheet and returns the picked value.
  Future<String?> _openLookupSheet({
    required List<PickerOption> options,
    required String searchHint,
    Key? searchFieldKey,
  }) {
    return showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      backgroundColor: SimfTokens.cardBeige,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(12)),
      ),
      builder: (_) => LookupSearchSheet(
        options: options,
        searchHint: searchHint,
        searchFieldKey: searchFieldKey,
      ),
    );
  }

  /// D-373 — the 57-country list gets the shared searchable picker. Switching
  /// nationality also drives the document section (SA → national ID, else
  /// Iqama/Passport).
  Widget _buildNationalityField(AppL10n l10n) {
    final selected = _countries
        .where((c) => c.code == _nationalityCode)
        .toList();
    final hasValue = selected.isNotEmpty;
    final label = hasValue
        ? (l10n.isArabic ? selected.first.nameArabic : selected.first.name)
        : l10n.nationalityLabel;
    final showError = _triedSubmit && _nationalityCode == null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.nationalityLabel),
        const SizedBox(height: SimfTokens.space2),
        SimfPickerField(
          fieldKey: 'nationalityPicker',
          displayText: label,
          isPlaceholder: !hasValue,
          onTap: () => unawaited(_pickNationality(l10n)),
          errorText: showError ? l10n.nationalityRequired : null,
        ),
      ],
    );
  }

  /// Opens the searchable country sheet and applies the pick. Clearing the
  /// stale national-id/iqama input when the Saudi-ness flips keeps the
  /// derived document section consistent (D-373).
  Future<void> _pickNationality(AppL10n l10n) async {
    final pickedCode = await _openLookupSheet(
      options: <PickerOption>[
        for (final CountryItem c in _countries)
          PickerOption(
            value: c.code,
            label: l10n.isArabic ? c.nameArabic : c.name,
            search: '${c.name} ${c.nameArabic}',
          ),
      ],
      searchHint: l10n.searchCountryHint,
      searchFieldKey: const ValueKey<String>('countrySearchField'),
    );
    if (pickedCode == null || !mounted) {
      return;
    }
    setState(() {
      final wasSaudi = _isSaudi;
      _nationalityCode = pickedCode;
      if (wasSaudi != _isSaudi) {
        _nationalId.clear();
        _documentNumber.clear();
        // D-469 — the birth-location control flips with nationality: becoming
        // Saudi keeps the value only if it matches a region (else the picker
        // starts empty); leaving Saudi keeps it as free text.
        if (_isSaudi) {
          _birthRegionCode = regionByName(_placeOfBirth.text)?.code;
          if (_birthRegionCode == null) {
            _placeOfBirth.clear();
          }
        }
      }
    });
  }

  /// D-547 — the active regions for the birth-location picker. Owner decision:
  /// the data SOURCE is now `GET /app/regions` (the seeded 13 regions), with the
  /// const [saudiRegions] kept as an OFFLINE FALLBACK. On [AsyncData] with a
  /// non-empty list, the API regions win; on loading / error / empty, the const
  /// list is used so the picker never throws on build. Each entry maps to the
  /// shared display shape (code + ar/en names) so the picker + display label
  /// stay identical regardless of source.
  List<_BirthRegionOption> _activeBirthRegions() {
    // ref.read (not watch): this runs from both build helpers and the picker's
    // async handler. build() owns the watch so the field still rebuilds on data.
    final List<RegionItem>? api =
        ref.read(regionsProvider).asData?.value;
    if (api != null && api.isNotEmpty) {
      return <_BirthRegionOption>[
        for (final RegionItem r in api)
          _BirthRegionOption(
            code: r.code,
            // English uses name ?? nameArabic, mirroring SaudiRegion.name.
            english: r.name ?? r.nameArabic,
            arabic: r.nameArabic,
          ),
      ];
    }
    return <_BirthRegionOption>[
      for (final SaudiRegion r in saudiRegions)
        _BirthRegionOption(code: r.code, english: r.english, arabic: r.arabic),
    ];
  }

  /// The active region matching [code] (API or fallback), or null.
  _BirthRegionOption? _birthRegionByCode(String? code) {
    if (code == null) {
      return null;
    }
    for (final _BirthRegionOption r in _activeBirthRegions()) {
      if (r.code == code) {
        return r;
      }
    }
    return null;
  }

  /// D-469/D-470 — opens the shared searchable sheet over the active regions
  /// (D-547: API with const fallback) and stores the picked region's localized
  /// name in [_placeOfBirth].
  Future<void> _pickBirthRegion(AppL10n l10n, bool isArabic) async {
    final regions = _activeBirthRegions();
    final pickedCode = await _openLookupSheet(
      options: <PickerOption>[
        for (final _BirthRegionOption r in regions)
          PickerOption(
            value: r.code,
            label: r.name(isArabic: isArabic),
            search: '${r.arabic} ${r.english}',
          ),
      ],
      searchHint: l10n.placeOfBirthRegionHint,
      searchFieldKey: const ValueKey<String>('birthRegionSearchField'),
    );
    if (pickedCode == null || !mounted) {
      return;
    }
    setState(() {
      _birthRegionCode = pickedCode;
      _placeOfBirth.text =
          _birthRegionByCode(pickedCode)?.name(isArabic: isArabic) ?? '';
    });
  }

  List<Widget> _buildDocumentFields(AppL10n l10n) {
    if (_isSaudi) {
      return <Widget>[
        SimfLabeledTextField(
          label: l10n.nationalIdLabel,
          controller: _nationalId,
          keyboardType: TextInputType.number,
          maxLength: 10,
          // Accept an id typed in Arabic-Indic digits — fold to Western so it
          // validates and submits as `1XXXXXXXXX` (owner 2026-07-06).
          inputFormatters: <TextInputFormatter>[
            const WesternDigitsFormatter(),
            FilteringTextInputFormatter.digitsOnly,
          ],
          validator: _validateNationalId,
        ),
      ];
    }
    return <Widget>[
      SimfFieldLabel(l10n.documentTypeLabel),
      const SizedBox(height: SimfTokens.space2),
      BeigeTabs(
        options: <String>[l10n.iqamaSegment, l10n.passportSegment],
        selectedIndex: _docType == _DocType.iqama ? 0 : 1,
        onChanged: (index) => setState(() {
          _docType = index == 0 ? _DocType.iqama : _DocType.passport;
          _documentNumber.clear();
        }),
      ),
      const SizedBox(height: SimfTokens.space4),
      SimfLabeledTextField(
        label: l10n.documentNumberLabel,
        controller: _documentNumber,
        maxLength: _docType == _DocType.iqama ? 10 : 9,
        // Fold Arabic-Indic digits to Western (letters pass for passports).
        inputFormatters: const <TextInputFormatter>[WesternDigitsFormatter()],
        validator: _validateDocumentNumber,
      ),
    ];
  }

  /// D-469 — birth location: a Saudi registrant picks one of the 13 official
  /// regions from a dropdown; everyone else types it free-form "as in passport".
  /// The selection's localized name is kept in [_placeOfBirth] (the submitted
  /// value), so storage stays the existing free-text field.
  Widget _buildPlaceOfBirthField(AppL10n l10n) {
    final bool isArabic = l10n.isArabic;
    // Keep the stored name in the active locale when a region is selected, so a
    // language toggle re-syncs the submitted value (the dropdown is code-keyed).
    if (_isSaudi && _birthRegionCode != null) {
      final String name =
          _birthRegionByCode(_birthRegionCode)?.name(isArabic: isArabic) ?? '';
      if (_placeOfBirth.text != name) {
        _placeOfBirth.text = name;
      }
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.placeOfBirthLabel),
        const SizedBox(height: SimfTokens.space2),
        if (_isSaudi)
          SimfPickerField(
            fieldKey: 'birthRegionPicker',
            displayText: _birthRegionCode == null
                ? l10n.placeOfBirthRegionHint
                : (_birthRegionByCode(_birthRegionCode)
                        ?.name(isArabic: isArabic) ??
                    l10n.placeOfBirthRegionHint),
            isPlaceholder: _birthRegionCode == null,
            onTap: () => unawaited(_pickBirthRegion(l10n, isArabic)),
            errorText: (_triedSubmit && _birthRegionCode == null)
                ? l10n.placeOfBirthRequired
                : null,
          )
        else
          TextFormField(
            controller: _placeOfBirth,
            maxLength: 128,
            style: simfInputStyle,
            autovalidateMode: AutovalidateMode.onUserInteraction,
            // D-723 — place of birth is required for non-Saudi registrants too.
            validator: (String? v) => (v == null || v.trim().isEmpty)
                ? l10n.placeOfBirthRequired
                : null,
            decoration: simfFieldDecoration(
              counterText: '',
              hintText: l10n.placeOfBirthPassportHint,
            ),
          ),
      ],
    );
  }

  /// C6 (D-371/D-459) — رقم اللوحة: optional. Rendered as three letter
  /// dropdowns (the official 17 Saudi plate letters, shown "Arabic · Latin")
  /// plus a 1–4 digit field; the picks are assembled into [_plate] and
  /// validated against the shared `isStandardPlateNumber`.
  Widget _buildPlateField(AppL10n l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.plateNumberLabel),
        const SizedBox(height: SimfTokens.space2),
        Row(
          textDirection: TextDirection.ltr,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Expanded(
              child: _plateLetterField(
                l10n,
                _plateLetter1,
                (String? v) => _plateLetter1 = v,
                position: 1,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: _plateLetterField(
                l10n,
                _plateLetter2,
                (String? v) => _plateLetter2 = v,
                position: 2,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: _plateLetterField(
                l10n,
                _plateLetter3,
                (String? v) => _plateLetter3 = v,
                position: 3,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            SizedBox(
              // Sized for exactly the 4 digits, so the three letter pickers
              // (Expanded) absorb the freed width and show the picked letter.
              width: 92,
              // a11y: name the digit field (its hint vanishes on input).
              child: Semantics(
                label: l10n.plateDigitsLabel,
                textField: true,
                child: TextFormField(
                  controller: _plateDigits,
                  textDirection: TextDirection.ltr,
                  maxLength: 4,
                  keyboardType: TextInputType.number,
                  inputFormatters: <TextInputFormatter>[
                    const WesternDigitsFormatter(),
                    FilteringTextInputFormatter.digitsOnly,
                  ],
                  style: simfInputStyle,
                  autovalidateMode: AutovalidateMode.onUserInteraction,
                  onChanged: (_) => setState(_syncPlate),
                  validator: (_) => _validatePlate(_plate.text),
                  decoration: simfFieldDecoration(
                    counterText: '',
                    hintText: l10n.plateDigitsHint,
                  ),
                ),
              ),
            ),
          ],
        ),
      ],
    );
  }

  /// One of the three plate-letter pickers: the 17 letters, each shown as
  /// "Arabic · Latin", stored by its Latin [SaudiPlateLetter.code]. Uses the
  /// same searchable sheet as the nationality + region fields (D-470). [position]
  /// (1–3) gives each field a distinct accessible name ("Letter 1/2/3") so a
  /// screen reader can tell them apart (a11y).
  Widget _plateLetterField(
    AppL10n l10n,
    String? value,
    ValueChanged<String?> onPicked, {
    required int position,
  }) {
    final letter = value == null ? null : _plateLetterByCode(value);
    return Semantics(
      label: '${l10n.plateLetterHint} $position',
      child: SimfPickerField(
        fieldKey: 'plateLetter$position',
        displayText: letter == null
            ? l10n.plateLetterHint
            : '${letter.arabic} · ${letter.english}',
        isPlaceholder: letter == null,
        onTap: () => unawaited(_pickPlateLetter(l10n, position, onPicked)),
        showChevron: false,
      ),
    );
  }

  static SaudiPlateLetter? _plateLetterByCode(String code) {
    for (final SaudiPlateLetter letter in saudiPlateLetters) {
      if (letter.code == code) {
        return letter;
      }
    }
    return null;
  }

  /// Opens the shared searchable sheet over the 17 official plate letters (shown
  /// "Arabic · Latin") and stores the picked Latin code, then re-assembles the
  /// plate.
  Future<void> _pickPlateLetter(
    AppL10n l10n,
    int position,
    ValueChanged<String?> onPicked,
  ) async {
    final pickedCode = await _openLookupSheet(
      options: <PickerOption>[
        for (final SaudiPlateLetter letter in saudiPlateLetters)
          PickerOption(
            value: letter.code,
            label: '${letter.arabic} · ${letter.english}',
            search: '${letter.arabic} ${letter.english} ${letter.code}',
          ),
      ],
      searchHint: l10n.plateLetterHint,
      searchFieldKey: ValueKey<String>('plateLetterSearch$position'),
    );
    if (pickedCode == null || !mounted) {
      return;
    }
    setState(() {
      onPicked(pickedCode);
      _syncPlate();
    });
  }

  /// Re-assembles [_plate] from the dropdown picks + digits, preserving the
  /// stored order ([_plateDigitsFirst]) so a digits-first plate round-trips
  /// unchanged (D-471 fix). Letters-then-digits is the default for fresh entry.
  /// Empty when nothing is picked — the plate is optional.
  void _syncPlate() {
    _plate.text = assemblePlate(
      letter1: _plateLetter1,
      letter2: _plateLetter2,
      letter3: _plateLetter3,
      digits: _plateDigits.text,
      digitsFirst: _plateDigitsFirst,
    );
  }

  /// Splits a stored plate code into the three letter dropdowns + the digits
  /// field, then refreshes [_plate]. The stored value is first normalised to the
  /// canonical Latin code (so an Arabic-script or pre-D-459 plate still parses);
  /// a value the 17-letter dropdowns can't represent is kept verbatim in [_plate]
  /// so an unrelated profile edit never silently erases it (D-468 review).
  void _setPlateFromCode(String? code) {
    final PlateParts parts = parsePlate(code);
    _plateLetter1 = parts.letter1;
    _plateLetter2 = parts.letter2;
    _plateLetter3 = parts.letter3;
    _plateDigits.text = parts.digits;
    _plateDigitsFirst = parts.digitsFirst;
    final String? override = parts.rawOverride;
    if (override != null) {
      _plate.text = override;
    } else {
      _syncPlate();
    }
  }

  /// "Upload ID" — the mandatory ID-document attachment. The "required" hint
  /// stays hidden until a blocked Next, then shows in danger red — like the
  /// text-field validators, not surfaced up-front in grey (D-674).
  Widget _buildIdImageField(AppL10n l10n) {
    final needsImage = _idImageBytes == null && !_hasExistingIdImage;
    return AttachmentField(
      label: l10n.attachmentsLabel,
      hintText: (_triedSubmit && needsImage) ? l10n.idImageRequired : null,
      hintDanger: true,
      bytes: _idImageBytes,
      round: false,
      attachLabel: l10n.attachFileLabel,
      attachIcon: Icons.add_circle_outline,
      onAttach: () => unawaited(_pickIdImage()),
      attachedName: _idImageName ?? l10n.idImageAttachedLabel,
      actionLabel: l10n.removeLabel,
      onAction: _removeIdImage,
    );
  }

  /// "Face photo" — the live face capture (→ profile avatar). Mandatory for
  /// men, optional for women. Once captured the face shows at the top of the
  /// card and this row confirms it with a Retake. The male-**required** hint
  /// stays hidden until a blocked Next (then danger red, D-674); the
  /// women-**optional** hint is informational, so it stays visible in grey.
  Widget _buildFacePhotoField(AppL10n l10n) {
    final bytes = _faceImageBytes;
    final maleNeedsFace =
        _gender == AppGender.male && bytes == null && !_hasExistingAvatar;
    final showOptionalHint =
        bytes == null && !_hasExistingAvatar && !maleNeedsFace;
    final showRequiredHint = _triedSubmit && maleNeedsFace;
    return AttachmentField(
      label: l10n.facePhotoLabel,
      hintText: showRequiredHint
          ? l10n.facePhotoRequiredForMen
          : (showOptionalHint ? l10n.facePhotoOptionalForWomen : null),
      hintDanger: showRequiredHint,
      bytes: bytes,
      round: true,
      attachLabel: l10n.facePhotoCaptureLabel,
      attachIcon: Icons.photo_camera_outlined,
      onAttach: () => unawaited(_pickFacePhoto()),
      attachedName: l10n.facePhotoCaptured,
      actionLabel: l10n.retakeLabel,
      onAction: () => unawaited(_pickFacePhoto()),
    );
  }

  Widget _buildOrganisationField(AppL10n l10n) {
    if (_organisationId != null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          SimfFieldLabel(l10n.organisationLabel),
          const SizedBox(height: SimfTokens.space2),
          InputDecorator(
            decoration: simfFieldDecoration(),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    _organisationLabel ?? l10n.organisationSelected,
                    style: simfInputStyle,
                  ),
                ),
                TextButton(
                  onPressed: _clearOrganisation,
                  style:
                      TextButton.styleFrom(foregroundColor: SimfTokens.accent),
                  child: Text(l10n.clearLabel),
                ),
              ],
            ),
          ),
        ],
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.organisationLabel),
        const SizedBox(height: SimfTokens.space2),
        TextField(
          controller: _organisationSearch,
          style: simfInputStyle,
          decoration: simfFieldDecoration(
            hintText: l10n.organisationSearchHint,
            prefixIcon:
                const Icon(Icons.search, color: SimfTokens.greyText, size: 18),
            // B3 — D-221: required — flag the empty pick after a submit attempt.
            errorText: (_triedSubmit && _organisationId == null)
                ? l10n.organisationRequired
                : null,
          ),
          onChanged: _onOrganisationSearchChanged,
        ),
        if (_organisationSearch.text.trim().isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space2),
          // D-375 — fetch state first: spinner while searching, retry on
          // failure; "no matches" only describes a COMPLETED empty search.
          if (_organisationSearching)
            Padding(
              padding: const EdgeInsets.all(SimfTokens.space2),
              child: Row(
                children: <Widget>[
                  const SizedBox(
                    width: 14,
                    height: 14,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: SimfTokens.accent,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Text(
                    l10n.loadingLabel,
                    style: const TextStyle(color: SimfTokens.greyText),
                  ),
                ],
              ),
            )
          else if (_organisationSearchFailed)
            Padding(
              padding: const EdgeInsets.all(SimfTokens.space2),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      l10n.lookupLoadError,
                      style: const TextStyle(
                        color: SimfTokens.danger,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                  ),
                  TextButton(
                    key: const ValueKey<String>('organisationRetry'),
                    onPressed: () => unawaited(
                      _runOrganisationSearch(_organisationSearch.text.trim()),
                    ),
                    style: TextButton.styleFrom(
                      foregroundColor: SimfTokens.accent,
                    ),
                    child: Text(l10n.retryLabel),
                  ),
                ],
              ),
            )
          else if (_organisationResults.isEmpty)
            Padding(
              padding: const EdgeInsets.all(SimfTokens.space2),
              child: Text(
                l10n.organisationEmpty,
                style: const TextStyle(color: SimfTokens.greyText),
              ),
            )
          else
            ..._organisationResults.take(8).map(
                  (organisation) => ListTile(
                    dense: true,
                    contentPadding: EdgeInsets.zero,
                    title: Text(
                      l10n.isArabic
                          ? organisation.nameAr
                          : (organisation.nameEn ?? organisation.nameAr),
                      style: const TextStyle(color: SimfTokens.headlineInk),
                    ),
                    subtitle: organisation.city == null
                        ? null
                        : Text(
                            organisation.city!,
                            style: const TextStyle(color: SimfTokens.greyText),
                          ),
                    onTap: () => _selectOrganisation(organisation, l10n),
                  ),
                ),
        ],
      ],
    );
  }

  // ---- Pure helpers --------------------------------------------------------

  static String? _emptyToNull(String value) {
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  static String _formatDate(DateTime date) {
    final year = date.year.toString().padLeft(4, '0');
    final month = date.month.toString().padLeft(2, '0');
    final day = date.day.toString().padLeft(2, '0');
    return '$year-$month-$day';
  }

  /// The on-screen date-of-birth text in Saudi civil order `dd-MM-yyyy` (owner:
  /// every displayed date uses dd-MM-yyyy). The API payload keeps the ISO
  /// `yyyy-MM-dd` wire value via [_formatDate]; only the visible text differs.
  static String _formatDateDisplay(DateTime date) {
    final year = date.year.toString().padLeft(4, '0');
    final month = date.month.toString().padLeft(2, '0');
    final day = date.day.toString().padLeft(2, '0');
    return '$day-$month-$year';
  }
}

/// A single birth-location region for the picker, unifying the API [RegionItem]
/// (D-547) and the const [SaudiRegion] fallback behind one display shape so the
/// picker + label rendering is identical whatever the source. Mirrors
/// `SaudiRegion.name(isArabic:)`.
@immutable
class _BirthRegionOption {
  const _BirthRegionOption({
    required this.code,
    required this.english,
    required this.arabic,
  });

  final String code;
  final String english;
  final String arabic;

  String name({required bool isArabic}) => isArabic ? arabic : english;
}
