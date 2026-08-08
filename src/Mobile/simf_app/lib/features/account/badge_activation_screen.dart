import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../core/errors/api_error_l10n.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/validation/email_validation.dart';
import '../../core/validation/field_limits.dart';
import '../../core/validation/password_validation.dart';
import '../../core/validation/required_validation.dart';
import 'widgets/account_sub_header.dart';
import 'widgets/auth_chrome.dart';
import 'widgets/navi_form_field.dart';
import 'widgets/navy_password_toggle.dart';
import 'widgets/otp_code_boxes.dart';

/// Part B (D-430) — activate a passwordless badge account: verify an emailed
/// code, then set the first password. Reached from the badge-scan screen. When
/// the account already has a real email the code is sent there automatically on
/// open; when it has none (`needsEmail`) the holder enters one first, which is
/// verified and attached. Built on the navy auth family (D-659) — the same
/// `Scaffold(navySurface)` + [AccountSubHeader] + [OtpMark] + gold CTA as its
/// sibling reset-password (918:2341); no dedicated Figma node.
class BadgeActivationScreen extends ConsumerStatefulWidget {
  const BadgeActivationScreen({
    required this.qrId,
    required this.needsEmail,
    this.maskedEmail,
    super.key,
  });

  final String qrId;
  final bool needsEmail;
  final String? maskedEmail;

  @override
  ConsumerState<BadgeActivationScreen> createState() =>
      _BadgeActivationScreenState();
}

class _BadgeActivationScreenState extends ConsumerState<BadgeActivationScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _email = TextEditingController();
  final TextEditingController _code = TextEditingController();
  final TextEditingController _password = TextEditingController();
  final TextEditingController _confirm = TextEditingController();
  bool _obscure = true;
  bool _busy = false;
  bool _codeSent = false;
  String? _maskedShown;
  bool _passwordTouched = false;
  List<PasswordRequirement> _passwordUnmet = <PasswordRequirement>[];
  String? _error;

  @override
  void initState() {
    super.initState();
    _maskedShown = widget.maskedEmail;
    // When the account already has a real email, send the code on open.
    if (!widget.needsEmail) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        unawaited(_start());
      });
    }
  }

  @override
  void dispose() {
    _email.dispose();
    _code.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  /// True on the email-entry step (no email on file, code not yet sent).
  bool get _emailStep => widget.needsEmail && !_codeSent;

  Future<void> _start() async {
    final l10n = AppL10n.of(context);
    // Validate only the manual email-entry step; the auto-send path (an account
    // that already has an email) has no email field to validate and must not be
    // blocked by the still-empty code/password fields.
    if (widget.needsEmail && !(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final result = await ref.read(authRepositoryProvider).badgeActivationStart(
            qrId: widget.qrId,
            email: widget.needsEmail ? _email.text.trim() : null,
          );
      if (!mounted) {
        return;
      }
      setState(() {
        _codeSent = true;
        _maskedShown = result.maskedEmail;
      });
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

  Future<void> _complete() async {
    final l10n = AppL10n.of(context);
    if (!(_formKey.currentState?.validate() ?? false)) {
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
      await ref.read(authRepositoryProvider).badgeActivationComplete(
            qrId: widget.qrId,
            code: _code.text.trim(),
            newPassword: _password.text,
            confirmPassword: _confirm.text,
          );
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.badgeActivatedDone)),
      );
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
    context.goNamed(RouteNames.signIn);
  }

  bool get _canComplete =>
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            AccountSubHeader(
              title: l10n.badgeActivateTitle,
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
        maxWidth: SimfTokens.badgeActivationScreenMaxWidth,
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              const SizedBox(height: SimfTokens.badgeActivationScreenHeight),
              const Center(child: OtpMark(icon: Icons.lock_outline)),
              const SizedBox(height: SimfTokens.space6),
              Text(
                _emailStep
                    ? l10n.badgeActivateEmailIntro
                    : l10n.badgeActivateCodeSent(_maskedShown ?? ''),
                textAlign: TextAlign.center,
                style: SimfTokens.bodyBeige,
              ),
              const SizedBox(height: SimfTokens.space8),
              if (_emailStep) ..._emailStepFields(l10n) else ..._codeStepFields(l10n),
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

  List<Widget> _emailStepFields(AppL10n l10n) => <Widget>[
        NaviFormField(
          label: l10n.emailLabelGeneric,
          controller: _email,
          keyboardType: TextInputType.emailAddress,
          textDirection: TextDirection.ltr,
          maxLength: FieldLimits.email,
          enabled: !_busy,
          autovalidateMode: AutovalidateMode.disabled,
          onChanged: (_) => setState(() {}),
          validator: (value) {
            if (isBlank(value)) {
              return l10n.requiredField;
            }
            if (!isValidEmail(value!.trim())) {
              return l10n.invalidEmail;
            }
            return null;
          },
        ),
      ];

  List<Widget> _codeStepFields(AppL10n l10n) => <Widget>[
        NaviFormField(
          label: l10n.otpLabel,
          controller: _code,
          keyboardType: TextInputType.number,
          textDirection: TextDirection.ltr,
          maxLength: FieldLimits.otpCode,
          enabled: !_busy,
          autovalidateMode: AutovalidateMode.disabled,
          inputFormatters: <TextInputFormatter>[
            FilteringTextInputFormatter.digitsOnly,
          ],
          onChanged: (_) => setState(() {}),
          validator: (value) => isBlank(value) ? l10n.requiredField : null,
        ),
        const SizedBox(height: SimfTokens.space4),
        NaviFormField(
          label: l10n.newPasswordLabel,
          controller: _password,
          obscureText: _obscure,
          maxLength: FieldLimits.password,
          enabled: !_busy,
          autovalidateMode: AutovalidateMode.disabled,
          onChanged: _onPasswordChanged,
          suffixIcon: NavyPasswordToggle(
            obscure: _obscure,
            onToggle: () => setState(() => _obscure = !_obscure),
          ),
          validator: (value) => isBlank(value) ? l10n.requiredField : null,
        ),
        _buildPasswordErrors(l10n),
        const SizedBox(height: SimfTokens.space4),
        NaviFormField(
          label: l10n.confirmPasswordLabel,
          controller: _confirm,
          obscureText: _obscure,
          maxLength: FieldLimits.password,
          enabled: !_busy,
          autovalidateMode: AutovalidateMode.disabled,
          onChanged: (_) => setState(() {}),
          onFieldSubmitted: (_) {
            if (_canComplete) {
              unawaited(_complete());
            }
          },
          validator: (value) =>
              value == _password.text ? null : l10n.passwordsDoNotMatch,
        ),
      ];

  Widget _buildBottomActions(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(
        maxWidth: SimfTokens.badgeActivationScreenMaxWidth,
        child: _emailStep
            ? AuthSubmitButton(
                label: l10n.badgeSendCodeButton,
                busy: _busy,
                onPressed: _email.text.trim().isNotEmpty && !_busy
                    ? () => unawaited(_start())
                    : null,
              )
            : AuthSubmitButton(
                label: l10n.badgeActivateButton,
                busy: _busy,
                onPressed: _canComplete ? () => unawaited(_complete()) : null,
              ),
      ),
    );
  }
}
