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
import '../../core/validation/field_limits.dart';
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
  bool _passwordTouched = false;
  List<PasswordRequirement> _passwordUnmet = <PasswordRequirement>[];
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
      PasswordRequirement req, AppL10n l10n) {
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

  Future<void> _submit() async {
    final l10n = AppL10n.of(context);
    // Client-side validation (required + password policy + confirm-match) gates
    // the round-trip; the inline errors render in the fields' own error border.
    if (!_formKey.currentState!.validate()) {
      return;
    }
    if (unmetPasswordRequirements(_password.text).isNotEmpty) {
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
            const SizedBox(height: SimfTokens.space6),
          ],
        ),
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(
        maxWidth: SimfTokens.resetPasswordScreenMaxWidth,
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              const SizedBox(height: SimfTokens.resetPasswordScreenHeight),
              const Center(child: OtpMark(icon: Icons.lock_outline)),
              const SizedBox(height: SimfTokens.space6),
              Text(
                l10n.resetPasswordSent,
                textAlign: TextAlign.center,
                style: SimfTokens.bodyBeige,
              ),
              const SizedBox(height: SimfTokens.space8),
              NaviFormField(
                label: l10n.otpLabel,
                controller: _code,
                enabled: !_busy,
                keyboardType: TextInputType.number,
                textDirection: TextDirection.ltr,
                maxLength: FieldLimits.otpCode,
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
              const SizedBox(height: SimfTokens.space4),
              NaviFormField(
                label: l10n.newPasswordLabel,
                controller: _password,
                enabled: !_busy,
                obscureText: _obscure,
                maxLength: FieldLimits.password,
                suffixIcon: NavyPasswordToggle(
                  obscure: _obscure,
                  onToggle: () => setState(() => _obscure = !_obscure),
                ),
                validator: (value) =>
                    isBlank(value) ? l10n.requiredField : null,
                onChanged: _onPasswordChanged,
              ),
              _buildPasswordErrors(l10n),
              const SizedBox(height: SimfTokens.space4),
              NaviFormField(
                label: l10n.confirmPasswordLabel,
                controller: _confirm,
                enabled: !_busy,
                obscureText: _obscure,
                maxLength: FieldLimits.password,
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
                const SizedBox(height: SimfTokens.space3),
                Text(
                  _error!,
                  style: SimfTokens.labelDangerSm,
                ),
              ],
              const SizedBox(height: SimfTokens.space6),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildBottomActions(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(
        maxWidth: SimfTokens.resetPasswordScreenMaxWidth,
        child: AuthSubmitButton(
          label: l10n.resetPasswordButton,
          busy: _busy,
          onPressed: _canSubmit ? () => unawaited(_submit()) : null,
        ),
      ),
    );
  }
}
