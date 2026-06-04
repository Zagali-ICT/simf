import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Page 005 — إنشاء حساب · Sign up (Page_005 docs).
///
/// Sign-up **step 1**: the visitor supplies email + password + confirm-password.
/// On the generic **201** the app forwards to the email-OTP screen (Page 006)
/// carrying the address. The success is **enumeration-resistant** — identical
/// for a new and an already-registered email (D-198), so there is no "you
/// already have an account" branch. This screen does **not** sign the user in;
/// it only creates the under-review Visitor account and triggers the email code.
/// `confirmPassword` is checked locally for instant feedback **and** sent in the
/// body — the server re-validates `confirmPassword == password` (D-270).
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

  String? _validateEmail(String? value) {
    final email = value?.trim() ?? '';
    final valid = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(email);
    return valid ? null : AppL10n.of(context).invalidEmail;
  }

  /// Client-side mirror of the server policy (≥8 chars + a letter + a digit;
  /// SIMF-MOB-API-001) for instant feedback only — the server re-validates.
  String? _validatePassword(String? value) {
    final password = value ?? '';
    final valid = password.length >= 8 &&
        RegExp(r'[A-Za-z]').hasMatch(password) &&
        RegExp(r'\d').hasMatch(password);
    return valid ? null : AppL10n.of(context).passwordPolicyError;
  }

  String? _validateConfirm(String? value) {
    return value == _password.text
        ? null
        : AppL10n.of(context).passwordsDoNotMatch;
  }

  Future<void> _submit() async {
    final l10n = AppL10n.of(context);
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.signUpTitle)),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                const SizedBox(height: SimfTokens.space4),
                TextFormField(
                  controller: _email,
                  keyboardType: TextInputType.emailAddress,
                  textDirection: TextDirection.ltr,
                  enabled: !_busy,
                  maxLength: 50,
                  autovalidateMode: AutovalidateMode.onUserInteraction,
                  validator: _validateEmail,
                  decoration: InputDecoration(
                    labelText: l10n.emailLabel,
                    counterText: '',
                  ),
                ),
                const SizedBox(height: SimfTokens.space3),
                TextFormField(
                  controller: _password,
                  obscureText: _obscure,
                  enabled: !_busy,
                  maxLength: 32,
                  autovalidateMode: AutovalidateMode.onUserInteraction,
                  validator: _validatePassword,
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
                const SizedBox(height: SimfTokens.space3),
                TextFormField(
                  controller: _confirm,
                  obscureText: _obscureConfirm,
                  enabled: !_busy,
                  maxLength: 32,
                  autovalidateMode: AutovalidateMode.onUserInteraction,
                  validator: _validateConfirm,
                  onFieldSubmitted: (_) {
                    if (!_busy) {
                      unawaited(_submit());
                    }
                  },
                  decoration: InputDecoration(
                    labelText: l10n.confirmPasswordLabel,
                    counterText: '',
                    suffixIcon: IconButton(
                      tooltip: _obscureConfirm
                          ? l10n.showPasswordTooltip
                          : l10n.hidePasswordTooltip,
                      icon: Icon(
                        _obscureConfirm
                            ? Icons.visibility_outlined
                            : Icons.visibility_off_outlined,
                      ),
                      onPressed: () =>
                          setState(() => _obscureConfirm = !_obscureConfirm),
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
                  onPressed: _busy ? null : () => unawaited(_submit()),
                  child: _busy
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Text(l10n.signUpButton),
                ),
                const SizedBox(height: SimfTokens.space4),
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Text(l10n.haveAccountQuestion),
                    TextButton(
                      onPressed: _busy
                          ? null
                          : () => context.goNamed(RouteNames.signIn),
                      child: Text(l10n.signInTitle),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
