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
import 'package:simf_app/core/validation/digit_normalization.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/core/validation/name_validation.dart';
import 'package:simf_app/core/validation/phone_validation.dart';
import 'package:simf_app/core/validation/required_validation.dart';
import 'package:simf_app/core/validation/saudi_id_validation.dart';
import 'package:simf_app/core/widgets/simf_image_source_sheet.dart';
import 'package:simf_app/core/widgets/simf_labeled_text_field.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/widgets/attachment_field.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';
import 'package:simf_app/features/account/widgets/mobile_field.dart';
import 'package:simf_app/features/account/widgets/terms_and_next_buttons.dart';
import 'package:simf_app/features/staff/data/staff_models.dart';
import 'package:simf_app/features/staff/data/staff_repository.dart';
import 'package:simf_app/features/staff/widgets/staff_document_type_field.dart';
import 'package:simf_app/features/staff/widgets/staff_form_row.dart';
import 'package:simf_app/features/staff/widgets/staff_gender_field.dart';
import 'package:simf_app/features/staff/widgets/staff_lookup_field.dart';
import 'package:simf_app/features/staff/widgets/staff_register_card_header.dart';
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
      iqamaNumber: !isSaudi && _docType == VisitorDocType.iqama
          ? _emptyToNull(_documentNumber.text)
          : null,
      passportNumber: !isSaudi && _docType == VisitorDocType.passport
          ? _emptyToNull(_documentNumber.text)
          : null,
      saudiMobile: isSaudi ? _emptyToNull(_phone.text) : null,
      internationalMobile: !isSaudi ? _emptyToNull(_phone.text) : null,
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
    _revealFirstProblem(l10n);
  }

  /// The input backing a server property name, or null when the form has no
  /// field for it (e.g. a whole-request rule).
  TextEditingController? _controllerFor(String property) {
    switch (property) {
      case 'ArabicName':
        return _arabicName;
      case 'EnglishName':
      case 'DisplayName':
        return _englishName;
      case 'Email':
        return _email;
      case 'JobTitle':
        return _jobTitle;
      case 'JobTitleArabic':
        return _jobTitleArabic;
      case 'NationalId':
        return _nationalId;
      case 'IqamaNumber':
      case 'PassportNumber':
        return _documentNumber;
      case 'SaudiMobile':
      case 'InternationalMobile':
        return _phone;
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
  void _revealFirstProblem(AppL10n l10n) {
    final anchor = _firstProblemAnchor(l10n)?.currentContext;
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
      _email.clear();
      _arabicName.clear();
      _englishName.clear();
      _jobTitle.clear();
      _jobTitleArabic.clear();
      _phone.clear();
      _nationalId.clear();
      _documentNumber.clear();
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
    final l10n = AppL10n.of(context);
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
                          child: _buildForm(l10n, wide: wide),
                        ),
                      ),
                    ),
                  ),
                ),
    );
  }

  Widget _buildForm(AppL10n l10n, {required bool wide}) {
    const gap = SizedBox(height: SimfTokens.space4);
    final profileType =
        _profileTypes.where((t) => t.id == _profileTypeId).toList();
    final nationality =
        _countries.where((c) => c.code == _nationalityCode).toList();
    final organisation =
        _organisations.where((o) => o.id == _organisationId).toList();
    // DEF-STF-007 — when the lookup comes back EMPTY there is nothing to pick,
    // so submit could never pass and the operator had no correctable field.
    // The field now says so, and says what to do about it, instead of silently
    // blocking.
    final typesUnavailable = _profileTypes.isEmpty;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const StaffRegisterCardHeader(),
        const SizedBox(height: SimfTokens.space6),
        // 19g — the walk-in classification is no longer silently pinned to the
        // row literally named "Normal": the operator picks from the
        // visitor-eligible types (seeded to "Normal" when it exists).
        StaffLookupField(
          key: _profileTypeAnchor,
          label: l10n.profileTypeLabel,
          fieldKey: 'staffProfileTypePicker',
          displayText: profileType.isNotEmpty
              ? (l10n.isArabic
                  ? profileType.first.nameArabic
                  : profileType.first.name)
              : (typesUnavailable
                  ? l10n.staffProfileTypeUnavailable
                  : l10n.profileTypeLabel),
          isPlaceholder: profileType.isEmpty,
          onTap:
              typesUnavailable ? null : () => unawaited(_pickProfileType(l10n)),
          errorText: typesUnavailable
              ? l10n.staffProfileTypeEmptyHelp
              : (_triedSubmit && _profileTypeId == null)
                  ? l10n.profileTypeRequired
                  : null,
        ),
        gap,
        StaffFormRow(
          wide: wide,
          start: _named(
            l10n.arabicNameLabel,
            KeyedSubtree(
              key: _arabicNameAnchor,
              child: SimfLabeledTextField(
                label: l10n.arabicNameLabel,
                controller: _arabicName,
                maxLength: FieldLimits.profileName,
                textDirection: TextDirection.rtl,
                // MERGE (BUG-019 rebuild + BUG-021): the rebuilt field keeps
                // the shared widget, but the character class is the widened
                // shared one so tashkeel/tatweel are no longer silently dropped
                // as you type.
                inputFormatters: <TextInputFormatter>[
                  FilteringTextInputFormatter.allow(arabicNameCharacters),
                ],
                validator: (v) =>
                    _required(l10n, v) ?? _serverError('ArabicName', v),
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
                maxLength: FieldLimits.profileName,
                textDirection: TextDirection.ltr,
                inputFormatters: <TextInputFormatter>[
                  FilteringTextInputFormatter.allow(RegExp(r'[A-Za-z\s]')),
                ],
                // DisplayName is derived from this field (the request sends the
                // English name as the display name), so its rejection lands
                // here too.
                validator: (v) =>
                    _required(l10n, v) ??
                    _serverError('EnglishName', v) ??
                    _serverError('DisplayName', v),
              ),
            ),
          ),
        ),
        gap,
        StaffFormRow(
          wide: wide,
          start: StaffGenderField(
            gender: _gender,
            onChanged: (value) => setState(() => _gender = value),
          ),
          // 19j — the 57-country list gets the shared searchable picker,
          // exactly like Create-profile, instead of a raw Material dropdown.
          end: StaffLookupField(
            key: _nationalityAnchor,
            label: l10n.nationalityLabel,
            fieldKey: 'staffNationalityPicker',
            displayText: nationality.isNotEmpty
                ? (l10n.isArabic
                    ? nationality.first.nameArabic
                    : nationality.first.name)
                : l10n.nationalityLabel,
            isPlaceholder: nationality.isEmpty,
            onTap: () => unawaited(_pickNationality(l10n)),
            errorText: (_triedSubmit && _nationalityCode == null)
                ? l10n.nationalityRequired
                : null,
          ),
        ),
        gap,
        StaffFormRow(
          wide: wide,
          start: _isSaudi
              ? _named(
                  l10n.nationalIdLabel,
                  KeyedSubtree(
                    key: _documentAnchor,
                    child: SimfLabeledTextField(
                      label: l10n.nationalIdLabel,
                      controller: _nationalId,
                      keyboardType: TextInputType.number,
                      maxLength: FieldLimits.nationalId,
                      // The id renders LTR (digits) even under Arabic —
                      // genuinely-LTR content, unlike the surrounding layout
                      // (19b).
                      textDirection: TextDirection.ltr,
                      inputFormatters: <TextInputFormatter>[
                        const WesternDigitsFormatter(),
                        FilteringTextInputFormatter.digitsOnly,
                      ],
                      validator: (v) => _validateNationalId(l10n, v),
                    ),
                  ),
                )
              : StaffDocumentTypeField(
                  key: _documentAnchor,
                  docType: _docType,
                  onChanged: (type) => setState(() {
                    _docType = type;
                    _documentNumber.clear();
                  }),
                ),
          end: _isSaudi
              ? null
              : _named(
                  l10n.documentNumberLabel,
                  KeyedSubtree(
                    key: _documentNumberAnchor,
                    child: SimfLabeledTextField(
                      label: l10n.documentNumberLabel,
                      controller: _documentNumber,
                      maxLength: _docType == VisitorDocType.iqama ? 10 : 9,
                      textDirection: TextDirection.ltr,
                      inputFormatters: const <TextInputFormatter>[
                        WesternDigitsFormatter(),
                      ],
                      validator: (v) => _validateDocumentNumber(l10n, v),
                    ),
                  ),
                ),
        ),
        gap,
        StaffFormRow(
          wide: wide,
          start: _named(
            l10n.jobTitleLabel,
            KeyedSubtree(
              key: _jobTitleAnchor,
              child: SimfLabeledTextField(
                label: l10n.jobTitleLabel,
                controller: _jobTitle,
                maxLength: FieldLimits.fullName,
                textDirection: TextDirection.ltr,
                // D-723 — required (matches the app self-registration form).
                validator: (v) =>
                    _required(l10n, v) ?? _serverError('JobTitle', v),
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
              maxLength: FieldLimits.fullName,
              textDirection: TextDirection.rtl,
              validator: (v) => _serverError('JobTitleArabic', v),
            ),
          ),
        ),
        gap,
        StaffFormRow(
          wide: wide,
          // 19b — email and phone are genuinely LTR CONTENT, so the inputs stay
          // explicitly LTR while the layout follows the locale.
          start: _named(
            l10n.staffEmailLabel,
            SimfLabeledTextField(
              label: l10n.staffEmailLabel,
              controller: _email,
              maxLength: FieldLimits.email,
              keyboardType: TextInputType.emailAddress,
              textDirection: TextDirection.ltr,
              validator: (v) => _serverError('Email', v),
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
        // 19j — the organisation list also moves to the shared searchable
        // picker; the type-to-filter sheet handles the 200 loaded rows.
        StaffLookupField(
          key: _organisationAnchor,
          label: l10n.staffOrganisationLabel,
          fieldKey: 'staffOrganisationPicker',
          displayText: organisation.isNotEmpty
              ? _organisationName(organisation.first, l10n)
              : l10n.organisationSearchHint,
          isPlaceholder: organisation.isEmpty,
          onTap: () => unawaited(_pickOrganisation(l10n)),
          errorText: (_triedSubmit && _organisationId == null)
              ? l10n.organisationRequired
              : null,
        ),
        gap,
        StaffFormRow(
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

  /// 19h — the shared field widgets render a visible caption but leave the
  /// input itself unnamed for a screen reader; this names it.
  Widget _named(String label, Widget field) =>
      Semantics(label: label, textField: true, child: field);

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

  // D-700 — mirror the self-service shape + Luhn checks client-side so staff
  // get instant feedback (the server already enforces the same via
  // AdminWalkInRegistrationRequestValidator). Empty keeps the "required"
  // message.
  String? _validateNationalId(AppL10n l10n, String? value) {
    final id = value?.trim() ?? '';
    if (id.isEmpty) {
      return _required(l10n, value);
    }
    if (!isValidNationalId(id)) {
      return l10n.nationalIdInvalid;
    }
    return _serverError('NationalId', value);
  }

  String? _validateDocumentNumber(AppL10n l10n, String? value) {
    final number = value?.trim() ?? '';
    if (number.isEmpty) {
      return _required(l10n, value);
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
