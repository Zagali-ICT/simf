import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/localization/locale_controller.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_logo.dart';
import '../myarea/identity_verification_screen.dart' show CapturedSelfie;
import 'data/profile_models.dart';
import 'data/profile_repository.dart';
import 'phone_validation.dart';
import 'plate_validation.dart';
import 'saudi_regions.dart';

const Color _sweepTint = Color(0x0AFFFFFF);
const BorderRadius _radius4 =
    BorderRadius.all(Radius.circular(SimfTokens.radiusSmall));

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
      setState(() {
        _loadError = failure.message;
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
  /// silently hid the التصنيف field (the owner-reported "removed list").
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
    if (picked != null) {
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
  /// page owns the camera-permission request, the on-device face + liveness
  /// check and the gallery fallback; the returned selfie becomes the avatar
  /// and is shown at the top of the card immediately. Mandatory for men,
  /// optional for women.
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

  /// Validates the data fields, then carries the collected profile data (+ the
  /// optional ID image) to the interests screen (Page 007‑01) as a
  /// [SignUpProfileDraft]. **No API write happens here** — the single save fires
  /// on the interests screen once interests are picked (D-332).
  void _next() {
    setState(() => _triedSubmit = true);
    final formValid = _formKey.currentState?.validate() ?? false;
    final dateOfBirthValid = _dateOfBirth != null;
    // B3 — D-221 (الجهة): organisation is required (server enforces it too).
    final organisationValid = _organisationId != null;
    // D-373 — nationality drives the document section and is required server-
    // side; the picker is not a FormField, so its inline error (line ~985)
    // must also gate Next, otherwise an empty code reaches the server (400).
    final nationalityValid = _nationalityCode != null;
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
    final draft = SignUpProfileDraft(
      request: _buildRequest(),
      idImageBytes: _idImageBytes,
      idImageName: _idImageName,
      faceImageBytes: _faceImageBytes,
      faceImageName: _faceImageName,
    );
    context.pushNamed(RouteNames.signUpInterests, extra: draft);
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
      saudiMobile: isSaudi ? _emptyToNull(_saudiMobile.text) : null,
      internationalMobile:
          !isSaudi ? _emptyToNull(_internationalMobile.text) : null,
      plateNumber: _emptyToNull(_plate.text),
      organisationId: _organisationId,
      gender: _gender,
    );
  }

  // ---- Validators (client mirror of UpsertUserProfileRequestValidator) -----

  // Name rules (mirror UpsertUserProfileRequestValidator): the Arabic name must
  // be Arabic letters only, the English name Latin letters only, and a "full
  // name" is 2 to 4 whitespace-separated parts (in that one language).
  static final RegExp _arabicLettersOnly = RegExp(r'^[ء-ي\s]+$');
  static final RegExp _englishLettersOnly = RegExp(r'^[A-Za-z\s]+$');

  /// Shared name rule for both scripts: required, [lettersOnly] only, and 2–4
  /// whitespace-separated parts. [lettersOnlyMsg] is the per-script message.
  String? _validateName(String? value, RegExp lettersOnly, String lettersOnlyMsg) {
    final l10n = AppL10n.of(context);
    final name = value?.trim() ?? '';
    if (name.isEmpty) {
      return l10n.requiredField;
    }
    if (!lettersOnly.hasMatch(name)) {
      return lettersOnlyMsg;
    }
    final parts = name.split(RegExp(r'\s+')).length;
    if (parts < 2 || parts > 4) {
      return l10n.fullNameFourParts;
    }
    return null;
  }

  String? _validateArabicName(String? value) => _validateName(
        value,
        _arabicLettersOnly,
        AppL10n.of(context).arabicNameLettersOnly,
      );

  String? _validateEnglishName(String? value) => _validateName(
        value,
        _englishLettersOnly,
        AppL10n.of(context).englishNameLettersOnly,
      );

  String? _validateNationalId(String? value) {
    final id = value?.trim() ?? '';
    final valid = RegExp(r'^1\d{9}$').hasMatch(id) && _isValidLuhn(id);
    return valid ? null : AppL10n.of(context).nationalIdInvalid;
  }

  String? _validateDocumentNumber(String? value) {
    final l10n = AppL10n.of(context);
    final number = value?.trim() ?? '';
    if (number.isEmpty) {
      return l10n.documentRequired;
    }
    if (_docType == _DocType.iqama) {
      final valid =
          RegExp(r'^2\d{9}$').hasMatch(number) && _isValidLuhn(number);
      return valid ? null : l10n.iqamaInvalid;
    }
    final valid = RegExp(r'^[A-Za-z0-9]{6,9}$').hasMatch(number);
    return valid ? null : l10n.passportInvalid;
  }

  /// C4 (D-371) — the standard shapes live in `phone_validation.dart`,
  /// mirroring `UpsertUserProfileRequestValidator` exactly.
  String? _validateSaudiMobile(String? value) {
    final phone = value?.trim() ?? '';
    if (phone.isEmpty) {
      return null;
    }
    return isStandardSaudiMobile(phone)
        ? null
        : AppL10n.of(context).saudiMobileInvalid;
  }

  String? _validateInternationalMobile(String? value) {
    final phone = value?.trim() ?? '';
    if (phone.isEmpty) {
      return null;
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

  /// The globe button toggles AR ↔ EN and persists the choice (D-363 pattern).
  void _toggleLanguage() {
    final isArabic = ref.read(localeControllerProvider).languageCode == 'ar';
    unawaited(
      ref
          .read(localeControllerProvider.notifier)
          .setLanguage(isArabic ? 'en' : 'ar'),
    );
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
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          // Decorative diagonal sweep behind the header (Figma 168:3180).
          Positioned(
            top: -180,
            right: -40,
            child: Transform.rotate(
              angle: 0.4936, // 28.28°
              child: Container(
                width: 313,
                height: 323,
                decoration: BoxDecoration(
                  color: _sweepTint,
                  borderRadius: BorderRadius.circular(40),
                ),
              ),
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                // Top controls (Figma 627:2398): chevron left, language toggle
                // right — forced LTR so the sides match the frame under RTL.
                Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 8,
                  ),
                  child: Row(
                    textDirection: TextDirection.ltr,
                    children: <Widget>[
                      IconButton(
                        onPressed: _back,
                        icon: const Icon(
                          Icons.arrow_back_ios_new,
                          color: Colors.white,
                          size: 20,
                          textDirection: TextDirection.ltr,
                        ),
                      ),
                      const Spacer(),
                      SizedBox(
                        width: 40,
                        height: 40,
                        child: IconButton(
                          tooltip: l10n.languageToggleLabel,
                          onPressed: _toggleLanguage,
                          style: IconButton.styleFrom(
                            backgroundColor: SimfTokens.navyDeep,
                            shape: const RoundedRectangleBorder(
                              borderRadius: _radius4,
                            ),
                          ),
                          icon: const Icon(
                            Icons.language,
                            color: SimfTokens.accent,
                            size: 24,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                // Forum header (Figma 168:2974) — logo at the inline start.
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    const SimfLogo(size: 44),
                    const SizedBox(width: 16),
                    Flexible(
                      child: Text(
                        l10n.signInForumTitle,
                        style: const TextStyle(
                          fontSize: 24,
                          fontWeight: FontWeight.w500,
                          color: Colors.white,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 24),
                Expanded(child: _buildBody(l10n)),
              ],
            ),
          ),
        ],
      ),
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
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 400),
            // A Material (not a decorated Container) so the ListTile/switch
            // ink inside the card renders above the beige fill.
            child: Material(
              color: SimfTokens.cardBeige,
              borderRadius: _radius4,
              child: Padding(
                padding: const EdgeInsets.all(24),
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
                              fontSize: 24,
                              fontWeight: FontWeight.w600,
                              color: SimfTokens.headlineInk,
                            ),
                          ),
                        ),
                        // The captured face photo replaces the placeholder
                        // person icon at the top once taken (owner follow-up).
                        _buildHeaderAvatar(),
                      ],
                    ),
                    const SizedBox(height: 16),
                    // D-434 — a clear notice that this is the complete-profile
                    // step, so the user pays attention to the required items
                    // (white-on-beige + gold border to stand out from the card).
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: SimfTokens.surface,
                        border: Border.all(color: SimfTokens.accent),
                        borderRadius: _radius4,
                      ),
                      child: Row(
                        children: <Widget>[
                          const Icon(
                            Icons.info_outline,
                            size: 18,
                            color: SimfTokens.accent,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              l10n.completeProfilePrompt,
                              style: const TextStyle(
                                fontSize: 12,
                                color: SimfTokens.inputInk,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 24),
                    // نوع التسجيل (Visitor / Other) — beige tabs (Figma 505:1075).
                    _BeigeTabs(
                      options: <String>[
                        l10n.signUpTypeVisitor,
                        l10n.signUpTypeOther,
                      ],
                      selectedIndex: _isVisitorType ? 0 : 1,
                      onChanged: (index) =>
                          unawaited(_onTypeChanged(index == 0)),
                    ),
                    const SizedBox(height: 24),
                    _buildProfileTypeField(l10n),
                    const SizedBox(height: 16),
                    _FieldLabel(l10n.arabicNameLabel),
                    const SizedBox(height: 8),
                    TextFormField(
                      controller: _arabicName,
                      maxLength: 256,
                      style: _inputStyle,
                      autovalidateMode: AutovalidateMode.onUserInteraction,
                      // Arabic letters + spaces only — block other scripts at
                      // the keystroke so the field can never hold mixed text.
                      inputFormatters: <TextInputFormatter>[
                        FilteringTextInputFormatter.allow(RegExp(r'[ء-ي\s]')),
                      ],
                      validator: _validateArabicName,
                      decoration: _fieldDecoration(counterText: ''),
                    ),
                    const SizedBox(height: 16),
                    _FieldLabel(l10n.englishNameLabel),
                    const SizedBox(height: 8),
                    TextFormField(
                      controller: _englishName,
                      maxLength: 256,
                      textDirection: TextDirection.ltr,
                      style: _inputStyle,
                      autovalidateMode: AutovalidateMode.onUserInteraction,
                      // Latin letters + spaces only.
                      inputFormatters: <TextInputFormatter>[
                        FilteringTextInputFormatter.allow(RegExp(r'[A-Za-z\s]')),
                      ],
                      validator: _validateEnglishName,
                      decoration: _fieldDecoration(counterText: ''),
                    ),
                    const SizedBox(height: 16),
                    _FieldLabel(l10n.genderLabel),
                    const SizedBox(height: 8),
                    _buildGenderPills(l10n),
                    const SizedBox(height: 16),
                    _buildOrganisationField(l10n),
                    const SizedBox(height: 16),
                    _FieldLabel(l10n.jobTitleLabel),
                    const SizedBox(height: 8),
                    TextFormField(
                      controller: _jobTitle,
                      maxLength: 128,
                      style: _inputStyle,
                      decoration: _fieldDecoration(counterText: ''),
                    ),
                    const SizedBox(height: 16),
                    _buildNationalityField(l10n),
                    const SizedBox(height: 16),
                    // D-373 — the Saudi switch is gone: the nationality pick
                    // drives national-ID vs iqama/passport (SA → national ID).
                    ..._buildDocumentFields(l10n),
                    const SizedBox(height: 16),
                    _buildMobileField(l10n),
                    const SizedBox(height: 16),
                    _buildDateOfBirthField(l10n),
                    const SizedBox(height: 16),
                    _buildPlaceOfBirthField(l10n),
                    const SizedBox(height: 16),
                    // D-373 — the plate is the last input before the attach.
                    _buildPlateField(l10n),
                    const SizedBox(height: 16),
                    _buildIdImageField(l10n),
                    const SizedBox(height: 16),
                    _buildFacePhotoField(l10n),
                    const SizedBox(height: 16),
                    // Underlined terms link (Figma 522:2179) — opens Page 009.
                    Center(
                      child: TextButton(
                        onPressed: () => context.pushNamed(RouteNames.terms),
                        style: TextButton.styleFrom(
                          padding: EdgeInsets.zero,
                          minimumSize: Size.zero,
                          tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                          foregroundColor: SimfTokens.navy,
                        ),
                        child: Text(
                          l10n.termsAgreeQuestion,
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                            decoration: TextDecoration.underline,
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(height: 24),
                    FilledButton(
                      onPressed: () => _next(),
                      child: Text(
                        l10n.nextLabel,
                        style: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                  ],
                ),
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
          _FieldLabel(l10n.profileTypeLabel),
          const SizedBox(height: 8),
          InputDecorator(
            decoration: _fieldDecoration(),
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
                const SizedBox(width: 12),
                Text(
                  l10n.loadingLabel,
                  style: const TextStyle(
                    color: SimfTokens.greyText,
                    fontSize: 14,
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
          _FieldLabel(l10n.profileTypeLabel),
          const SizedBox(height: 8),
          InputDecorator(
            decoration: _fieldDecoration(),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    l10n.lookupLoadError,
                    style: const TextStyle(
                      color: SimfTokens.danger,
                      fontSize: 12,
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
    // D-471 — the same searchable bottom-sheet picker as nationality / birth-
    // region / plate, so every lookup field on the form is identical. Under
    // "Other" a pick is required (the empty-lookup case returns above per L-6);
    // the picker is not a FormField, so the required gate lives in _next().
    final selected =
        _profileTypes.where((ProfileTypeItem t) => t.id == _profileTypeId).toList();
    final hasValue = selected.isNotEmpty;
    final label = hasValue
        ? (l10n.isArabic ? selected.first.nameArabic : selected.first.name)
        : l10n.profileTypeLabel;
    final showError = _triedSubmit && _profileTypeId == null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.profileTypeLabel),
        const SizedBox(height: 8),
        _searchPickerField(
          fieldKey: 'profileTypePicker',
          displayText: label,
          isPlaceholder: !hasValue,
          onTap: () => unawaited(_pickProfileType(l10n)),
          errorText: showError ? l10n.profileTypeRequired : null,
        ),
      ],
    );
  }

  /// Gender as the design's two radio pills (Figma 522:2150) — white pills on
  /// the beige card, an 18 px gold-ringed radio that fills when selected.
  Widget _buildGenderPills(AppL10n l10n) {
    return Row(
      children: <Widget>[
        Expanded(
          child: _RadioPill(
            label: l10n.genderMale,
            selected: _gender == AppGender.male,
            onTap: () => setState(() => _gender = AppGender.male),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _RadioPill(
            label: l10n.genderFemale,
            selected: _gender == AppGender.female,
            onTap: () => setState(() => _gender = AppGender.female),
          ),
        ),
      ],
    );
  }

  /// The shared "tap to open the searchable sheet" field chrome — the same look
  /// (beige field + down-arrow) the nationality, birth-region and plate-letter
  /// pickers all use (D-373/D-469/D-470). [displayText] is the selected label or
  /// the placeholder ([isPlaceholder] greys it).
  Widget _searchPickerField({
    required String fieldKey,
    required String displayText,
    required bool isPlaceholder,
    required VoidCallback onTap,
    String? errorText,
    bool showChevron = true,
  }) {
    return InkWell(
      key: ValueKey<String>(fieldKey),
      onTap: onTap,
      borderRadius: _radius4,
      child: InputDecorator(
        decoration: _fieldDecoration(
          errorText: errorText,
          // The narrow plate-letter boxes drop the arrow so the picked
          // "Arabic · Latin" letter has the full width to show (D-459).
          suffixIcon: showChevron
              ? const Icon(
                  Icons.keyboard_arrow_down,
                  color: SimfTokens.greyText,
                )
              : null,
        ),
        child: Text(
          displayText,
          style: isPlaceholder
              ? _inputStyle.copyWith(color: SimfTokens.greyText)
              : _inputStyle,
          overflow: TextOverflow.ellipsis,
        ),
      ),
    );
  }

  /// Opens the shared searchable picker sheet and returns the picked value.
  Future<String?> _openLookupSheet({
    required List<_PickerOption> options,
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
      builder: (_) => _LookupSearchSheet(
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
        _FieldLabel(l10n.nationalityLabel),
        const SizedBox(height: 8),
        _searchPickerField(
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
      options: <_PickerOption>[
        for (final CountryItem c in _countries)
          _PickerOption(
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

  /// D-469/D-470 — opens the shared searchable sheet over the 13 Saudi regions
  /// and stores the picked region's localized name in [_placeOfBirth].
  Future<void> _pickBirthRegion(AppL10n l10n, bool isArabic) async {
    final pickedCode = await _openLookupSheet(
      options: <_PickerOption>[
        for (final SaudiRegion r in saudiRegions)
          _PickerOption(
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
      _placeOfBirth.text = regionByCode(pickedCode)?.name(isArabic: isArabic) ?? '';
    });
  }

  /// D-471 — opens the shared searchable sheet over the loaded "Other" profile
  /// types and stores the picked id. Mirrors the nationality / birth-region
  /// pickers so every lookup field uses the identical sheet.
  Future<void> _pickProfileType(AppL10n l10n) async {
    final picked = await _openLookupSheet(
      options: <_PickerOption>[
        for (final ProfileTypeItem t in _profileTypes)
          _PickerOption(
            value: t.id,
            label: l10n.isArabic ? t.nameArabic : t.name,
            search: '${t.name} ${t.nameArabic}',
          ),
      ],
      searchHint: l10n.profileTypeSearchHint,
      searchFieldKey: const ValueKey<String>('profileTypeSearchField'),
    );
    if (picked == null || !mounted) {
      return;
    }
    setState(() => _profileTypeId = picked);
  }

  List<Widget> _buildDocumentFields(AppL10n l10n) {
    if (_isSaudi) {
      return <Widget>[
        _FieldLabel(l10n.nationalIdLabel),
        const SizedBox(height: 8),
        TextFormField(
          controller: _nationalId,
          keyboardType: TextInputType.number,
          maxLength: 10,
          style: _inputStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: _validateNationalId,
          decoration: _fieldDecoration(counterText: ''),
        ),
      ];
    }
    return <Widget>[
      _FieldLabel(l10n.documentTypeLabel),
      const SizedBox(height: 8),
      _BeigeTabs(
        options: <String>[l10n.iqamaSegment, l10n.passportSegment],
        selectedIndex: _docType == _DocType.iqama ? 0 : 1,
        onChanged: (index) => setState(() {
          _docType = index == 0 ? _DocType.iqama : _DocType.passport;
          _documentNumber.clear();
        }),
      ),
      const SizedBox(height: 16),
      _FieldLabel(l10n.documentNumberLabel),
      const SizedBox(height: 8),
      TextFormField(
        controller: _documentNumber,
        maxLength: _docType == _DocType.iqama ? 10 : 9,
        style: _inputStyle,
        autovalidateMode: AutovalidateMode.onUserInteraction,
        validator: _validateDocumentNumber,
        decoration: _fieldDecoration(counterText: ''),
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
          regionByCode(_birthRegionCode)?.name(isArabic: isArabic) ?? '';
      if (_placeOfBirth.text != name) {
        _placeOfBirth.text = name;
      }
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.placeOfBirthLabel),
        const SizedBox(height: 8),
        if (_isSaudi)
          _searchPickerField(
            fieldKey: 'birthRegionPicker',
            displayText: _birthRegionCode == null
                ? l10n.placeOfBirthRegionHint
                : (regionByCode(_birthRegionCode)?.name(isArabic: isArabic) ??
                    l10n.placeOfBirthRegionHint),
            isPlaceholder: _birthRegionCode == null,
            onTap: () => unawaited(_pickBirthRegion(l10n, isArabic)),
          )
        else
          TextFormField(
            controller: _placeOfBirth,
            maxLength: 128,
            style: _inputStyle,
            decoration: _fieldDecoration(
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
        _FieldLabel(l10n.plateNumberLabel),
        const SizedBox(height: 8),
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
            const SizedBox(width: 8),
            Expanded(
              child: _plateLetterField(
                l10n,
                _plateLetter2,
                (String? v) => _plateLetter2 = v,
                position: 2,
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _plateLetterField(
                l10n,
                _plateLetter3,
                (String? v) => _plateLetter3 = v,
                position: 3,
              ),
            ),
            const SizedBox(width: 8),
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
                    FilteringTextInputFormatter.digitsOnly,
                  ],
                  style: _inputStyle,
                  autovalidateMode: AutovalidateMode.onUserInteraction,
                  onChanged: (_) => setState(_syncPlate),
                  validator: (_) => _validatePlate(_plate.text),
                  decoration: _fieldDecoration(
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
      child: _searchPickerField(
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
      options: <_PickerOption>[
        for (final SaudiPlateLetter letter in saudiPlateLetters)
          _PickerOption(
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
    final String letters =
        '${_plateLetter1 ?? ''}${_plateLetter2 ?? ''}${_plateLetter3 ?? ''}';
    final String digits = _plateDigits.text.trim();
    if (letters.isEmpty && digits.isEmpty) {
      _plate.text = '';
    } else {
      _plate.text = _plateDigitsFirst ? '$digits$letters' : '$letters$digits';
    }
  }

  /// Splits a stored plate code into the three letter dropdowns + the digits
  /// field, then refreshes [_plate]. The stored value is first normalised to the
  /// canonical Latin code (so an Arabic-script or pre-D-459 plate still parses);
  /// a value the 17-letter dropdowns can't represent is kept verbatim in [_plate]
  /// so an unrelated profile edit never silently erases it (D-468 review).
  void _setPlateFromCode(String? code) {
    _plateLetter1 = null;
    _plateLetter2 = null;
    _plateLetter3 = null;
    _plateDigits.text = '';
    _plateDigitsFirst = false;
    final raw = code?.trim() ?? '';
    if (raw.isEmpty) {
      _plate.text = '';
      return;
    }
    final canonical = normalizePlate(raw) ?? raw;
    final List<String> letters = <String>[];
    final StringBuffer digits = StringBuffer();
    for (final int rune in canonical.runes) {
      if (rune >= 0x30 && rune <= 0x39) {
        digits.writeCharCode(rune);
      } else {
        letters.add(String.fromCharCode(rune).toUpperCase());
      }
    }
    final Set<String> codes =
        saudiPlateLetters.map((SaudiPlateLetter l) => l.code).toSet();
    if (letters.length == 3 && letters.every(codes.contains)) {
      _plateLetter1 = letters[0];
      _plateLetter2 = letters[1];
      _plateLetter3 = letters[2];
      _plateDigits.text = digits.toString();
      // Preserve the stored order: a leading digit means digits-then-letters
      // (e.g. "1234ABJ"); _syncPlate re-emits in that same order (D-471 fix).
      final int firstRune = canonical.runes.first;
      _plateDigitsFirst = firstRune >= 0x30 && firstRune <= 0x39;
      _syncPlate();
    } else {
      // Legacy / non-conforming code the dropdowns can't represent — keep it so
      // an unrelated edit doesn't wipe it (the server re-validates on save).
      _plate.text = canonical;
    }
  }

  Widget _buildMobileField(AppL10n l10n) {
    final saudi = _isSaudi;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(
          saudi ? l10n.saudiMobileLabel : l10n.internationalMobileLabel,
        ),
        const SizedBox(height: 8),
        TextFormField(
          controller: saudi ? _saudiMobile : _internationalMobile,
          keyboardType: TextInputType.phone,
          textDirection: TextDirection.ltr,
          style: _inputStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator:
              saudi ? _validateSaudiMobile : _validateInternationalMobile,
          decoration: _fieldDecoration(),
        ),
      ],
    );
  }

  Widget _buildDateOfBirthField(AppL10n l10n) {
    final hasError = _triedSubmit && _dateOfBirth == null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.dateOfBirthLabel),
        const SizedBox(height: 8),
        InkWell(
          onTap: () => unawaited(_pickDateOfBirth()),
          borderRadius: _radius4,
          child: InputDecorator(
            decoration: _fieldDecoration(
              errorText: hasError ? l10n.dateOfBirthRequired : null,
              suffixIcon: const Icon(
                Icons.calendar_today_outlined,
                color: SimfTokens.greyText,
                size: 18,
              ),
            ),
            child: Text(
              _dateOfBirth == null ? '—' : _formatDate(_dateOfBirth!),
              style: _inputStyle,
            ),
          ),
        ),
      ],
    );
  }

  /// The card-head avatar: the captured face photo once taken (owner follow-up
  /// — "show at top by replacing the icon with the real profile photo"), else
  /// the placeholder person icon.
  Widget _buildHeaderAvatar() {
    final bytes = _faceImageBytes;
    if (bytes == null) {
      return const Icon(
        Icons.account_circle_outlined,
        size: 40,
        color: SimfTokens.headlineInk,
      );
    }
    return ClipOval(
      child: Image.memory(bytes, width: 40, height: 40, fit: BoxFit.cover),
    );
  }

  /// The empty 56 px bordered attach box (shared by the ID + face fields):
  /// a centred label + trailing icon that triggers [onTap] when tapped.
  Widget _attachBox({
    required String label,
    required IconData icon,
    required VoidCallback onTap,
  }) {
    return InkWell(
      onTap: onTap,
      borderRadius: _radius4,
      child: Container(
        height: 56,
        decoration: BoxDecoration(
          border: Border.all(color: SimfTokens.beigeBorder),
          borderRadius: _radius4,
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Text(
              label,
              style: const TextStyle(
                color: SimfTokens.inputInk,
                fontSize: 14,
                fontWeight: FontWeight.w500,
              ),
            ),
            const SizedBox(width: 8),
            Icon(icon, size: 24, color: SimfTokens.greyText),
          ],
        ),
      ),
    );
  }

  /// "Upload ID" — the ID DOCUMENT attach box (gallery pick): a 56 px bordered
  /// row with the plus mark; once attached it shows the thumbnail + name +
  /// remove. Mandatory for EVERY registrant — the "required" hint shows up
  /// front, escalating to danger after a blocked Next.
  Widget _buildIdImageField(AppL10n l10n) {
    final bytes = _idImageBytes;
    final needsImage = bytes == null && !_hasExistingIdImage;
    final triedAndMissing = _triedSubmit && needsImage;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.attachmentsLabel),
        const SizedBox(height: 8),
        if (needsImage) ...<Widget>[
          Text(
            l10n.idImageRequired,
            style: TextStyle(
              color: triedAndMissing ? SimfTokens.danger : SimfTokens.greyText,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 8),
        ],
        if (bytes == null)
          _attachBox(
            label: l10n.attachFileLabel,
            icon: Icons.add_circle_outline,
            onTap: () => unawaited(_pickIdImage()),
          )
        else
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              border: Border.all(color: SimfTokens.beigeBorder),
              borderRadius: _radius4,
            ),
            child: Row(
              children: <Widget>[
                ClipRRect(
                  borderRadius: _radius4,
                  child: Image.memory(
                    bytes,
                    width: 40,
                    height: 40,
                    fit: BoxFit.cover,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    _idImageName ?? l10n.idImageAttachedLabel,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: SimfTokens.inputInk,
                      fontSize: 14,
                    ),
                  ),
                ),
                TextButton(
                  onPressed: _removeIdImage,
                  style: TextButton.styleFrom(
                    foregroundColor: SimfTokens.accent,
                  ),
                  child: Text(l10n.removeLabel),
                ),
              ],
            ),
          ),
      ],
    );
  }

  /// "Face photo" — the live face capture (→ profile avatar). Mandatory for
  /// men, optional for women. Once captured the face shows at the top of the
  /// card and this row confirms it with a Retake; an empty male field shows the
  /// "required" hint (danger after a blocked Next), a woman sees the optional
  /// hint.
  Widget _buildFacePhotoField(AppL10n l10n) {
    final bytes = _faceImageBytes;
    final maleNeedsFace = _gender == AppGender.male &&
        bytes == null &&
        !_hasExistingAvatar;
    final triedAndMissing = _triedSubmit && maleNeedsFace;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.facePhotoLabel),
        const SizedBox(height: 8),
        if (maleNeedsFace) ...<Widget>[
          Text(
            l10n.facePhotoRequiredForMen,
            style: TextStyle(
              color: triedAndMissing ? SimfTokens.danger : SimfTokens.greyText,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 8),
        ] else if (bytes == null && !_hasExistingAvatar) ...<Widget>[
          Text(
            l10n.facePhotoOptionalForWomen,
            style: const TextStyle(color: SimfTokens.greyText, fontSize: 12),
          ),
          const SizedBox(height: 8),
        ],
        if (bytes == null)
          _attachBox(
            label: l10n.facePhotoCaptureLabel,
            icon: Icons.photo_camera_outlined,
            onTap: () => unawaited(_pickFacePhoto()),
          )
        else
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              border: Border.all(color: SimfTokens.beigeBorder),
              borderRadius: _radius4,
            ),
            child: Row(
              children: <Widget>[
                ClipOval(
                  child: Image.memory(
                    bytes,
                    width: 40,
                    height: 40,
                    fit: BoxFit.cover,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    l10n.facePhotoCaptured,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: SimfTokens.inputInk,
                      fontSize: 14,
                    ),
                  ),
                ),
                TextButton(
                  onPressed: () => unawaited(_pickFacePhoto()),
                  style: TextButton.styleFrom(
                    foregroundColor: SimfTokens.accent,
                  ),
                  child: Text(l10n.retakeLabel),
                ),
              ],
            ),
          ),
      ],
    );
  }

  Widget _buildOrganisationField(AppL10n l10n) {
    if (_organisationId != null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _FieldLabel(l10n.organisationLabel),
          const SizedBox(height: 8),
          InputDecorator(
            decoration: _fieldDecoration(),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    _organisationLabel ?? l10n.organisationSelected,
                    style: _inputStyle,
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
        _FieldLabel(l10n.organisationLabel),
        const SizedBox(height: 8),
        TextField(
          controller: _organisationSearch,
          style: _inputStyle,
          decoration: _fieldDecoration(
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
          const SizedBox(height: 8),
          // D-375 — fetch state first: spinner while searching, retry on
          // failure; "no matches" only describes a COMPLETED empty search.
          if (_organisationSearching)
            Padding(
              padding: const EdgeInsets.all(8),
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
              padding: const EdgeInsets.all(8),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      l10n.lookupLoadError,
                      style: const TextStyle(
                        color: SimfTokens.danger,
                        fontSize: 12,
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
              padding: const EdgeInsets.all(8),
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

  // ---- Styling (the login-card field language — Figma 168:2972) ------------

  static const TextStyle _inputStyle = TextStyle(
    fontSize: 14,
    fontWeight: FontWeight.w500,
    color: SimfTokens.inputInk,
  );

  static const OutlineInputBorder _restingBorder = OutlineInputBorder(
    borderRadius: _radius4,
    borderSide: BorderSide(color: SimfTokens.beigeBorder),
  );
  static const OutlineInputBorder _focusedBorder = OutlineInputBorder(
    borderRadius: _radius4,
    borderSide: BorderSide(color: SimfTokens.accent),
  );

  InputDecoration _fieldDecoration({
    String? counterText,
    String? hintText,
    String? errorText,
    Widget? prefixIcon,
    Widget? suffixIcon,
  }) {
    return InputDecoration(
      counterText: counterText,
      hintText: hintText,
      errorText: errorText,
      prefixIcon: prefixIcon,
      suffixIcon: suffixIcon,
      isDense: true,
      filled: false,
      hintStyle: const TextStyle(color: SimfTokens.greyText),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 15),
      border: _restingBorder,
      enabledBorder: _restingBorder,
      focusedBorder: _focusedBorder,
      disabledBorder: _restingBorder,
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

  /// Standard Luhn mod-10 — mirrors the server check for the Saudi national id /
  /// Iqama. Instant client feedback only; the server re-validates (D-197).
  static bool _isValidLuhn(String number) {
    var sum = 0;
    var doubleDigit = false;
    for (var i = number.length - 1; i >= 0; i--) {
      final code = number.codeUnitAt(i);
      if (code < 0x30 || code > 0x39) {
        return false;
      }
      var digit = code - 0x30;
      if (doubleDigit) {
        digit *= 2;
        if (digit > 9) {
          digit -= 9;
        }
      }
      sum += digit;
      doubleDigit = !doubleDigit;
    }
    return sum % 10 == 0;
  }
}

/// A field caption above its input — the design's 12-grey label
/// (Figma "Title" rows).
class _FieldLabel extends StatelessWidget {
  const _FieldLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Text(
        text,
        style: const TextStyle(
          color: SimfTokens.greyText,
          fontSize: 12,
          fontWeight: FontWeight.w500,
        ),
      ),
    );
  }
}

/// The design's beige segmented tabs (Figma 505:1075 / 505:1030) — D-373
/// owner fix: the **selected** segment is a **white pill** with navy text
/// (the old selected-beige-on-beige rendered invisible); the unselected
/// segment stays on the container beige with white text.
class _BeigeTabs extends StatelessWidget {
  const _BeigeTabs({
    required this.options,
    required this.selectedIndex,
    required this.onChanged,
  });

  final List<String> options;
  final int selectedIndex;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      decoration: BoxDecoration(
        color: SimfTokens.beigeBorder,
        borderRadius: _radius4,
      ),
      child: Row(
        children: <Widget>[
          for (int i = 0; i < options.length; i++) ...<Widget>[
            if (i > 0) const SizedBox(width: 1),
            Expanded(
              child: InkWell(
                onTap: () => onChanged(i),
                borderRadius: _radius4,
                child: Container(
                  height: 34,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: i == selectedIndex
                        ? Colors.white
                        : Colors.transparent,
                    borderRadius: _radius4,
                  ),
                  child: Text(
                    options[i],
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight:
                          i == selectedIndex ? FontWeight.w600 : FontWeight.w500,
                      color:
                          i == selectedIndex ? SimfTokens.navy : Colors.white,
                    ),
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// One option for the shared [_LookupSearchSheet]: a stable [value], a display
/// [label], and the [search] text matched against the query (defaults to the
/// label).
class _PickerOption {
  const _PickerOption({
    required this.value,
    required this.label,
    String? search,
  }) : search = search ?? label;

  final String value;
  final String label;
  final String search;
}

/// D-373/D-469/D-470 — the shared searchable picker sheet used by the
/// nationality, birth-region and plate-letter fields: one beige type-to-filter
/// list so all three look and behave identically. Pops the picked
/// [_PickerOption.value].
class _LookupSearchSheet extends StatefulWidget {
  const _LookupSearchSheet({
    required this.options,
    required this.searchHint,
    this.searchFieldKey,
  });

  final List<_PickerOption> options;
  final String searchHint;
  final Key? searchFieldKey;

  @override
  State<_LookupSearchSheet> createState() => _LookupSearchSheetState();
}

class _LookupSearchSheetState extends State<_LookupSearchSheet> {
  static const TextStyle _itemStyle = TextStyle(
    fontSize: 14,
    fontWeight: FontWeight.w500,
    color: SimfTokens.inputInk,
  );

  String _query = '';

  @override
  Widget build(BuildContext context) {
    final term = _query.trim().toLowerCase();
    final filtered = term.isEmpty
        ? widget.options
        : widget.options
            .where((o) => o.search.toLowerCase().contains(term))
            .toList();
    return SafeArea(
      child: Padding(
        // Keeps the search field above the soft keyboard.
        padding: EdgeInsets.only(
          bottom: MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: SizedBox(
          height: MediaQuery.sizeOf(context).height * 0.7,
          child: Column(
            children: <Widget>[
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
                child: TextField(
                  key: widget.searchFieldKey,
                  autofocus: true,
                  style: _itemStyle,
                  onChanged: (value) => setState(() => _query = value),
                  decoration: InputDecoration(
                    isDense: true,
                    hintText: widget.searchHint,
                    hintStyle: const TextStyle(color: SimfTokens.greyText),
                    prefixIcon:
                        const Icon(Icons.search, color: SimfTokens.greyText),
                    enabledBorder: const OutlineInputBorder(
                      borderRadius: _radius4,
                      borderSide: BorderSide(color: SimfTokens.beigeBorder),
                    ),
                    focusedBorder: const OutlineInputBorder(
                      borderRadius: _radius4,
                      borderSide: BorderSide(color: SimfTokens.accent),
                    ),
                  ),
                ),
              ),
              Expanded(
                child: ListView.builder(
                  itemCount: filtered.length,
                  itemBuilder: (context, index) {
                    final option = filtered[index];
                    return ListTile(
                      dense: true,
                      title: Text(option.label, style: _itemStyle),
                      onTap: () => Navigator.of(context).pop(option.value),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// One of the design's gender radio pills (Figma 522:2151): a white pill with
/// the label and an 18 px gold-ringed radio that fills when selected.
class _RadioPill extends StatelessWidget {
  const _RadioPill({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: _radius4,
      child: Container(
        height: 48,
        decoration: const BoxDecoration(
          color: Color(0xE6FFFFFF), // white at 90% over the beige card
          borderRadius: _radius4,
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Text(
              label,
              style: const TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w500,
                color: SimfTokens.navy,
              ),
            ),
            const SizedBox(width: 12),
            Container(
              width: 18,
              height: 18,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(color: SimfTokens.accent, width: 1.2),
              ),
              alignment: Alignment.center,
              child: selected
                  ? Container(
                      width: 10,
                      height: 10,
                      decoration: const BoxDecoration(
                        shape: BoxShape.circle,
                        color: SimfTokens.accent,
                      ),
                    )
                  : null,
            ),
          ],
        ),
      ),
    );
  }
}
