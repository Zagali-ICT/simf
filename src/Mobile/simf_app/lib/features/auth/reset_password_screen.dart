import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Page 003 — Reset password (Logic L-6). Collects the emailed OTP + a new
/// password and calls `POST /app/auth/reset-password`, then returns to sign-in
/// with the email pre-filled. The email is carried in from the forgot screen.
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
      await ref
          .read(simfPrefsStorageProvider)
          .setString(StorageKeys.lastEmail, widget.email);
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.resetPasswordTitle)),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              const SizedBox(height: SimfTokens.space5),
              Text(
                l10n.resetPasswordSent,
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: SimfTokens.space5),
              TextField(
                controller: _code,
                keyboardType: TextInputType.number,
                textDirection: TextDirection.ltr,
                maxLength: 6,
                enabled: !_busy,
                inputFormatters: <TextInputFormatter>[
                  FilteringTextInputFormatter.digitsOnly,
                ],
                onChanged: (_) => setState(() {}),
                decoration: InputDecoration(
                  labelText: l10n.otpLabel,
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
                decoration: InputDecoration(
                  labelText: l10n.newPasswordLabel,
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
                decoration: InputDecoration(
                  labelText: l10n.confirmPasswordLabel,
                  counterText: '',
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
                    : Text(l10n.resetPasswordButton),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
