import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_form_scaffold.dart';
import 'package:simf_app/core/motion/motion_durations.dart';
import 'package:simf_app/core/responsive/breakpoints.dart';
import 'package:simf_app/features/account/data/app_gender.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/staff/data/register_visitor_validators.dart';
import 'package:simf_app/features/staff/data/staff_repository.dart';
import 'package:simf_app/features/staff/data/walk_in_attachments.dart';
import 'package:simf_app/features/staff/data/walk_in_field_errors.dart';
import 'package:simf_app/features/staff/data/walk_in_lookups.dart';
import 'package:simf_app/features/staff/data/walk_in_request_builder.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_card.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_form.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_form_fields.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_pickers.dart';
import 'package:simf_app/features/staff/widgets/staff_register_load_error.dart';
import 'package:simf_app/features/staff/widgets/walk_in_upload_retry.dart';
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

class _StaffRegisterVisitorScreenState
    extends ConsumerState<StaffRegisterVisitorScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();

  /// What the server rejected on the last 400, echoed back onto the fields it
  /// named (DEF-STF-003). The validators read it; only this screen paints it.
  final WalkInFieldErrors _serverErrors = WalkInFieldErrors();

  /// The two optional images, held until there is a visitor id to attach them
  /// to — the account is created first, the files afterwards.
  final WalkInAttachments _attachments = WalkInAttachments();

  /// The form's rules, bound once to this screen's live state.
  late final RegisterVisitorValidators _validators;

  /// The inputs themselves — controllers, scroll anchors and the validator
  /// bound to each — built once and handed to [RegisterVisitorForm] whole.
  late final RegisterVisitorFormFields _fields;

  WalkInLookups _lookups = WalkInLookups.empty;

  AppGender _gender = AppGender.male;
  String? _nationalityCode;
  String? _organisationId;
  String? _profileTypeId;
  VisitorDocType _docType = VisitorDocType.iqama;

  bool get _isSaudi => _nationalityCode == 'SA';

  bool _loading = true;
  String? _loadError;
  bool _submitting = false;
  bool _triedSubmit = false;

  @override
  void initState() {
    super.initState();
    _validators = RegisterVisitorValidators(
      // Resolved at validate time, not captured: the shared EN/ع pill switches
      // language while the form is on screen.
      l10n: () => AppL10n.of(context),
      isSaudi: () => _isSaudi,
      docType: () => _docType,
      serverErrors: _serverErrors,
    );
    _fields = _validators.buildFields();
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
      final lookups = await loadWalkInLookups(repo);
      if (!mounted) {
        return;
      }
      setState(() {
        _lookups = lookups;
        _nationalityCode ??= lookups.defaultNationalityCode;
        _profileTypeId = lookups.defaultProfileTypeId;
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

  Future<void> _attach(WalkInAttachment which) async {
    final picked = await pickWalkInAttachment(context);
    if (picked == null || !mounted) {
      return;
    }
    _setAttachment(which, picked);
  }

  void _setAttachment(WalkInAttachment which, WalkInAttachmentFile? file) {
    setState(() {
      if (which == WalkInAttachment.idDocument) {
        _attachments.idDocument = file;
      } else {
        _attachments.photo = file;
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
      // Paints the lookups' own "required" messages, which are gated on a
      // submit having been tried, before the scroll lands on the first one.
      setState(() {});
      _revealFirstProblem();
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.staffCompletePrompt)));
      return;
    }

    final request = buildStaffWalkInRequest(
      fields: _fields,
      profileTypeId: _profileTypeId!,
      nationalityCode: _nationalityCode!,
      isSaudi: _isSaudi,
      gender: _gender,
      docType: _docType,
      organisationId: _organisationId,
    );

    setState(() => _submitting = true);
    final repo = ref.read(staffRepositoryProvider);
    try {
      final result = await repo.registerVisitor(request);
      // Attach the optional images by the new visitor's id. A failed upload
      // does NOT undo the (already-created) registration, so the operator is
      // offered a retry of the UPLOAD instead of re-registering the person
      // (DEF-STF-004).
      final failed = await uploadWalkInAttachments(
        repo,
        _attachments,
        userId: result.userId,
      );
      if (!mounted) {
        return;
      }
      if (failed.isNotEmpty) {
        // The registration itself is finished — drop the busy state before the
        // modal so its controls are live (and the CTA is not left spinning).
        setState(() => _submitting = false);
        await resolveFailedWalkInUploads(
          context: context,
          messenger: messenger,
          repo: repo,
          attachments: _attachments,
          userId: result.userId,
          failed: failed,
        );
        if (!mounted) {
          return;
        }
      } else {
        // Only on the clean path: a successful retry has just raised its own
        // toast, and hiding it there would cut it off (DEF-STF-004).
        messenger.hideCurrentSnackBar();
      }
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.staffRegisterSuccess)),
      );
      _resetForm();
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      if (_serverErrors.absorb(e, l10n: l10n, fields: _fields)) {
        setState(() {});
        // Re-run the validators so the freshly-attached messages paint, then
        // bring the first rejected field into view.
        _formKey.currentState?.validate();
        _revealFirstProblem();
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

  /// 19l — brings the first invalid field into view after a blocked submit; the
  /// CTA sits at the bottom of a long form, so every error is otherwise
  /// off-screen above it.
  void _revealFirstProblem() {
    final anchor = _validators
        .firstProblemAnchor(
          _fields,
          profileTypeId: _profileTypeId,
          nationalityCode: _nationalityCode,
          organisationId: _organisationId,
        )
        ?.currentContext;
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
      _attachments.clear();
      _triedSubmit = false;
      _serverErrors.clear();
    });
    _formKey.currentState?.reset();
  }

  void _back() {
    if (context.canPop()) {
      context.pop();
      return;
    }
    context.goNamed(RouteNames.home);
  }

  Future<void> _pickProfileType() async {
    final picked = await pickWalkInProfileType(context, _lookups.profileTypes);
    if (picked == null || !mounted) {
      return;
    }
    setState(() => _profileTypeId = picked);
  }

  Future<void> _pickNationality() async {
    final picked = await pickWalkInNationality(context, _lookups.countries);
    if (picked == null || !mounted) {
      return;
    }
    setState(() {
      final wasSaudi = _isSaudi;
      _nationalityCode = picked;
      // Crossing the Saudi border swaps which identity field is shown, so a
      // number captured against the other one must not survive the change.
      if (wasSaudi != _isSaudi) {
        _fields.nationalId.clear();
        _fields.documentNumber.clear();
      }
    });
  }

  Future<void> _pickOrganisation() async {
    final picked =
        await pickWalkInOrganisation(context, _lookups.organisations);
    if (picked == null || !mounted) {
      return;
    }
    setState(() => _organisationId = picked);
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
                  child: RegisterVisitorCard(
                    wide: wide,
                    child: RegisterVisitorForm(
                      fields: _fields,
                      wide: wide,
                      profileTypes: _lookups.profileTypes,
                      profileTypeId: _profileTypeId,
                      countries: _lookups.countries,
                      nationalityCode: _nationalityCode,
                      organisations: _lookups.organisations,
                      organisationId: _organisationId,
                      gender: _gender,
                      docType: _docType,
                      isSaudi: _isSaudi,
                      triedSubmit: _triedSubmit,
                      submitting: _submitting,
                      idBytes: _attachments.idDocument?.bytes,
                      idName: _attachments.idDocument?.filename,
                      photoBytes: _attachments.photo?.bytes,
                      photoName: _attachments.photo?.filename,
                      onPickProfileType: () => unawaited(_pickProfileType()),
                      onPickNationality: () => unawaited(_pickNationality()),
                      onPickOrganisation: () => unawaited(_pickOrganisation()),
                      onGenderChanged: (value) =>
                          setState(() => _gender = value),
                      onDocTypeChanged: (type) => setState(() {
                        _docType = type;
                        _fields.documentNumber.clear();
                      }),
                      onAttachId: () =>
                          unawaited(_attach(WalkInAttachment.idDocument)),
                      onRemoveId: () =>
                          _setAttachment(WalkInAttachment.idDocument, null),
                      onAttachPhoto: () =>
                          unawaited(_attach(WalkInAttachment.photo)),
                      onRemovePhoto: () =>
                          _setAttachment(WalkInAttachment.photo, null),
                      onSubmit: () => unawaited(_submit()),
                    ),
                  ),
                ),
    );
  }
}
