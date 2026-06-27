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
import '../profile/data/profile_models.dart';
import '../profile/data/profile_repository.dart';
import '../profile/phone_validation.dart';
import 'data/staff_models.dart';
import 'data/staff_repository.dart';

const BorderRadius _radius4 =
    BorderRadius.all(Radius.circular(SimfTokens.radiusSmall));

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
        child: Column(
          children: <Widget>[
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              child: Row(
                textDirection: TextDirection.ltr,
                children: <Widget>[
                  IconButton(
                    onPressed: _back,
                    icon: const Icon(
                      Icons.arrow_back_ios_new,
                      color: Colors.white,
                      size: 20,
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
                        shape:
                            const RoundedRectangleBorder(borderRadius: _radius4),
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
            const SizedBox(height: 16),
            Expanded(child: _buildBody(l10n)),
          ],
        ),
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
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 760),
            child: Material(
              color: SimfTokens.cardBeige,
              borderRadius: _radius4,
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: LayoutBuilder(
                  builder: (context, constraints) {
                    final wide = constraints.maxWidth >= 640;
                    return _buildForm(l10n, wide);
                  },
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildForm(AppL10n l10n, bool wide) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Text(
          l10n.staffRegisterVisitorTitle,
          style: const TextStyle(
            fontSize: 24,
            fontWeight: FontWeight.w600,
            color: SimfTokens.headlineInk,
          ),
        ),
        const SizedBox(height: 16),
        _notice(l10n.staffCompletePrompt),
        const SizedBox(height: 24),
        _twoCol(
          wide,
          _textField(
            l10n.staffEmailLabel,
            _email,
            keyboardType: TextInputType.emailAddress,
            ltr: true,
          ),
          _textField(
            l10n.staffPhoneLabel,
            _phone,
            keyboardType: TextInputType.phone,
            ltr: true,
            validator: _validatePhone,
          ),
        ),
        _twoCol(
          wide,
          _textField(
            l10n.arabicNameLabel,
            _arabicName,
            validator: (v) => _required(l10n, v),
            inputFormatters: <TextInputFormatter>[
              FilteringTextInputFormatter.allow(RegExp(r'[ء-ي\s]')),
            ],
          ),
          _textField(
            l10n.englishNameLabel,
            _englishName,
            ltr: true,
            validator: (v) => _required(l10n, v),
            inputFormatters: <TextInputFormatter>[
              FilteringTextInputFormatter.allow(RegExp(r'[A-Za-z\s]')),
            ],
          ),
        ),
        _twoCol(
          wide,
          _genderField(l10n),
          _nationalityField(l10n),
        ),
        _twoCol(
          wide,
          _documentField(l10n),
          _isSaudi ? const SizedBox.shrink() : _documentNumberField(l10n),
        ),
        _twoCol(
          wide,
          _organisationField(l10n),
          _textField(l10n.jobTitleLabel, _jobTitle, maxLength: 128),
        ),
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
        const SizedBox(height: 8),
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
          onPressed: _submitting ? null : () => unawaited(_submit()),
          child: _submitting
              ? const SizedBox(
                  height: 20,
                  width: 20,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: SimfTokens.navy,
                  ),
                )
              : Text(
                  l10n.nextLabel,
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
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
      return Padding(
        padding: const EdgeInsets.only(bottom: 16),
        child: a,
      );
    }
    if (!wide) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          a,
          const SizedBox(height: 16),
          b,
          const SizedBox(height: 16),
        ],
      );
    }
    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(child: a),
          const SizedBox(width: 16),
          Expanded(child: b),
        ],
      ),
    );
  }

  Widget _notice(String text) => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: SimfTokens.surface,
          border: Border.all(color: SimfTokens.accent),
          borderRadius: _radius4,
        ),
        child: Row(
          children: <Widget>[
            const Icon(Icons.info_outline, size: 18, color: SimfTokens.accent),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                text,
                style:
                    const TextStyle(fontSize: 12, color: SimfTokens.inputInk),
              ),
            ),
          ],
        ),
      );

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
        _FieldLabel(label),
        const SizedBox(height: 8),
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
        _FieldLabel(l10n.genderLabel),
        const SizedBox(height: 8),
        Row(
          children: <Widget>[
            Expanded(
              child: _Pill(
                label: l10n.genderMale,
                selected: _gender == AppGender.male,
                onTap: () => setState(() => _gender = AppGender.male),
              ),
            ),
            const SizedBox(width: 8),
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

  Widget _nationalityField(AppL10n l10n) {
    final isArabic = l10n.isArabic;
    final showError = _triedSubmit && _nationalityCode == null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.nationalityLabel),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          initialValue: _nationalityCode,
          isExpanded: true,
          style: _inputStyle,
          decoration: _decoration(
            errorText: showError ? l10n.nationalityRequired : null,
          ),
          items: <DropdownMenuItem<String>>[
            for (final c in _countries)
              DropdownMenuItem<String>(
                value: c.code,
                child: Text(
                  isArabic ? c.nameArabic : c.name,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
          ],
          onChanged: (code) {
            setState(() {
              final wasSaudi = _isSaudi;
              _nationalityCode = code;
              if (wasSaudi != _isSaudi) {
                _nationalId.clear();
                _documentNumber.clear();
              }
            });
          },
        ),
      ],
    );
  }

  Widget _documentField(AppL10n l10n) {
    if (_isSaudi) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _FieldLabel(l10n.nationalIdLabel),
          const SizedBox(height: 8),
          TextFormField(
            controller: _nationalId,
            keyboardType: TextInputType.number,
            maxLength: 10,
            autovalidateMode: AutovalidateMode.onUserInteraction,
            validator: (v) => _required(l10n, v),
            style: _inputStyle,
            decoration: _decoration(counterText: ''),
          ),
        ],
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.documentTypeLabel),
        const SizedBox(height: 8),
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
            const SizedBox(width: 8),
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
        _FieldLabel(l10n.documentNumberLabel),
        const SizedBox(height: 8),
        TextFormField(
          controller: _documentNumber,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: (v) => _required(l10n, v),
          style: _inputStyle,
          decoration: _decoration(counterText: ''),
        ),
      ],
    );
  }

  Widget _organisationField(AppL10n l10n) {
    final isArabic = l10n.isArabic;
    final showError = _triedSubmit && _organisationId == null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FieldLabel(l10n.staffOrganisationLabel),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          initialValue: _organisationId,
          isExpanded: true,
          style: _inputStyle,
          decoration: _decoration(
            errorText: showError ? l10n.requiredField : null,
          ),
          items: <DropdownMenuItem<String>>[
            for (final o in _organisations)
              DropdownMenuItem<String>(
                value: o.id,
                child: Text(
                  isArabic ? o.nameAr : (o.nameEn ?? o.nameAr),
                  overflow: TextOverflow.ellipsis,
                ),
              ),
          ],
          onChanged: (id) => setState(() => _organisationId = id),
        ),
      ],
    );
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
        _FieldLabel(label),
        const SizedBox(height: 8),
        InkWell(
          onTap: onTap,
          borderRadius: _radius4,
          child: InputDecorator(
            decoration: _decoration(
              suffixIcon: const Icon(
                Icons.add_circle_outline,
                color: SimfTokens.accent,
              ),
            ),
            child: Text(
              pickedName ?? action,
              overflow: TextOverflow.ellipsis,
              style: pickedName == null
                  ? _inputStyle.copyWith(color: SimfTokens.greyText)
                  : _inputStyle,
            ),
          ),
        ),
      ],
    );
  }

  String? _required(AppL10n l10n, String? value) =>
      (value == null || value.trim().isEmpty) ? l10n.requiredField : null;

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
      contentPadding:
          const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      enabledBorder: OutlineInputBorder(
        borderRadius: _radius4,
        borderSide: const BorderSide(color: SimfTokens.beigeBorder),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: _radius4,
        borderSide: const BorderSide(color: SimfTokens.accent),
      ),
      border: const OutlineInputBorder(borderRadius: _radius4),
    );
  }

  static const TextStyle _inputStyle =
      TextStyle(fontSize: 15, color: SimfTokens.inputInk);
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        fontSize: 13,
        fontWeight: FontWeight.w600,
        color: SimfTokens.headlineInk,
      ),
    );
  }
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
    return InkWell(
      onTap: onTap,
      borderRadius: _radius4,
      child: Container(
        height: 48,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: selected ? SimfTokens.accent : SimfTokens.surface,
          borderRadius: _radius4,
          border: Border.all(
            color: selected ? SimfTokens.accent : SimfTokens.beigeBorder,
          ),
        ),
        child: Text(
          label,
          style: TextStyle(
            color: selected ? Colors.white : SimfTokens.inputInk,
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
    );
  }
}
