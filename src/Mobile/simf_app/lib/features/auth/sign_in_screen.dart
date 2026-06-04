import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:local_auth/local_auth.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../profile/data/profile_repository.dart';

/// Page 003 — تسجيل الدخول · Sign in (Page_003 docs).
///
/// Email + password sign-in against `POST /app/auth/sign-in`. On a 2FA account
/// the server returns an emailed OTP challenge and the app routes to the OTP
/// screen (visitor second factor — no TOTP). The email is pre-filled from the
/// last successful sign-in (Logic L-3); the client caps email≤50 / password≤32
/// (Logic D2, UI-only). The biometric (device-key) button is added in a follow-up.
class SignInScreen extends ConsumerStatefulWidget {
  const SignInScreen({super.key});

  @override
  ConsumerState<SignInScreen> createState() => _SignInScreenState();
}

class _SignInScreenState extends ConsumerState<SignInScreen> {
  final TextEditingController _email = TextEditingController();
  final TextEditingController _password = TextEditingController();
  final LocalAuthentication _localAuth = LocalAuthentication();
  bool _obscure = true;
  bool _busy = false;
  bool _biometricAvailable = false;
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
    unawaited(_checkBiometric());
  }

  /// Offers the biometric button only when a device key is enrolled AND the
  /// device supports biometrics. Any failure (e.g. no native plugin in this
  /// tree) silently leaves it hidden — the password path is always available.
  Future<void> _checkBiometric() async {
    try {
      final enrolled = await ref
          .read(authControllerProvider.notifier)
          .hasEnrolledDeviceKey();
      if (!enrolled) {
        return;
      }
      final supported = await _localAuth.isDeviceSupported();
      if (mounted) {
        setState(() => _biometricAvailable = supported);
      }
    } catch (_) {
      // local_auth unavailable — keep the biometric button hidden.
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

  Future<void> _submit() async {
    final email = _email.text.trim();
    final password = _password.text;
    if (email.isEmpty || password.isEmpty) {
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref
          .read(authControllerProvider.notifier)
          .signIn(email: email, password: password);
      await ref
          .read(simfPrefsStorageProvider)
          .setString(StorageKeys.lastEmail, email);
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
        _error = failure is NetworkUnavailable
            ? l10n.networkErrorBody
            : failure.source.message;
        _password.clear();
      });
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _onSignedIn() async {
    // Best-effort enrolment for future biometric sign-in; uses the
    // container-level controller so it survives this screen's disposal.
    unawaited(
      _maybeEnrolBiometric(ref.read(authControllerProvider.notifier)),
    );
    await _routeAfterSignIn();
  }

  /// After a successful sign-in, route a profile-incomplete visitor to the
  /// profile-completion screen (Page_007); everyone else goes home. The profile
  /// probe must never block sign-in, so any failure falls back to home
  /// (Page_007 auto-route, D-288 Slice 3).
  Future<void> _routeAfterSignIn() async {
    try {
      final profile =
          await ref.read(profileRepositoryProvider).getMyProfile();
      if (!mounted) {
        return;
      }
      if (!profile.isComplete) {
        context.goNamed(RouteNames.signUpVisitor);
        return;
      }
    } catch (_) {
      // Never block sign-in on the profile probe — fall through to home.
    }
    if (mounted) {
      context.go('/');
    }
  }

  Future<void> _maybeEnrolBiometric(AuthController notifier) async {
    try {
      if (await notifier.hasEnrolledDeviceKey()) {
        return;
      }
      if (!await _localAuth.isDeviceSupported()) {
        return;
      }
      await notifier.enrolDeviceKey();
    } catch (_) {
      // Enrolment is best-effort; never block sign-in on it.
    }
  }

  Future<void> _biometricSignIn() async {
    final l10n = AppL10n.of(context);
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
      if (!mounted) {
        return;
      }
      if (ref.read(authControllerProvider) is AuthStateSignedIn) {
        await _routeAfterSignIn();
      }
    } on AuthFailure catch (failure) {
      if (mounted) {
        setState(() {
          _error = failure is NetworkUnavailable
              ? l10n.networkErrorBody
              : failure.source.message;
        });
      }
    } catch (_) {
      // Biometric / plugin failure — fall back to the password path silently.
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.signInTitle),
        // Placeholder controls — buttons only, no wiring yet (owner: "for lang
        // and dark mode only add btn — no fun"). Theme switching + language
        // switching are wired in a later increment.
        actions: <Widget>[
          IconButton(
            tooltip: l10n.themeToggleTooltip,
            icon: const Icon(Icons.dark_mode_outlined),
            onPressed: () {},
          ),
          TextButton(
            onPressed: () {},
            style: TextButton.styleFrom(foregroundColor: SimfTokens.accent),
            child: Text(l10n.languageToggleLabel),
          ),
          const SizedBox(width: SimfTokens.space2),
        ],
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              const SizedBox(height: SimfTokens.space5),
              const Center(child: _LogoMark()),
              const SizedBox(height: SimfTokens.space8),
              TextField(
                controller: _email,
                keyboardType: TextInputType.emailAddress,
                textDirection: TextDirection.ltr,
                maxLength: 50,
                enabled: !_busy,
                onChanged: (_) => setState(() {}),
                decoration: InputDecoration(
                  labelText: l10n.emailLabel,
                  counterText: '',
                ),
              ),
              const SizedBox(height: SimfTokens.space3),
              TextField(
                controller: _password,
                obscureText: _obscure,
                maxLength: 32,
                enabled: !_busy,
                onChanged: (_) => setState(() {}),
                onSubmitted: (_) {
                  if (_canSubmit) {
                    unawaited(_submit());
                  }
                },
                decoration: InputDecoration(
                  labelText: l10n.passwordLabel,
                  counterText: '',
                  suffixIcon: IconButton(
                    tooltip: _obscure
                        ? l10n.showPasswordTooltip
                        : l10n.hidePasswordTooltip,
                    icon: Icon(
                      _obscure
                          ? Icons.visibility_outlined
                          : Icons.visibility_off_outlined,
                    ),
                    onPressed: () => setState(() => _obscure = !_obscure),
                  ),
                ),
              ),
              if (_error != null) ...<Widget>[
                const SizedBox(height: SimfTokens.space3),
                Text(
                  _error!,
                  style: const TextStyle(color: SimfTokens.danger),
                ),
              ],
              const SizedBox(height: SimfTokens.space5),
              FilledButton(
                onPressed: _canSubmit ? () => unawaited(_submit()) : null,
                child: _busy
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(l10n.signInButton),
              ),
              if (_biometricAvailable) ...<Widget>[
                const SizedBox(height: SimfTokens.space3),
                OutlinedButton.icon(
                  onPressed:
                      _busy ? null : () => unawaited(_biometricSignIn()),
                  icon: const Icon(Icons.fingerprint),
                  label: Text(l10n.biometricSignInTooltip),
                ),
              ],
              const SizedBox(height: SimfTokens.space2),
              TextButton(
                onPressed: _busy
                    ? null
                    : () => context.goNamed(RouteNames.forgotPassword),
                child: Text(l10n.forgotPasswordLink),
              ),
              const SizedBox(height: SimfTokens.space4),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Text(l10n.createAccountQuestion),
                  TextButton(
                    onPressed: _busy
                        ? null
                        : () => context.pushNamed(RouteNames.signUpType),
                    child: Text(l10n.createAccountLink),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Interim brass-on-navy logo placeholder (final asset per SIMF-VID-001).
class _LogoMark extends StatelessWidget {
  const _LogoMark();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 72,
      height: 72,
      decoration: const BoxDecoration(
        color: SimfTokens.navy,
        shape: BoxShape.circle,
      ),
      alignment: Alignment.center,
      child: const Text(
        'SIMF',
        style: TextStyle(
          color: SimfTokens.accent,
          fontWeight: FontWeight.w800,
          fontSize: SimfTokens.textMd,
          letterSpacing: 1.5,
        ),
      ),
    );
  }
}
