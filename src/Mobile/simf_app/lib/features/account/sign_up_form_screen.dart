import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/localization/locale_controller.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_logo.dart';
import '../../core/widgets/simf_field_label.dart';
import '../../core/widgets/simf_field_style.dart';

// Screen-local shorthands for the KSA-Project design tokens (Phase 0, D-359).
const Color _bgNavy = SimfTokens.navySurface;
const Color _card = SimfTokens.cardBeige;
const Color _gold = SimfTokens.accent;
const Color _headline = SimfTokens.headlineInk;
const Color _grey = SimfTokens.greyText;
const Color _linkNavy = SimfTokens.linkNavy;
const Color _danger = SimfTokens.danger;
const Color _sweepTint = SimfTokens.surfaceTint;

// The design's card / field / button corner radius.
/// Page 005 — إنشاء حساب · Sign up. The KSA-Project Figma design (node
/// 168:3454), replacing the mockup screen at the official `/sign-up`
/// (D-370, app redesign programme Wave 2); the previous screen is parked
/// in `_legacy_mockup/`.
///
/// Sign-up **step 1**: the visitor supplies email + password + confirm-password.
/// On the generic **201** the app forwards to the email-OTP screen (Page 006)
/// carrying the address. The success is **enumeration-resistant** — identical
/// for a new and an already-registered email (D-198), so there is no "you
/// already have an account" branch. This screen does **not** sign the user in;
/// it only creates the under-review Visitor account and triggers the email code.
/// `confirmPassword` is checked locally for instant feedback **and** sent in the
/// body — the server re-validates `confirmPassword == password` (D-270).
///
/// Visuals follow the frame: the navy entry surface with the diagonal sweep,
/// the back/globe top controls (matching the D-363 sign-in header), the forum
/// logo header, and the beige card holding the three outlined fields, the gold
/// create-account button, and the "have account? sign in" foot.
class SignUpFormScreen extends ConsumerStatefulWidget {
  const SignUpFormScreen({super.key});

  @override
  ConsumerState<SignUpFormScreen> createState() => _SignUpFormScreenState();
}

class _SignUpFormScreenState extends ConsumerState<SignUpFormScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _email = TextEditingController();
  final TextEditingController _password = TextEditingController();
  final TextEditingController _confirm = TextEditingController();
  bool _obscure = true;
  bool _obscureConfirm = true;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  static final RegExp _emailPattern = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
  static final RegExp _letterPattern = RegExp(r'[A-Za-z]');
  static final RegExp _digitPattern = RegExp(r'\d');

  String? _validateEmail(String? value) {
    final email = value?.trim() ?? '';
    final valid = _emailPattern.hasMatch(email);
    return valid ? null : AppL10n.of(context).invalidEmail;
  }

  /// Client-side mirror of the server policy (≥8 chars + a letter + a digit;
  /// SIMF-MOB-API-001) for instant feedback only — the server re-validates.
  String? _validatePassword(String? value) {
    final password = value ?? '';
    final valid = password.length >= 8 &&
        _letterPattern.hasMatch(password) &&
        _digitPattern.hasMatch(password);
    return valid ? null : AppL10n.of(context).passwordPolicyError;
  }

  String? _validateConfirm(String? value) {
    return value == _password.text
        ? null
        : AppL10n.of(context).passwordsDoNotMatch;
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    final l10n = AppL10n.of(context);
    final email = _email.text.trim().toLowerCase();
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(authControllerProvider.notifier).signUp(
            email: email,
            password: _password.text,
            confirmPassword: _confirm.text,
          );
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.signUpCheckEmail)));
      // Generic 201 for both new and already-registered (D-198): always the
      // email-OTP step (Page 006), carrying the address it was sent to.
      unawaited(
        context.pushNamed(
          RouteNames.emailOtp,
          queryParameters: <String, String>{'email': email},
        ),
      );
    } on AuthFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = failure is NetworkUnavailable
            ? l10n.networkErrorBody
            : failure.source.message;
      });
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
    context.goNamed(RouteNames.signIn);
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
          // Decorative diagonal sweep behind the header (Figma node 168:3534,
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
                // Top controls (Figma 627:2407): back chevron left, language
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
                              borderRadius: SimfTokens.borderRadiusSmall,
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
        borderRadius: SimfTokens.borderRadiusSmall,
      ),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              l10n.signUpTitle,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 24,
                fontWeight: FontWeight.w600,
                color: _headline,
              ),
            ),
            const SizedBox(height: 24),
            SimfFieldLabel(l10n.emailLabel),
            const SizedBox(height: 8),
            TextFormField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              textDirection: TextDirection.ltr,
              textAlign: TextAlign.left,
              maxLength: 50,
              enabled: !_busy,
              style: simfInputStyle,
              autovalidateMode: AutovalidateMode.onUserInteraction,
              validator: _validateEmail,
              decoration: simfFieldDecoration(counterText: ''),
            ),
            const SizedBox(height: 16),
            SimfFieldLabel(l10n.passwordLabel),
            const SizedBox(height: 8),
            TextFormField(
              controller: _password,
              obscureText: _obscure,
              maxLength: 32,
              enabled: !_busy,
              style: simfInputStyle,
              autovalidateMode: AutovalidateMode.onUserInteraction,
              validator: _validatePassword,
              decoration: simfFieldDecoration(
                counterText: '',
                suffixIcon: _visibilityToggle(
                  l10n,
                  obscured: _obscure,
                  onPressed: () => setState(() => _obscure = !_obscure),
                ),
              ),
            ),
            const SizedBox(height: 16),
            SimfFieldLabel(l10n.confirmPasswordLabel),
            const SizedBox(height: 8),
            TextFormField(
              controller: _confirm,
              obscureText: _obscureConfirm,
              maxLength: 32,
              enabled: !_busy,
              style: simfInputStyle,
              autovalidateMode: AutovalidateMode.onUserInteraction,
              validator: _validateConfirm,
              onFieldSubmitted: (_) => unawaited(_submit()),
              decoration: simfFieldDecoration(
                counterText: '',
                suffixIcon: _visibilityToggle(
                  l10n,
                  obscured: _obscureConfirm,
                  onPressed: () =>
                      setState(() => _obscureConfirm = !_obscureConfirm),
                ),
              ),
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
              onPressed: _busy ? null : () => unawaited(_submit()),
              style: FilledButton.styleFrom(
                backgroundColor: _gold,
                disabledBackgroundColor: const Color(0x80C9A84C),
                minimumSize: const Size.fromHeight(48),
                shape: const RoundedRectangleBorder(borderRadius: SimfTokens.borderRadiusSmall),
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
                      l10n.signUpButton,
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
                    l10n.haveAccountQuestion,
                    style: const TextStyle(fontSize: 12, color: _grey),
                  ),
                ),
                const SizedBox(width: 6),
                Flexible(
                  child: TextButton(
                    onPressed: _busy
                        ? null
                        : () => context.goNamed(RouteNames.signIn),
                    style: TextButton.styleFrom(
                      padding: EdgeInsets.zero,
                      minimumSize: Size.zero,
                      tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                      foregroundColor: _linkNavy,
                    ),
                    child: Text(
                      l10n.signInTitle,
                      style: const TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  /// The eye-off show/hide suffix shared by the two password fields.
  IconButton _visibilityToggle(
    AppL10n l10n, {
    required bool obscured,
    required VoidCallback onPressed,
  }) {
    return IconButton(
      tooltip: obscured ? l10n.showPasswordTooltip : l10n.hidePasswordTooltip,
      icon: Icon(
        obscured ? Icons.visibility_off_outlined : Icons.visibility_outlined,
        size: 18,
        color: _grey,
      ),
      onPressed: onPressed,
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

