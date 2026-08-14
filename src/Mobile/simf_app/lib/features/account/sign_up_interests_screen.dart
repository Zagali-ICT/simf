import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/widgets/account_sub_header.dart';
import 'package:simf_app/features/account/widgets/interest_chip.dart';
import 'package:simf_app/features/account/widgets/sign_up_interests_body.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Page 007‑01 — اهتماماتي · Sign up — interests. The KSA-Project Figma design
/// (node 505:1083 — D-365): navy surface + sweep, custom header, the
/// اختر اهتماماتك heading, a two-column pill grid (gold selected /
/// `navyDeep`-with-border unselected), the n/10 counter, and the gold متابعة
/// button pinned at the bottom. The previous screen is parked in
/// `_legacy_mockup/`.
///
/// Second save step (D-684, was the single D-332 save): the profile fields +
/// both images are already saved on Page 007, so this screen receives the
/// [SignUpProfileDraft], loads the interests lookup, requires **1–10** picks,
/// then fires `POST /app/account/user-profile` again to add the interestIds to
/// the existing profile, and routes to Page 010. AUTH-only; a draft-less deep
/// link shows the recover state back to the profile-data screen.
///
/// Clean-code pass (D-550, Phase 3): screen-local colour consts dropped for
/// `SimfTokens` (`chipBorderNavy` added); the pill extracted to [InterestChip];
/// header to the shared [AccountSubHeader] (D-658); the body capped by
/// [MaxWidthBody]. Behaviour + render unchanged — the 505:1083 golden locks it.
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
  List<InterestItem> _interests = const <InterestItem>[];
  final Set<String> _selected = <String>{};

  /// #14 edit mode — the loaded profile, re-sent in full on save so an
  /// interests-only change nulls no other field.
  UserProfileResponse? _editProfile;

  /// "Show me in Meet People Like You" visibility. The in-app opt-in was
  /// removed (owner 2026-07-24) — this now lives only in the CP; the value is
  /// seeded from the loaded profile and re-sent verbatim on save so a full
  /// profile re-POST never clobbers the CP-set flag.
  bool _showInMeetLikeYou = true;

  bool _loading = true;
  String? _loadError;
  bool _submitting = false;
  String? _submitError;

  @override
  void initState() {
    super.initState();
    if (widget.editMode) {
      // #14 edit mode — self-load the profile + lookup; the current interests
      // are pre-selected inside _loadForEdit once both have loaded.
      unawaited(_loadForEdit());
      return;
    }
    // Pre-select any interests already on the carried draft (re-entry / edit).
    final existing = widget.draft?.request.interestIds ?? const <String>[];
    _selected.addAll(existing);
    if (widget.draft != null) {
      unawaited(_load());
    } else {
      _loading = false;
    }
  }

  /// #14 edit mode — load the current profile (for a lossless re-save) and the
  /// interests lookup, then pre-select the profile's saved interests.
  Future<void> _loadForEdit() async {
    final repo = ref.read(profileRepositoryProvider);
    setState(() {
      _loading = true;
      _loadError = null;
    });
    try {
      final profile = await repo.getMyProfile();
      final interests = await repo.getInterests();
      if (!mounted) {
        return;
      }
      setState(() {
        _editProfile = profile;
        _showInMeetLikeYou = profile.showInMeetLikeYou;
        _interests = interests;
        _selected
          ..clear()
          ..addAll(
            profile.interestIds.where((id) => interests.any((i) => i.id == id)),
          );
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      final l10n = AppL10n.of(context);
      setState(() {
        _loadError = failure.localizedMessage(l10n);
        _loading = false;
      });
    }
  }

  Future<void> _load() async {
    final repo = ref.read(profileRepositoryProvider);
    setState(() {
      _loading = true;
      _loadError = null;
    });
    try {
      final interests = await repo.getInterests();
      if (!mounted) {
        return;
      }
      setState(() {
        _interests = interests;
        // Drop any pre-selected id that is not in the active lookup.
        _selected.retainWhere((id) => interests.any((i) => i.id == id));
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      final l10n = AppL10n.of(context);
      setState(() {
        _loadError = failure.localizedMessage(l10n);
        _loading = false;
      });
    }
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

  Widget _buildBody(AppL10n l10n) => SignUpInterestsBody(
        l10n: l10n,
        interests: _interests,
        selected: _selected,
        loading: _loading,
        loadError: _loadError,
        submitting: _submitting,
        submitError: _submitError,
        editMode: widget.editMode,
        draft: widget.draft,
        onToggleInterest: _toggleInterest,
        onSave: _save,
        onRetry: _load,
        onRetryEdit: _loadForEdit,
      );

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
