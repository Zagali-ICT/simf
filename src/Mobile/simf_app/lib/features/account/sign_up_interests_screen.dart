import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../core/errors/api_error_l10n.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/widgets/simf_auth_sweep.dart';
import 'data/profile_models.dart';
import 'data/profile_repository.dart';
import 'widgets/account_sub_header.dart';
import 'widgets/interest_chip.dart';

/// Page 007‑01 — اهتماماتي · Sign up — interests. The KSA-Project Figma design
/// (node 505:1083 — D-365): navy surface + sweep, custom header, the
/// اختر اهتماماتك heading, a two-column pill grid (gold selected /
/// `navyDeep`-with-border unselected), the n/10 counter, and the gold متابعة
/// button pinned at the bottom. The previous screen is parked in
/// `_legacy_mockup/`.
///
/// Contract unchanged (D-332/D-050): receives the [SignUpProfileDraft] from
/// Page 007, loads the interests lookup, requires **1–10** picks, fires the
/// **single** `POST /app/account/user-profile` (draft + interestIds), uploads
/// the optional ID image, then routes to Page 010. AUTH-only; a draft-less
/// deep link shows the recover state back to the profile-data screen.
///
/// Clean-code frozen (D-550, Phase 3): screen-local colour consts dropped for
/// `SimfTokens` (`chipBorderNavy` added); the pill extracted to [InterestChip];
/// header to the shared [AccountSubHeader] (D-658); the body capped by
/// [MaxWidthBody]. Behaviour + render unchanged — the 505:1083 golden locks it.
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
      // Two-photo split — both images must land on the server BEFORE the
      // profile save: the server now rejects a profile with no stored ID
      // document (all registrants) or, for a male, no stored face photo. A
      // pre-save upload keeps the app from saving an incomplete profile that
      // would bounce back to this form on every sign-in.
      final isMale = draft.request.gender == AppGender.male;

      // 1) ID document — mandatory for EVERYONE; a failed upload blocks.
      final idBytes = draft.idImageBytes;
      final idName = draft.idImageName;
      if (idBytes != null && idName != null) {
        try {
          await repo.uploadIdImage(bytes: idBytes, filename: idName);
        } on ApiFailure {
          if (!mounted) {
            return;
          }
          setState(() => _submitError = l10n.idImageUploadFailed);
          return;
        }
      }

      // 2) Face photo (avatar) — mandatory for men, optional for women. On
      //    success bump the avatar bust so the top profile photo refreshes
      //    everywhere; a failed upload blocks only for a male.
      final faceBytes = draft.faceImageBytes;
      final faceName = draft.faceImageName;
      if (faceBytes != null && faceName != null) {
        try {
          await repo.uploadAvatar(bytes: faceBytes, filename: faceName);
          ref.read(avatarBustProvider.notifier).state++;
        } on ApiFailure {
          if (!mounted) {
            return;
          }
          if (isMale) {
            setState(() => _submitError = l10n.facePhotoUploadFailed);
            return;
          }
          // Optional for women — fall through and save.
        }
      }
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
                onPressed: () => context.goNamed(RouteNames.signUpVisitor),
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
    return Column(
      children: <Widget>[
        Expanded(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: MaxWidthBody(
              maxWidth: 560,
              child: Column(
                children: <Widget>[
                  const SizedBox(height: 24),
                  Text(
                    l10n.interestsChooseTitle,
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 24,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: 16),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 24),
                    child: Text(
                      l10n.interestsHelper,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: 16,
                        fontWeight: FontWeight.w500,
                        height: 21 / 16,
                      ),
                    ),
                  ),
                  const SizedBox(height: 32),
                  _buildChips(l10n),
                  const SizedBox(height: 24),
                  Text(
                    l10n.interestsCounter(_selected.length),
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: 14,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  if (_submitError != null) ...<Widget>[
                    const SizedBox(height: 12),
                    Text(
                      _submitError!,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        color: SimfTokens.danger,
                        fontSize: 12,
                      ),
                    ),
                  ],
                  const SizedBox(height: 24),
                ],
              ),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
          child: MaxWidthBody(
            maxWidth: 560,
            child: SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: (_submitting || _selected.isEmpty)
                    ? null
                    : () => unawaited(_save()),
                child: _submitting
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : Text(
                        l10n.continueLabel,
                        style: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
              ),
            ),
          ),
        ),
      ],
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

  /// The design's two-column pill grid (Figma 505:1222): gold when selected,
  /// `navyDeep` with a muted border otherwise.
  Widget _buildChips(AppL10n l10n) {
    if (_interests.isEmpty) {
      return Text(
        l10n.interestsEmpty,
        style: const TextStyle(color: SimfTokens.txtSecondary),
      );
    }
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        crossAxisSpacing: 10,
        mainAxisSpacing: 12,
        mainAxisExtent: 43,
      ),
      itemCount: _interests.length,
      itemBuilder: (context, index) {
        final interest = _interests[index];
        return InterestChip(
          label: l10n.isArabic ? interest.nameArabic : interest.name,
          selected: _selected.contains(interest.id),
          onTap: _submitting ? null : () => _toggleInterest(interest.id, l10n),
        );
      },
    );
  }
}
