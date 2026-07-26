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
import '../../app/theme/app_assets.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_language_toggle.dart';
import '../../app/widgets/simf_logo.dart';
import '../../app/widgets/simf_svg_icon.dart';
import '../../core/validation/digit_normalization.dart';
import '../../core/widgets/simf_field_label.dart';
import '../../core/widgets/simf_picker_field.dart';
import '../../core/validation/phone_validation.dart';
import '../../core/validation/required_validation.dart';
import '../../core/validation/saudi_id_validation.dart';
import '../account/data/profile_models.dart';
import '../account/data/profile_repository.dart';
import '../account/widgets/lookup_search_sheet.dart';
import '../account/widgets/sign_up_visitor_header_avatar.dart';
import 'data/staff_models.dart';
import 'data/staff_repository.dart';

/// D-509 — "إنشاء ملف زائر" / add a visitor at the exhibition (staff). Figma
/// 1467:12357 — the navy header (logo + forum name + back + globe) over a beige
/// card holding a two-column form (single column on phones). Staff fill in the
/// walk-in visitor's details and submit; the API creates a **PendingApproval**
/// visitor (no QR until an admin approves, D-425) and the optional ID-document +
/// personal photo are attached afterwards.
///
/// Role-gated to `AppRole.staff`+ (router) and the server's
/// `Visitors.RegisterOnsite` permission. The visitor **classification**
/// (ProfileType) is required by the API but not shown in the frame, so it is
/// auto-assigned to the seeded "Normal" audience tier (parity with self-service
/// sign-up); the server re-validates everything and returns a bilingual message.
///
/// Route: `RouteNames.staffRegisterVisitor` (`/staff/register-visitor`, #114),
/// from the staff-only drawer. Data: `profileRepository` (countries / visitor
/// profile-types / organisations) + `staffRepository.registerVisitor` (+ the
/// optional id-document / avatar uploads). Perf: one eager lookup load, no
/// scrolling list. Contract: `StaffWalkInRequest` / `StaffWalkInResult` JSON
/// is frozen (D-219).
class StaffRegisterVisitorScreen extends ConsumerStatefulWidget {
  const StaffRegisterVisitorScreen({super.key});

  @override
  ConsumerState<StaffRegisterVisitorScreen> createState() =>
      _StaffRegisterVisitorScreenState();
}

enum _DocType { iqama, passport }

class _StaffRegisterVisitorScreenState
    extends ConsumerState<StaffRegisterVisitorScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();

  final TextEditingController _email = TextEditingController();
  final TextEditingController _arabicName = TextEditingController();
  final TextEditingController _englishName = TextEditingController();
  final TextEditingController _jobTitle = TextEditingController();
  final TextEditingController _jobTitleArabic = TextEditingController();
  final TextEditingController _phone = TextEditingController();
  final TextEditingController _nationalId = TextEditingController();
  final TextEditingController _documentNumber = TextEditingController();

  AppGender _gender = AppGender.male;
  String? _nationalityCode;
  String? _organisationId;
  String? _profileTypeId;
  _DocType _docType = _DocType.iqama;

  bool get _isSaudi => _nationalityCode == 'SA';

  List<CountryItem> _countries = const <CountryItem>[];
  List<OrganisationItem> _organisations = const <OrganisationItem>[];

  Uint8List? _idBytes;
  String? _idName;
  Uint8List? _photoBytes;
  String? _photoName;

  bool _loading = true;
  String? _loadError;
  bool _submitting = false;
  bool _triedSubmit = false;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  @override
  void dispose() {
    _email.dispose();
    _arabicName.dispose();
    _englishName.dispose();
    _jobTitle.dispose();
    _jobTitleArabic.dispose();
    _phone.dispose();
    _nationalId.dispose();
    _documentNumber.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final repo = ref.read(profileRepositoryProvider);
    setState(() {
      _loading = true;
      _loadError = null;
    });
    try {
      final results = await Future.wait(<Future<Object>>[
        repo.getCountries(),
        repo.getProfileTypes(isVisitor: true),
        repo.searchOrganisations(top: 200),
      ]);
      if (!mounted) {
        return;
      }
      final types = results[1] as List<ProfileTypeItem>;
      setState(() {
        _countries = results[0] as List<CountryItem>;
        _organisations = results[2] as List<OrganisationItem>;
        _nationalityCode ??=
            _countries.any((c) => c.code == 'SA') ? 'SA' : null;
        _profileTypeId = _defaultVisitorTypeId(types);
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

  /// The classification is not shown in the frame; auto-assign the seeded
  /// "Normal" (عادي) audience tier, falling back to the only row (parity with
  /// the self-service sign-up's visitor lock, C5/D-371).
  static String? _defaultVisitorTypeId(List<ProfileTypeItem> types) {
    final normal = types.where((t) => t.name == 'Normal').toList();
    if (normal.isNotEmpty) {
      return normal.first.id;
    }
    return types.length == 1 ? types.first.id : null;
  }

  Future<void> _pickImage(bool isIdDocument) async {
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
        if (isIdDocument) {
          _idBytes = bytes;
          _idName = file.name;
        } else {
          _photoBytes = bytes;
          _photoName = file.name;
        }
      });
    } catch (_) {
      // The gallery is unavailable; the attachments are optional, so ignore.
    }
  }

  Future<void> _submit() async {
    setState(() => _triedSubmit = true);
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final formValid = _formKey.currentState?.validate() ?? false;
    final ok = formValid &&
        _nationalityCode != null &&
        _organisationId != null &&
        _profileTypeId != null &&
        _gender != AppGender.unspecified;
    if (!ok) {
      setState(() {});
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.staffCompletePrompt)));
      return;
    }

    final englishName = _englishName.text.trim();
    final arabicName = _arabicName.text.trim();
    final isSaudi = _isSaudi;
    final request = StaffWalkInRequest(
      displayName: englishName.isNotEmpty ? englishName : arabicName,
      arabicName: arabicName,
      englishName: englishName,
      profileTypeId: _profileTypeId!,
      nationalityCode: _nationalityCode!,
      isSaudi: isSaudi,
      gender: _gender,
      email: _emptyToNull(_email.text),
      jobTitle: _emptyToNull(_jobTitle.text),
      jobTitleArabic: _emptyToNull(_jobTitleArabic.text),
      organisationId: _organisationId,
      nationalId: isSaudi ? _emptyToNull(_nationalId.text) : null,
      iqamaNumber: !isSaudi && _docType == _DocType.iqama
          ? _emptyToNull(_documentNumber.text)
          : null,
      passportNumber: !isSaudi && _docType == _DocType.passport
          ? _emptyToNull(_documentNumber.text)
          : null,
      saudiMobile: isSaudi ? _emptyToNull(_phone.text) : null,
      internationalMobile: !isSaudi ? _emptyToNull(_phone.text) : null,
    );

    setState(() => _submitting = true);
    final repo = ref.read(staffRepositoryProvider);
    try {
      final result = await repo.registerVisitor(request);
      // Attach the optional images by the new visitor's id; a failed upload does
      // not undo the (already-created) registration — surface it but keep going.
      await _uploadAttachments(repo, result.userId);
      if (!mounted) {
        return;
      }
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.staffRegisterSuccess)));
      _resetForm();
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(
              e.message.trim().isNotEmpty ? e.message : l10n.staffRegisterError,
            ),
          ),
        );
    } finally {
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  Future<void> _uploadAttachments(StaffRepository repo, String userId) async {
    if (userId.isEmpty) {
      return;
    }
    final idBytes = _idBytes;
    final idName = _idName;
    if (idBytes != null && idName != null) {
      try {
        await repo.uploadIdImage(
          userId: userId,
          bytes: idBytes,
          filename: idName,
        );
      } on ApiFailure {
        // Non-fatal — the account exists; the document can be added later.
      }
    }
    final photoBytes = _photoBytes;
    final photoName = _photoName;
    if (photoBytes != null && photoName != null) {
      try {
        await repo.uploadAvatar(
          userId: userId,
          bytes: photoBytes,
          filename: photoName,
        );
      } on ApiFailure {
        // Non-fatal.
      }
    }
  }

  void _resetForm() {
    setState(() {
      _email.clear();
      _arabicName.clear();
      _englishName.clear();
      _jobTitle.clear();
      _jobTitleArabic.clear();
      _phone.clear();
      _nationalId.clear();
      _documentNumber.clear();
      _gender = AppGender.male;
      _docType = _DocType.iqama;
      _idBytes = null;
      _idName = null;
      _photoBytes = null;
      _photoName = null;
      _triedSubmit = false;
    });
    _formKey.currentState?.reset();
  }

  static String? _emptyToNull(String value) {
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

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
    context.goNamed(RouteNames.home);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: SafeArea(
        bottom: false,
        child: Column(
          children: <Widget>[
            _buildHeader(l10n),
            Expanded(child: _buildBody(l10n)),
          ],
        ),
      ),
    );
  }

  /// The navy header block (Figma 1467:12565 — #071832): the forced-LTR back +
  /// globe row, then the centred forum title with the crest.
  Widget _buildHeader(AppL10n l10n) {
    return Container(
      color: SimfTokens.navyHeader,
      padding: const EdgeInsets.only(bottom: SimfTokens.space6),
      child: Column(
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space5,
              SimfTokens.space2,
              SimfTokens.space5,
              0,
            ),
            child: Row(
              textDirection: TextDirection.ltr,
              children: <Widget>[
                // Circular navy back button (Figma) — auth-back SVG never mirrors
                // under RTL (D-601), matching the sign-in screen's pattern.
                Padding(
                  padding: const EdgeInsets.all(SimfTokens.space2),
                  child: Material(
                    color: SimfTokens.navyDeep,
                    shape: const CircleBorder(),
                    child: InkWell(
                      customBorder: const CircleBorder(),
                      onTap: _back,
                      child: const Padding(
                        padding: EdgeInsets.all(10),
                        child: SimfSvgIcon(
                          AppAssets.authBack,
                          size: 24,
                          color: SimfTokens.surface,
                        ),
                      ),
                    ),
                  ),
                ),
                const Spacer(),
                SimfLanguageToggle(
                  onPressed: _toggleLanguage,
                  busy: _submitting,
                ),
              ],
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              const SimfLogo(size: 44),
              const SizedBox(width: SimfTokens.space4),
              Flexible(
                child: Text(
                  l10n.signInForumTitle,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: SimfTokens.text32,
                    fontWeight: FontWeight.w500,
                    color: SimfTokens.surface,
                  ),
                ),
              ),
            ],
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
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                l10n.staffRegisterError,
                textAlign: TextAlign.center,
                style: const TextStyle(color: SimfTokens.beigeBorder),
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
    return Form(
      key: _formKey,
      child: SingleChildScrollView(
        padding: EdgeInsets.zero,
        child: Material(
          color: SimfTokens.cardBeige,
          borderRadius: SimfTokens.borderRadiusSmall,
          child: Padding(
            padding: const EdgeInsets.all(SimfTokens.space6),
            child: LayoutBuilder(
              builder: (context, constraints) {
                final wide = constraints.maxWidth >= 600;
                return _buildForm(l10n, wide);
              },
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildForm(AppL10n l10n, bool wide) {
    const gap = SizedBox(height: SimfTokens.space8);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // Card head — avatar (left) + title (right), Figma 805:1441.
        Row(
          children: <Widget>[
            const SignUpVisitorHeaderAvatar(bytes: null),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: Text(
                l10n.staffRegisterVisitorTitle,
                textAlign: TextAlign.end,
                style: const TextStyle(
                  fontSize: SimfTokens.textHero,
                  fontWeight: FontWeight.w600,
                  color: SimfTokens.headlineInk,
                ),
              ),
            ),
          ],
        ),
        gap,
        _twoCol(
          wide,
          _textField(
            l10n.staffEmailLabel,
            _email,
            keyboardType: TextInputType.emailAddress,
            ltr: true,
            maxLength: 50,
          ),
          _textField(
            l10n.staffPhoneLabel,
            _phone,
            keyboardType: TextInputType.phone,
            ltr: true,
            maxLength: 16,
            validator: _validatePhone,
          ),
        ),
        gap,
        _twoCol(
          wide,
          _textField(
            l10n.arabicNameLabel,
            _arabicName,
            maxLength: 100,
            validator: (v) => _required(l10n, v),
            inputFormatters: <TextInputFormatter>[
              FilteringTextInputFormatter.allow(RegExp(r'[ء-ي\s]')),
            ],
          ),
          _textField(
            l10n.englishNameLabel,
            _englishName,
            ltr: true,
            maxLength: 100,
            validator: (v) => _required(l10n, v),
            inputFormatters: <TextInputFormatter>[
              FilteringTextInputFormatter.allow(RegExp(r'[A-Za-z\s]')),
            ],
          ),
        ),
        gap,
        _twoCol(
          wide,
          _genderField(l10n),
          _nationalityField(l10n),
        ),
        gap,
        _twoCol(
          wide,
          _documentField(l10n),
          _isSaudi ? const SizedBox.shrink() : _documentNumberField(l10n),
        ),
        gap,
        _twoCol(
          wide,
          _textField(
            l10n.jobTitleLabel,
            _jobTitle,
            maxLength: 100,
            // D-723 — required (matches the app self-registration form).
            validator: (v) => _required(l10n, v),
          ),
          // Optional Arabic job title — the backend already carries
          // AdminWalkInRegistrationRequest.JobTitleArabic; capture it here too.
          _textField(
            l10n.jobTitleArabicLabel,
            _jobTitleArabic,
            maxLength: 100,
          ),
        ),
        gap,
        _organisationField(l10n),
        gap,
        _twoCol(
          wide,
          _attachField(
            l10n.staffAttachIdLabel,
            l10n.staffAttachFile,
            _idName,
            () => unawaited(_pickImage(true)),
          ),
          _attachField(
            l10n.staffAttachPhotoLabel,
            l10n.staffAttachPhoto,
            _photoName,
            () => unawaited(_pickImage(false)),
          ),
        ),
        const SizedBox(height: SimfTokens.space6),
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
                fontSize: SimfTokens.textTitle,
                fontWeight: FontWeight.w600,
                decoration: TextDecoration.underline,
              ),
            ),
          ),
        ),
        gap,
        // التالي — full-width gold CTA, h56 (Figma 805:1547).
        SizedBox(
          height: 56,
          child: FilledButton(
            onPressed: _submitting ? null : () => unawaited(_submit()),
            style: FilledButton.styleFrom(
              backgroundColor: SimfTokens.accent,
              foregroundColor: SimfTokens.surface,
              shape: RoundedRectangleBorder(borderRadius: SimfTokens.borderRadiusSmall),
            ),
            child: _submitting
                ? const SizedBox(
                    height: 20,
                    width: 20,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: SimfTokens.surface,
                    ),
                  )
                : Text(
                    l10n.nextLabel,
                    style: const TextStyle(
                      fontSize: SimfTokens.textTitle,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
          ),
        ),
      ],
    );
  }

  /// Lays two fields side-by-side on a wide (tablet) card, stacked on a phone.
  Widget _twoCol(bool wide, Widget a, Widget b) {
    if (b is SizedBox) {
      // The second slot is empty (e.g. Saudi → no document-type toggle).
      return a;
    }
    if (!wide) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[a, const SizedBox(height: SimfTokens.space6), b],
      );
    }
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Expanded(child: a),
        const SizedBox(width: SimfTokens.space4),
        Expanded(child: b),
      ],
    );
  }

  Widget _textField(
    String label,
    TextEditingController controller, {
    String? Function(String?)? validator,
    TextInputType? keyboardType,
    List<TextInputFormatter>? inputFormatters,
    int? maxLength,
    bool ltr = false,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(label, color: SimfTokens.navy, fontSize: SimfTokens.textXxl),
        const SizedBox(height: SimfTokens.space4),
        TextFormField(
          controller: controller,
          keyboardType: keyboardType,
          textDirection: ltr ? TextDirection.ltr : null,
          inputFormatters: inputFormatters,
          maxLength: maxLength,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: validator,
          style: _inputStyle,
          decoration: _decoration(counterText: ''),
        ),
      ],
    );
  }

  Widget _genderField(AppL10n l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.genderLabel, color: SimfTokens.navy, fontSize: SimfTokens.textXxl),
        const SizedBox(height: SimfTokens.space4),
        Row(
          children: <Widget>[
            Expanded(
              child: _Pill(
                label: l10n.genderMale,
                selected: _gender == AppGender.male,
                onTap: () => setState(() => _gender = AppGender.male),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: _Pill(
                label: l10n.genderFemale,
                selected: _gender == AppGender.female,
                onTap: () => setState(() => _gender = AppGender.female),
              ),
            ),
          ],
        ),
      ],
    );
  }

  Future<String?> _openLookupSheet({
    required List<PickerOption> options,
    required String searchHint,
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
      ),
    );
  }

  Widget _nationalityField(AppL10n l10n) {
    final isArabic = l10n.isArabic;
    final selected = _countries
        .where((c) => c.code == _nationalityCode)
        .toList();
    final hasValue = selected.isNotEmpty;
    final label = hasValue
        ? (isArabic ? selected.first.nameArabic : selected.first.name)
        : l10n.nationalityLabel;
    final showError = _triedSubmit && _nationalityCode == null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.nationalityLabel, color: SimfTokens.navy, fontSize: SimfTokens.textXxl),
        const SizedBox(height: SimfTokens.space4),
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
      }
    });
  }

  Widget _documentField(AppL10n l10n) {
    if (_isSaudi) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          SimfFieldLabel(l10n.nationalIdLabel, color: SimfTokens.navy, fontSize: SimfTokens.textXxl),
          const SizedBox(height: SimfTokens.space4),
          TextFormField(
            controller: _nationalId,
            keyboardType: TextInputType.number,
            maxLength: 10,
            inputFormatters: <TextInputFormatter>[
              const WesternDigitsFormatter(),
              FilteringTextInputFormatter.digitsOnly,
            ],
            autovalidateMode: AutovalidateMode.onUserInteraction,
            validator: (v) => _validateNationalId(l10n, v),
            style: _inputStyle,
            decoration: _decoration(counterText: ''),
          ),
        ],
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.documentTypeLabel, color: SimfTokens.navy, fontSize: SimfTokens.textXxl),
        const SizedBox(height: SimfTokens.space4),
        Row(
          children: <Widget>[
            Expanded(
              child: _Pill(
                label: l10n.iqamaSegment,
                selected: _docType == _DocType.iqama,
                onTap: () => setState(() {
                  _docType = _DocType.iqama;
                  _documentNumber.clear();
                }),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: _Pill(
                label: l10n.passportSegment,
                selected: _docType == _DocType.passport,
                onTap: () => setState(() {
                  _docType = _DocType.passport;
                  _documentNumber.clear();
                }),
              ),
            ),
          ],
        ),
      ],
    );
  }

  Widget _documentNumberField(AppL10n l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.documentNumberLabel, color: SimfTokens.navy, fontSize: SimfTokens.textXxl),
        const SizedBox(height: SimfTokens.space4),
        TextFormField(
          controller: _documentNumber,
          maxLength: _docType == _DocType.iqama ? 10 : 9,
          inputFormatters: const <TextInputFormatter>[WesternDigitsFormatter()],
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: (v) => _validateDocumentNumber(l10n, v),
          style: _inputStyle,
          decoration: _decoration(counterText: ''),
        ),
      ],
    );
  }

  Widget _organisationField(AppL10n l10n) {
    final isArabic = l10n.isArabic;
    final selected = _organisations
        .where((o) => o.id == _organisationId)
        .toList();
    final hasValue = selected.isNotEmpty;
    final label = hasValue
        ? (isArabic ? selected.first.nameAr : (selected.first.nameEn ?? selected.first.nameAr))
        : l10n.staffOrganisationLabel;
    final showError = _triedSubmit && _organisationId == null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.staffOrganisationLabel, color: SimfTokens.navy, fontSize: SimfTokens.textXxl),
        const SizedBox(height: SimfTokens.space4),
        SimfPickerField(
          fieldKey: 'organisationPicker',
          displayText: label,
          isPlaceholder: !hasValue,
          onTap: () => unawaited(_pickOrganisation(l10n)),
          errorText: showError ? l10n.requiredField : null,
        ),
      ],
    );
  }

  Future<void> _pickOrganisation(AppL10n l10n) async {
    final pickedId = await _openLookupSheet(
      options: <PickerOption>[
        for (final OrganisationItem o in _organisations)
          PickerOption(
            value: o.id,
            label: l10n.isArabic
                ? (o.nameAr.isNotEmpty ? o.nameAr : o.nameEn ?? o.id)
                : (o.nameEn ?? o.nameAr),
            search: '${o.nameEn ?? ''} ${o.nameAr}',
          ),
      ],
      searchHint: l10n.organisationSearchHint,
    );
    if (pickedId == null || !mounted) {
      return;
    }
    setState(() => _organisationId = pickedId);
  }

  Widget _attachField(
    String label,
    String action,
    String? pickedName,
    VoidCallback onTap,
  ) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(label, color: SimfTokens.navy, fontSize: SimfTokens.textXxl),
        const SizedBox(height: SimfTokens.space4),
        InkWell(
          onTap: onTap,
          borderRadius: SimfTokens.borderRadiusSmall,
          child: Container(
            height: 56,
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
            decoration: BoxDecoration(
              color: SimfTokens.surface,
              borderRadius: SimfTokens.borderRadiusSmall,
              border: Border.all(color: SimfTokens.beigeBorder),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Flexible(
                  child: Text(
                    pickedName ?? action,
                    overflow: TextOverflow.ellipsis,
                    style: pickedName == null
                        ? _inputStyle.copyWith(color: SimfTokens.greyText)
                        : _inputStyle,
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
                const Icon(
                  Icons.add_circle_outline,
                  color: SimfTokens.accent,
                  size: 28,
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  String? _required(AppL10n l10n, String? value) =>
      isBlank(value) ? l10n.requiredField : null;

  // D-700 — mirror the self-service shape + Luhn checks client-side so staff get
  // instant feedback (the server already enforces the same via
  // AdminWalkInRegistrationRequestValidator). Empty keeps the "required" message.
  String? _validateNationalId(AppL10n l10n, String? value) {
    final id = value?.trim() ?? '';
    if (id.isEmpty) {
      return _required(l10n, value);
    }
    return isValidNationalId(id) ? null : l10n.nationalIdInvalid;
  }

  String? _validateDocumentNumber(AppL10n l10n, String? value) {
    final number = value?.trim() ?? '';
    if (number.isEmpty) {
      return _required(l10n, value);
    }
    if (_docType == _DocType.iqama) {
      return isValidIqama(number) ? null : l10n.iqamaInvalid;
    }
    return isValidPassport(number) ? null : l10n.passportInvalid;
  }

  /// Phone is required server-side (Saudi or international); validate inline like
  /// every other required field, with the same standard shapes as self-service.
  String? _validatePhone(String? value) {
    final l10n = AppL10n.of(context);
    final phone = value?.trim() ?? '';
    if (phone.isEmpty) {
      return l10n.requiredField;
    }
    final valid = _isSaudi
        ? isStandardSaudiMobile(phone)
        : isStandardInternationalMobile(phone);
    if (valid) {
      return null;
    }
    return _isSaudi ? l10n.saudiMobileInvalid : l10n.internationalMobileInvalid;
  }

  InputDecoration _decoration({
    String? counterText,
    String? errorText,
    Widget? suffixIcon,
  }) {
    return InputDecoration(
      counterText: counterText,
      errorText: errorText,
      suffixIcon: suffixIcon,
      filled: true,
      fillColor: SimfTokens.surface,
      contentPadding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space4,
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: SimfTokens.borderRadiusSmall,
        borderSide: const BorderSide(color: SimfTokens.beigeBorder),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: SimfTokens.borderRadiusSmall,
        borderSide: const BorderSide(color: SimfTokens.accent),
      ),
      border: const OutlineInputBorder(borderRadius: SimfTokens.borderRadiusSmall),
    );
  }

  static const TextStyle _inputStyle = TextStyle(
    fontFamily: 'Inter',
    fontFamilyFallback: <String>['Cairo'],
    fontSize: SimfTokens.textTitle,
    color: SimfTokens.inputInk,
  );
}

class _Pill extends StatelessWidget {
  const _Pill({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Figma "Tabbing" half: selected = gold fill (radius 4), unselected = white
    // with a thin navy hairline (radius 7); bold 20px label.
    final radius = BorderRadius.circular(selected ? 4 : 7);
    return InkWell(
      onTap: onTap,
      borderRadius: radius,
      child: Container(
        height: 48,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: selected ? SimfTokens.accent : SimfTokens.surface,
          borderRadius: radius,
          border: selected
              ? null
              : Border.all(color: SimfTokens.navy, width: 0.6),
        ),
        child: Text(
          label,
          style: TextStyle(
            color: selected ? SimfTokens.surface : SimfTokens.navyDeep,
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textXl,
          ),
        ),
      ),
    );
  }
}
