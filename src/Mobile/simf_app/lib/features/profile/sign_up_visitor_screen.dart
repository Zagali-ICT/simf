import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import 'data/profile_models.dart';
import 'data/profile_repository.dart';

/// Page 007 — إنشاء حساب · زائر · Sign up — visitor (Page_007 docs).
///
/// Profile completion for a signed-in but profile-incomplete visitor. On open
/// it loads the existing profile (pre-fill) plus the four lookups (countries,
/// profile-types, interests, organisations) concurrently, then renders the
/// richer registration form + the inline interests sub-step (min 1 / max 10,
/// D12). Save sends one `POST /app/account/user-profile` carrying the form
/// fields **and** the picked interest ids; the actor comes from the token
/// (D7), so the body never carries a user id / email. The optional ID-document
/// image is uploaded (multipart) **after** the profile row exists. On success
/// the account is profile-complete and the app routes to the wait-for-approval
/// (registration-status) screen.
///
/// AUTH-only (Page_007 L-1): the route is in the auth gate, so an anonymous open
/// is impossible. The visuals follow the Mockup.html sign-up `.dark-form` frame:
/// the navy `.mob-top` header (the AppBar) over a navy form surface that carries
/// `.mfield` captioned white fields, `.type-chip` selectors, the interest tag
/// pills and the gold `.mbtn` save button. The UI is the interim placeholder
/// design (SIMF-VID-001 swaps the visuals later); the API + validation
/// behaviour is real.
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

  bool _isSaudi = true;
  _DocType _docType = _DocType.iqama;
  DateTime? _dateOfBirth;
  AppGender _gender = AppGender.unspecified;
  String? _nationalityCode;
  String? _profileTypeId;
  String? _organisationId;
  String? _organisationLabel;
  final Set<String> _interestIds = <String>{};

  List<CountryItem> _countries = const <CountryItem>[];
  List<ProfileTypeItem> _profileTypes = const <ProfileTypeItem>[];
  List<InterestItem> _interests = const <InterestItem>[];
  List<OrganisationItem> _organisationResults = const <OrganisationItem>[];

  Timer? _organisationDebounce;
  Uint8List? _idImageBytes;
  String? _idImageName;

  bool _loading = true;
  String? _loadError;
  bool _submitting = false;
  bool _triedSubmit = false;
  String? _submitError;

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
        repo.getProfileTypes(),
        repo.getInterests(),
        repo.searchOrganisations(top: 20),
      ]);
      if (!mounted) {
        return;
      }
      setState(() {
        _countries = results[1] as List<CountryItem>;
        _profileTypes = results[2] as List<ProfileTypeItem>;
        _interests = results[3] as List<InterestItem>;
        _organisationResults = results[4] as List<OrganisationItem>;
        _applyProfile(results[0] as UserProfileResponse);
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
    _nationalityCode =
        _countries.any((c) => c.code == code) ? code : null;

    final typeId = profile.profileTypeId;
    _profileTypeId =
        _profileTypes.any((t) => t.id == typeId) ? typeId : null;

    _organisationId = profile.organisationId;
    _organisationLabel = null;

    if ((profile.dateOfBirth ?? '').isNotEmpty) {
      _dateOfBirth = DateTime.tryParse(profile.dateOfBirth!);
    }

    _interestIds
      ..clear()
      ..addAll(
        profile.interestIds.where((id) => _interests.any((i) => i.id == id)),
      );
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

  // ---- Interests sub-step --------------------------------------------------

  void _toggleInterest(String id, bool selected, AppL10n l10n) {
    if (selected && _interestIds.length >= 10) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.interestsMaxReached)));
      return;
    }
    setState(() {
      if (selected) {
        _interestIds.add(id);
      } else {
        _interestIds.remove(id);
      }
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
      final file =
          await ImagePicker().pickImage(source: ImageSource.gallery);
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

  // ---- Save ----------------------------------------------------------------

  Future<void> _save() async {
    final l10n = AppL10n.of(context);
    setState(() {
      _triedSubmit = true;
      _submitError = null;
    });
    final formValid = _formKey.currentState?.validate() ?? false;
    final interestsValid =
        _interestIds.isNotEmpty && _interestIds.length <= 10;
    final dateOfBirthValid = _dateOfBirth != null;
    if (!formValid || !interestsValid || !dateOfBirthValid) {
      setState(() {});
      return;
    }

    setState(() => _submitting = true);
    final repo = ref.read(profileRepositoryProvider);
    try {
      await repo.upsertMyProfile(_buildRequest());
      final imageFailed = await _uploadIdImageIfAny(repo);
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(
              imageFailed ? l10n.idImageUploadFailed : l10n.profileSavedToast,
            ),
          ),
        );
      context.goNamed(RouteNames.registrationSuccess);
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() => _submitError = failure.message);
    } finally {
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  /// Uploads the picked ID image after the profile exists. Returns true when an
  /// image was picked but its upload failed — the profile save still succeeded,
  /// so this is surfaced as a non-blocking warning, not an error.
  Future<bool> _uploadIdImageIfAny(ProfileRepository repo) async {
    final bytes = _idImageBytes;
    final name = _idImageName;
    if (bytes == null || name == null) {
      return false;
    }
    try {
      await repo.uploadIdImage(bytes: bytes, filename: name);
      return false;
    } on ApiFailure {
      return true;
    }
  }

  UpsertUserProfileRequest _buildRequest() {
    final isSaudi = _isSaudi;
    return UpsertUserProfileRequest(
      profileTypeId: _profileTypeId,
      interestIds: _interestIds.toList(),
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
      final valid = RegExp(r'^2\d{9}$').hasMatch(number) && _isValidLuhn(number);
      return valid ? null : l10n.iqamaInvalid;
    }
    final valid = RegExp(r'^[A-Za-z0-9]{6,9}$').hasMatch(number);
    return valid ? null : l10n.passportInvalid;
  }

  String? _validatePhone(String? value) {
    final phone = value?.trim() ?? '';
    if (phone.isEmpty) {
      return null;
    }
    final valid = RegExp(r'^\+?\d{1,4}[-\s]?\d{4,15}$').hasMatch(phone);
    return valid ? null : AppL10n.of(context).phoneInvalid;
  }

  // ---- Build ---------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navy,
      appBar: AppBar(title: Text(l10n.signUpVisitorTitle)),
      body: SafeArea(child: _buildBody(l10n)),
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
    // Navy `.dark-form` surface holding the captioned `.mfield` rows.
    return Form(
      key: _formKey,
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space5,
          SimfTokens.space4,
          SimfTokens.space8,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            _sectionHeader(l10n.profileSectionPersonal),
            ..._buildPersonalFields(l10n),
            _sectionHeader(l10n.profileSectionAffiliation),
            _buildOrganisationField(l10n),
            const SizedBox(height: SimfTokens.space4),
            _buildProfileTypeField(l10n),
            _sectionHeader(l10n.profileSectionInterests),
            _buildInterestsField(l10n),
            const SizedBox(height: SimfTokens.space6),
            if (_submitError != null) ...<Widget>[
              Text(
                _submitError!,
                style: const TextStyle(color: SimfTokens.danger),
              ),
              const SizedBox(height: SimfTokens.space3),
            ],
            FilledButton(
              onPressed: _submitting ? null : () => unawaited(_save()),
              child: _submitting
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : Text(l10n.saveLabel),
            ),
          ],
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

  List<Widget> _buildPersonalFields(AppL10n l10n) {
    return <Widget>[
      _FieldLabel(l10n.arabicNameLabel),
      TextFormField(
        controller: _arabicName,
        enabled: !_submitting,
        maxLength: 256,
        style: _fieldTextStyle,
        autovalidateMode: AutovalidateMode.onUserInteraction,
        validator: _validateRequired,
        decoration: _whiteFieldDecoration(counterText: ''),
      ),
      const SizedBox(height: SimfTokens.space3),
      _FieldLabel(l10n.englishNameLabel),
      TextFormField(
        controller: _englishName,
        enabled: !_submitting,
        maxLength: 256,
        style: _fieldTextStyle,
        autovalidateMode: AutovalidateMode.onUserInteraction,
        validator: _validateRequired,
        decoration: _whiteFieldDecoration(counterText: ''),
      ),
      const SizedBox(height: SimfTokens.space3),
      _FieldLabel(l10n.jobTitleLabel),
      TextFormField(
        controller: _jobTitle,
        enabled: !_submitting,
        maxLength: 128,
        style: _fieldTextStyle,
        decoration: _whiteFieldDecoration(counterText: ''),
      ),
      const SizedBox(height: SimfTokens.space3),
      _buildNationalityField(l10n),
      const SizedBox(height: SimfTokens.space3),
      SwitchListTile(
        contentPadding: EdgeInsets.zero,
        value: _isSaudi,
        activeThumbColor: SimfTokens.accent,
        onChanged: _submitting
            ? null
            : (value) => setState(() => _isSaudi = value),
        title: Text(
          l10n.isSaudiLabel,
          style: const TextStyle(color: SimfTokens.surface),
        ),
      ),
      const SizedBox(height: SimfTokens.space2),
      ..._buildDocumentFields(l10n),
      const SizedBox(height: SimfTokens.space3),
      _buildMobileField(l10n),
      const SizedBox(height: SimfTokens.space3),
      _buildDateOfBirthField(l10n),
      const SizedBox(height: SimfTokens.space3),
      _FieldLabel(l10n.placeOfBirthLabel),
      TextFormField(
        controller: _placeOfBirth,
        enabled: !_submitting,
        maxLength: 128,
        style: _fieldTextStyle,
        decoration: _whiteFieldDecoration(counterText: ''),
      ),
      const SizedBox(height: SimfTokens.space3),
      _buildGenderField(l10n),
      const SizedBox(height: SimfTokens.space4),
      _buildIdImageField(l10n),
    ];
  }

  Widget _buildNationalityField(AppL10n l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.nationalityLabel),
        DropdownButtonFormField<String>(
          initialValue: _nationalityCode,
          isExpanded: true,
          style: _fieldTextStyle,
          dropdownColor: SimfTokens.surface,
          decoration: _whiteFieldDecoration(),
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
          onChanged: _submitting
              ? null
              : (value) => setState(() => _nationalityCode = value),
        ),
      ],
    );
  }

  List<Widget> _buildDocumentFields(AppL10n l10n) {
    if (_isSaudi) {
      return <Widget>[
        _FieldLabel(l10n.nationalIdLabel),
        TextFormField(
          controller: _nationalId,
          enabled: !_submitting,
          keyboardType: TextInputType.number,
          maxLength: 10,
          style: _fieldTextStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: _validateNationalId,
          decoration: _whiteFieldDecoration(counterText: ''),
        ),
      ];
    }
    return <Widget>[
      SegmentedButton<_DocType>(
        style: _segmentedStyle,
        segments: <ButtonSegment<_DocType>>[
          ButtonSegment<_DocType>(
            value: _DocType.iqama,
            label: Text(l10n.iqamaSegment),
          ),
          ButtonSegment<_DocType>(
            value: _DocType.passport,
            label: Text(l10n.passportSegment),
          ),
        ],
        selected: <_DocType>{_docType},
        onSelectionChanged: _submitting
            ? null
            : (selection) => setState(() {
                  _docType = selection.first;
                  _documentNumber.clear();
                }),
      ),
      const SizedBox(height: SimfTokens.space3),
      _FieldLabel(
        _docType == _DocType.iqama
            ? l10n.iqamaNumberLabel
            : l10n.passportNumberLabel,
      ),
      TextFormField(
        controller: _documentNumber,
        enabled: !_submitting,
        maxLength: _docType == _DocType.iqama ? 10 : 9,
        style: _fieldTextStyle,
        autovalidateMode: AutovalidateMode.onUserInteraction,
        validator: _validateDocumentNumber,
        decoration: _whiteFieldDecoration(counterText: ''),
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
        TextFormField(
          controller: saudi ? _saudiMobile : _internationalMobile,
          enabled: !_submitting,
          keyboardType: TextInputType.phone,
          textDirection: TextDirection.ltr,
          style: _fieldTextStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: _validatePhone,
          decoration: _whiteFieldDecoration(),
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
        InkWell(
          onTap: _submitting ? null : () => unawaited(_pickDateOfBirth()),
          borderRadius:
              const BorderRadius.all(Radius.circular(SimfTokens.radius)),
          child: InputDecorator(
            decoration: _whiteFieldDecoration(
              errorText: hasError ? l10n.dateOfBirthRequired : null,
              suffixIcon: const Icon(
                Icons.calendar_today_outlined,
                color: SimfTokens.inkMuted,
              ),
            ),
            child: Text(
              _dateOfBirth == null ? '—' : _formatDate(_dateOfBirth!),
              style: _fieldTextStyle,
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildGenderField(AppL10n l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.genderLabel),
        DropdownButtonFormField<AppGender>(
          initialValue: _gender,
          style: _fieldTextStyle,
          dropdownColor: SimfTokens.surface,
          decoration: _whiteFieldDecoration(),
          items: <DropdownMenuItem<AppGender>>[
            DropdownMenuItem<AppGender>(
              value: AppGender.unspecified,
              child: Text(l10n.genderUnspecified),
            ),
            DropdownMenuItem<AppGender>(
              value: AppGender.male,
              child: Text(l10n.genderMale),
            ),
            DropdownMenuItem<AppGender>(
              value: AppGender.female,
              child: Text(l10n.genderFemale),
            ),
          ],
          onChanged: _submitting
              ? null
              : (value) =>
                  setState(() => _gender = value ?? AppGender.unspecified),
        ),
      ],
    );
  }

  Widget _buildIdImageField(AppL10n l10n) {
    final bytes = _idImageBytes;
    if (bytes == null) {
      return Align(
        alignment: AlignmentDirectional.centerStart,
        child: OutlinedButton.icon(
          onPressed: _submitting ? null : () => unawaited(_pickIdImage()),
          icon: const Icon(Icons.attach_file, color: SimfTokens.accent),
          label: Text(l10n.attachIdImageLabel),
        ),
      );
    }
    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: ClipRRect(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Image.memory(bytes, width: 48, height: 48, fit: BoxFit.cover),
      ),
      title: Text(
        l10n.idImageAttachedLabel,
        style: const TextStyle(color: SimfTokens.surface),
      ),
      subtitle: Text(
        _idImageName ?? '',
        overflow: TextOverflow.ellipsis,
        style: const TextStyle(color: SimfTokens.txtSecondary),
      ),
      trailing: TextButton(
        onPressed: _submitting ? null : _removeIdImage,
        style: TextButton.styleFrom(foregroundColor: SimfTokens.accent),
        child: Text(l10n.removeLabel),
      ),
    );
  }

  Widget _buildOrganisationField(AppL10n l10n) {
    if (_organisationId != null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _FieldLabel(l10n.organisationLabel),
          InputDecorator(
            decoration: _whiteFieldDecoration(),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    _organisationLabel ?? l10n.organisationSelected,
                    style: _fieldTextStyle,
                  ),
                ),
                TextButton(
                  onPressed: _submitting ? null : _clearOrganisation,
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
        TextField(
          controller: _organisationSearch,
          enabled: !_submitting,
          style: _fieldTextStyle,
          decoration: _whiteFieldDecoration(
            hintText: l10n.organisationSearchHint,
            prefixIcon: const Icon(Icons.search, color: SimfTokens.inkMuted),
          ),
          onChanged: _onOrganisationSearchChanged,
        ),
        if (_organisationSearch.text.trim().isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space2),
          if (_organisationResults.isEmpty)
            Padding(
              padding: const EdgeInsets.all(SimfTokens.space2),
              child: Text(
                l10n.organisationEmpty,
                style: const TextStyle(color: SimfTokens.txtSecondary),
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
                      style: const TextStyle(color: SimfTokens.surface),
                    ),
                    subtitle: organisation.city == null
                        ? null
                        : Text(
                            organisation.city!,
                            style: const TextStyle(
                              color: SimfTokens.txtSecondary,
                            ),
                          ),
                    onTap: _submitting
                        ? null
                        : () => _selectOrganisation(organisation, l10n),
                  ),
                ),
        ],
      ],
    );
  }

  Widget _buildProfileTypeField(AppL10n l10n) {
    if (_profileTypes.isEmpty) {
      return const SizedBox.shrink();
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        _FieldLabel(l10n.profileTypeLabel),
        const SizedBox(height: SimfTokens.space1),
        Wrap(
          spacing: SimfTokens.space2,
          runSpacing: SimfTokens.space2,
          children: _profileTypes
              .map(
                (type) => ChoiceChip(
                  label: Text(l10n.isArabic ? type.nameArabic : type.name),
                  selected: _profileTypeId == type.id,
                  onSelected: _submitting
                      ? null
                      : (value) => setState(
                            () => _profileTypeId = value ? type.id : null,
                          ),
                ),
              )
              .toList(),
        ),
      ],
    );
  }

  Widget _buildInterestsField(AppL10n l10n) {
    if (_interests.isEmpty) {
      return Text(
        l10n.interestsEmpty,
        style: const TextStyle(color: SimfTokens.txtSecondary),
      );
    }
    final showRequiredError = _triedSubmit && _interestIds.isEmpty;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: <Widget>[
            Flexible(
              child: Text(
                l10n.interestsHelper,
                style: const TextStyle(
                  color: SimfTokens.txtSecondary,
                  fontSize: SimfTokens.textSm,
                ),
              ),
            ),
            Text(
              l10n.interestsCounter(_interestIds.length),
              style: const TextStyle(
                color: SimfTokens.txtTertiary,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space3),
        Wrap(
          spacing: SimfTokens.space2,
          runSpacing: SimfTokens.space2,
          children: _interests
              .map(
                (interest) => FilterChip(
                  label: Text(
                    l10n.isArabic ? interest.nameArabic : interest.name,
                  ),
                  selected: _interestIds.contains(interest.id),
                  onSelected: _submitting
                      ? null
                      : (value) => _toggleInterest(interest.id, value, l10n),
                ),
              )
              .toList(),
        ),
        if (showRequiredError) ...<Widget>[
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.interestsRequired,
            style: const TextStyle(color: SimfTokens.danger),
          ),
        ],
      ],
    );
  }

  /// A `.dark-form` section caption — the kicker above a field group
  /// (txt-3 tertiary, uppercase-weight, letter-spaced).
  Widget _sectionHeader(String title) {
    return Padding(
      padding: const EdgeInsets.only(
        top: SimfTokens.space5,
        bottom: SimfTokens.space3,
      ),
      child: Text(
        title,
        style: const TextStyle(
          color: SimfTokens.txtTertiary,
          fontWeight: FontWeight.w700,
          fontSize: SimfTokens.textXs,
          letterSpacing: 0.8,
        ),
      ),
    );
  }

  // ---- Styling helpers (mockup `.dark-form` / `.mfield` / `.type-chip`) -----

  /// Ink-on-white field text, matching the mockup's `.mfield .in` colour.
  static const TextStyle _fieldTextStyle = TextStyle(
    color: SimfTokens.ink,
    fontSize: SimfTokens.textMd,
  );

  /// Per-field white decoration overriding the navy theme to the mockup's
  /// `.mfield .in` (white fill, ink hint, accent focus ring) — the same field
  /// treatment as the Page 005 sign-up card.
  InputDecoration _whiteFieldDecoration({
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
      filled: true,
      fillColor: SimfTokens.surface,
      hintStyle: const TextStyle(color: SimfTokens.inkMuted),
      border: const OutlineInputBorder(
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
        borderSide: BorderSide.none,
      ),
      enabledBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
        borderSide: BorderSide.none,
      ),
      focusedBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
        borderSide: BorderSide(color: SimfTokens.accent),
      ),
    );
  }

  /// `.type-grid` segmented selector — transparent on navy with an accent
  /// fill + navy text on the chosen segment, mirroring `.type-chip.on`.
  static final ButtonStyle _segmentedStyle = ButtonStyle(
    backgroundColor: WidgetStateProperty.resolveWith<Color>(
      (states) => states.contains(WidgetState.selected)
          ? SimfTokens.accent
          : Colors.transparent,
    ),
    foregroundColor: WidgetStateProperty.resolveWith<Color>(
      (states) => states.contains(WidgetState.selected)
          ? SimfTokens.navy
          : SimfTokens.txtSecondary,
    ),
    side: WidgetStateProperty.all(
      const BorderSide(color: SimfTokens.line),
    ),
  );

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

/// A field caption above its input — the mockup's `.mfield label`
/// (txt-2 secondary on the navy form).
class _FieldLabel extends StatelessWidget {
  const _FieldLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: SimfTokens.space2),
      child: Text(
        text,
        style: const TextStyle(
          color: SimfTokens.txtSecondary,
          fontSize: SimfTokens.textSm,
          fontWeight: FontWeight.w500,
        ),
      ),
    );
  }
}
