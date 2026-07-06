import 'dart:async';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:local_auth/local_auth.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/localization/locale_controller.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../core/errors/api_error_l10n.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/validation/email_validation.dart';
import '../../core/validation/required_validation.dart';
import '../../core/widgets/simf_auth_sweep.dart';
import 'biometric_auth.dart';
import 'post_auth_route.dart';
import 'widgets/account_auth_prompt.dart';
import 'widgets/account_card.dart';
import 'widgets/account_form_field.dart';
import 'widgets/account_header.dart';
import 'widgets/account_remember_forgot.dart';
import 'widgets/account_top_controls.dart';
import 'widgets/auth_chrome.dart';
import 'widgets/sign_in_alt_actions.dart';

/// Page 003 — تسجيل الدخول · Sign in. The KSA-Project Figma design (node
/// 168:2800), promoted from the D-358 preview to the official sign-in
/// (D-360); the previous mockup screen is parked in `_legacy_mockup/`.
///
/// Email + password against `POST /app/auth/sign-in` with the 2FA email-OTP
/// redirect, the post-sign-in profile-completeness route (D-288), best-effort
/// device-key enrolment after a successful sign-in, and the Face-ID device-key
/// path. The email is pre-filled from the last successful sign-in; the design's
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
  final LocalAuthentication _localAuth = LocalAuthentication();
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
  String? _validateEmail(String? value) {
    final email = value?.trim() ?? '';
    final l10n = AppL10n.of(context);
    if (isBlank(email)) {
      return l10n.requiredField;
    }
    return isValidEmail(email) ? null : l10n.invalidEmail;
  }

  /// Sign-in only requires a non-empty password — it does NOT enforce the
  /// sign-up password policy (any existing password must be allowed to sign in;
  /// the server authenticates it).
  String? _validatePassword(String? value) {
    return isBlank(value) ? AppL10n.of(context).requiredField : null;
  }

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

  /// The design shows the Face-ID button unconditionally. The button now gives
  /// clear feedback for the two cases that previously failed silently (D-422):
  /// (1) the device has no enrolled OS face/fingerprint; (2) face login has not
  /// been enabled yet because the user hasn't done a password sign-in on this
  /// device (which enrols the device key). Otherwise it runs the OS biometric
  /// then the device-key sign-in.
  Future<void> _biometricSignIn() async {
    final l10n = AppL10n.of(context);
    final notifier = ref.read(authControllerProvider.notifier);
    // (1) The device must actually have a biometric enrolled.
    try {
      final supported = await _localAuth.isDeviceSupported();
      final available = supported
          ? await _localAuth.getAvailableBiometrics()
          : const <BiometricType>[];
      if (!supported || available.isEmpty) {
        if (mounted) {
          setState(() => _error = l10n.biometricUnavailable);
        }
        return;
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error = l10n.biometricUnavailable);
      }
      return;
    }
    // (2) Face login needs a device key, enrolled on a prior password sign-in.
    try {
      if (!await notifier.hasEnrolledDeviceKey()) {
        if (mounted) {
          setState(() => _error = l10n.biometricNotEnrolled);
        }
        return;
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error = l10n.biometricNotEnrolled);
      }
      return;
    }
    try {
      final ok = await _localAuth.authenticate(
        localizedReason: l10n.biometricSignInTooltip,
        options: const AuthenticationOptions(
          biometricOnly: true,
          stickyAuth: true,
        ),
      );
      if (!ok || !mounted) {
        return;
      }
      setState(() {
        _busy = true;
        _error = null;
      });
      await ref.read(authControllerProvider.notifier).signInWithDeviceKey();
    } on AuthFailure catch (failure) {
      if (mounted) {
        setState(() {
          _error = failure.source.localizedMessage(l10n);
        });
      }
    } catch (_) {
      // Biometric / plugin failure — fall back to the password path silently.
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
    // Route on the resulting auth state, OUTSIDE the try: signInWithDeviceKey
    // establishes the session before its trailing profile reload, so a
    // non-AuthFailure thrown there (swallowed above) must not skip the
    // navigation home — the biometric path now mirrors the password path (D-441).
    if (mounted && ref.read(authControllerProvider) is AuthStateSignedIn) {
      routeAfterAuth(context, ref);
    }
  }

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
                    horizontal: 16,
                    vertical: 56,
                  ),
                  child: MaxWidthBody(
                    maxWidth: 560,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        AccountHeader(title: l10n.signInForumTitle),
                        const SizedBox(height: 24),
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
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              l10n.signInTitle,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 24,
                fontWeight: FontWeight.w600,
                color: SimfTokens.headlineInk,
              ),
            ),
            const SizedBox(height: 24),
            AccountEmailField(
              controller: _email,
              label: l10n.emailLabel,
              enabled: !_busy,
              validator: _validateEmail,
              onChanged: (_) => setState(() {}),
            ),
            const SizedBox(height: 16),
            AccountPasswordField(
              controller: _password,
              label: l10n.passwordLabel,
              obscure: _obscure,
              onToggleObscure: () => setState(() => _obscure = !_obscure),
              enabled: !_busy,
              validator: _validatePassword,
              onChanged: (_) => setState(() {}),
              onSubmitted: (_) {
                if (_canSubmit) {
                  unawaited(_submit());
                }
              },
            ),
            const SizedBox(height: 8),
            AccountRememberForgot(
              rememberMe: _rememberMe,
              onRememberChanged: (v) => setState(() => _rememberMe = v),
              rememberLabel: l10n.rememberMeLabel,
              forgotLabel: l10n.forgotPasswordLink,
              onForgot: () => context.goNamed(RouteNames.forgotPassword),
              enabled: !_busy,
            ),
            if (_error != null) ...<Widget>[
              const SizedBox(height: 12),
              Text(
                _error!,
                style: const TextStyle(color: SimfTokens.danger, fontSize: 12),
              ),
            ],
            const SizedBox(height: 24),
            AuthSubmitButton(
              label: l10n.signInButton,
              busy: _busy,
              onPressed: _canSubmit ? () => unawaited(_submit()) : null,
            ),
            const SizedBox(height: 8),
            AccountAuthPrompt(
              question: l10n.createAccountQuestion,
              linkLabel: l10n.createAccountLink,
              onTap: () => context.pushNamed(RouteNames.signUpForm),
              enabled: !_busy,
            ),
            const SizedBox(height: 24),
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
    );
  }
}
