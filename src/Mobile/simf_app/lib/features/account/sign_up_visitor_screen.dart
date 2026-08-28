import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_form_scaffold.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/data/region_repository.dart';
import 'package:simf_app/features/account/data/sign_up_visitor_form.dart';
import 'package:simf_app/features/account/data/sign_up_visitor_lookups.dart';
import 'package:simf_app/features/account/sign_up_visitor_pickers.dart';
import 'package:simf_app/features/account/sign_up_visitor_submit.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_form_card.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_load_error.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_place_of_birth_field.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_form_state.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Sign up — profile data · إنشاء ملف شخصى · route: RouteNames.signUpVisitor ·
/// Figma 168:2972 (D-368; design deltas recorded there)
/// Contract: D-332 — NO API write happens here. The screen loads the existing
/// profile + the three lookups concurrently, and Next carries the collected
/// data (+ the optional ID image) forward as a `SignUpProfileDraft` to the
/// interests screen, which fires the single profile save. The visitor/other tab
/// is a client-only `?isVisitor=` filter over the ProfileType picker.
class SignUpVisitorScreen extends ConsumerStatefulWidget {
  const SignUpVisitorScreen({super.key});

  @override
  ConsumerState<SignUpVisitorScreen> createState() =>
      _SignUpVisitorScreenState();
}

class _SignUpVisitorScreenState extends ConsumerState<SignUpVisitorScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();

  /// The picks both registration surfaces share (SIMF-RSD-001 step 2), and the
  /// text/image fields only this one collects.
  final VisitorProfileFormState _form = VisitorProfileFormState();
  final SignUpVisitorForm _fields = SignUpVisitorForm();

  /// نوع التسجيل: the Visitor / Other tab and its ProfileType lookup.
  final SignUpVisitorTypeSelection _type = SignUpVisitorTypeSelection();

  /// The organisations fetched with the opening load, handed to the type-ahead
  /// so its list is populated before the first keystroke. The field owns the
  /// searching from there.
  List<OrganisationItem> _initialOrganisations = const <OrganisationItem>[];

  bool _loading = true;
  String? _loadError;
  // D-684 — the profile is saved on THIS step now (profile-first), so any
  // server error (e.g. the name) surfaces here, not two screens later on
  // interests.
  bool _saving = false;
  String? _saveError;

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

  /// Re-reads the place-of-birth field from the selected region code in the
  /// active language. A no-op unless a Saudi registrant has picked a region:
  /// for a non-Saudi registrant the field is free text, and both the picker and
  /// the nationality switch already maintain the value inside their own
  /// setState.
  void _syncBirthRegionName({required bool isArabic}) {
    if (!_form.isSaudi || _fields.birthRegionCode == null) {
      return;
    }
    final region = birthRegionByCode(ref, _fields.birthRegionCode);
    final name = region?.name(isArabic: isArabic) ?? '';
    if (_fields.placeOfBirth.text != name) {
      _fields.placeOfBirth.text = name;
    }
  }

  @override
  void dispose() {
    _fields.dispose();
    _form.dispose();
    super.dispose();
  }

  // ---- Loading -------------------------------------------------------------

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _loadError = null;
    });
    try {
      final data = await loadSignUpVisitorData(
        ref.read(profileRepositoryProvider),
        isVisitor: _type.isVisitor,
      );
      if (!mounted) {
        return;
      }
      setState(() {
        _form.setLookups(
          countries: data.countries,
          profileTypes: data.profileTypes,
        );
        _initialOrganisations = data.organisations;
        _fields.applyProfile(data.profile, _form);
        _type.lock(_form);
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      final l10n = AppL10n.of(context);
      setState(() => _loadError = failure.localizedMessage(l10n));
    } finally {
      // The 401-refresh path can throw past ApiFailure (a failed keystore
      // write), which would strand the screen on its spinner.
      if (mounted) {
        setState(() => _loading = false);
      }
    }
  }

  // ---- نوع التسجيل (Visitor / Other) ---------------------------------------

  /// Switching the tab re-filters the ProfileType picker (`?isVisitor=`) and
  /// drops any now-invalid ProfileType selection (Page_007 L-3). Under
  /// Visitor the picker is hidden and the type auto-locks to Normal (C5).
  Future<void> _onTypeChanged(bool isVisitor) async {
    if (isVisitor == _type.isVisitor) {
      return;
    }
    setState(() {
      _type.isVisitor = isVisitor;
      _form.profileTypeId = null;
      _form.setLookups(profileTypes: const <ProfileTypeItem>[]);
    });
    await _fetchProfileTypes();
  }

  Future<void> _fetchProfileTypes() async {
    setState(_type.beginFetch);
    List<ProfileTypeItem>? types;
    try {
      types = await fetchVisitorProfileTypes(
        ref.read(profileRepositoryProvider),
        isVisitor: _type.isVisitor,
      );
    } finally {
      // Same reason as _load; a null `types` is the failure state the retry
      // hangs off.
      if (mounted) {
        setState(() => _type.endFetch(_form, types));
      }
    }
  }

  // ---- Pickers -------------------------------------------------------------

  Future<void> _pickNationality(AppL10n l10n) async {
    final pickedCode = await pickVisitorNationality(
      context,
      countries: _form.countries,
      l10n: l10n,
    );
    if (pickedCode == null || !mounted) {
      return;
    }
    setState(() => _fields.applyNationality(_form, pickedCode));
  }

  Future<void> _pickMobileCallingCode(AppL10n l10n) async {
    final prefix = await pickVisitorCallingCode(
      context,
      countries: _form.countries,
      l10n: l10n,
    );
    if (prefix != null && mounted) {
      setState(() => _fields.mobileCallingCode = prefix);
    }
  }

  Future<void> _pickDateOfBirth() async {
    final picked = await pickVisitorDateOfBirth(
      context,
      current: _fields.dateOfBirth,
    );
    if (picked != null && mounted) {
      setState(() => _fields.dateOfBirth = picked);
    }
  }

  /// "Upload ID" — the ID DOCUMENT is picked from the gallery/library (a
  /// national-ID / Iqama / passport scan), so there is no live-camera or
  /// face-detection step here. Mandatory for every registrant; the Next gate
  /// reports a missing image.
  Future<void> _pickIdImage() async {
    final image = await pickIdImageFromGallery();
    if (image == null || !mounted) {
      return;
    }
    setState(() => _fields.setIdImage(image.bytes, image.name));
  }

  Future<void> _pickFacePhoto() async {
    final selfie = await pickVisitorFacePhoto(context);
    if (selfie == null || !mounted) {
      return;
    }
    setState(() {
      _fields.faceImageBytes = selfie.bytes;
      _fields.faceImageName = selfie.filename;
    });
  }

  // ---- Next (carry the draft to the interests screen) ----------------------

  /// Validates the data fields, uploads the images and **saves the profile
  /// here** (D-684, profile-first) so any server error — the name in particular
  /// — surfaces on THIS screen, not two steps later on interests. Only on a
  /// clean save does it carry the `SignUpProfileDraft` to the interests screen
  /// (Page 007‑01), where the interests are added in a second save.
  Future<void> _next() async {
    if (_saving) {
      return;
    }
    setState(() => _form.triedSubmit = true);
    final formValid = _formKey.currentState?.validate() ?? false;
    final complete = isSignUpVisitorComplete(
      form: _fields,
      picks: _form,
      type: _type,
    );
    if (!formValid || !complete) {
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
    try {
      final result = await saveSignUpVisitorProfile(
        repository: ref.read(profileRepositoryProvider),
        form: _fields,
        picks: _form,
        l10n: l10n,
        stillMounted: () => mounted,
        onAvatarUploaded: () => ref.read(avatarBustProvider.notifier).bump(),
      );
      if (!mounted) {
        return;
      }
      setState(() => _saveError = result.message);
      if (result.outcome != SignUpVisitorSaveOutcome.saved) {
        return;
      }
      unawaited(
        context.pushNamed(
          RouteNames.signUpInterests,
          extra: _fields.toDraft(_form),
        ),
      );
    } finally {
      // Same reason as _load: the per-step ApiFailure catches do not cover a
      // throw from the 401-refresh path, which left Next disabled for good.
      if (mounted) {
        setState(() => _saving = false);
      }
    }
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
          child: SignUpVisitorFormCard(
            form: _fields,
            picks: _form,
            type: _type,
            initialOrganisations: _initialOrganisations,
            saveError: _saveError,
            saving: _saving,
            onTypeChanged: (isVisitor) => unawaited(_onTypeChanged(isVisitor)),
            onRetryProfileTypes: () => unawaited(_fetchProfileTypes()),
            onProfileTypeChanged: (id) =>
                setState(() => _form.profileTypeId = id),
            onGenderChanged: (value) => setState(() => _form.gender = value),
            onPickMobileCallingCode: () =>
                unawaited(_pickMobileCallingCode(l10n)),
            onOrganisationSelected: (organisation) => setState(
              () => _fields.setOrganisation(
                _form,
                organisation,
                isArabic: l10n.isArabic,
              ),
            ),
            onOrganisationCleared: () =>
                setState(() => _fields.clearOrganisation(_form)),
            onPickNationality: () => unawaited(_pickNationality(l10n)),
            onDocTypeChanged: (value) => setState(() {
              _form.docType = value;
              _fields.documentNumber.clear();
            }),
            onPickDateOfBirth: () => unawaited(_pickDateOfBirth()),
            onBirthRegionPicked: (code, name) =>
                setState(() => _fields.setBirthRegion(code, name)),
            onPlateChanged: () => setState(() {}),
            onAttachIdImage: () => unawaited(_pickIdImage()),
            onRemoveIdImage: () => setState(_fields.clearIdImage),
            onCaptureFacePhoto: () => unawaited(_pickFacePhoto()),
            onNext: _next,
          ),
        ),
      ),
    );
  }
}
