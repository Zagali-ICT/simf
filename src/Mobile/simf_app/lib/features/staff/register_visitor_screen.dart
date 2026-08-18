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
import 'package:simf_app/core/motion/motion_durations.dart';
import 'package:simf_app/core/responsive/breakpoints.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/core/validation/phone_validation.dart';
import 'package:simf_app/core/validation/required_validation.dart';
import 'package:simf_app/core/validation/saudi_id_validation.dart';
import 'package:simf_app/core/widgets/simf_image_source_sheet.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';
import 'package:simf_app/features/staff/data/staff_models.dart';
import 'package:simf_app/features/staff/data/staff_repository.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_form.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_form_fields.dart';
import 'package:simf_app/features/staff/widgets/staff_register_load_error.dart';
import 'package:simf_app/features/staff/widgets/staff_upload_failed_dialog.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Staff walk-in visitor registration — route:
/// `RouteNames.staffRegisterVisitor` · D-509
/// Contract: `StaffWalkInRequest` / `StaffWalkInResult` JSON is frozen (D-219).
/// The API creates a **PendingApproval** visitor — no QR until an admin
/// approves (D-425) — and the optional ID document + personal photo are
/// attached afterwards. Role-gated to `AppRole.staff`+ in the router and to the
/// server's `Visitors.RegisterOnsite` permission.
class StaffRegisterVisitorScreen extends ConsumerStatefulWidget {
  const StaffRegisterVisitorScreen({super.key});

  @override
  ConsumerState<StaffRegisterVisitorScreen> createState() =>
      _StaffRegisterVisitorScreenState();
}

/// The two optional images the desk can attach after the account is created.
enum _Attachment { idDocument, photo }

/// A field the SERVER rejected, with the exact value it rejected — so the
/// message shows on that field and clears the moment the operator edits it
/// (DEF-STF-003).
@immutable
class _ServerFieldError {
  const _ServerFieldError({required this.message, required this.rejectedValue});

  final String message;
  final String rejectedValue;
}

/// The card's content cap: a phone/compact window gets the 560 form width the
/// Create-profile screen uses; a tablet gets the wider reading width so the
/// two-column grid has room (both are [MaxWidthBody]'s documented values).
const double _formMaxWidthCompact = 560;
const double _formMaxWidthWide = 840;

class _StaffRegisterVisitorScreenState
    extends ConsumerState<StaffRegisterVisitorScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();

  /// The inputs themselves — controllers, scroll anchors and the validator
  /// bound to each — built once and handed to [RegisterVisitorForm] whole.
  late final RegisterVisitorFormFields _fields;

  AppGender _gender = AppGender.male;
  String? _nationalityCode;
  String? _organisationId;
  String? _profileTypeId;
  VisitorDocType _docType = VisitorDocType.iqama;

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

  /// Server-side field rejections from the last 400, keyed by the request
  /// property name FluentValidation reports (DEF-STF-003).
  final Map<String, _ServerFieldError> _serverErrors =
      <String, _ServerFieldError>{};

  @override
  void initState() {
    super.initState();
    _fields = RegisterVisitorFormFields(
      validateArabicName: _validateArabicName,
      validateEnglishName: _validateEnglishName,
      validateEmail: _validateEmail,
      validateJobTitle: _validateJobTitle,
      validateJobTitleArabic: _validateJobTitleArabic,
      validateNationalId: _validateNationalId,
      validateDocumentNumber: _validateDocumentNumber,
      validatePhone: _validatePhone,
    );
    unawaited(_load());
  }

  @override
  void dispose() {
    _fields.dispose();
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
    setState(() {
      _triedSubmit = true;
      // A fresh attempt re-asks the server; last round's rejections are stale.
      _serverErrors.clear();
    });
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
      _revealFirstProblem();
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.staffCompletePrompt)));
      return;
    }

    final englishName = _fields.englishName.text.trim();
    final arabicName = _fields.arabicName.text.trim();
    final isSaudi = _isSaudi;
    final request = StaffWalkInRequest(
      displayName: englishName.isNotEmpty ? englishName : arabicName,
      arabicName: arabicName,
      englishName: englishName,
      profileTypeId: _profileTypeId!,
      nationalityCode: _nationalityCode!,
      isSaudi: isSaudi,
      gender: _gender,
      email: _emptyToNull(_fields.email.text),
      jobTitle: _emptyToNull(_fields.jobTitle.text),
      jobTitleArabic: _emptyToNull(_fields.jobTitleArabic.text),
      organisationId: _organisationId,
      nationalId: isSaudi ? _emptyToNull(_fields.nationalId.text) : null,
      iqamaNumber: !isSaudi && _docType == VisitorDocType.iqama
          ? _emptyToNull(_fields.documentNumber.text)
          : null,
      passportNumber: !isSaudi && _docType == VisitorDocType.passport
          ? _emptyToNull(_fields.documentNumber.text)
          : null,
      saudiMobile: isSaudi ? _emptyToNull(_fields.phone.text) : null,
      internationalMobile: !isSaudi ? _emptyToNull(_fields.phone.text) : null,
    );

    setState(() => _submitting = true);
    final repo = ref.read(staffRepositoryProvider);
    try {
      final result = await repo.registerVisitor(request);
      // Attach the optional images by the new visitor's id. A failed upload
      // does NOT undo the (already-created) registration, so the operator is
      // offered a retry of the UPLOAD instead of re-registering the person
      // (DEF-STF-004).
      final failed = await _uploadAttachments(repo, result.userId);
      if (!mounted) {
        return;
      }
      if (failed.isNotEmpty) {
        // The registration itself is finished — drop the busy state before the
        // modal so its controls are live (and the CTA is not left spinning).
        setState(() => _submitting = false);
        await _resolveFailedUploads(
            l10n, messenger, repo, result.userId, failed,);
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
      _applyServerFieldErrors(l10n, e);
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

  /// Moves the server's field-level rejections onto the matching inputs so a
  /// 400 highlights the offending field instead of only raising a toast
  /// (DEF-STF-003). Anything the form has no field for stays in the toast.
  void _applyServerFieldErrors(AppL10n l10n, ApiFailure failure) {
    final errors = <String, _ServerFieldError>{};
    for (final detail in failure.details) {
      final property = detail.field.trim();
      final controller = _controllerFor(property);
      if (property.isEmpty || controller == null) {
        continue;
      }
      final message = l10n.isArabic && detail.messageArabic.trim().isNotEmpty
          ? detail.messageArabic
          : detail.message;
      if (message.trim().isEmpty) {
        continue;
      }
      errors[property] = _ServerFieldError(
        message: message,
        rejectedValue: controller.text.trim(),
      );
    }
    if (errors.isEmpty) {
      return;
    }
    setState(() => _serverErrors.addAll(errors));
    // Re-run the validators so the freshly-attached messages paint, then bring
    // the first rejected field into view.
    _formKey.currentState?.validate();
    _revealFirstProblem();
  }

  /// The input backing a server property name, or null when the form has no
  /// field for it (e.g. a whole-request rule).
  TextEditingController? _controllerFor(String property) {
    switch (property) {
      case 'ArabicName':
        return _fields.arabicName;
      case 'EnglishName':
      case 'DisplayName':
        return _fields.englishName;
      case 'Email':
        return _fields.email;
      case 'JobTitle':
        return _fields.jobTitle;
      case 'JobTitleArabic':
        return _fields.jobTitleArabic;
      case 'NationalId':
        return _fields.nationalId;
      case 'IqamaNumber':
      case 'PassportNumber':
        return _fields.documentNumber;
      case 'SaudiMobile':
      case 'InternationalMobile':
        return _fields.phone;
      default:
        return null;
    }
  }

  /// The server's message for [property], while the field still holds the value
  /// the server rejected. Editing the field clears it without any listener.
  String? _serverError(String property, String? value) {
    final error = _serverErrors[property];
    if (error == null) {
      return null;
    }
    return (value?.trim() ?? '') == error.rejectedValue ? error.message : null;
  }

  /// 19l — brings the first invalid field into view after a blocked submit. The
  /// order mirrors the on-screen order so the operator lands on the top-most
  /// problem, not an arbitrary one.
  void _revealFirstProblem() {
    final anchor = _firstProblemAnchor()?.currentContext;
    if (anchor == null) {
      return;
    }
    unawaited(
      Scrollable.ensureVisible(
        anchor,
        alignment: 0.15,
        duration: MotionDurations.dotFade,
      ),
    );
  }

  GlobalKey? _firstProblemAnchor() {
    if (_profileTypeId == null) {
      return _fields.profileTypeAnchor;
    }
    if (_required(_fields.arabicName.text) != null) {
      return _fields.arabicNameAnchor;
    }
    if (_required(_fields.englishName.text) != null) {
      return _fields.englishNameAnchor;
    }
    if (_nationalityCode == null) {
      return _fields.nationalityAnchor;
    }
    if (_isSaudi) {
      if (_validateNationalId(_fields.nationalId.text) != null) {
        return _fields.documentAnchor;
      }
    } else if (_validateDocumentNumber(_fields.documentNumber.text) != null) {
      return _fields.documentNumberAnchor;
    }
    if (_required(_fields.jobTitle.text) != null) {
      return _fields.jobTitleAnchor;
    }
    if (_validatePhone(_fields.phone.text) != null) {
      return _fields.phoneAnchor;
    }
    if (_organisationId == null) {
      return _fields.organisationAnchor;
    }
    return null;
  }

  /// Uploads the attached images against the new visitor's id and returns the
  /// ones that did NOT land. An upload failure is non-fatal — the account
  /// already exists — but it must never be swallowed: the operator has to know
  /// the document is missing (DEF-STF-004).
  Future<List<_Attachment>> _uploadAttachments(
    StaffRepository repo,
    String userId, {
    Set<_Attachment>? only,
  }) async {
    if (userId.isEmpty) {
      return const <_Attachment>[];
    }
    final failed = <_Attachment>[];
    final idBytes = _idBytes;
    final idName = _idName;
    if (idBytes != null &&
        idName != null &&
        (only?.contains(_Attachment.idDocument) ?? true)) {
      try {
        await repo.uploadIdImage(
          userId: userId,
          bytes: idBytes,
          filename: idName,
        );
      } on ApiFailure {
        failed.add(_Attachment.idDocument);
      }
    }
    final photoBytes = _photoBytes;
    final photoName = _photoName;
    if (photoBytes != null &&
        photoName != null &&
        (only?.contains(_Attachment.photo) ?? true)) {
      try {
        await repo.uploadAvatar(
          userId: userId,
          bytes: photoBytes,
          filename: photoName,
        );
      } on ApiFailure {
        failed.add(_Attachment.photo);
      }
    }
    return failed;
  }

  /// Tells the operator exactly which attachment did not land and lets them
  /// retry the UPLOAD for the already-created visitor — the person is never
  /// registered twice (DEF-STF-004). The form is only cleared once the operator
  /// is done with the attachments (retried successfully, or chose to skip).
  Future<void> _resolveFailedUploads(
    AppL10n l10n,
    ScaffoldMessengerState messenger,
    StaffRepository repo,
    String userId,
    List<_Attachment> failed,
  ) async {
    var pending = failed;
    while (pending.isNotEmpty && mounted) {
      final retry = await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => StaffUploadFailedDialog(
          pendingLabels: <String>[
            for (final attachment in pending)
              _attachmentLabel(l10n, attachment),
          ],
        ),
      );
      if (!mounted) {
        return;
      }
      if (retry != true) {
        break;
      }
      pending = await _uploadAttachments(repo, userId, only: pending.toSet());
      if (!mounted) {
        return;
      }
      if (pending.isEmpty) {
        messenger
          ..hideCurrentSnackBar()
          ..showSnackBar(
            SnackBar(content: Text(l10n.staffUploadRetrySuccess)),
          );
      }
    }
    if (!mounted) {
      return;
    }
    messenger.showSnackBar(SnackBar(content: Text(l10n.staffRegisterSuccess)));
    _resetForm();
  }

  static String _attachmentLabel(AppL10n l10n, _Attachment attachment) =>
      attachment == _Attachment.idDocument
          ? l10n.staffAttachIdLabel
          : l10n.staffAttachPhotoLabel;

  void _resetForm() {
    setState(() {
      _fields.email.clear();
      _fields.arabicName.clear();
      _fields.englishName.clear();
      _fields.jobTitle.clear();
      _fields.jobTitleArabic.clear();
      _fields.phone.clear();
      _fields.nationalId.clear();
      _fields.documentNumber.clear();
      _gender = AppGender.male;
      _docType = VisitorDocType.iqama;
      _idBytes = null;
      _idName = null;
      _photoBytes = null;
      _photoName = null;
      _triedSubmit = false;
      _serverErrors.clear();
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
    // 19c — the window-size class, not an inline `maxWidth >= 600` literal.
    final wide = !WindowSize.of(context).isCompact;
    // 19a/19b/19c — the shared account/form chrome owns the back chevron, the
    // shared EN/ع language pill and the crest header, so this screen no longer
    // hand-rolls a globe button or pins whole blocks to LTR.
    return SimfFormScaffold(
      pinnedHeader: true,
      onBack: _back,
      busy: _submitting,
      child: _loading
          ? const Center(
              child: CircularProgressIndicator(color: SimfTokens.accent),
            )
          : _loadError != null
              ? StaffRegisterLoadError(onRefresh: _load)
              : Form(
                  key: _formKey,
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.fromLTRB(
                      SimfTokens.space4,
                      0,
                      SimfTokens.space4,
                      SimfTokens.space6,
                    ),
                    child: MaxWidthBody(
                      maxWidth:
                          wide ? _formMaxWidthWide : _formMaxWidthCompact,
                      child: Material(
                        color: SimfTokens.cardBeige,
                        borderRadius: SimfTokens.borderRadiusSmall,
                        child: Padding(
                          padding: const EdgeInsets.all(SimfTokens.space6),
                          child: RegisterVisitorForm(
                            fields: _fields,
                            wide: wide,
                            profileTypes: _profileTypes,
                            profileTypeId: _profileTypeId,
                            countries: _countries,
                            nationalityCode: _nationalityCode,
                            organisations: _organisations,
                            organisationId: _organisationId,
                            gender: _gender,
                            docType: _docType,
                            isSaudi: _isSaudi,
                            triedSubmit: _triedSubmit,
                            submitting: _submitting,
                            idBytes: _idBytes,
                            idName: _idName,
                            photoBytes: _photoBytes,
                            photoName: _photoName,
                            onPickProfileType: () =>
                                unawaited(_pickProfileType()),
                            onPickNationality: () =>
                                unawaited(_pickNationality()),
                            onPickOrganisation: () =>
                                unawaited(_pickOrganisation()),
                            onGenderChanged: (value) =>
                                setState(() => _gender = value),
                            onDocTypeChanged: (type) => setState(() {
                              _docType = type;
                              _fields.documentNumber.clear();
                            }),
                            onAttachId: () =>
                                unawaited(_pickImage(isIdDocument: true)),
                            onRemoveId: () => _removeImage(isIdDocument: true),
                            onAttachPhoto: () =>
                                unawaited(_pickImage(isIdDocument: false)),
                            onRemovePhoto: () =>
                                _removeImage(isIdDocument: false),
                            onSubmit: () => unawaited(_submit()),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
    );
  }

  Future<void> _pickProfileType() async {
    final l10n = AppL10n.of(context);
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

  Future<void> _pickNationality() async {
    final l10n = AppL10n.of(context);
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
        _fields.nationalId.clear();
        _fields.documentNumber.clear();
      }
    });
  }

  Future<void> _pickOrganisation() async {
    final l10n = AppL10n.of(context);
    final picked = await _openLookupSheet(
      options: <PickerOption>[
        for (final OrganisationItem o in _organisations)
          PickerOption(
            value: o.id,
            label: organisationDisplayName(o, l10n),
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

  String? _required(String? value) =>
      isBlank(value) ? AppL10n.of(context).requiredField : null;

  String? _validateArabicName(String? value) =>
      _required(value) ?? _serverError('ArabicName', value);

  /// DisplayName is derived from this field (the request sends the English name
  /// as the display name), so its rejection lands here too.
  String? _validateEnglishName(String? value) =>
      _required(value) ??
      _serverError('EnglishName', value) ??
      _serverError('DisplayName', value);

  String? _validateEmail(String? value) => _serverError('Email', value);

  /// D-723 — required (matches the app self-registration form).
  String? _validateJobTitle(String? value) =>
      _required(value) ?? _serverError('JobTitle', value);

  String? _validateJobTitleArabic(String? value) =>
      _serverError('JobTitleArabic', value);

  // D-700 — mirror the self-service shape + Luhn checks client-side so staff
  // get instant feedback (the server already enforces the same via
  // AdminWalkInRegistrationRequestValidator). Empty keeps the "required"
  // message.
  String? _validateNationalId(String? value) {
    final id = value?.trim() ?? '';
    if (id.isEmpty) {
      return _required(value);
    }
    if (!isValidNationalId(id)) {
      return AppL10n.of(context).nationalIdInvalid;
    }
    return _serverError('NationalId', value);
  }

  String? _validateDocumentNumber(String? value) {
    final l10n = AppL10n.of(context);
    final number = value?.trim() ?? '';
    if (number.isEmpty) {
      return _required(value);
    }
    if (_docType == VisitorDocType.iqama) {
      return isValidIqama(number)
          ? _serverError('IqamaNumber', value)
          : l10n.iqamaInvalid;
    }
    return isValidPassport(number)
        ? _serverError('PassportNumber', value)
        : l10n.passportInvalid;
  }

  /// Phone is required server-side (Saudi or international); validate inline
  /// like every other required field, with the same standard shapes as
  /// self-service.
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
      return _serverError(
        _isSaudi ? 'SaudiMobile' : 'InternationalMobile',
        value,
      );
    }
    return _isSaudi ? l10n.saudiMobileInvalid : l10n.internationalMobileInvalid;
  }
}
