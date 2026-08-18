import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/data/interests_setup.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/widgets/account_sub_header.dart';
import 'package:simf_app/features/account/widgets/sign_up_interests_body.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Sign up — interests · اهتماماتي · route: RouteNames.signUpInterests · Figma
/// 505:1083 (D-365)
/// Contract: D-684 (was the single D-332 save) — the profile fields + both
/// images are already saved on Page 007, so this second save only adds 1-10
/// interestIds to the existing profile. #14 re-uses the screen as the
/// standalone My-interests EDIT surface, where the save is a lossless full
/// re-POST. AUTH-only; a draft-less deep link shows the recover state.
class SignUpInterestsScreen extends ConsumerStatefulWidget {
  const SignUpInterestsScreen({super.key, this.draft, this.editMode = false});

  /// The in-memory draft from Page 007 (`state.extra`). Null only on a direct
  /// deep-link open with no preceding data screen. Ignored in [editMode].
  final SignUpProfileDraft? draft;

  /// #14 — when true this screen is the standalone "My interests" EDIT surface
  /// (opened from My-Area, route [RouteNames.myInterests]): it self-loads the
  /// current profile, pre-selects the saved interests, and on save re-POSTs the
  /// FULL profile with the new interests (via
  /// [UserProfileResponse.toUpsertRequest]) then pops. When false it is the
  /// sign-up interests step (create) — behaviour + 505:1083 render unchanged.
  final bool editMode;

  @override
  ConsumerState<SignUpInterestsScreen> createState() =>
      _SignUpInterestsScreenState();
}

class _SignUpInterestsScreenState extends ConsumerState<SignUpInterestsScreen> {

  final Set<String> _selected = <String>{};

  /// #14 edit mode — the loaded profile, re-sent in full on save so an
  /// interests-only change nulls no other field.
  UserProfileResponse? _editProfile;

  /// "Show me in Meet People Like You" visibility. The in-app opt-in was
  /// removed (owner 2026-07-24) — this now lives only in the CP; the value is
  /// seeded from the loaded profile and re-sent verbatim on save so a full
  /// profile re-POST never clobbers the CP-set flag.
  bool _showInMeetLikeYou = true;

  bool _submitting = false;
  String? _submitError;

  @override
  void initState() {
    super.initState();
    // Pre-select any interests already on the carried draft (re-entry / edit).
    _selected.addAll(widget.draft?.request.interestIds ?? const <String>[]);
    if (_needsLoad) {
      unawaited(_seedFromSetup());
    }
  }

  /// The third mode — no draft and not editing — has nothing to fetch, so it
  /// never watches the provider and renders the (empty) lookup directly.
  bool get _needsLoad => widget.editMode || widget.draft != null;

  /// Seeds the selection from whichever source this mode has, ONCE.
  ///
  /// Edit mode pre-selects the profile's saved interests and captures the
  /// profile for the lossless re-save; sign-up mode drops any carried draft id
  /// that is no longer in the active lookup. Awaits the provider's first future
  /// rather than listening, because the user edits the selection afterwards.
  Future<void> _seedFromSetup() async {
    final InterestsSetup setup;
    try {
      setup = await ref.read(interestsSetupProvider(widget.editMode).future);
    } on Object {
      return; // The load-error branch renders.
    }
    if (!mounted) {
      return;
    }
    setState(() {
      final profile = setup.profile;
      if (profile != null) {
        _editProfile = profile;
        _showInMeetLikeYou = profile.showInMeetLikeYou;
        _selected
          ..clear()
          ..addAll(
            profile.interestIds
                .where((id) => setup.interests.any((i) => i.id == id)),
          );
      } else {
        _selected.retainWhere((id) => setup.interests.any((i) => i.id == id));
      }
    });
  }

  void _toggleInterest(String id, AppL10n l10n) {
    final selecting = !_selected.contains(id);
    if (selecting && _selected.length >= 10) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.interestsMaxReached)));
      return;
    }
    setState(() {
      if (selecting) {
        _selected.add(id);
      } else {
        _selected.remove(id);
      }
    });
  }

  Widget _buildBody(AppL10n l10n) {
    final async = _needsLoad
        ? ref.watch(interestsSetupProvider(widget.editMode))
        : const AsyncValue<InterestsSetup>.data(
            InterestsSetup(interests: <InterestItem>[]),
          );
    final error = async.error;
    Future<void> retry() async =>
        ref.invalidate(interestsSetupProvider(widget.editMode));

    return SignUpInterestsBody(
      l10n: l10n,
      interests: async.value?.interests ?? const <InterestItem>[],
      selected: _selected,
      loading: async.isLoading,
      loadError: error == null
          ? null
          : (error is ApiFailure
              ? error.localizedMessage(l10n)
              : l10n.errorGenericBody),
      submitting: _submitting,
      submitError: _submitError,
      editMode: widget.editMode,
      draft: widget.draft,
      onToggleInterest: _toggleInterest,
      onSave: _save,
      // One provider behind both, so one retry serves both entry points.
      onRetry: retry,
      onRetryEdit: retry,
    );
  }

  Future<void> _save() async {
    if (widget.editMode) {
      await _saveEdit();
      return;
    }
    final l10n = AppL10n.of(context);
    final draft = widget.draft;
    if (draft == null) {
      return;
    }
    if (_selected.isEmpty || _selected.length > 10) {
      return;
    }
    setState(() {
      _submitError = null;
      _submitting = true;
    });
    final repo = ref.read(profileRepositoryProvider);
    try {
      // D-684 — the profile fields + both images were already saved on the
      // previous (profile) step, so this step only adds the picked interests to
      // the existing profile. Any profile-field error was surfaced back there.
      final saved = await repo.upsertMyProfile(
        draft.request.copyWith(interestIds: _selected.toList()),
      );
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.profileSavedToast)));
      // D-373 — the save response carries the freshly issued registration
      // reference; the success screen renders it without another fetch.
      context.goNamed(
        RouteNames.registrationSuccess,
        extra: saved.referenceNumber,
      );
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() => _submitError = failure.localizedMessage(l10n));
    } finally {
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  /// #14 edit-mode save — re-POST the FULL loaded profile with the new
  /// interests (nulling nothing) via `toUpsertRequest`, then pop back to
  /// My-Area. Uses the same 1-10 rule as the create step.
  Future<void> _saveEdit() async {
    final l10n = AppL10n.of(context);
    final profile = _editProfile;
    if (profile == null || _selected.isEmpty || _selected.length > 10) {
      return;
    }
    setState(() {
      _submitError = null;
      _submitting = true;
    });
    final repo = ref.read(profileRepositoryProvider);
    try {
      await repo.upsertMyProfile(
        profile
            .toUpsertRequest(showInMeetLikeYou: _showInMeetLikeYou)
            .copyWith(interestIds: _selected.toList()),
      );
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.interestsUpdatedToast)));
      if (context.canPop()) {
        context.pop();
      }
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() => _submitError = failure.localizedMessage(l10n));
    } finally {
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  void _back() {
    if (context.canPop()) {
      context.pop();
      return;
    }
    context.goNamed(RouteNames.signUpVisitor);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          const SimfAuthSweep(top: -180, left: null, right: -40),
          SafeArea(
            child: Column(
              children: <Widget>[
                AccountSubHeader(
                  title: l10n.interestsTitle,
                  onBack: _back,
                  busy: _submitting,
                ),
                Expanded(child: _buildBody(l10n)),
              ],
            ),
          ),
        ],
      ),
    );
  }

}
