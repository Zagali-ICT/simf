import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/localization/locale_controller.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../core/errors/api_error_l10n.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/validation/email_validation.dart';
import '../../core/validation/password_validation.dart';
import '../../core/widgets/simf_auth_sweep.dart';
import 'widgets/account_auth_prompt.dart';
import 'widgets/account_card.dart';
import 'widgets/account_form_field.dart';
import 'widgets/account_header.dart';
import 'widgets/account_terms_checkbox.dart';
import 'widgets/account_top_controls.dart';
import 'widgets/auth_chrome.dart';

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
/// Clean-code (D-655): the screen composes the shared account widgets
/// ([AccountHeader], [AccountTopControls], [AccountCard], [AccountEmailField],
/// [AccountPasswordField], [AccountAuthPrompt]) — the same set as the sign-in
/// sister card — instead of local `_build*` copies and hardcoded eye glyphs; the
/// decorative sweep is the shared [SimfAuthSweep]; the API error is localized
/// through [ApiFailureL10n]. Locked by the 168:3454 golden.
///
/// D-719 (owner batch 2026-07-09): a **mandatory** "accept the terms" checkbox
/// ([AccountTermsCheckbox]) gates the submit — registration requires an explicit
/// accept, not a link. This is an owner-mandated addition with no Figma frame of
/// its own (168:3454 predates it); the golden is re-locked with the box present.
/// Consent stays client-side (D8) — no wire-contract change.
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
  bool _acceptedTerms = false;
  bool _showTermsError = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  String? _validateEmail(String? value) {
    final email = value?.trim() ?? '';
    return isValidEmail(email) ? null : AppL10n.of(context).invalidEmail;
  }

  /// Client-side mirror of the server policy (≥8 chars + a letter + a digit;
  /// SIMF-MOB-API-001) for instant feedback only — the server re-validates.
  String? _validatePassword(String? value) {
    final password = value ?? '';
    return isValidPassword(password)
        ? null
        : AppL10n.of(context).passwordPolicyError;
  }

  String? _validateConfirm(String? value) {
    return value == _password.text
        ? null
        : AppL10n.of(context).passwordsDoNotMatch;
  }

  /// Checking the box clears the "must accept" error; unchecking leaves it
  /// hidden until the next submit attempt re-triggers it.
  void _setAcceptedTerms(bool accepted) {
    setState(() {
      _acceptedTerms = accepted;
      if (accepted) {
        _showTermsError = false;
      }
    });
  }

  /// Opens the terms screen (Page 009) in consent mode; a موافق there returns
  /// true and auto-checks the box, so the visitor never has to tick it twice.
  Future<void> _openTerms() async {
    final accepted = await context.pushNamed<bool>(
      RouteNames.terms,
      queryParameters: <String, String>{'consent': '1'},
    );
    if (!mounted) {
      return;
    }
    if (accepted ?? false) {
      _setAcceptedTerms(true);
    }
  }

  Future<void> _submit() async {
    final formValid = _formKey.currentState?.validate() ?? false;
    // Surface the terms error alongside any field errors, not one gate at a time.
    if (!_acceptedTerms) {
      setState(() => _showTermsError = true);
    }
    if (!formValid || !_acceptedTerms) {
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
        _error = failure.source.localizedMessage(l10n);
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
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          const SimfAuthSweep(),
          SafeArea(
            // The scroll body paints first; the top controls are the last Stack
            // child so they sit on top and always receive their taps (the
            // MaxWidthBody-centred body fills the cross axis behind them).
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
                        const SizedBox(height: 40),
                        _buildCard(l10n),
                      ],
                    ),
                  ),
                ),
                AccountTopControls(
                  onBack: _back,
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
    return AccountCard(
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
                color: SimfTokens.headlineInk,
              ),
            ),
            const SizedBox(height: 24),
            AccountEmailField(
              controller: _email,
              label: l10n.emailLabel,
              enabled: !_busy,
              validator: _validateEmail,
            ),
            const SizedBox(height: 16),
            AccountPasswordField(
              controller: _password,
              label: l10n.passwordLabel,
              obscure: _obscure,
              onToggleObscure: () => setState(() => _obscure = !_obscure),
              enabled: !_busy,
              validator: _validatePassword,
            ),
            const SizedBox(height: 16),
            AccountPasswordField(
              controller: _confirm,
              label: l10n.confirmPasswordLabel,
              obscure: _obscureConfirm,
              onToggleObscure: () =>
                  setState(() => _obscureConfirm = !_obscureConfirm),
              enabled: !_busy,
              validator: _validateConfirm,
              onSubmitted: (_) => unawaited(_submit()),
            ),
            const SizedBox(height: 16),
            AccountTermsCheckbox(
              accepted: _acceptedTerms,
              onChanged: _setAcceptedTerms,
              onOpenTerms: () => unawaited(_openTerms()),
              enabled: !_busy,
              showError: _showTermsError,
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
              label: l10n.signUpButton,
              busy: _busy,
              onPressed: _busy ? null : () => unawaited(_submit()),
            ),
            const SizedBox(height: 8),
            AccountAuthPrompt(
              question: l10n.haveAccountQuestion,
              linkLabel: l10n.signInTitle,
              onTap: () => context.goNamed(RouteNames.signIn),
              enabled: !_busy,
            ),
          ],
        ),
      ),
    );
  }
}
