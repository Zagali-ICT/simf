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
import '../../app/widgets/simf_logo.dart';
import '../../app/widgets/simf_svg_icon.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/validation/email_validation.dart';
import '../../core/validation/required_validation.dart';
import '../../core/widgets/simf_field_label.dart';
import '../../core/widgets/simf_field_style.dart';
import 'biometric_auth.dart';
import 'post_auth_route.dart';
import 'widgets/auth_chrome.dart';
import 'widgets/sign_in_alt_actions.dart';

// Exact iconify / Figma glyphs from frame 758:2555 (no 1:1 Material match):
// the top back-chevron + gold globe, and the password eye. (The Face-ID mark
// lives with the alt-actions block in widgets/sign_in_alt_actions.dart.)
const String _icBack = 'assets/icons/auth_back.svg'; // iconamoon:arrow-left-2
const String _icGlobe = 'assets/icons/auth_globe.svg'; // exact Figma globe
const String _icEyeOff = 'assets/icons/auth_eye_off.svg'; // iconamoon:eye-off
const String _icEye = 'assets/icons/auth_eye.svg'; // iconamoon:eye

/// Page 003 — تسجيل الدخول · Sign in. The KSA-Project Figma design (node
/// 168:2800), promoted from the D-358 preview to the official sign-in
/// (D-360); the previous mockup screen is parked in `_legacy_mockup/`.
///
/// Email + password against `POST /app/auth/sign-in` with the 2FA email-OTP
/// redirect, the post-sign-in profile-completeness route (D-288), best-effort
/// device-key enrolment after a successful sign-in, and the Face-ID
/// device-key path. The email is pre-filled from the last successful sign-in;
/// the design's "remember me" checkbox gates whether it is stored. The frame's
/// 2026-06-11 update (D-363) added the globe language toggle (top-right,
/// wired to [LocaleController]) and the underlined "الدخول كزائر" guest link —
/// the app's only guest-mode entry (Page_012).
///
/// Clean-code frozen (D-549, Phase 3): screen-local colour aliases dropped for
/// `SimfTokens`; the gold CTA reuses the shared [AuthSubmitButton]; the alt
/// entry methods moved to [SignInAltActions]; the body is capped by
/// [MaxWidthBody]. Render unchanged — the 168:2800 golden locks it.
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
    // Route on the resulting auth state, OUTSIDE the try: signInWithDeviceKey
    // establishes the session before its trailing profile reload, so a
    // non-AuthFailure thrown there (swallowed above) must not skip the
    // navigation home — the biometric path now mirrors the password path (D-441).
    if (mounted && ref.read(authControllerProvider) is AuthStateSignedIn) {
      routeAfterAuth(context, ref);
    }
  }

  void _back() {
    if (context.canPop()) {
      context.pop();
      return;
    }
    context.goNamed(RouteNames.onboarding);
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
          // Decorative diagonal sweep behind the header (Figma node 168:2850,
          // rotated 28.28° — approximated as a tinted rounded rectangle).
          Positioned(
            top: -156,
            left: 60,
            child: Transform.rotate(
              angle: 0.4936, // 28.28°
              child: Container(
                width: 313,
                height: 323,
                decoration: BoxDecoration(
                  color: SimfTokens.surfaceTint,
                  borderRadius: BorderRadius.circular(40),
                ),
              ),
            ),
          ),
          SafeArea(
            // The scroll body paints first; the back/language controls are the
            // last Stack child so they sit on top and always receive their taps
            // (the MaxWidthBody-centred body fills the cross axis behind them).
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
                        _Header(title: l10n.signInForumTitle),
                        const SizedBox(height: 24),
                        _buildCard(l10n),
                      ],
                    ),
                  ),
                ),
                _buildTopControls(l10n),
              ],
            ),
          ),
        ],
      ),
    );
  }

  /// Top controls (Figma 627:2361): back chevron left, language toggle right.
  /// Forced LTR so the sides — and the chevron glyph — match the frame even
  /// under RTL.
  Widget _buildTopControls(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          IconButton(
            key: const ValueKey<String>('signInBack'),
            onPressed: _busy ? null : _back,
            icon: const SimfSvgIcon(_icBack, size: 24, color: Colors.white),
          ),
          const Spacer(),
          SizedBox(
            width: 40,
            height: 40,
            child: IconButton(
              key: const ValueKey<String>('signInLanguage'),
              tooltip: l10n.languageToggleLabel,
              onPressed: _busy ? null : _toggleLanguage,
              style: IconButton.styleFrom(
                backgroundColor: SimfTokens.navyDeep,
                shape: const RoundedRectangleBorder(
                  borderRadius: SimfTokens.borderRadiusSmall,
                ),
              ),
              icon: const SimfSvgIcon(
                _icGlobe,
                size: 24,
                color: SimfTokens.accent,
              ),
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
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: const BoxDecoration(
        color: SimfTokens.cardBeige,
        borderRadius: SimfTokens.borderRadiusSmall,
      ),
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
            _buildEmailField(l10n),
            const SizedBox(height: 16),
            _buildPasswordField(l10n),
            const SizedBox(height: 8),
            _buildRememberForgotRow(l10n),
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
            _buildSignUpPrompt(l10n),
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

  Widget _buildEmailField(AppL10n l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.emailLabel, color: SimfTokens.navy),
        const SizedBox(height: 8),
        TextFormField(
          controller: _email,
          keyboardType: TextInputType.emailAddress,
          textDirection: TextDirection.ltr,
          textAlign: TextAlign.left,
          maxLength: 50,
          enabled: !_busy,
          onChanged: (_) => setState(() {}),
          style: simfInputStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: _validateEmail,
          decoration: simfFieldDecoration(counterText: ''),
        ),
      ],
    );
  }

  Widget _buildPasswordField(AppL10n l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.passwordLabel, color: SimfTokens.navy),
        const SizedBox(height: 8),
        TextFormField(
          controller: _password,
          obscureText: _obscure,
          maxLength: 32,
          enabled: !_busy,
          onChanged: (_) => setState(() {}),
          onFieldSubmitted: (_) {
            if (_canSubmit) {
              unawaited(_submit());
            }
          },
          style: simfInputStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: _validatePassword,
          decoration: simfFieldDecoration(
            counterText: '',
            suffixIcon: IconButton(
              tooltip: _obscure
                  ? l10n.showPasswordTooltip
                  : l10n.hidePasswordTooltip,
              icon: SimfSvgIcon(
                _obscure ? _icEyeOff : _icEye,
                size: 16,
                color: SimfTokens.greyText,
              ),
              onPressed: () => setState(() => _obscure = !_obscure),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildRememberForgotRow(AppL10n l10n) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        Flexible(
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              SizedBox(
                width: 19,
                height: 19,
                child: Checkbox(
                  value: _rememberMe,
                  onChanged: _busy
                      ? null
                      : (v) => setState(() => _rememberMe = v ?? true),
                  activeColor: SimfTokens.accent,
                  side: const BorderSide(
                    color: SimfTokens.greyText,
                    width: 1.5,
                  ),
                  shape: const RoundedRectangleBorder(
                    borderRadius: SimfTokens.borderRadiusSmall,
                  ),
                  materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                ),
              ),
              const SizedBox(width: 5),
              Flexible(
                child: Text(
                  l10n.rememberMeLabel,
                  style: const TextStyle(
                    fontSize: 12,
                    color: SimfTokens.greyText,
                  ),
                ),
              ),
            ],
          ),
        ),
        Flexible(
          child: TextButton(
            onPressed: _busy
                ? null
                : () => context.goNamed(RouteNames.forgotPassword),
            style: authLinkButtonStyle(SimfTokens.greyText),
            child: Text(
              l10n.forgotPasswordLink,
              style: const TextStyle(fontSize: 12),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildSignUpPrompt(AppL10n l10n) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Flexible(
          child: Text(
            l10n.createAccountQuestion,
            style: const TextStyle(fontSize: 12, color: SimfTokens.greyText),
          ),
        ),
        const SizedBox(width: 6),
        Flexible(
          child: TextButton(
            onPressed: _busy
                ? null
                : () => context.pushNamed(RouteNames.signUpForm),
            style: authLinkButtonStyle(SimfTokens.linkNavy),
            child: Text(
              l10n.createAccountLink,
              style: const TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ),
      ],
    );
  }

}

/// Forum logo + name header (logo sits at the inline start — the right under
/// RTL, matching the Figma frame).
class _Header extends StatelessWidget {
  const _Header({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        const SimfLogo(size: 44),
        const SizedBox(width: 16),
        Flexible(
          child: Text(
            title,
            style: const TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w500,
              color: Colors.white,
            ),
          ),
        ),
      ],
    );
  }
}
