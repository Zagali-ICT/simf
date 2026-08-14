import 'dart:async';

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
import 'package:simf_app/core/validation/email_validation.dart';
import 'package:simf_app/core/validation/password_validation.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/widgets/account_auth_prompt.dart';
import 'package:simf_app/features/account/widgets/account_card.dart';
import 'package:simf_app/features/account/widgets/account_form_field.dart';
import 'package:simf_app/features/account/widgets/account_header.dart';
import 'package:simf_app/features/account/widgets/account_terms_checkbox.dart';
import 'package:simf_app/features/account/widgets/account_top_controls.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Page 005 — إنشاء حساب · Sign up. The KSA-Project Figma design (node
/// 168:3454), replacing the mockup screen at the official `/sign-up` (D-370,
/// app redesign programme Wave 2); the previous screen is parked in
/// `_legacy_mockup/`.
///
/// Sign-up **step 1**: the visitor supplies email + password +
/// confirm-password. On the generic **201** the app forwards to the email-OTP
/// screen (Page 006) carrying the address. The success is
/// **enumeration-resistant** — identical for a new and an already-registered
/// email (D-198), so there is no "you already have an account" branch. This
/// screen does **not** sign the user in; it only creates the under-review
/// Visitor account and triggers the email code. `confirmPassword` is checked
/// locally for instant feedback **and** sent in the body — the server
/// re-validates `confirmPassword == password` (D-270).
///
/// Clean-code (D-655): the screen composes the shared account widgets
/// ([AccountHeader], [AccountTopControls], [AccountCard], [AccountEmailField],
/// [AccountPasswordField], [AccountAuthPrompt]) — the same set as the sign-in
/// sister card — instead of local `_build*` copies and hardcoded eye glyphs;
/// the decorative sweep is the shared [SimfAuthSweep]; the API error is
/// localized through [ApiFailureL10n]. Locked by the 168:3454 golden.
///
/// D-719 (owner batch 2026-07-09): a **mandatory** "accept the terms" checkbox
/// ([AccountTermsCheckbox]) gates the submit — registration requires an
/// explicit accept, not a link. This is an owner-mandated addition with no
/// Figma frame of its own (168:3454 predates it); the golden is re-locked with
/// the box present. Consent stays client-side (D8) — no wire-contract change.
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
  bool _passwordTouched = false;
  List<PasswordRequirement> _passwordUnmet = <PasswordRequirement>[];
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

  void _onPasswordChanged(String value) {
    setState(() {
      _passwordTouched = true;
      _passwordUnmet = unmetPasswordRequirements(value);
    });
  }

  Widget _buildPasswordErrors(AppL10n l10n) {
    if (!_passwordTouched || _passwordUnmet.isEmpty) {
      return const SizedBox.shrink();
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const SizedBox(height: SimfTokens.space2),
        for (final PasswordRequirement req in _passwordUnmet)
          Padding(
            padding: const EdgeInsets.only(top: 2),
            child: Text(
              _passwordRequirementMessage(req, l10n),
              style: SimfTokens.labelDangerSm,
            ),
          ),
      ],
    );
  }

  String _passwordRequirementMessage(
    PasswordRequirement req,
    AppL10n l10n,
  ) {
    switch (req) {
      case PasswordRequirement.length:
        return l10n.passwordLength;
      case PasswordRequirement.uppercase:
        return l10n.passwordUppercase;
      case PasswordRequirement.lowercase:
        return l10n.passwordLowercase;
      case PasswordRequirement.digit:
        return l10n.passwordDigit;
      case PasswordRequirement.special:
        return l10n.passwordSpecial;
    }
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
    if (!_acceptedTerms) {
      setState(() => _showTermsError = true);
    }
    final passwordValid = unmetPasswordRequirements(_password.text).isEmpty;
    if (!formValid || !passwordValid || !_acceptedTerms) {
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
      // Commit the FINAL submitted email/password to the OS autofill store now,
      // so a corrected address replaces any first-typed guess the OS grabbed
      // (owner: login kept offering the mistyped sign-up email).
      TextInput.finishAutofillContext();
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
            // The scroll body paints first; the top controls are the last Stack
            // child so they sit on top and always receive their taps (the
            // MaxWidthBody-centred body fills the cross axis behind them).
            child: Stack(
              children: <Widget>[
                SingleChildScrollView(
                  padding: const EdgeInsets.symmetric(
                    horizontal: SimfTokens.space4,
                    vertical: 56,
                  ),
                  child: MaxWidthBody(
                    maxWidth: SimfTokens.signUpFormScreenMaxWidth,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        AccountHeader(title: l10n.signInForumTitle),
                        const SizedBox(height: SimfTokens.space10),
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
      // AutofillGroup + the fields' hints let the OS treat this as one
      // new-account form and, on a successful submit, save the FINAL
      // email/password (see _submit's finishAutofillContext) rather than a
      // first-typed guess.
      child: AutofillGroup(
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Text(
                l10n.signUpTitle,
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
                validator: _validateEmail,
                autofillHints: const <String>[
                  AutofillHints.newUsername,
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
                onChanged: _onPasswordChanged,
                autofillHints: const <String>[AutofillHints.newPassword],
              ),
              _buildPasswordErrors(l10n),
              const SizedBox(height: SimfTokens.space4),
              AccountPasswordField(
                controller: _confirm,
                label: l10n.confirmPasswordLabel,
                obscure: _obscureConfirm,
                onToggleObscure: () =>
                    setState(() => _obscureConfirm = !_obscureConfirm),
                enabled: !_busy,
                validator: _validateConfirm,
                onSubmitted: (_) => unawaited(_submit()),
                autofillHints: const <String>[AutofillHints.newPassword],
              ),
              const SizedBox(height: SimfTokens.space4),
              AccountTermsCheckbox(
                accepted: _acceptedTerms,
                onChanged: _setAcceptedTerms,
                onOpenTerms: () => unawaited(_openTerms()),
                enabled: !_busy,
                showError: _showTermsError,
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
                label: l10n.signUpButton,
                busy: _busy,
                onPressed: _busy ? null : () => unawaited(_submit()),
              ),
              const SizedBox(height: SimfTokens.space2),
              AccountAuthPrompt(
                question: l10n.haveAccountQuestion,
                linkLabel: l10n.signInTitle,
                onTap: () => context.goNamed(RouteNames.signIn),
                enabled: !_busy,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
