import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/localization/locale_controller.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_logo.dart';
import 'data/profile_models.dart';
import 'data/profile_repository.dart';
import 'phone_validation.dart';

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
  final TextEditingController _nationalId = TextEditingController();
  final TextEditingController _documentNumber = TextEditingController();
  final TextEditingController _saudiMobile = TextEditingController();
  final TextEditingController _internationalMobile = TextEditingController();
  final TextEditingController _organisationSearch = TextEditingController();

  /// نوع التسجيل: Visitor (true) / Other (false) — the `ProfileType.IsForVisitor`
  /// filter (D-332). Client-only; not persisted.
  bool _isVisitorType = true;
  bool _isSaudi = true;
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

  Timer? _organisationDebounce;
  Uint8List? _idImageBytes;
  String? _idImageName;

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
    _isSaudi = profile.isSaudi;
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
    _gender = profile.gender;

    final code = profile.nationalityCode;
    _nationalityCode = _countries.any((c) => c.code == code) ? code : null;

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
    final repo = ref.read(profileRepositoryProvider);
    try {
      final types = await repo.getProfileTypes(isVisitor: isVisitor);
      if (!mounted) {
        return;
      }
      setState(() {
        _profileTypes = types;
        _lockVisitorProfileType();
      });
    } on ApiFailure {
      // Non-blocking — the picker just stays empty until a retry.
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
    final repo = ref.read(profileRepositoryProvider);
    try {
      final results = await repo.searchOrganisations(search: value, top: 20);
      if (!mounted) {
        return;
      }
      setState(() => _organisationResults = results);
    } on ApiFailure {
      // A typeahead failure is non-blocking; keep the last results.
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
      // The picker is unavailable (e.g. no native plugin in this tree). The
      // image is optional and can be added later — fail silently.
    }
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
    if (!formValid || !dateOfBirthValid || !organisationValid) {
      setState(() {});
      return;
    }
    final draft = SignUpProfileDraft(
      request: _buildRequest(),
      idImageBytes: _idImageBytes,
      idImageName: _idImageName,
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
      organisationId: _organisationId,
      gender: _gender,
    );
  }

  // ---- Validators (client mirror of UpsertUserProfileRequestValidator) -----

  String? _validateRequired(String? value) {
    return (value?.trim().isNotEmpty ?? false)
        ? null
        : AppL10n.of(context).requiredField;
  }

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
                        const Icon(
                          Icons.account_circle_outlined,
                          size: 40,
                          color: SimfTokens.headlineInk,
                        ),
                      ],
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
                      validator: _validateRequired,
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
                      validator: _validateRequired,
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
                    // The Saudi switch drives national-ID vs iqama/passport —
                    // kept from the shipped contract (the frame omits it).
                    SwitchListTile(
                      contentPadding: EdgeInsets.zero,
                      value: _isSaudi,
                      activeThumbColor: SimfTokens.accent,
                      onChanged: (value) => setState(() => _isSaudi = value),
                      title: Text(
                        l10n.isSaudiLabel,
                        style: const TextStyle(
                          color: SimfTokens.headlineInk,
                          fontSize: 14,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ),
                    const SizedBox(height: 8),
                    ..._buildDocumentFields(l10n),
                    const SizedBox(height: 16),
                    _buildMobileField(l10n),
                    const SizedBox(height: 16),
                    _buildDateOfBirthField(l10n),
                    const SizedBox(height: 16),
                    _FieldLabel(l10n.placeOfBirthLabel),
                    const SizedBox(height: 8),
                    TextFormField(
                      controller: _placeOfBirth,
                      maxLength: 128,
                      style: _inputStyle,
                      decoration: _fieldDecoration(counterText: ''),
                    ),
                    const SizedBox(height: 16),
                    _buildIdImageField(l10n),
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
    if (_profileTypes.isEmpty) {
      return const SizedBox.shrink();
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.profileTypeLabel),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          key: const ValueKey<String>('profileTypePicker'),
          initialValue: _profileTypeId,
          isExpanded: true,
          style: _inputStyle,
          dropdownColor: SimfTokens.cardBeige,
          icon: const Icon(
            Icons.keyboard_arrow_down,
            color: SimfTokens.greyText,
          ),
          decoration: _fieldDecoration(),
          autovalidateMode: AutovalidateMode.onUserInteraction,
          // C5 (D-371) — under "Other" a pick is required (the empty-lookup
          // case is excluded above per L-6: never block on missing data).
          validator: (value) =>
              value == null ? l10n.profileTypeRequired : null,
          items: _profileTypes
              .map(
                (type) => DropdownMenuItem<String>(
                  value: type.id,
                  child: Text(
                    l10n.isArabic ? type.nameArabic : type.name,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              )
              .toList(),
          onChanged: (value) => setState(() => _profileTypeId = value),
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

  Widget _buildNationalityField(AppL10n l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.nationalityLabel),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          initialValue: _nationalityCode,
          isExpanded: true,
          style: _inputStyle,
          dropdownColor: SimfTokens.cardBeige,
          icon: const Icon(
            Icons.keyboard_arrow_down,
            color: SimfTokens.greyText,
          ),
          decoration: _fieldDecoration(),
          items: _countries
              .map(
                (c) => DropdownMenuItem<String>(
                  value: c.code,
                  child: Text(
                    l10n.isArabic ? c.nameArabic : c.name,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              )
              .toList(),
          validator: (value) => (value == null || value.isEmpty)
              ? l10n.nationalityRequired
              : null,
          onChanged: (value) => setState(() => _nationalityCode = value),
        ),
      ],
    );
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

  /// The attach box (Figma 505:1322): a 56 px bordered row with the plus mark;
  /// once attached it shows the thumbnail + name + remove.
  Widget _buildIdImageField(AppL10n l10n) {
    final bytes = _idImageBytes;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.attachmentsLabel),
        const SizedBox(height: 8),
        if (bytes == null)
          InkWell(
            onTap: () => unawaited(_pickIdImage()),
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
                    l10n.attachFileLabel,
                    style: const TextStyle(
                      color: SimfTokens.inputInk,
                      fontSize: 14,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  const SizedBox(width: 8),
                  const Icon(
                    Icons.add_circle_outline,
                    size: 24,
                    color: SimfTokens.greyText,
                  ),
                ],
              ),
            ),
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
          if (_organisationResults.isEmpty)
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

/// The design's beige segmented tabs (Figma 505:1075 / 505:1030): a
/// `beigeBorder` container; the **unselected** segment is a white pill with
/// ink text, the **selected** segment shows the container beige with white
/// text.
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
                        ? SimfTokens.beigeBorder
                        : Colors.white,
                    borderRadius: _radius4,
                  ),
                  child: Text(
                    options[i],
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w500,
                      color:
                          i == selectedIndex ? Colors.white : SimfTokens.navy,
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
