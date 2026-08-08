import 'dart:async';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show TextInput;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_app/features/account/biometric_sign_in.dart';
import 'package:simf_app/features/account/data/sign_in_validators.dart';
import 'package:simf_app/features/account/post_auth_route.dart';
import 'package:simf_app/features/account/widgets/account_auth_prompt.dart';
import 'package:simf_app/features/account/widgets/account_card.dart';
import 'package:simf_app/features/account/widgets/account_form_field.dart';
import 'package:simf_app/features/account/widgets/account_header.dart';
import 'package:simf_app/features/account/widgets/account_remember_forgot.dart';
import 'package:simf_app/features/account/widgets/account_top_controls.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';
import 'package:simf_app/features/account/widgets/sign_in_alt_actions.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Page 003 — تسجيل الدخول · Sign in. The KSA-Project Figma design (node
/// 168:2800), promoted from the D-358 preview to the official sign-in
/// (D-360); the previous mockup screen is parked in `_legacy_mockup/`.
///
/// Email + password against `POST /app/auth/sign-in` with the 2FA email-OTP
/// redirect, the post-sign-in profile-completeness route (D-288), and the
/// Face-ID device-key path. Face-ID enrolment is offered post-sign-in via the
/// step-up nudge (D-486/D-738: emailed OTP + OS device-credential confirm), not
/// auto-enrolled. The email is pre-filled from the last successful sign-in; the design's
/// "remember me" checkbox gates whether it is stored. The globe language toggle
/// (top-right, wired to [LocaleController], D-363) and the underlined guest link
/// (Page_012) round out the screen.
///
/// Clean-code (D-655): the screen composes the shared account widgets
/// ([AccountHeader], [AccountTopControls], [AccountCard], [AccountEmailField],
/// [AccountPasswordField], [AccountRememberForgot], [AccountAuthPrompt]) instead
/// of local `_build*` copies; the decorative sweep is the shared [SimfAuthSweep];
/// the back button was removed (it only dead-ended to onboarding — owner).
class SignInScreen extends ConsumerStatefulWidget {
  const SignInScreen({super.key});

  @override
  ConsumerState<SignInScreen> createState() => _SignInScreenState();
}

class _SignInScreenState extends ConsumerState<SignInScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _email = TextEditingController();
  final TextEditingController _password = TextEditingController();
  bool _obscure = true;
  bool _busy = false;
  // Default ON natively; OFF on the web PoC, where prefs live in shared browser
  // localStorage and a remembered email could surface to the next user on a
  // kiosk (D-384 — web = PoC exception; production is mobile-only).
  bool _rememberMe = !kIsWeb;
  String? _error;

  @override
  void initState() {
    super.initState();
    final last = ref
        .read(simfPrefsStorageProvider)
        .getString(StorageKeys.lastEmail);
    if (last != null && last.isNotEmpty) {
      _email.text = last;
    }
  }

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  bool get _canSubmit =>
      _email.text.trim().isNotEmpty && _password.text.isNotEmpty && !_busy;

  /// #7 — empty → required; otherwise the malformed-email shape is rejected
  /// before the network round-trip, with an inline bilingual error.
  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    final email = _email.text.trim();
    final password = _password.text;
    final l10n = AppL10n.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref
          .read(authControllerProvider.notifier)
          .signIn(email: email, password: password, rememberSession: _rememberMe);
      // The password was accepted (a direct session or a 2FA challenge) — save
      // the final submitted credentials to the OS autofill store so it keeps
      // THIS email, not the heuristic first-typed guess it grabbed. Mirror
      // "remember me": unchecked → discard without saving.
      TextInput.finishAutofillContext(shouldSave: _rememberMe);
      final prefs = ref.read(simfPrefsStorageProvider);
      if (_rememberMe) {
        await prefs.setString(StorageKeys.lastEmail, email);
      } else {
        // Honour an unchecked "remember me" in both directions: forget any
        // email a previous remembered sign-in stored, so unchecking actually
        // stops the address pre-filling next time.
        await prefs.remove(StorageKeys.lastEmail);
      }
      if (!mounted) {
        return;
      }
      final state = ref.read(authControllerProvider);
      if (state is AuthStateAwaitingOtp) {
        context.goNamed(RouteNames.verifyOtp);
      } else if (state is AuthStateSignedIn) {
        await _onSignedIn();
      }
    } on AuthFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = failure.source.localizedMessage(l10n);
        _password.clear();
      });
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _onSignedIn() async {
    // One-time offer to enable Face-ID for next time (D-441) — runs on both the
    // direct sign-in (here) and the 2FA OTP completion, so every path can
    // activate it. Then route: D-374 — the profileComplete flag rides the
    // sign-in hydration, so the shared post-auth rule routes directly (the same
    // rule runs after the OTP step and the splash restore).
    await maybeOfferBiometricEnrolment(context, ref);
    if (mounted) {
      routeAfterAuth(context, ref);
    }
  }

  /// The design shows the Face-ID button unconditionally. The button gives clear
  /// feedback for the cases that previously failed silently (D-422):
  /// (1) the device has no enrolled OS biometric / secured lock; (2) face login
  /// isn't enabled yet (no device key). Otherwise it runs the OS prompt
  /// (biometric-first with a device-PIN fallback, the banking standard — D-738)
  /// then the device-key sign-in. Prompt outcomes are surfaced explicitly: a
  /// user cancel is silent (their own choice), a lockout / unavailable shows a
  /// message instead of the old silent password-path fallback.
  Future<void> _biometricSignIn() => runBiometricSignIn(
        context: context,
        ref: ref,
        l10n: AppL10n.of(context),
        onError: (message) {
          if (mounted) setState(() => _error = message);
        },
        onBusy: ({required busy}) {
          if (mounted) setState(() => _busy = busy);
        },
      );

  /// The globe button toggles AR ↔ EN and persists the choice (D-363).
  void _toggleLanguage() {
    final isArabic =
        ref.read(localeControllerProvider).languageCode == 'ar';
    unawaited(
      ref
          .read(localeControllerProvider.notifier)
          .setLanguage(isArabic ? 'en' : 'ar'),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          const SimfAuthSweep(),
          SafeArea(
            // The scroll body paints first; the language control is the last
            // Stack child so it sits on top and always receives its tap (the
            // MaxWidthBody-centred body fills the cross axis behind it).
            child: Stack(
              children: <Widget>[
                SingleChildScrollView(
                  padding: const EdgeInsets.symmetric(
                    horizontal: SimfTokens.space4,
                    vertical: 56,
                  ),
                  child: MaxWidthBody(
                    maxWidth: SimfTokens.signInScreenMaxWidth,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        AccountHeader(title: l10n.signInForumTitle),
                        const SizedBox(height: SimfTokens.space6),
                        _buildCard(l10n),
                      ],
                    ),
                  ),
                ),
                // Language globe only — the sign-in screen has no back target
                // (the back button was removed per owner directive, D-655).
                AccountTopControls(
                  onToggleLanguage: _toggleLanguage,
                  busy: _busy,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCard(AppL10n l10n) {
    // Only show the Face-ID sign-in button on a device with a usable biometric
    // (hardware + an OS-enrolled face/finger); hidden entirely on sensorless
    // devices (loading / unsupported → hidden) rather than shown then erroring.
    final biometricAvailable = ref.watch(biometricAvailableProvider).maybeWhen(
          data: (available) => available,
          orElse: () => false,
        );
    return AccountCard(
      // AutofillGroup + the fields' hints let the OS treat this as one login
      // form and, on a successful submit, save the FINAL credentials (see
      // _submit's finishAutofillContext) rather than a first-typed guess.
      child: AutofillGroup(
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Text(
                l10n.signInTitle,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: SimfTokens.text24,
                  fontWeight: FontWeight.w600,
                  color: SimfTokens.headlineInk,
                ),
              ),
              const SizedBox(height: SimfTokens.space6),
              AccountEmailField(
                controller: _email,
                label: l10n.emailLabel,
                enabled: !_busy,
                validator: (v) => validateSignInEmail(v, l10n),
                onChanged: (_) => setState(() {}),
                autofillHints: const <String>[
                  AutofillHints.username,
                  AutofillHints.email,
                ],
              ),
              const SizedBox(height: SimfTokens.space4),
              AccountPasswordField(
                controller: _password,
                label: l10n.passwordLabel,
                obscure: _obscure,
                onToggleObscure: () => setState(() => _obscure = !_obscure),
                enabled: !_busy,
                validator: (v) => validateSignInPassword(v, l10n),
                onChanged: (_) => setState(() {}),
                onSubmitted: (_) {
                  if (_canSubmit) {
                    unawaited(_submit());
                  }
                },
                autofillHints: const <String>[AutofillHints.password],
              ),
              const SizedBox(height: SimfTokens.space2),
              AccountRememberForgot(
                rememberMe: _rememberMe,
                onRememberChanged: (v) => setState(() => _rememberMe = v),
                rememberLabel: l10n.rememberMeLabel,
                forgotLabel: l10n.forgotPasswordLink,
                onForgot: () => context.goNamed(RouteNames.forgotPassword),
                enabled: !_busy,
              ),
              if (_error != null) ...<Widget>[
                const SizedBox(height: SimfTokens.space3),
                Text(
                  _error!,
                  style: SimfTokens.labelDangerSm,
                ),
              ],
              const SizedBox(height: SimfTokens.space6),
              AuthSubmitButton(
                label: l10n.signInButton,
                busy: _busy,
                onPressed: _canSubmit ? () => unawaited(_submit()) : null,
              ),
              const SizedBox(height: SimfTokens.space2),
              AccountAuthPrompt(
                question: l10n.createAccountQuestion,
                linkLabel: l10n.createAccountLink,
                onTap: () => context.pushNamed(RouteNames.signUpForm),
                enabled: !_busy,
              ),
              const SizedBox(height: SimfTokens.space6),
              SignInAltActions(
                biometricAvailable: biometricAvailable,
                busy: _busy,
                onBiometric: () => unawaited(_biometricSignIn()),
                onBadge: () => context.pushNamed(RouteNames.badgeSignIn),
                onGuest: () => context.pushNamed(RouteNames.guestMode),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
