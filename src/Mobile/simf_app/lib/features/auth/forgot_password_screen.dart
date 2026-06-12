import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import 'widgets/auth_chrome.dart';

/// Page 003 — Forgot password (Logic L-6), rebuilt on the KSA entry chrome
/// (D-374: "same card and color and style" as sign-in/sign-up). Emails a
/// reset OTP, then routes to the reset screen with the email carried
/// forward. The request is enumeration-resistant on the server (always
/// success-shaped), so the app always proceeds to the reset step.
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

  void _back() {
    if (context.canPop()) {
      context.pop();
      return;
    }
    context.goNamed(RouteNames.signIn);
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
            l10n.forgotPasswordTitle,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w600,
              color: SimfTokens.headlineInk,
            ),
          ),
          const SizedBox(height: 12),
          Text(
            l10n.forgotPasswordBody,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 12,
              color: SimfTokens.greyText,
            ),
          ),
          const SizedBox(height: 24),
          KsaFieldLabel(text: l10n.emailLabel),
          const SizedBox(height: 8),
          TextField(
            controller: _email,
            keyboardType: TextInputType.emailAddress,
            textDirection: TextDirection.ltr,
            textAlign: TextAlign.left,
            maxLength: 50,
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
            label: l10n.sendCodeButton,
            busy: _busy,
            onPressed: _canSubmit ? () => unawaited(_submit()) : null,
          ),
        ],
      ),
    );
  }
}
