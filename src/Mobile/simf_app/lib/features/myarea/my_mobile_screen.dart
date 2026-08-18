import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/core/validation/phone_validation.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/widgets/account_sub_header.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';
import 'package:simf_app/features/account/widgets/mobile_field.dart';
import 'package:simf_app/features/myarea/data/my_mobile_providers.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// My mobile number — رقم الجوال · route: `RouteNames.myMobile`
/// Contract: validation ONLY — deliberately no OTP and no verification
/// step; the number is saved as typed (normalised). The profile's
/// nationality picks the field — a Saudi national edits `saudiMobile`,
/// everyone else `internationalMobile` (E.164). Owner 2026-07-26.
class MyMobileScreen extends ConsumerStatefulWidget {
  const MyMobileScreen({super.key});

  @override
  ConsumerState<MyMobileScreen> createState() => _MyMobileScreenState();
}

class _MyMobileScreenState extends ConsumerState<MyMobileScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _mobile = TextEditingController();

  bool _saving = false;
  String? _saveError;

  @override
  void initState() {
    super.initState();
    unawaited(_seedFromProfile());
  }

  @override
  void dispose() {
    _mobile.dispose();
    super.dispose();
  }

  /// Seeds the field from the stored number, ONCE.
  ///
  /// Awaits the provider's first future rather than listening: this screen is
  /// one the user then edits, so re-seeding on any later emission would discard
  /// what they had typed. (It is exempt from the pull-to-refresh rule for the
  /// same reason — CLAUDE.md 13.6.)
  Future<void> _seedFromProfile() async {
    final UserProfileResponse profile;
    try {
      profile = await ref.read(myMobileProfileProvider.future);
    } on Object {
      return; // The error branch renders the server's reason.
    }
    if (mounted) {
      _mobile.text = _storedMobile(profile);
    }
  }

  /// The stored number for this profile — the Saudi field for a Saudi national,
  /// the international field otherwise.
  String _storedMobile(UserProfileResponse profile) =>
      (profile.isSaudi ? profile.saudiMobile : profile.internationalMobile) ??
          '';

  Future<void> _save() async {
    final profile = ref.read(myMobileProfileProvider).value;
    if (profile == null || _formKey.currentState?.validate() != true) {
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() {
      _saveError = null;
      _saving = true;
    });
    try {
      // Submit the canonical phone — Arabic-Indic digits folded, a leading `00`
      // rewritten to `+` — so the value matches the server's `+`-only shapes.
      // The loaded profile goes back in full, so a mobile-only change nulls no
      // other field (the upsert is the only write path).
      await ref.read(profileRepositoryProvider).upsertMyProfile(
            profile.toUpsertRequest(mobile: normalizePhone(_mobile.text)),
          );
      // The shared profile read is cached (not autoDispose), so a save that
      // does not invalidate it leaves every selector on the pre-save row.
      ref
        ..invalidate(myMobileProfileProvider)
        // Main's rule: anything that WRITES the profile must invalidate the
        // shared cache, or every selector over it serves the pre-save row.
        ..invalidate(myProfileProvider);
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.myMobileUpdatedToast)));
      if (context.canPop()) {
        context.pop();
      }
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() => _saveError = failure.localizedMessage(l10n));
    } finally {
      if (mounted) {
        setState(() => _saving = false);
      }
    }
  }

  void _back() {
    if (context.canPop()) {
      context.pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          const SimfAuthSweep(top: -180, left: null, right: -80),
          SafeArea(
            child: Column(
              children: <Widget>[
                AccountSubHeader(
                  title: l10n.myMobileTitle,
                  onBack: _back,
                  busy: _saving,
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
    return ref.watch(myMobileProfileProvider).when(
          loading: () => const Center(
            child: CircularProgressIndicator(color: SimfTokens.accent),
          ),
          error: (error, _) => _buildLoadError(l10n, error),
          data: (profile) => _form(l10n, profile),
        );
  }

  Widget _form(AppL10n l10n, UserProfileResponse profile) {
    final stored = _storedMobile(profile);
    return Column(
      children: <Widget>[
        Expanded(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
            child: MaxWidthBody(
              maxWidth: SimfTokens.myMobileScreenMaxWidth,
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    const SizedBox(height: SimfTokens.space6),
                    Text(l10n.myMobileHelper, style: SimfTokens.bodyBeige),
                    const SizedBox(height: SimfTokens.space6),
                    Text(
                      l10n.myMobileCurrentLabel,
                      style: SimfTokens.bodyBeige,
                    ),
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      stored.isEmpty ? l10n.myMobileNoneYet : stored,
                      // A phone number is always read left-to-right, even in
                      // the Arabic RTL layout.
                      textDirection: TextDirection.ltr,
                      textAlign: TextAlign.start,
                      style: const TextStyle(
                        color: SimfTokens.accent,
                        fontSize: SimfTokens.myMobileScreenFontSize,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: SimfTokens.space6),
                    MobileField(
                      saudi: profile.isSaudi,
                      controller: _mobile,
                      validator: (value) => validateMobile(
                        value,
                        saudi: profile.isSaudi,
                        l10n: l10n,
                      ),
                    ),
                    if (_saveError != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space3),
                      Text(
                        _saveError!,
                        style: const TextStyle(
                          color: SimfTokens.danger,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                    ],
                    const SizedBox(height: SimfTokens.space6),
                  ],
                ),
              ),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            0,
            SimfTokens.space4,
            SimfTokens.space6,
          ),
          child: MaxWidthBody(
            maxWidth: SimfTokens.myMobileScreenMaxWidth,
            child: SizedBox(
              width: double.infinity,
              child: AuthSubmitButton(
                key: const ValueKey<String>('myMobileSave'),
                label: l10n.saveLabel,
                busy: _saving,
                onPressed: _saving ? null : () => unawaited(_save()),
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildLoadError(AppL10n l10n, Object error) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              error is ApiFailure
                  ? error.localizedMessage(l10n)
                  : l10n.profileLoadError,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.txtSecondary),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(
              onPressed: () => ref.invalidate(myMobileProfileProvider),
              child: Text(l10n.retryLabel),
            ),
          ],
        ),
      ),
    );
  }
}
