import 'dart:async';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import 'widgets/auth_chrome.dart';

/// Page 003 — Reset password (Logic L-6), rebuilt on the KSA entry chrome
/// (D-374: "same card and color and style" as sign-in/sign-up). Collects the
/// emailed OTP + a new password and calls `POST /app/auth/reset-password`,
/// then returns to sign-in with the email pre-filled. The email is carried
/// in from the forgot screen.
class ResetPasswordScreen extends ConsumerStatefulWidget {
  const ResetPasswordScreen({required this.email, super.key});

  final String email;

  @override
  ConsumerState<ResetPasswordScreen> createState() =>
      _ResetPasswordScreenState();
}

class _ResetPasswordScreenState extends ConsumerState<ResetPasswordScreen> {
  final TextEditingController _code = TextEditingController();
  final TextEditingController _password = TextEditingController();
  final TextEditingController _confirm = TextEditingController();
  bool _obscure = true;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _code.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  bool get _canSubmit =>
      _code.text.trim().isNotEmpty &&
      _password.text.isNotEmpty &&
      _confirm.text.isNotEmpty &&
      !_busy;

  Future<void> _submit() async {
    final l10n = AppL10n.of(context);
    if (_password.text != _confirm.text) {
      setState(() => _error = l10n.passwordsDoNotMatch);
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(authRepositoryProvider).resetPassword(
            email: widget.email,
            code: _code.text.trim(),
            newPassword: _password.text,
            confirmPassword: _confirm.text,
          );
      // Pre-fill the just-reset email on the sign-in screen — but NOT on the
      // web PoC, where prefs are shared-browser localStorage and the address
      // would surface to the next kiosk user (D-384 — web = PoC exception;
      // matches the sign-in remember-me-OFF-on-web default).
      if (!kIsWeb) {
        await ref
            .read(simfPrefsStorageProvider)
            .setString(StorageKeys.lastEmail, widget.email);
      }
      if (!mounted) {
        return;
      }
      context.goNamed(RouteNames.signIn);
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
    context.goNamed(RouteNames.forgotPassword);
  }

  Widget _passwordToggle(AppL10n l10n) {
    return IconButton(
      tooltip: _obscure ? l10n.showPasswordTooltip : l10n.hidePasswordTooltip,
      icon: Icon(
        _obscure ? Icons.visibility_off_outlined : Icons.visibility_outlined,
        size: 18,
        color: SimfTokens.greyText,
      ),
      onPressed: () => setState(() => _obscure = !_obscure),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return KsaAuthScaffold(
      busy: _busy,
      onBack: _back,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.resetPasswordTitle,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w600,
              color: SimfTokens.headlineInk,
            ),
          ),
          const SizedBox(height: 12),
          Text(
            l10n.resetPasswordSent,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 12,
              color: SimfTokens.greyText,
            ),
          ),
          const SizedBox(height: 24),
          KsaFieldLabel(text: l10n.otpLabel),
          const SizedBox(height: 8),
          TextField(
            controller: _code,
            keyboardType: TextInputType.number,
            textDirection: TextDirection.ltr,
            textAlign: TextAlign.left,
            maxLength: 6,
            enabled: !_busy,
            inputFormatters: <TextInputFormatter>[
              FilteringTextInputFormatter.digitsOnly,
            ],
            onChanged: (_) => setState(() {}),
            style: ksaInputTextStyle,
            decoration: ksaInputDecoration(),
          ),
          const SizedBox(height: 16),
          KsaFieldLabel(text: l10n.newPasswordLabel),
          const SizedBox(height: 8),
          TextField(
            controller: _password,
            obscureText: _obscure,
            maxLength: 32,
            enabled: !_busy,
            onChanged: (_) => setState(() {}),
            style: ksaInputTextStyle,
            decoration: ksaInputDecoration(suffixIcon: _passwordToggle(l10n)),
          ),
          const SizedBox(height: 16),
          KsaFieldLabel(text: l10n.confirmPasswordLabel),
          const SizedBox(height: 8),
          TextField(
            controller: _confirm,
            obscureText: _obscure,
            maxLength: 32,
            enabled: !_busy,
            onChanged: (_) => setState(() {}),
            onSubmitted: (_) {
              if (_canSubmit) {
                unawaited(_submit());
              }
            },
            style: ksaInputTextStyle,
            decoration: ksaInputDecoration(),
          ),
          if (_error != null) ...<Widget>[
            const SizedBox(height: 12),
            Text(
              _error!,
              style: const TextStyle(color: SimfTokens.danger, fontSize: 12),
            ),
          ],
          const SizedBox(height: 24),
          KsaSubmitButton(
            label: l10n.resetPasswordButton,
            busy: _busy,
            onPressed: _canSubmit ? () => unawaited(_submit()) : null,
          ),
        ],
      ),
    );
  }
}
