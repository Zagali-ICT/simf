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
import '../../core/validation/password_validation.dart';
import '../../core/validation/required_validation.dart';
import '../../core/widgets/simf_field_label.dart';
import '../../core/widgets/simf_field_style.dart';
import 'widgets/account_sub_header.dart';
import 'widgets/auth_chrome.dart';
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
                _emailStep
                    ? l10n.badgeActivateEmailIntro
                    : l10n.badgeActivateCodeSent(_maskedShown ?? ''),
                textAlign: TextAlign.center,
                style: SimfTokens.bodyBeige,
              ),
              const SizedBox(height: 32),
              if (_emailStep) ..._emailStepFields(l10n) else ..._codeStepFields(l10n),
              if (_error != null) ...<Widget>[
                const SizedBox(height: 12),
                Text(
                  _error!,
                  style: const TextStyle(color: SimfTokens.danger, fontSize: 12),
                ),
              ],
              const SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }

  List<Widget> _emailStepFields(AppL10n l10n) => <Widget>[
        SimfFieldLabel(l10n.emailLabelGeneric, color: Colors.white),
        const SizedBox(height: 8),
        TextFormField(
          controller: _email,
          keyboardType: TextInputType.emailAddress,
          textDirection: TextDirection.ltr,
          maxLength: 50,
          enabled: !_busy,
          onChanged: (_) => setState(() {}),
          style: simfInputStyleOnNavy,
          decoration: simfFieldDecoration(counterText: ''),
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
        SimfFieldLabel(l10n.otpLabel, color: Colors.white),
        const SizedBox(height: 8),
        TextFormField(
          controller: _code,
          keyboardType: TextInputType.number,
          textDirection: TextDirection.ltr,
          maxLength: 6,
          enabled: !_busy,
          inputFormatters: <TextInputFormatter>[
            FilteringTextInputFormatter.digitsOnly,
          ],
          onChanged: (_) => setState(() {}),
          style: simfInputStyleOnNavy,
          decoration: simfFieldDecoration(counterText: ''),
          validator: (value) => isBlank(value) ? l10n.requiredField : null,
        ),
        const SizedBox(height: 16),
        SimfFieldLabel(l10n.newPasswordLabel, color: Colors.white),
        const SizedBox(height: 8),
        TextFormField(
          controller: _password,
          obscureText: _obscure,
          maxLength: 128,
          enabled: !_busy,
          onChanged: (_) => setState(() {}),
          style: simfInputStyleOnNavy,
          decoration: simfFieldDecoration(
            counterText: '',
            suffixIcon: NavyPasswordToggle(
              obscure: _obscure,
              onToggle: () => setState(() => _obscure = !_obscure),
            ),
          ),
          validator: (value) {
            if (isBlank(value)) {
              return l10n.requiredField;
            }
            if (!isValidPassword(value!)) {
              return l10n.passwordPolicyError;
            }
            return null;
          },
        ),
        const SizedBox(height: 16),
        SimfFieldLabel(l10n.confirmPasswordLabel, color: Colors.white),
        const SizedBox(height: 8),
        TextFormField(
          controller: _confirm,
          obscureText: _obscure,
          maxLength: 128,
          enabled: !_busy,
          onChanged: (_) => setState(() {}),
          onFieldSubmitted: (_) {
            if (_canComplete) {
              unawaited(_complete());
            }
          },
          style: simfInputStyleOnNavy,
          decoration: simfFieldDecoration(counterText: ''),
          validator: (value) =>
              value == _password.text ? null : l10n.passwordsDoNotMatch,
        ),
      ];

  Widget _buildBottomActions(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: MaxWidthBody(
        maxWidth: 560,
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
