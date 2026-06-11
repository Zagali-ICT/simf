import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../profile/data/profile_models.dart';
import '../profile/data/profile_repository.dart';

/// LEGACY — the pre-redesign Page 007-01 interests step, parked here when the
/// KSA-Project design landed at the real path (D-365). Never routed; kept
/// compiling until programme close (§6 freeze rules).
///
/// The interests step of sign-up, split out of Page 007. It receives the
/// [SignUpProfileDraft] collected on the profile-data screen (Page 007), loads
/// the interests lookup, and requires the user to pick **1–10**. On **Save** it
/// fires the **single** `POST /app/account/user-profile` — the Page-007 data
/// **plus** the picked `interestIds` (via `copyWith`) — then uploads the optional
/// ID image (after the row exists) and routes to the registration-success /
/// "please wait" screen (Page 010). There is no separate interests write (D-050).
///
/// AUTH-only (Page_007-01 L-1): the route is in the auth gate, so an anonymous
/// open is impossible. A direct open without a draft (deep link) shows a recover
/// state that sends the user back to the profile-data screen.
class SignUpInterestsScreen extends ConsumerStatefulWidget {
  const SignUpInterestsScreen({super.key, this.draft});

  /// The in-memory draft from Page 007 (`state.extra`). Null only on a direct
  /// deep-link open with no preceding data screen.
  final SignUpProfileDraft? draft;

  @override
  ConsumerState<SignUpInterestsScreen> createState() =>
      _SignUpInterestsScreenState();
}

class _SignUpInterestsScreenState extends ConsumerState<SignUpInterestsScreen> {
  List<InterestItem> _interests = const <InterestItem>[];
  final Set<String> _selected = <String>{};

  bool _loading = true;
  String? _loadError;
  bool _submitting = false;
  String? _submitError;

  @override
  void initState() {
    super.initState();
    // Pre-select any interests already on the carried draft (re-entry / edit).
    final existing = widget.draft?.request.interestIds ?? const <String>[];
    _selected.addAll(existing);
    if (widget.draft != null) {
      unawaited(_load());
    } else {
      _loading = false;
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
      setState(() {
        _loadError = failure.message;
        _loading = false;
      });
    }
  }

  void _toggleInterest(String id, bool selected, AppL10n l10n) {
    if (selected && _selected.length >= 10) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.interestsMaxReached)));
      return;
    }
    setState(() {
      if (selected) {
        _selected.add(id);
      } else {
        _selected.remove(id);
      }
    });
  }

  Future<void> _save() async {
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
      await repo.upsertMyProfile(
        draft.request.copyWith(interestIds: _selected.toList()),
      );
      final imageFailed = await _uploadIdImageIfAny(repo, draft);
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

  /// Uploads the carried ID image after the profile exists. Returns true when an
  /// image was carried but its upload failed — the profile save still succeeded,
  /// so this is a non-blocking warning, not an error.
  Future<bool> _uploadIdImageIfAny(
    ProfileRepository repo,
    SignUpProfileDraft draft,
  ) async {
    final bytes = draft.idImageBytes;
    final name = draft.idImageName;
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navy,
      appBar: AppBar(title: Text(l10n.interestsTitle)),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (widget.draft == null) {
      // Direct open with no preceding data screen — recover by sending the user
      // to the profile-data step.
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
                onPressed: () =>
                    context.goNamed(RouteNames.signUpVisitor),
                child: Text(l10n.signUpVisitorTitle),
              ),
            ],
          ),
        ),
      );
    }
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_loadError != null) {
      return _buildLoadError(l10n);
    }
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space5,
        SimfTokens.space4,
        SimfTokens.space8,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _buildInterests(l10n),
          const SizedBox(height: SimfTokens.space6),
          if (_submitError != null) ...<Widget>[
            Text(
              _submitError!,
              style: const TextStyle(color: SimfTokens.danger),
            ),
            const SizedBox(height: SimfTokens.space3),
          ],
          FilledButton(
            onPressed: (_submitting || _selected.isEmpty)
                ? null
                : () => unawaited(_save()),
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
              _loadError!,
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

  Widget _buildInterests(AppL10n l10n) {
    if (_interests.isEmpty) {
      return Text(
        l10n.interestsEmpty,
        style: const TextStyle(color: SimfTokens.txtSecondary),
      );
    }
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
              l10n.interestsCounter(_selected.length),
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
                  selected: _selected.contains(interest.id),
                  onSelected: _submitting
                      ? null
                      : (value) => _toggleInterest(interest.id, value, l10n),
                ),
              )
              .toList(),
        ),
      ],
    );
  }
}
