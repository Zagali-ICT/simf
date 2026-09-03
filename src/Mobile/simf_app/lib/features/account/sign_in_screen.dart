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

/// Sign in — تسجيل الدخول · route: RouteNames.signIn · Figma 168:2800 (D-360)
/// Contract: Face-ID is offered post-sign-in through the step-up nudge
/// (D-486/D-738 — emailed OTP + an OS device-credential confirm), never
/// auto-enrolled; the "remember me" checkbox gates whether the email is stored;
/// the back button was removed by owner directive (D-655).
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
    final last =
        ref.read(simfPrefsStorageProvider).getString(StorageKeys.lastEmail);
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
      await ref.read(authControllerProvider.notifier).signIn(
          email: email, password: password, rememberSession: _rememberMe,);
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
      // A never-verified account used to dead-end here. The server answers 403
      // AUTH_EMAIL_NOT_VERIFIED and the message says "verify your email
      // address" — while offering nowhere to do it. Sign-up is closed to them
      // (the address is already registered), so the account was stranded.
      // Send a fresh code and hand them the verification screen instead.
      if (failure is EmailNotVerified) {
        await _resumeEmailVerification(email, l10n);
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

  /// Re-issues the sign-up verification code and opens the verification screen
  /// with it, so an unverified account can finish where it got stuck.
  ///
  /// The code goes out FIRST and the screen only opens if it was sent: that
  /// screen starts a two-minute resend cooldown on entry, so arriving there
  /// with no code in the inbox would leave the user staring at a countdown
  /// with nothing to type. If the resend is refused — the server caps how many
  /// codes one address may be sent — the reason is shown here instead.
  Future<void> _resumeEmailVerification(String email, AppL10n l10n) async {
    // Captured before the await: routing must survive this State being
    // disposed by the navigation it triggers.
    final router = GoRouter.of(context);
    try {
      await ref.read(authControllerProvider.notifier).resendCode(email: email);
    } on AuthFailure catch (failure) {
      if (mounted) {
        setState(() {
          _error = failure.source.localizedMessage(l10n);
          _password.clear();
        });
      }
      return;
    }
    _password.clear();
    unawaited(
      router.pushNamed(
        RouteNames.emailOtp,
        queryParameters: <String, String>{'email': email},
      ),
    );
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

  /// The design shows the Face-ID button unconditionally. The button gives
  /// clear feedback for the cases that previously failed silently (D-422): (1)
  /// the device has no enrolled OS biometric / secured lock; (2) face login
  /// isn't enabled yet (no device key). Otherwise it runs the OS prompt
  /// (biometric-first with a device-PIN fallback, the banking standard — D-738)
  /// then the device-key sign-in. Prompt outcomes are surfaced explicitly: a
  /// user cancel is silent (their own choice), a lockout / unavailable shows a
  /// message instead of the old silent password-path fallback.
  Future<void> _biometricSignIn() => runBiometricSignIn(
        context: context,
        ref: ref,
        l10n: AppL10n.of(context),
        // The typed address, so the controller can refuse a credential that
        // belongs to someone else BEFORE the challenge. The button is already
        // disabled in that case; this is the backstop for any future caller
        // that forgets, and it is what the package test pins.
        expectedEmail: _email.text,
        onError: (message) {
          if (mounted) setState(() => _error = message);
        },
        onBusy: ({required busy}) {
          if (mounted) setState(() => _busy = busy);
        },
      );

  /// The globe button toggles AR ↔ EN and persists the choice (D-363).
  void _toggleLanguage() {
    // LocaleController.toggle() is, by its own doc, the single code path
    // for this. Four screens re-derived it.
    unawaited(ref.read(localeControllerProvider.notifier).toggle());
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
    // The Face-ID button needs BOTH halves: a device that can prompt, and a
    // credential enrolled on this install. Hardware alone used to be enough,
    // so a fresh phone offered a button whose only outcome was "not enrolled".

    // When both hold it NAMES the account the credential opens: the request
    // carries no address and the server resolves the account from the key, so
    // an anonymous button signs the holder into whoever last enrolled here.
    // A different address disables it with the reason — hiding it explains
    // nothing, and refusing after the OS prompt spends the user's face.
    final biometricAvailable = ref.watch(biometricAvailableProvider).maybeWhen(
          data: (available) => available,
          orElse: () => false,
        );
    final enrolled = ref.watch(enrolledDeviceKeyProvider).value;
    String? biometricLabel;
    String? biometricBlockedHint;
    if (biometricAvailable && enrolled != null) {
      final masked = enrolled.binding.maskedEmail;
      biometricLabel = l10n.faceIdContinueAs(masked);
      final typed = _email.text.trim();
      final mismatch = typed.isNotEmpty &&
          !enrolled.binding.matchesEmail(
            deviceKeyId: enrolled.id,
            email: typed,
          );
      biometricBlockedHint = mismatch ? l10n.faceIdOtherAccount(masked) : null;
    }
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
                biometricLabel: biometricLabel,
                biometricBlockedHint: biometricBlockedHint,
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
