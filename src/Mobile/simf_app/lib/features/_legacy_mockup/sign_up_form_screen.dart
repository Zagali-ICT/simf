import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// LEGACY — the pre-redesign mockup Page 005 sign-up, parked here when the
/// KSA-Project design (frame 168:3454) replaced it at the official `/sign-up`
/// (D-370, app redesign programme Wave 2). Never routed; kept compiling until
/// the owner approves deleting the legacy directory at programme close
/// (§6 freeze rules).
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
/// Visuals follow the Mockup.html sign-up frame: a white anchor-splash top
/// (anchor mark on white) over a navy anchor-card holding the form, with white
/// fields and the gold create-account button.
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
      backgroundColor: SimfTokens.surface,
      body: SafeArea(
        bottom: false,
        child: SingleChildScrollView(
          child: ConstrainedBox(
            constraints: BoxConstraints(
              minHeight: MediaQuery.sizeOf(context).height -
                  MediaQuery.paddingOf(context).top,
            ),
            child: IntrinsicHeight(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  const _AnchorSplashTop(),
                  Expanded(
                    child: _SignUpCard(
                      formKey: _formKey,
                      email: _email,
                      password: _password,
                      confirm: _confirm,
                      obscure: _obscure,
                      obscureConfirm: _obscureConfirm,
                      busy: _busy,
                      error: _error,
                      l10n: l10n,
                      onToggleObscure: () =>
                          setState(() => _obscure = !_obscure),
                      onToggleObscureConfirm: () =>
                          setState(() => _obscureConfirm = !_obscureConfirm),
                      validateEmail: _validateEmail,
                      validatePassword: _validatePassword,
                      validateConfirm: _validateConfirm,
                      onSubmit: () => unawaited(_submit()),
                      onSignIn: () => context.goNamed(RouteNames.signIn),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// The white "anchor-splash" top — the brass-on-white anchor mark over the
/// white entry surface, matching the Mockup.html sign-up frame.
class _AnchorSplashTop extends StatelessWidget {
  const _AnchorSplashTop();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space8,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      child: Center(child: _AnchorMark()),
    );
  }
}

/// Interim brass anchor mark drawn on the white splash (final asset per
/// SIMF-VID-001). Uses [SimfTokens.ink] on white, like the mockup's `a-mark`.
class _AnchorMark extends StatelessWidget {
  const _AnchorMark();

  @override
  Widget build(BuildContext context) {
    return const SizedBox(
      width: 54,
      height: 54,
      child: Icon(
        Icons.anchor,
        size: 48,
        color: SimfTokens.ink,
      ),
    );
  }
}

/// The navy "anchor-card" holding the sign-up form — rounded top corners,
/// white fields, gold create-account button, and the "have account / sign in"
/// foot, matching the Mockup.html navy card.
class _SignUpCard extends StatelessWidget {
  const _SignUpCard({
    required this.formKey,
    required this.email,
    required this.password,
    required this.confirm,
    required this.obscure,
    required this.obscureConfirm,
    required this.busy,
    required this.error,
    required this.l10n,
    required this.onToggleObscure,
    required this.onToggleObscureConfirm,
    required this.validateEmail,
    required this.validatePassword,
    required this.validateConfirm,
    required this.onSubmit,
    required this.onSignIn,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController email;
  final TextEditingController password;
  final TextEditingController confirm;
  final bool obscure;
  final bool obscureConfirm;
  final bool busy;
  final String? error;
  final AppL10n l10n;
  final VoidCallback onToggleObscure;
  final VoidCallback onToggleObscureConfirm;
  final FormFieldValidator<String> validateEmail;
  final FormFieldValidator<String> validatePassword;
  final FormFieldValidator<String> validateConfirm;
  final VoidCallback onSubmit;
  final VoidCallback onSignIn;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      decoration: const BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(SimfTokens.radiusXl),
        ),
      ),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space6,
        SimfTokens.space6,
        SimfTokens.space6,
        SimfTokens.space6,
      ),
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              l10n.signUpTitle,
              style: const TextStyle(
                color: SimfTokens.surface,
                fontSize: SimfTokens.textXl,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: SimfTokens.space5),
            _FieldLabel(l10n.emailLabel),
            const SizedBox(height: SimfTokens.space2),
            TextFormField(
              controller: email,
              keyboardType: TextInputType.emailAddress,
              textDirection: TextDirection.ltr,
              enabled: !busy,
              maxLength: 50,
              style: _fieldTextStyle,
              autovalidateMode: AutovalidateMode.onUserInteraction,
              validator: validateEmail,
              decoration: _whiteFieldDecoration(counterText: ''),
            ),
            const SizedBox(height: SimfTokens.space3),
            _FieldLabel(l10n.passwordLabel),
            const SizedBox(height: SimfTokens.space2),
            TextFormField(
              controller: password,
              obscureText: obscure,
              enabled: !busy,
              maxLength: 32,
              style: _fieldTextStyle,
              autovalidateMode: AutovalidateMode.onUserInteraction,
              validator: validatePassword,
              decoration: _whiteFieldDecoration(
                counterText: '',
                suffixIcon: IconButton(
                  tooltip: obscure
                      ? l10n.showPasswordTooltip
                      : l10n.hidePasswordTooltip,
                  icon: Icon(
                    obscure
                        ? Icons.visibility_outlined
                        : Icons.visibility_off_outlined,
                    color: SimfTokens.inkMuted,
                  ),
                  onPressed: onToggleObscure,
                ),
              ),
            ),
            const SizedBox(height: SimfTokens.space3),
            _FieldLabel(l10n.confirmPasswordLabel),
            const SizedBox(height: SimfTokens.space2),
            TextFormField(
              controller: confirm,
              obscureText: obscureConfirm,
              enabled: !busy,
              maxLength: 32,
              style: _fieldTextStyle,
              autovalidateMode: AutovalidateMode.onUserInteraction,
              validator: validateConfirm,
              onFieldSubmitted: (_) {
                if (!busy) {
                  onSubmit();
                }
              },
              decoration: _whiteFieldDecoration(
                counterText: '',
                suffixIcon: IconButton(
                  tooltip: obscureConfirm
                      ? l10n.showPasswordTooltip
                      : l10n.hidePasswordTooltip,
                  icon: Icon(
                    obscureConfirm
                        ? Icons.visibility_outlined
                        : Icons.visibility_off_outlined,
                    color: SimfTokens.inkMuted,
                  ),
                  onPressed: onToggleObscureConfirm,
                ),
              ),
            ),
            if (error != null) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              Text(
                error!,
                style: const TextStyle(color: SimfTokens.danger),
              ),
            ],
            const SizedBox(height: SimfTokens.space5),
            FilledButton(
              onPressed: busy ? null : onSubmit,
              child: busy
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
                Text(
                  l10n.haveAccountQuestion,
                  style: const TextStyle(color: SimfTokens.txtSecondary),
                ),
                TextButton(
                  onPressed: busy ? null : onSignIn,
                  style: TextButton.styleFrom(
                    foregroundColor: SimfTokens.accent,
                  ),
                  child: Text(l10n.signInTitle),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  /// White text-field text colour for the navy card (ink on white fill),
  /// mirroring the mockup's `.mfield .in` colour.
  static const TextStyle _fieldTextStyle = TextStyle(
    color: SimfTokens.ink,
    fontSize: SimfTokens.textMd,
  );

  /// Per-field decoration overriding the navy theme to the mockup's white
  /// field (white fill, ink hint, accent focus ring).
  InputDecoration _whiteFieldDecoration({
    required String counterText,
    Widget? suffixIcon,
  }) {
    return InputDecoration(
      counterText: counterText,
      suffixIcon: suffixIcon,
      filled: true,
      fillColor: SimfTokens.surface,
      hintStyle: const TextStyle(color: SimfTokens.inkMuted),
      border: const OutlineInputBorder(
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
        borderSide: BorderSide.none,
      ),
      enabledBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
        borderSide: BorderSide.none,
      ),
      focusedBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
        borderSide: BorderSide(color: SimfTokens.accent),
      ),
    );
  }
}

/// A field caption above its input — the mockup's `.mfield label`
/// (txt-2 secondary on the navy card).
class _FieldLabel extends StatelessWidget {
  const _FieldLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: SimfTokens.txtSecondary,
        fontSize: SimfTokens.textSm,
        fontWeight: FontWeight.w500,
      ),
    );
  }
}
