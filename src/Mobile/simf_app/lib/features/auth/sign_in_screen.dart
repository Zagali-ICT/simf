import 'dart:async';

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
import 'post_auth_route.dart';

// Screen-local shorthands for the KSA-Project design tokens. The palette was
// promoted into SimfTokens in Phase 0 of the app redesign (D-359), closing the
// D-358 note that kept it screen-local while the design was a preview.
const Color _bgNavy = SimfTokens.navySurface;
const Color _card = SimfTokens.cardBeige;
const Color _fieldBorder = SimfTokens.beigeBorder;
const Color _gold = SimfTokens.accent;
const Color _goldText = SimfTokens.goldSoft;
const Color _headline = SimfTokens.headlineInk;
const Color _grey = SimfTokens.greyText;
const Color _linkNavy = SimfTokens.linkNavy;
const Color _inputText = SimfTokens.inputInk;
const Color _danger = SimfTokens.danger;
const Color _sweepTint = SimfTokens.surfaceTint;

// The design's card / field / button corner radius.
const BorderRadius _radius4 =
    BorderRadius.all(Radius.circular(SimfTokens.radiusSmall));

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
  bool _rememberMe = true;
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
      if (_rememberMe) {
        await ref
            .read(simfPrefsStorageProvider)
            .setString(StorageKeys.lastEmail, email);
      }
      if (!mounted) {
        return;
      }
      final state = ref.read(authControllerProvider);
      if (state is AuthStateAwaitingOtp) {
        context.goNamed(RouteNames.verifyOtp);
      } else if (state is AuthStateSignedIn) {
        _onSignedIn();
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

  void _onSignedIn() {
    // Best-effort enrolment for future Face-ID sign-in; uses the
    // container-level controller so it survives this screen's disposal.
    unawaited(
      _maybeEnrolBiometric(ref.read(authControllerProvider.notifier)),
    );
    // D-374 — the profileComplete flag rides the sign-in hydration, so the
    // shared post-auth rule routes directly (the old getMyProfile probe is
    // gone); the same rule runs after the 2FA OTP step and the splash restore.
    routeAfterAuth(context, ref);
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

  /// The design shows the Face-ID button unconditionally, so unlike Page 003
  /// the button is always rendered; an unsupported device / missing plugin
  /// falls through silently and the password path stays available.
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
        routeAfterAuth(context, ref);
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
      backgroundColor: _bgNavy,
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
                  color: _sweepTint,
                  borderRadius: BorderRadius.circular(40),
                ),
              ),
            ),
          ),
          SafeArea(
            child: Stack(
              children: <Widget>[
                // Top controls (Figma 627:2361): back chevron left, language
                // toggle right. Forced LTR so the sides — and the chevron
                // glyph — match the frame even under RTL.
                Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 8,
                  ),
                  child: Row(
                    textDirection: TextDirection.ltr,
                    children: <Widget>[
                      IconButton(
                        onPressed: _busy ? null : _back,
                        icon: const Icon(
                          Icons.arrow_back_ios_new,
                          color: Colors.white,
                          size: 20,
                          textDirection: TextDirection.ltr,
                        ),
                      ),
                      const Spacer(),
                      SizedBox(
                        width: 40,
                        height: 40,
                        child: IconButton(
                          tooltip: l10n.languageToggleLabel,
                          onPressed: _busy ? null : _toggleLanguage,
                          style: IconButton.styleFrom(
                            backgroundColor: SimfTokens.navyDeep,
                            shape: const RoundedRectangleBorder(
                              borderRadius: _radius4,
                            ),
                          ),
                          icon: const Icon(
                            Icons.language,
                            color: SimfTokens.accent,
                            size: 24,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                Center(
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 56,
                    ),
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 400),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: <Widget>[
                          _Header(title: l10n.signInForumTitle),
                          const SizedBox(height: 40),
                          _buildCard(l10n),
                        ],
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCard(AppL10n l10n) {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: const BoxDecoration(
        color: _card,
        borderRadius: _radius4,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.signInTitle,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w600,
              color: _headline,
            ),
          ),
          const SizedBox(height: 24),
          _FieldLabel(text: l10n.emailLabel),
          const SizedBox(height: 8),
          TextField(
            controller: _email,
            keyboardType: TextInputType.emailAddress,
            textDirection: TextDirection.ltr,
            textAlign: TextAlign.left,
            maxLength: 50,
            enabled: !_busy,
            onChanged: (_) => setState(() {}),
            style: _inputStyle,
            decoration: _inputDecoration(),
          ),
          const SizedBox(height: 16),
          _FieldLabel(text: l10n.passwordLabel),
          const SizedBox(height: 8),
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
            style: _inputStyle,
            decoration: _inputDecoration(
              suffixIcon: IconButton(
                tooltip: _obscure
                    ? l10n.showPasswordTooltip
                    : l10n.hidePasswordTooltip,
                icon: Icon(
                  _obscure
                      ? Icons.visibility_off_outlined
                      : Icons.visibility_outlined,
                  size: 18,
                  color: _grey,
                ),
                onPressed: () => setState(() => _obscure = !_obscure),
              ),
            ),
          ),
          const SizedBox(height: 8),
          Row(
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
                        activeColor: _gold,
                        side: const BorderSide(color: _grey, width: 1.5),
                        shape: const RoundedRectangleBorder(
                          borderRadius: _radius4,
                        ),
                        materialTapTargetSize:
                            MaterialTapTargetSize.shrinkWrap,
                      ),
                    ),
                    const SizedBox(width: 5),
                    Flexible(
                      child: Text(
                        l10n.rememberMeLabel,
                        style: const TextStyle(fontSize: 12, color: _grey),
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
                  style: _linkButtonStyle(_grey),
                  child: Text(
                    l10n.forgotPasswordLink,
                    style: const TextStyle(fontSize: 12),
                  ),
                ),
              ),
            ],
          ),
          if (_error != null) ...<Widget>[
            const SizedBox(height: 12),
            Text(
              _error!,
              style: const TextStyle(color: _danger, fontSize: 12),
            ),
          ],
          const SizedBox(height: 24),
          FilledButton(
            onPressed: _canSubmit ? () => unawaited(_submit()) : null,
            style: FilledButton.styleFrom(
              backgroundColor: _gold,
              disabledBackgroundColor: const Color(0x80C9A84C),
              minimumSize: const Size.fromHeight(48),
              shape: const RoundedRectangleBorder(borderRadius: _radius4),
            ),
            child: _busy
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : Text(
                    l10n.signInButton,
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Colors.white,
                    ),
                  ),
          ),
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Flexible(
                child: Text(
                  l10n.createAccountQuestion,
                  style: const TextStyle(fontSize: 12, color: _grey),
                ),
              ),
              const SizedBox(width: 6),
              Flexible(
                child: TextButton(
                  onPressed: _busy
                      ? null
                      : () => context.pushNamed(RouteNames.signUpForm),
                  style: _linkButtonStyle(_linkNavy),
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
          ),
          const SizedBox(height: 24),
          Row(
            children: <Widget>[
              const Expanded(child: Divider(color: _fieldBorder, height: 1)),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Text(
                  l10n.orDividerLabel,
                  style: const TextStyle(fontSize: 12, color: _grey),
                ),
              ),
              const Expanded(child: Divider(color: _fieldBorder, height: 1)),
            ],
          ),
          const SizedBox(height: 24),
          OutlinedButton(
            onPressed: _busy ? null : () => unawaited(_biometricSignIn()),
            style: OutlinedButton.styleFrom(
              side: const BorderSide(color: _fieldBorder),
              minimumSize: const Size.fromHeight(48),
              shape: const RoundedRectangleBorder(borderRadius: _radius4),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Flexible(
                  child: Text(
                    l10n.faceIdSignInButton,
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: _goldText,
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                const Icon(Icons.face, size: 20, color: _goldText),
              ],
            ),
          ),
          // Guest entry (Figma 627:2390, D-363) — the underlined design-native
          // link; the app's only path into guest mode (Page_012).
          SizedBox(
            height: 48,
            child: Center(
              child: TextButton(
                onPressed: _busy
                    ? null
                    : () => context.pushNamed(RouteNames.guestMode),
                style: _linkButtonStyle(_grey),
                child: Text(
                  l10n.guestSignInLink,
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    decoration: TextDecoration.underline,
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  static const TextStyle _inputStyle = TextStyle(
    fontSize: 14,
    fontWeight: FontWeight.w500,
    color: _inputText,
  );

  static const OutlineInputBorder _restingBorder = OutlineInputBorder(
    borderRadius: _radius4,
    borderSide: BorderSide(color: _fieldBorder),
  );
  static const OutlineInputBorder _focusedBorder = OutlineInputBorder(
    borderRadius: _radius4,
    borderSide: BorderSide(color: _gold),
  );

  static ButtonStyle _linkButtonStyle(Color color) => TextButton.styleFrom(
        padding: EdgeInsets.zero,
        minimumSize: Size.zero,
        tapTargetSize: MaterialTapTargetSize.shrinkWrap,
        foregroundColor: color,
      );

  InputDecoration _inputDecoration({Widget? suffixIcon}) {
    return InputDecoration(
      counterText: '',
      isDense: true,
      filled: false,
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 15),
      enabledBorder: _restingBorder,
      focusedBorder: _focusedBorder,
      disabledBorder: _restingBorder,
      suffixIcon: suffixIcon,
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

/// A small field label aligned to the inline start (right under RTL).
class _FieldLabel extends StatelessWidget {
  const _FieldLabel({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Text(
        text,
        style: const TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w500,
          color: _grey,
        ),
      ),
    );
  }
}
