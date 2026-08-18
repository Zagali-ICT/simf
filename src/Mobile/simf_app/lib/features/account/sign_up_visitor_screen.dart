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
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/core/validation/phone_validation.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/data/region_repository.dart';
import 'package:simf_app/features/account/saudi_regions.dart';
import 'package:simf_app/features/account/widgets/beige_tabs.dart';
import 'package:simf_app/features/account/widgets/date_of_birth_field.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet_launcher.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_face_photo_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_header_avatar.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_id_image_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_load_error.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_organisation_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_place_of_birth_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_plate_field.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_profile_type_field.dart';
import 'package:simf_app/features/account/widgets/terms_and_next_buttons.dart';
import 'package:simf_app/features/myarea/data/liveness.dart'
    show CapturedSelfie;
import 'package:simf_app/features/visitor_profile/data/visitor_profile_completeness.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_form_state.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';
import 'package:simf_app/features/visitor_profile/widgets/contact_section.dart';
import 'package:simf_app/features/visitor_profile/widgets/document_section.dart';
import 'package:simf_app/features/visitor_profile/widgets/identity_section.dart';
import 'package:simf_app/features/visitor_profile/widgets/nationality_section.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Sign up — profile data · إنشاء ملف شخصى · route: RouteNames.signUpVisitor ·
/// Figma 168:2972 (D-368; design deltas recorded there)
/// Contract: D-332 — NO API write happens here. The screen loads the existing
/// profile + the three lookups concurrently, and Next carries the collected
/// data (+ the optional ID image) forward as a [SignUpProfileDraft] to the
/// interests screen, which fires the single profile save. The visitor/other tab
/// is a client-only `?isVisitor=` filter over the ProfileType picker.
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
  // D-469 — the selected Saudi region code (birth-location dropdown); null for
  // a non-Saudi (free-text place of birth) or an unmatched stored value.
  String? _birthRegionCode;
  final TextEditingController _nationalId = TextEditingController();
  final TextEditingController _documentNumber = TextEditingController();
  final TextEditingController _saudiMobile = TextEditingController();
  final TextEditingController _internationalMobile = TextEditingController();
  // C6 (D-459) — the three letter picks, the digits and the assembled plate
  // code, which are only ever useful together.
  final SignUpVisitorPlateState _plate = SignUpVisitorPlateState();

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

  /// The organisations fetched with the opening load, handed to the type-ahead
  /// so its list is populated before the first keystroke. The field owns the
  /// searching from there.
  List<OrganisationItem> _initialOrganisations = const <OrganisationItem>[];

  // D-375 — API-fed pickers always surface their fetch state (owner rule:
  // every dropdown loaded from the API shows loading, and a failure is a
  // visible retry — never a silently missing/empty control).
  bool _profileTypesLoading = false;
  bool _profileTypesFailed = false;

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
  // D-684 — the profile is saved on THIS step now (profile-first), so any
  // server error (e.g. the name) surfaces here, not two screens later on
  // interests.
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
  void didChangeDependencies() {
    super.didChangeDependencies();
    // D-469/D-470 — the birth-region picker is code-keyed, so the stored name
    // has to be re-read in the active language when the user toggles it.
    //
    // This belongs here and not in build(): assigning to a
    // TextEditingController notifies its listeners, and doing that during the
    // build phase is what Flutter forbids. didChangeDependencies is the hook
    // that fires on exactly the change this needs to react to — the
    // Localizations inherited widget above us.
    _syncBirthRegionName(isArabic: AppL10n.of(context).isArabic);
  }

  /// Re-reads [_placeOfBirth] from the selected region code in the active
  /// language. A no-op unless a Saudi registrant has picked a region: for a
  /// non-Saudi registrant the field is free text, and both the picker and the
  /// nationality switch already maintain the value inside their own setState.
  void _syncBirthRegionName({required bool isArabic}) {
    if (!_isSaudi || _birthRegionCode == null) {
      return;
    }
    final region = birthRegionByCode(ref, _birthRegionCode);
    final name = region?.name(isArabic: isArabic) ?? '';
    if (_placeOfBirth.text != name) {
      _placeOfBirth.text = name;
    }
  }

  @override
  void dispose() {
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
        _initialOrganisations = results[3] as List<OrganisationItem>;
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
    _plate.setFromCode(profile.plateNumber);
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
    _form.profileTypeId =
        _form.profileTypes.any((t) => t.id == typeId) ? typeId : null;

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
    final normal = _form.profileTypes.where((t) => t.name == 'Normal').toList();
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
  /// silently hid the الفئة (category) field (the owner-reported "removed
  /// list").
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

  void _selectOrganisation(OrganisationItem organisation, AppL10n l10n) {
    setState(() {
      _form.organisationId = organisation.id;
      _organisationLabel = l10n.isArabic
          ? organisation.nameAr
          : (organisation.nameEn ?? organisation.nameAr);
    });
  }

  void _clearOrganisation() {
    setState(() {
      _form.organisationId = null;
      _organisationLabel = null;
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
    } on Object catch (_) {
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

  /// Validates the data fields, uploads the images and **saves the profile
  /// here** (D-684, profile-first) so any server error — the name in particular
  /// — surfaces on THIS screen, not two steps later on interests. Only on a
  /// clean save does it carry the [SignUpProfileDraft] to the interests screen
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
      unawaited(context.pushNamed(RouteNames.signUpInterests, extra: draft));
    } on ApiFailure catch (failure) {
      if (!mounted) return;
      setState(() => _saveError = failure.localizedMessage(l10n));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  /// Builds the request from the data fields. `interestIds` carries any
  /// existing picks (for pre-selection); the interests screen replaces it via
  /// `copyWith` before the save.
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
      plateNumber: _emptyToNull(_plate.value),
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
    // D-547 — watch the region lookup here so the place-of-birth picker
    // rebuilds when the API list lands (the picker reads it via ref.read in its
    // handler, so build must own the dependency for the closed-field name to
    // refresh).
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
      return SignUpVisitorLoadError(l10n: l10n, onRefresh: _load);
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
                    onChanged: (index) => unawaited(_onTypeChanged(index == 0)),
                  ),
                  const SizedBox(height: SimfTokens.space6),
                  SignUpVisitorProfileTypeField(
                    l10n: l10n,
                    form: _form,
                    isVisitorType: _isVisitorType,
                    loading: _profileTypesLoading,
                    failed: _profileTypesFailed,
                    onRetry: () => unawaited(_fetchProfileTypes()),
                    onChanged: (id) =>
                        setState(() => _form.profileTypeId = id),
                  ),
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
                    organisationField: SignUpVisitorOrganisationField(
                      l10n: l10n,
                      initialResults: _initialOrganisations,
                      selectedId: _form.organisationId,
                      selectedLabel: _organisationLabel,
                      showError:
                          _form.triedSubmit && _form.organisationId == null,
                      onSelected: (organisation) =>
                          _selectOrganisation(organisation, l10n),
                      onCleared: _clearOrganisation,
                    ),
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
                  SignUpVisitorPlaceOfBirthField(
                    l10n: l10n,
                    isSaudi: _isSaudi,
                    controller: _placeOfBirth,
                    regionCode: _birthRegionCode,
                    showError: _form.triedSubmit && _birthRegionCode == null,
                    onRegionPicked: _onBirthRegionPicked,
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  // D-373 — the plate is the last input before the attach.
                  SignUpVisitorPlateField(
                    l10n: l10n,
                    state: _plate,
                    onChanged: () => setState(() {}),
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  SignUpVisitorIdImageField(
                    l10n: l10n,
                    bytes: _idImageBytes,
                    filename: _idImageName,
                    hasStoredImage: _hasExistingIdImage,
                    triedSubmit: _form.triedSubmit,
                    onAttach: () => unawaited(_pickIdImage()),
                    onRemove: _removeIdImage,
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  SignUpVisitorFacePhotoField(
                    l10n: l10n,
                    bytes: _faceImageBytes,
                    gender: _form.gender,
                    hasStoredAvatar: _hasExistingAvatar,
                    triedSubmit: _form.triedSubmit,
                    onCapture: () => unawaited(_pickFacePhoto()),
                  ),
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

  /// Opens the searchable country sheet and applies the pick. Clearing the
  /// stale national-id/iqama input when the Saudi-ness flips keeps the
  /// derived document section consistent (D-373).
  Future<void> _pickNationality(AppL10n l10n) async {
    final pickedCode = await showLookupSearchSheet(
      context: context,
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

  /// Stores the birth-region pick. The code is kept as well as the name so the
  /// field can be re-read in the other language when the user toggles it
  /// (D-469/D-470).
  void _onBirthRegionPicked(String code, String name) {
    setState(() {
      _birthRegionCode = code;
      _placeOfBirth.text = name;
    });
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
