import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Page 003 — Forgot password (Logic L-6). Emails a reset OTP, then routes to
/// the reset screen with the email carried forward. The request is
/// enumeration-resistant on the server (always success-shaped), so the app
/// always proceeds to the reset step.
class ForgotPasswordScreen extends ConsumerStatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  ConsumerState<ForgotPasswordScreen> createState() =>
      _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> {
  final TextEditingController _email = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  bool get _canSubmit => _email.text.trim().isNotEmpty && !_busy;

  Future<void> _submit() async {
    final email = _email.text.trim();
    if (email.isEmpty) {
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(authRepositoryProvider).forgotPassword(email: email);
      if (!mounted) {
        return;
      }
      context.goNamed(
        RouteNames.resetPassword,
        queryParameters: <String, String>{'email': email},
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
      appBar: AppBar(title: Text(l10n.forgotPasswordTitle)),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              const SizedBox(height: SimfTokens.space5),
              Text(
                l10n.forgotPasswordBody,
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: SimfTokens.space5),
              TextField(
                controller: _email,
                keyboardType: TextInputType.emailAddress,
                textDirection: TextDirection.ltr,
                maxLength: 50,
                enabled: !_busy,
                onChanged: (_) => setState(() {}),
                onSubmitted: (_) {
                  if (_canSubmit) {
                    unawaited(_submit());
                  }
                },
                decoration: InputDecoration(
                  labelText: l10n.emailLabel,
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
                    : Text(l10n.sendCodeButton),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
