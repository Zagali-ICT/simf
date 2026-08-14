import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_form_scaffold.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/motion/motion_durations.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/core/validation/phone_validation.dart';
import 'package:simf_app/core/validation/plate_validation.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/data/region_models.dart';
import 'package:simf_app/features/account/data/region_repository.dart';
import 'package:simf_app/features/account/saudi_regions.dart';
import 'package:simf_app/features/account/widgets/attachment_field.dart';
import 'package:simf_app/features/account/widgets/beige_tabs.dart';
import 'package:simf_app/features/account/widgets/date_of_birth_field.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';
import 'package:simf_app/features/account/widgets/organisation_typeahead_field.dart';
import 'package:simf_app/features/account/widgets/place_of_birth_field.dart';
import 'package:simf_app/features/account/widgets/plate_number_field.dart';
import 'package:simf_app/features/account/widgets/profile_type_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_header_avatar.dart';
import 'package:simf_app/features/account/widgets/terms_and_next_buttons.dart';
import 'package:simf_app/features/myarea/data/liveness.dart' show CapturedSelfie;
import 'package:simf_app/features/visitor_profile/data/visitor_profile_completeness.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_form_state.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';
import 'package:simf_app/features/visitor_profile/widgets/contact_section.dart';
import 'package:simf_app/features/visitor_profile/widgets/document_section.dart';
import 'package:simf_app/features/visitor_profile/widgets/identity_section.dart';
import 'package:simf_app/features/visitor_profile/widgets/nationality_section.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

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
class _SignUpVisitorScreenState extends ConsumerState<SignUpVisitorScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();

  /// The fields both registration surfaces share (SIMF-RSD-001 step 2). The
  /// text controllers below stay here: a controller owns its own value and
  /// must be disposed by whoever creates it.
  final VisitorProfileFormState _form = VisitorProfileFormState();

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
  bool get _isSaudi => _form.isSaudi;
  DateTime? _dateOfBirth;
  String? _organisationLabel;

  /// Any interest ids already on the profile (pre-fill). Carried forward in the
  /// draft so the interests screen (Page 007‑01) pre-selects them on re-entry.
  List<String> _existingInterestIds = const <String>[];

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
    _form.dispose();
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
        repo.searchOrganisations(),
      ]);
      if (!mounted) {
        return;
      }
      setState(() {
        _form.setLookups(
          countries: results[1] as List<CountryItem>,
          profileTypes: results[2] as List<ProfileTypeItem>,
        );
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
      _form.docType = VisitorDocType.iqama;
      _documentNumber.text = profile.iqamaNumber!;
    } else if ((profile.passportNumber ?? '').isNotEmpty) {
      _form.docType = VisitorDocType.passport;
      _documentNumber.text = profile.passportNumber!;
    }
    _saudiMobile.text = profile.saudiMobile ?? '';
    _internationalMobile.text = profile.internationalMobile ?? '';
    _setPlateFromCode(profile.plateNumber);
    // D-373 defaults — Male and Saudi Arabia pre-selected on a first-time
    // (empty) profile; a saved profile keeps its own values.
    _form.gender = profile.gender == AppGender.unspecified
        ? AppGender.male
        : profile.gender;
    _hasExistingIdImage = profile.hasIdImage;
    _hasExistingAvatar = profile.hasAvatar;

    final code = profile.nationalityCode;
    _form.nationalityCode = _form.countries.any((c) => c.code == code)
        ? code
        : (_form.countries.any((c) => c.code == 'SA') ? 'SA' : null);

    final typeId = profile.profileTypeId;
    _form.profileTypeId = _form.profileTypes.any((t) => t.id == typeId) ? typeId : null;

    _form.organisationId = profile.organisationId;
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
        _form.profileTypes.where((t) => t.name == 'Normal').toList();
    if (normal.isNotEmpty) {
      _form.profileTypeId = normal.first.id;
    } else if (_form.profileTypes.length == 1) {
      _form.profileTypeId = _form.profileTypes.first.id;
    } else {
      _form.profileTypeId = null;
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
      _form.profileTypeId = null;
      _form.setLookups(profileTypes: const <ProfileTypeItem>[]);
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
        _form.setLookups(profileTypes: types);
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
      MotionDurations.searchDebounce,
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
      final results = await repo.searchOrganisations(search: value);
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
      _form.organisationId = organisation.id;
      _organisationLabel = l10n.isArabic
          ? organisation.nameAr
          : (organisation.nameEn ?? organisation.nameAr);
      _organisationResults = const <OrganisationItem>[];
    });
  }

  void _clearOrganisation() {
    setState(() {
      _form.organisationId = null;
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
    setState(() => _form.triedSubmit = true);
    final formValid = _formKey.currentState?.validate() ?? false;
    // The cross-field gates the Form itself cannot express: every control below
    // is a picker, a segmented tab or an image slot, not a FormField. The rules
    // and the decisions behind them live in VisitorProfileCompleteness.
    final dateOfBirthValid =
        VisitorProfileCompleteness.dateOfBirth(_dateOfBirth);
    final organisationValid =
        VisitorProfileCompleteness.organisation(_form.organisationId);
    final nationalityValid =
        VisitorProfileCompleteness.nationality(_form.nationalityCode);
    final placeOfBirthValid = VisitorProfileCompleteness.placeOfBirth(
      isSaudi: _isSaudi,
      birthRegionCode: _birthRegionCode,
    );
    final profileTypeValid = VisitorProfileCompleteness.profileType(
      isVisitorType: _isVisitorType,
      loading: _profileTypesLoading,
      failed: _profileTypesFailed,
      hasItems: _form.profileTypes.isNotEmpty,
      profileTypeId: _form.profileTypeId,
    );
    final idImageValid = VisitorProfileCompleteness.idImage(
      hasPickedImage: _idImageBytes != null,
      hasStoredImage: _hasExistingIdImage,
    );
    final faceImageValid = VisitorProfileCompleteness.facePhoto(
      gender: _form.gender,
      hasPickedImage: _faceImageBytes != null,
      hasStoredImage: _hasExistingAvatar,
    );
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
          if (_form.gender == AppGender.male) {
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
      profileTypeId: _form.profileTypeId,
      interestIds: _existingInterestIds,
      arabicName: _arabicName.text.trim(),
      englishName: _englishName.text.trim(),
      jobTitle: _emptyToNull(_jobTitle.text),
      jobTitleArabic: _emptyToNull(_jobTitleArabic.text),
      nationalityCode: _form.nationalityCode ?? '',
      dateOfBirth: _dateOfBirth == null ? null : _formatDate(_dateOfBirth!),
      placeOfBirth: _placeOfBirth.text.trim(),
      isSaudi: isSaudi,
      nationalId: isSaudi ? _emptyToNull(_nationalId.text) : null,
      iqamaNumber: !isSaudi && _form.docType == VisitorDocType.iqama
          ? _emptyToNull(_documentNumber.text)
          : null,
      passportNumber: !isSaudi && _form.docType == VisitorDocType.passport
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
      organisationId: _form.organisationId,
      gender: _form.gender,
      showInMeetLikeYou: _showInMeetLikeYou,
    );
  }

  // ---- Validators (client mirror of UpsertUserProfileRequestValidator) -----

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
          maxWidth: SimfTokens.signUpVisitorScreenMaxWidth,
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
                  IdentitySection(
                    l10n: l10n,
                    arabicName: _arabicName,
                    englishName: _englishName,
                    jobTitle: _jobTitle,
                    jobTitleArabic: _jobTitleArabic,
                    gender: _form.gender,
                    onGenderChanged: (value) =>
                        setState(() => _form.gender = value),
                    organisationField: _buildOrganisationField(l10n),
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  NationalitySection(
                    l10n: l10n,
                    countries: _form.countries,
                    selectedCode: _form.nationalityCode,
                    showError:
                        _form.triedSubmit && _form.nationalityCode == null,
                    onTap: () => unawaited(_pickNationality(l10n)),
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  // D-373 — the Saudi switch is gone: the nationality pick
                  // drives national-ID vs iqama/passport (SA → national ID).
                  DocumentSection(
                    l10n: l10n,
                    isSaudi: _isSaudi,
                    docType: _form.docType,
                    nationalId: _nationalId,
                    documentNumber: _documentNumber,
                    onDocTypeChanged: (value) => setState(() {
                      _form.docType = value;
                      _documentNumber.clear();
                    }),
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  ContactSection(
                    l10n: l10n,
                    isSaudi: _isSaudi,
                    saudiMobile: _saudiMobile,
                    internationalMobile: _internationalMobile,
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  DateOfBirthField(
                    displayValue: _dateOfBirth == null
                        ? '—'
                        : _formatDateDisplay(_dateOfBirth!),
                    hasError: _form.triedSubmit && _dateOfBirth == null,
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
                        fontSize: SimfTokens.signUpVisitorScreenFontSize,
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

  /// Kept local rather than swapped for the shared [SimfErrorState]: that one
  /// draws white text, which is invisible on this screen's beige form card.
  ///
  /// Only this branch is pull-to-refreshable. The loaded form must NOT be —
  /// `_load()` runs `_applyProfile`, which overwrites every text controller, so
  /// a stray pull on a half-filled form would silently discard the input.
  /// The rule exists so nobody is stranded with no way to re-fetch, and
  /// that can only happen here.
  Widget _buildLoadError(AppL10n l10n) {
    return SimfRefreshableMessage(
      onRefresh: _load,
      child: Center(
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
      ),
    );
  }

  Widget _buildProfileTypeField(AppL10n l10n) {
    // C5 (D-371) — Visitor is locked to "Normal": no picker rendered, the id is
    // auto-assigned by _lockVisitorProfileType.
    if (_isVisitorType) {
      return const SizedBox.shrink();
    }
    return ProfileTypeField(
      l10n: l10n,
      loading: _profileTypesLoading,
      failed: _profileTypesFailed,
      items: _form.profileTypes,
      selectedId: _form.profileTypeId,
      showError: _form.triedSubmit && _form.profileTypeId == null,
      onRetry: () => unawaited(_fetchProfileTypes()),
      onChanged: (id) => setState(() => _form.profileTypeId = id),
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
        borderRadius: BorderRadius.vertical(top: Radius.circular(SimfTokens.radiusLarge)),
      ),
      builder: (_) => LookupSearchSheet(
        options: options,
        searchHint: searchHint,
        searchFieldKey: searchFieldKey,
      ),
    );
  }

  /// Opens the searchable country sheet and applies the pick. Clearing the
  /// stale national-id/iqama input when the Saudi-ness flips keeps the
  /// derived document section consistent (D-373).
  Future<void> _pickNationality(AppL10n l10n) async {
    final pickedCode = await _openLookupSheet(
      options: <PickerOption>[
        for (final CountryItem c in _form.countries)
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
      _form.nationalityCode = pickedCode;
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
    final api =
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
    for (final r in _activeBirthRegions()) {
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

  Widget _buildPlaceOfBirthField(AppL10n l10n) {
    final isArabic = l10n.isArabic;
    final regionName = _birthRegionCode == null
        ? null
        : _birthRegionByCode(_birthRegionCode)?.name(isArabic: isArabic);
    // Keep the stored name in the active locale while a region is selected, so a
    // language toggle re-syncs the submitted value (the picker is code-keyed).
    if (_isSaudi && _birthRegionCode != null && _placeOfBirth.text != (regionName ?? '')) {
      _placeOfBirth.text = regionName ?? '';
    }
    return PlaceOfBirthField(
      l10n: l10n,
      isSaudi: _isSaudi,
      controller: _placeOfBirth,
      regionDisplayName: regionName,
      hasRegion: _birthRegionCode != null,
      showRegionError: _form.triedSubmit && _birthRegionCode == null,
      onPickRegion: () => unawaited(_pickBirthRegion(l10n, isArabic)),
    );
  }

  Widget _buildPlateField(AppL10n l10n) {
    return PlateNumberField(
      l10n: l10n,
      letter1: _plateLetter1,
      letter2: _plateLetter2,
      letter3: _plateLetter3,
      digits: _plateDigits,
      onPickLetter: (position) => unawaited(
        _pickPlateLetter(l10n, position, _plateLetterSetter(position)),
      ),
      onDigitsChanged: () => setState(_syncPlate),
      validateDigits: (_) => validatePlate(_plate.text, l10n),
    );
  }

  /// Routes a picked letter to the right slot — the three pickers differ only by
  /// position, so the field takes the position and the screen owns the storage.
  ValueChanged<String?> _plateLetterSetter(int position) {
    switch (position) {
      case 1:
        return (String? v) => _plateLetter1 = v;
      case 2:
        return (String? v) => _plateLetter2 = v;
      default:
        return (String? v) => _plateLetter3 = v;
    }
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
    final parts = parsePlate(code);
    _plateLetter1 = parts.letter1;
    _plateLetter2 = parts.letter2;
    _plateLetter3 = parts.letter3;
    _plateDigits.text = parts.digits;
    _plateDigitsFirst = parts.digitsFirst;
    final override = parts.rawOverride;
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
      hintText: (_form.triedSubmit && needsImage) ? l10n.idImageRequired : null,
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
        _form.gender == AppGender.male && bytes == null && !_hasExistingAvatar;
    final showOptionalHint =
        bytes == null && !_hasExistingAvatar && !maleNeedsFace;
    final showRequiredHint = _form.triedSubmit && maleNeedsFace;
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
    return OrganisationTypeaheadField(
      l10n: l10n,
      controller: _organisationSearch,
      selectedId: _form.organisationId,
      selectedLabel: _organisationLabel,
      searching: _organisationSearching,
      searchFailed: _organisationSearchFailed,
      results: _organisationResults,
      showError: _form.triedSubmit && _form.organisationId == null,
      onSearchChanged: _onOrganisationSearchChanged,
      onRetry: () => unawaited(
        _runOrganisationSearch(_organisationSearch.text.trim()),
      ),
      onSelected: (organisation) => _selectOrganisation(organisation, l10n),
      onCleared: _clearOrganisation,
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
