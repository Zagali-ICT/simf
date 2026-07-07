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
import '../../core/errors/api_error_l10n.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/validation/password_validation.dart';
import '../../core/validation/required_validation.dart';
import 'widgets/account_sub_header.dart';
import 'widgets/auth_chrome.dart';
import 'widgets/navi_form_field.dart';
import 'widgets/navy_password_toggle.dart';
import 'widgets/otp_code_boxes.dart';

/// Page 003 — Reset password (Logic L-6). No dedicated Figma frame exists for
/// this step, so it is built to match its navy sibling — the forgot-password
/// screen (node 918:2341, D-656): the navy surface, the [AccountSubHeader], the
/// gold-ringed lock mark, an instruction body, the emailed OTP + the new
/// password + its confirmation, and the gold CTA pinned at the bottom (D-658).
/// Collects the emailed OTP + a new password, calls `POST /app/auth/reset-
/// password`, then returns to sign-in with the email pre-filled. The email is
/// carried in from the forgot screen.
class ResetPasswordScreen extends ConsumerStatefulWidget {
  const ResetPasswordScreen({required this.email, super.key});

  final String email;

  @override
  ConsumerState<ResetPasswordScreen> createState() =>
      _ResetPasswordScreenState();
}

class _ResetPasswordScreenState extends ConsumerState<ResetPasswordScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
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
    // Client-side validation (required + password policy + confirm-match) gates
    // the round-trip; the inline errors render in the fields' own error border.
    if (!_formKey.currentState!.validate()) {
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
      // If the user reached reset from their profile while signed in, the
      // password change invalidates the old session server-side — sign out
      // locally so the sign-in screen is a genuine fresh login, not a stale
      // signed-in state (D-659).
      if (ref.read(authControllerProvider) is AuthStateSignedIn) {
        await ref.read(authControllerProvider.notifier).signOut();
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
    context.goNamed(RouteNames.forgotPassword);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            AccountSubHeader(
              title: l10n.resetPasswordTitle,
              onBack: _back,
              busy: _busy,
            ),
            Expanded(child: _buildBody(l10n)),
            _buildBottomActions(l10n),
            const SizedBox(height: 24),
          ],
        ),
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: MaxWidthBody(
        maxWidth: 560,
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              const SizedBox(height: 48),
              const Center(child: OtpMark(icon: Icons.lock_outline)),
              const SizedBox(height: 24),
              Text(
                l10n.resetPasswordSent,
                textAlign: TextAlign.center,
                style: SimfTokens.bodyBeige,
              ),
              const SizedBox(height: 32),
              NaviFormField(
                label: l10n.otpLabel,
                controller: _code,
                enabled: !_busy,
                keyboardType: TextInputType.number,
                textDirection: TextDirection.ltr,
                maxLength: 6,
                inputFormatters: <TextInputFormatter>[
                  FilteringTextInputFormatter.digitsOnly,
                ],
                // No dedicated "6-digit code" key; reuse requiredField for the
                // empty + wrong-length case (reported to owner).
                validator: (value) => isBlank(value) || value!.trim().length != 6
                    ? l10n.requiredField
                    : null,
                onChanged: (_) => setState(() {}),
              ),
              const SizedBox(height: 16),
              NaviFormField(
                label: l10n.newPasswordLabel,
                controller: _password,
                enabled: !_busy,
                obscureText: _obscure,
                maxLength: 128,
                suffixIcon: NavyPasswordToggle(
                  obscure: _obscure,
                  onToggle: () => setState(() => _obscure = !_obscure),
                ),
                // Reset SETS a new password, so apply the policy here.
                validator: (value) {
                  if (isBlank(value)) {
                    return l10n.requiredField;
                  }
                  return isValidPassword(value!)
                      ? null
                      : l10n.passwordPolicyError;
                },
                onChanged: (_) => setState(() {}),
              ),
              const SizedBox(height: 16),
              NaviFormField(
                label: l10n.confirmPasswordLabel,
                controller: _confirm,
                enabled: !_busy,
                obscureText: _obscure,
                maxLength: 128,
                // Must equal the new password typed above.
                validator: (value) =>
                    value == _password.text ? null : l10n.passwordsDoNotMatch,
                onChanged: (_) => setState(() {}),
                onFieldSubmitted: (_) {
                  if (_canSubmit) {
                    unawaited(_submit());
                  }
                },
              ),
              if (_error != null) ...<Widget>[
                const SizedBox(height: 12),
                Text(
                  _error!,
                  style: const TextStyle(
                    color: SimfTokens.danger,
                    fontSize: 12,
                  ),
                ),
              ],
              const SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildBottomActions(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: MaxWidthBody(
        maxWidth: 560,
        child: AuthSubmitButton(
          label: l10n.resetPasswordButton,
          busy: _busy,
          onPressed: _canSubmit ? () => unawaited(_submit()) : null,
        ),
      ),
    );
  }
}
