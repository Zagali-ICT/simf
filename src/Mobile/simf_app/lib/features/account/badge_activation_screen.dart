import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/validation/email_validation.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/core/validation/password_validation.dart';
import 'package:simf_app/core/validation/required_validation.dart';
import 'package:simf_app/features/account/widgets/auth_bottom_bar.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';
import 'package:simf_app/features/account/widgets/auth_screen_scaffold.dart';
import 'package:simf_app/features/account/widgets/auth_scroll_body.dart';
import 'package:simf_app/features/account/widgets/navi_form_field.dart';
import 'package:simf_app/features/account/widgets/navy_password_toggle.dart';
import 'package:simf_app/features/account/widgets/otp_code_boxes.dart';
import 'package:simf_app/features/account/widgets/password_requirement_errors.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Badge activation — route: RouteNames.badgeActivation · Figma: no bound
/// node (navy auth family, D-659).
/// Contract: D-430 part B — a passwordless badge account verifies an emailed
/// code, then sets its first password. An account with a real email on file is
/// sent the code on open; one without (`needsEmail`) enters an address first,
/// which is verified and attached.
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
      final result =
          await ref.read(authRepositoryProvider).badgeActivationStart(
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return AuthScreenScaffold(
      title: l10n.badgeActivateTitle,
      onBack: _back,
      busy: _busy,
      body: AuthScrollBody(
        maxWidth: SimfTokens.badgeActivationScreenMaxWidth,
        formKey: _formKey,
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
          if (_emailStep)
            ..._emailStepFields(l10n)
          else
            ..._codeStepFields(l10n),
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
      bottom: <Widget>[
        AuthBottomBar(
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
        const SizedBox(height: SimfTokens.space6),
      ],
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
        PasswordRequirementErrors(
          unmet: _passwordTouched
              ? _passwordUnmet
              : const <PasswordRequirement>[],
        ),
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
}
