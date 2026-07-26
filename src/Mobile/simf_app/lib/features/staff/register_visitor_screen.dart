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
import '../../core/responsive/breakpoints.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/validation/digit_normalization.dart';
import '../../core/validation/phone_validation.dart';
import '../../core/validation/required_validation.dart';
import '../../core/validation/saudi_id_validation.dart';
import '../../core/widgets/simf_field_label.dart';
import '../../core/widgets/simf_image_source_sheet.dart';
import '../../core/widgets/simf_labeled_text_field.dart';
import '../../core/widgets/simf_picker_field.dart';
import '../account/data/profile_models.dart';
import '../account/data/profile_repository.dart';
import '../account/widgets/attachment_field.dart';
import '../account/widgets/beige_tabs.dart';
import '../account/widgets/gender_pills_field.dart';
import '../account/widgets/lookup_search_sheet.dart';
import '../account/widgets/mobile_field.dart';
import '../account/widgets/terms_and_next_buttons.dart';
import 'data/staff_models.dart';
import 'data/staff_repository.dart';

/// D-509 — "إنشاء ملف زائر" / add a visitor at the exhibition (staff). Staff
/// fill in the walk-in visitor's details and submit; the API creates a
/// **PendingApproval** visitor (no QR until an admin approves, D-425) and the
/// optional ID-document + personal photo are attached afterwards.
///
/// Role-gated to `AppRole.staff`+ (router) and the server's
/// `Visitors.RegisterOnsite` permission; the server re-validates everything and
/// returns a bilingual message.
///
/// **BUG-019 rebuild (2026-07-26).** The screen used to hand-roll its own field
/// system (a local `filled: true` decoration + label, raw Material dropdowns, a
/// page-local globe language button, hardcoded `TextDirection.ltr` blocks and
/// raw pixel literals), which is why it read as a different product from the
/// visitor Create-profile screen. It now composes the SAME shared pieces that
/// screen uses — [SimfFormScaffold] (back + the shared language pill + crest),
/// [MaxWidthBody] + [WindowSize] for the responsive card, [SimfLabeledTextField]
/// / [MobileField] on `simfFieldDecoration`, [SimfPickerField] + the searchable
/// [LookupSearchSheet] for every lookup, [GenderPillsField], [BeigeTabs],
/// [AttachmentField] and [TermsAndNextButtons] — plus a camera-or-file
/// attachment source and an operator-chosen visitor classification.
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

/// The card's content cap: a phone/compact window gets the 560 form width the
/// Create-profile screen uses; a tablet gets the wider reading width so the
/// two-column grid has room (both are [MaxWidthBody]'s documented values).
const double _formMaxWidthCompact = 560;
const double _formMaxWidthWide = 840;

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

  // 19l — scroll anchors, in visual order, so a blocked submit can bring the
  // FIRST problem into view instead of leaving the operator at the bottom of a
  // long form with every error off-screen above.
  final GlobalKey _profileTypeAnchor = GlobalKey();
  final GlobalKey _arabicNameAnchor = GlobalKey();
  final GlobalKey _englishNameAnchor = GlobalKey();
  final GlobalKey _nationalityAnchor = GlobalKey();
  final GlobalKey _documentAnchor = GlobalKey();
  final GlobalKey _documentNumberAnchor = GlobalKey();
  final GlobalKey _jobTitleAnchor = GlobalKey();
  final GlobalKey _phoneAnchor = GlobalKey();
  final GlobalKey _organisationAnchor = GlobalKey();

  AppGender _gender = AppGender.male;
  String? _nationalityCode;
  String? _organisationId;
  String? _profileTypeId;
  _DocType _docType = _DocType.iqama;

  bool get _isSaudi => _nationalityCode == 'SA';

  List<CountryItem> _countries = const <CountryItem>[];
  List<OrganisationItem> _organisations = const <OrganisationItem>[];
  List<ProfileTypeItem> _profileTypes = const <ProfileTypeItem>[];

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
      setState(() {
        _countries = results[0] as List<CountryItem>;
        _profileTypes = results[1] as List<ProfileTypeItem>;
        _organisations = results[2] as List<OrganisationItem>;
        _nationalityCode ??=
            _countries.any((c) => c.code == 'SA') ? 'SA' : null;
        _profileTypeId = _defaultVisitorTypeId(_profileTypes);
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

  /// 19g — the operator now PICKS the classification; this only seeds the
  /// field. The seeded "Normal" (عادي) audience tier is the sensible default
  /// (parity with the self-service sign-up's visitor lock, C5/D-371), falling
  /// back to the only row when the lookup has exactly one.
  static String? _defaultVisitorTypeId(List<ProfileTypeItem> types) {
    final normal = types.where((t) => t.name == 'Normal').toList();
    if (normal.isNotEmpty) {
      return normal.first.id;
    }
    return types.length == 1 ? types.first.id : null;
  }

  /// 19f — offer the CAMERA as well as a file pick: a registration desk must be
  /// able to shoot the visitor's document without leaving the app.
  Future<void> _pickImage({required bool isIdDocument}) async {
    final source = await showSimfImageSourceSheet(context);
    if (source == null || !mounted) {
      return;
    }
    try {
      final file = await ImagePicker().pickImage(source: source);
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
    } on Exception {
      // The camera / gallery is unavailable; the attachments are optional, so
      // the registration still goes through without them.
    }
  }

  void _removeImage({required bool isIdDocument}) {
    setState(() {
      if (isIdDocument) {
        _idBytes = null;
        _idName = null;
      } else {
        _photoBytes = null;
        _photoName = null;
      }
    });
  }

  Future<void> _submit() async {
    setState(() => _triedSubmit = true);
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    // 19l — validate() reveals EVERY field error at once (it does not wait for
    // the per-field onUserInteraction gate), and the anchor below scrolls the
    // first problem into view.
    final formValid = _formKey.currentState?.validate() ?? false;
    final ok = formValid &&
        _nationalityCode != null &&
        _organisationId != null &&
        _profileTypeId != null &&
        _gender != AppGender.unspecified;
    if (!ok) {
      setState(() {});
      _revealFirstProblem(l10n);
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

  /// 19l — brings the first invalid field into view after a blocked submit.
  /// The order mirrors the on-screen order so the operator lands on the top-most
  /// problem, not an arbitrary one.
  void _revealFirstProblem(AppL10n l10n) {
    final anchor = _firstProblemAnchor(l10n)?.currentContext;
    if (anchor == null) {
      return;
    }
    unawaited(
      Scrollable.ensureVisible(
        anchor,
        alignment: 0.15,
        duration: const Duration(milliseconds: 250),
      ),
    );
  }

  GlobalKey? _firstProblemAnchor(AppL10n l10n) {
    if (_profileTypeId == null) {
      return _profileTypeAnchor;
    }
    if (_required(l10n, _arabicName.text) != null) {
      return _arabicNameAnchor;
    }
    if (_required(l10n, _englishName.text) != null) {
      return _englishNameAnchor;
    }
    if (_nationalityCode == null) {
      return _nationalityAnchor;
    }
    if (_isSaudi) {
      if (_validateNationalId(l10n, _nationalId.text) != null) {
        return _documentAnchor;
      }
    } else if (_validateDocumentNumber(l10n, _documentNumber.text) != null) {
      return _documentNumberAnchor;
    }
    if (_required(l10n, _jobTitle.text) != null) {
      return _jobTitleAnchor;
    }
    if (_validatePhone(_phone.text) != null) {
      return _phoneAnchor;
    }
    if (_organisationId == null) {
      return _organisationAnchor;
    }
    return null;
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
    // 19a/19b/19c — the shared account/form chrome owns the back chevron, the
    // shared EN/ع language pill and the crest header, so this screen no longer
    // hand-rolls a globe button or pins whole blocks to LTR.
    return SimfFormScaffold(
      pinnedHeader: true,
      onBack: _back,
      busy: _submitting,
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
    // 19c — the window-size class, not an inline `maxWidth >= 600` literal.
    final wide = !WindowSize.of(context).isCompact;
    return Form(
      key: _formKey,
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          0,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        child: MaxWidthBody(
          maxWidth: wide ? _formMaxWidthWide : _formMaxWidthCompact,
          child: Material(
            color: SimfTokens.cardBeige,
            borderRadius: SimfTokens.borderRadiusSmall,
            child: Padding(
              padding: const EdgeInsets.all(SimfTokens.space6),
              child: _buildForm(l10n, wide: wide),
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
              l10n.staffRegisterError,
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

  Widget _buildForm(AppL10n l10n, {required bool wide}) {
    const gap = SizedBox(height: SimfTokens.space4);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // Card head — title then the visitor glyph, laid out by Directionality
        // (19b) rather than a hardcoded direction + TextAlign.end.
        Row(
          children: <Widget>[
            Expanded(
              child: Text(
                l10n.staffRegisterVisitorTitle,
                style: const TextStyle(
                  fontSize: SimfTokens.text24,
                  fontWeight: FontWeight.w600,
                  color: SimfTokens.headlineInk,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space4),
            Container(
              width: SimfTokens.controlHeight,
              height: SimfTokens.controlHeight,
              decoration: const BoxDecoration(
                color: SimfTokens.navyDeep,
                borderRadius: SimfTokens.borderRadiusSmall,
              ),
              child: const Icon(
                Icons.person_outline,
                color: SimfTokens.accent,
                size: SimfTokens.metaIconBox,
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space6),
        _profileTypeField(l10n),
        gap,
        _twoCol(
          wide: wide,
          start: _named(
            l10n.arabicNameLabel,
            KeyedSubtree(
              key: _arabicNameAnchor,
              child: SimfLabeledTextField(
                label: l10n.arabicNameLabel,
                controller: _arabicName,
                maxLength: 100,
                textDirection: TextDirection.rtl,
                inputFormatters: <TextInputFormatter>[
                  FilteringTextInputFormatter.allow(RegExp(r'[ء-ي\s]')),
                ],
                validator: (v) => _required(l10n, v),
              ),
            ),
          ),
          end: _named(
            l10n.englishNameLabel,
            KeyedSubtree(
              key: _englishNameAnchor,
              child: SimfLabeledTextField(
                label: l10n.englishNameLabel,
                controller: _englishName,
                maxLength: 100,
                textDirection: TextDirection.ltr,
                inputFormatters: <TextInputFormatter>[
                  FilteringTextInputFormatter.allow(RegExp(r'[A-Za-z\s]')),
                ],
                validator: (v) => _required(l10n, v),
              ),
            ),
          ),
        ),
        gap,
        _twoCol(
          wide: wide,
          start: _genderField(l10n),
          end: _nationalityField(l10n),
        ),
        gap,
        _twoCol(
          wide: wide,
          start: _documentField(l10n),
          end: _isSaudi ? null : _documentNumberField(l10n),
        ),
        gap,
        _twoCol(
          wide: wide,
          start: _named(
            l10n.jobTitleLabel,
            KeyedSubtree(
              key: _jobTitleAnchor,
              child: SimfLabeledTextField(
                label: l10n.jobTitleLabel,
                controller: _jobTitle,
                maxLength: 100,
                textDirection: TextDirection.ltr,
                // D-723 — required (matches the app self-registration form).
                validator: (v) => _required(l10n, v),
              ),
            ),
          ),
          // Optional Arabic job title — the backend already carries
          // AdminWalkInRegistrationRequest.JobTitleArabic; capture it here too.
          end: _named(
            l10n.jobTitleArabicLabel,
            SimfLabeledTextField(
              label: l10n.jobTitleArabicLabel,
              controller: _jobTitleArabic,
              maxLength: 100,
              textDirection: TextDirection.rtl,
            ),
          ),
        ),
        gap,
        _twoCol(
          wide: wide,
          // 19b — email and phone are genuinely LTR CONTENT, so the inputs stay
          // explicitly LTR while the layout follows the locale.
          start: _named(
            l10n.staffEmailLabel,
            SimfLabeledTextField(
              label: l10n.staffEmailLabel,
              controller: _email,
              maxLength: 50,
              keyboardType: TextInputType.emailAddress,
              textDirection: TextDirection.ltr,
            ),
          ),
          end: _named(
            l10n.staffPhoneLabel,
            KeyedSubtree(
              key: _phoneAnchor,
              child: MobileField(
                saudi: _isSaudi,
                controller: _phone,
                validator: _validatePhone,
              ),
            ),
          ),
        ),
        gap,
        _organisationField(l10n),
        gap,
        _twoCol(
          wide: wide,
          start: Semantics(
            label: l10n.staffAttachIdLabel,
            child: AttachmentField(
              label: l10n.staffAttachIdLabel,
              // 19k — the long "ID / Iqama / passport" detail lives here now,
              // so the caption stays one line like every sibling.
              hintText: l10n.staffAttachIdHint,
              bytes: _idBytes,
              round: false,
              attachLabel: l10n.staffAttachFile,
              attachIcon: Icons.add_circle_outline,
              onAttach: () => unawaited(_pickImage(isIdDocument: true)),
              attachedName: _idName ?? l10n.idImageAttachedLabel,
              actionLabel: l10n.removeLabel,
              onAction: () => _removeImage(isIdDocument: true),
            ),
          ),
          end: Semantics(
            label: l10n.staffAttachPhotoLabel,
            child: AttachmentField(
              label: l10n.staffAttachPhotoLabel,
              // Keeps the two attach boxes on the same baseline as the ID
              // field's hint line, and states the (true) optionality.
              hintText: l10n.staffAttachOptionalHint,
              bytes: _photoBytes,
              round: true,
              attachLabel: l10n.staffAttachPhoto,
              attachIcon: Icons.photo_camera_outlined,
              onAttach: () => unawaited(_pickImage(isIdDocument: false)),
              attachedName: _photoName ?? l10n.idImageAttachedLabel,
              actionLabel: l10n.removeLabel,
              onAction: () => _removeImage(isIdDocument: false),
            ),
          ),
        ),
        const SizedBox(height: SimfTokens.space6),
        TermsAndNextButtons(
          onNext: () => unawaited(_submit()),
          busy: _submitting,
        ),
      ],
    );
  }

  /// Lays two fields side-by-side on a wide (tablet) card, stacked on a phone.
  /// A null [end] (e.g. Saudi → no document-number field) collapses to [start].
  Widget _twoCol({required bool wide, required Widget start, Widget? end}) {
    if (end == null) {
      return start;
    }
    if (!wide) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          start,
          const SizedBox(height: SimfTokens.space4),
          end,
        ],
      );
    }
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Expanded(child: start),
        const SizedBox(width: SimfTokens.space4),
        Expanded(child: end),
      ],
    );
  }

  /// 19h — the shared field widgets render a visible caption but leave the
  /// input itself unnamed for a screen reader; this names it.
  Widget _named(String label, Widget field) =>
      Semantics(label: label, textField: true, child: field);

  Widget _genderField(AppL10n l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.genderLabel),
        const SizedBox(height: SimfTokens.space2),
        GenderPillsField(
          gender: _gender,
          onChanged: (value) => setState(() => _gender = value),
        ),
      ],
    );
  }

  /// 19g — the walk-in classification is no longer silently pinned to the row
  /// literally named "Normal": the operator picks from the visitor-eligible
  /// types (seeded to "Normal" when it exists).
  Widget _profileTypeField(AppL10n l10n) {
    final selected =
        _profileTypes.where((t) => t.id == _profileTypeId).toList();
    final hasValue = selected.isNotEmpty;
    return Column(
      key: _profileTypeAnchor,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.profileTypeLabel),
        const SizedBox(height: SimfTokens.space2),
        Semantics(
          label: l10n.profileTypeLabel,
          child: SimfPickerField(
            fieldKey: 'staffProfileTypePicker',
            displayText: hasValue
                ? (l10n.isArabic
                    ? selected.first.nameArabic
                    : selected.first.name)
                : l10n.profileTypeLabel,
            isPlaceholder: !hasValue,
            onTap: () => unawaited(_pickProfileType(l10n)),
            errorText: (_triedSubmit && _profileTypeId == null)
                ? l10n.profileTypeRequired
                : null,
          ),
        ),
      ],
    );
  }

  Future<void> _pickProfileType(AppL10n l10n) async {
    final picked = await _openLookupSheet(
      options: <PickerOption>[
        for (final ProfileTypeItem t in _profileTypes)
          PickerOption(
            value: t.id,
            label: l10n.isArabic ? t.nameArabic : t.name,
            search: '${t.name} ${t.nameArabic}',
          ),
      ],
      searchHint: l10n.profileTypeSearchHint,
      searchFieldKey: const ValueKey<String>('staffProfileTypeSearchField'),
    );
    if (picked == null || !mounted) {
      return;
    }
    setState(() => _profileTypeId = picked);
  }

  /// 19j — the 57-country list gets the shared searchable picker, exactly like
  /// Create-profile, instead of a raw Material dropdown.
  Widget _nationalityField(AppL10n l10n) {
    final selected =
        _countries.where((c) => c.code == _nationalityCode).toList();
    final hasValue = selected.isNotEmpty;
    return Column(
      key: _nationalityAnchor,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.nationalityLabel),
        const SizedBox(height: SimfTokens.space2),
        Semantics(
          label: l10n.nationalityLabel,
          child: SimfPickerField(
            fieldKey: 'staffNationalityPicker',
            displayText: hasValue
                ? (l10n.isArabic ? selected.first.nameArabic : selected.first.name)
                : l10n.nationalityLabel,
            isPlaceholder: !hasValue,
            onTap: () => unawaited(_pickNationality(l10n)),
            errorText: (_triedSubmit && _nationalityCode == null)
                ? l10n.nationalityRequired
                : null,
          ),
        ),
      ],
    );
  }

  Future<void> _pickNationality(AppL10n l10n) async {
    final picked = await _openLookupSheet(
      options: <PickerOption>[
        for (final CountryItem c in _countries)
          PickerOption(
            value: c.code,
            label: l10n.isArabic ? c.nameArabic : c.name,
            search: '${c.name} ${c.nameArabic}',
          ),
      ],
      searchHint: l10n.searchCountryHint,
      searchFieldKey: const ValueKey<String>('staffCountrySearchField'),
    );
    if (picked == null || !mounted) {
      return;
    }
    setState(() {
      final wasSaudi = _isSaudi;
      _nationalityCode = picked;
      if (wasSaudi != _isSaudi) {
        _nationalId.clear();
        _documentNumber.clear();
      }
    });
  }

  Widget _documentField(AppL10n l10n) {
    if (_isSaudi) {
      return _named(
        l10n.nationalIdLabel,
        KeyedSubtree(
          key: _documentAnchor,
          child: SimfLabeledTextField(
            label: l10n.nationalIdLabel,
            controller: _nationalId,
            keyboardType: TextInputType.number,
            maxLength: 10,
            // The id renders LTR (digits) even under Arabic — genuinely-LTR
            // content, unlike the surrounding layout (19b).
            textDirection: TextDirection.ltr,
            inputFormatters: <TextInputFormatter>[
              const WesternDigitsFormatter(),
              FilteringTextInputFormatter.digitsOnly,
            ],
            validator: (v) => _validateNationalId(l10n, v),
          ),
        ),
      );
    }
    return Column(
      key: _documentAnchor,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.documentTypeLabel),
        const SizedBox(height: SimfTokens.space2),
        Semantics(
          label: l10n.documentTypeLabel,
          child: BeigeTabs(
            options: <String>[l10n.iqamaSegment, l10n.passportSegment],
            selectedIndex: _docType == _DocType.iqama ? 0 : 1,
            onChanged: (index) => setState(() {
              _docType = index == 0 ? _DocType.iqama : _DocType.passport;
              _documentNumber.clear();
            }),
          ),
        ),
      ],
    );
  }

  Widget _documentNumberField(AppL10n l10n) {
    return _named(
      l10n.documentNumberLabel,
      KeyedSubtree(
        key: _documentNumberAnchor,
        child: SimfLabeledTextField(
          label: l10n.documentNumberLabel,
          controller: _documentNumber,
          maxLength: _docType == _DocType.iqama ? 10 : 9,
          textDirection: TextDirection.ltr,
          inputFormatters: const <TextInputFormatter>[WesternDigitsFormatter()],
          validator: (v) => _validateDocumentNumber(l10n, v),
        ),
      ),
    );
  }

  /// 19j — the organisation list also moves to the shared searchable picker;
  /// the type-to-filter sheet handles the 200 loaded rows.
  Widget _organisationField(AppL10n l10n) {
    final selected =
        _organisations.where((o) => o.id == _organisationId).toList();
    final hasValue = selected.isNotEmpty;
    return Column(
      key: _organisationAnchor,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.staffOrganisationLabel),
        const SizedBox(height: SimfTokens.space2),
        Semantics(
          label: l10n.staffOrganisationLabel,
          child: SimfPickerField(
            fieldKey: 'staffOrganisationPicker',
            displayText: hasValue
                ? _organisationName(selected.first, l10n)
                : l10n.organisationSearchHint,
            isPlaceholder: !hasValue,
            onTap: () => unawaited(_pickOrganisation(l10n)),
            errorText: (_triedSubmit && _organisationId == null)
                ? l10n.organisationRequired
                : null,
          ),
        ),
      ],
    );
  }

  Future<void> _pickOrganisation(AppL10n l10n) async {
    final picked = await _openLookupSheet(
      options: <PickerOption>[
        for (final OrganisationItem o in _organisations)
          PickerOption(
            value: o.id,
            label: _organisationName(o, l10n),
            search: '${o.nameAr} ${o.nameEn ?? ''}',
          ),
      ],
      searchHint: l10n.organisationSearchHint,
      searchFieldKey: const ValueKey<String>('staffOrganisationSearchField'),
    );
    if (picked == null || !mounted) {
      return;
    }
    setState(() => _organisationId = picked);
  }

  static String _organisationName(OrganisationItem o, AppL10n l10n) =>
      l10n.isArabic ? o.nameAr : (o.nameEn ?? o.nameAr);

  /// Opens the shared searchable picker sheet and returns the picked value —
  /// the same sheet the Create-profile lookups use (19j).
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
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(SimfTokens.radiusLarge),
        ),
      ),
      builder: (_) => LookupSearchSheet(
        options: options,
        searchHint: searchHint,
        searchFieldKey: searchFieldKey,
      ),
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
}
