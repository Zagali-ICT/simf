import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import 'data/profile_models.dart';
import 'data/profile_repository.dart';

// Interests-frame colours (Figma 505:1083) not yet shared by a second screen.
const Color _chipBorder = Color(0xFF2A4066);
const Color _sweepTint = Color(0x0AFFFFFF);

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
      final saved = await repo.upsertMyProfile(
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
          // Decorative diagonal sweep (Figma 505:1086, top-right area).
          Positioned(
            top: -180,
            right: -40,
            child: Transform.rotate(
              angle: 0.4936, // 28.28°
              child: Container(
                width: 313,
                height: 323,
                decoration: BoxDecoration(
                  color: _sweepTint,
                  borderRadius: BorderRadius.circular(40),
                ),
              ),
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                // Header band (Figma 505:1190): chevron left, centred title.
                SizedBox(
                  height: 56,
                  child: Stack(
                    alignment: Alignment.center,
                    children: <Widget>[
                      Align(
                        alignment: Alignment.centerLeft,
                        child: Padding(
                          padding: const EdgeInsets.only(left: 8),
                          child: IconButton(
                            onPressed: _submitting ? null : _back,
                            icon: const Icon(
                              Icons.arrow_back_ios_new,
                              color: Colors.white,
                              size: 20,
                              textDirection: TextDirection.ltr,
                            ),
                          ),
                        ),
                      ),
                      Text(
                        l10n.interestsTitle,
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 24,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  ),
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
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 400),
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
        final selected = _selected.contains(interest.id);
        return InkWell(
          onTap: _submitting ? null : () => _toggleInterest(interest.id, l10n),
          borderRadius: BorderRadius.circular(999),
          child: Container(
            alignment: Alignment.center,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            decoration: BoxDecoration(
              color: selected ? SimfTokens.accent : SimfTokens.navyDeep,
              borderRadius: BorderRadius.circular(999),
              border: Border.all(
                width: 1.2,
                color: selected ? SimfTokens.accent : _chipBorder,
              ),
            ),
            child: Text(
              l10n.isArabic ? interest.nameArabic : interest.name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 14,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        );
      },
    );
  }
}
