import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import 'widgets/auth_chrome.dart';

/// Part B (D-430) — activate a passwordless badge account: verify an emailed
/// code, then set the first password. Reached from the badge-scan screen. When
/// the account already has a real email the code is sent there automatically on
/// open; when it has none (`needsEmail`) the holder enters one first, which is
/// verified and attached. Built on the same KSA auth chrome as reset-password.
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

  Future<void> _start() async {
    final l10n = AppL10n.of(context);
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

  Future<void> _complete() async {
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
    // The email step shows only when the account has no email AND we haven't
    // sent a code yet; otherwise we're on the code + password step.
    final emailStep = widget.needsEmail && !_codeSent;
    return KsaAuthScaffold(
      busy: _busy,
      onBack: _back,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.badgeActivateTitle,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w600,
              color: SimfTokens.headlineInk,
            ),
          ),
          const SizedBox(height: 12),
          Text(
            emailStep
                ? l10n.badgeActivateEmailIntro
                : l10n.badgeActivateCodeSent(_maskedShown ?? ''),
            textAlign: TextAlign.center,
            style: const TextStyle(fontSize: 12, color: SimfTokens.greyText),
          ),
          const SizedBox(height: 24),
          if (emailStep) ..._emailStep(l10n) else ..._codeStep(l10n),
          if (_error != null) ...<Widget>[
            const SizedBox(height: 12),
            Text(
              _error!,
              style: const TextStyle(color: SimfTokens.danger, fontSize: 12),
            ),
          ],
        ],
      ),
    );
  }

  List<Widget> _emailStep(AppL10n l10n) => <Widget>[
        KsaFieldLabel(text: l10n.emailLabelGeneric),
        const SizedBox(height: 8),
        TextField(
          controller: _email,
          keyboardType: TextInputType.emailAddress,
          textDirection: TextDirection.ltr,
          textAlign: TextAlign.left,
          enabled: !_busy,
          onChanged: (_) => setState(() {}),
          style: ksaInputTextStyle,
          decoration: ksaInputDecoration(),
        ),
        const SizedBox(height: 24),
        KsaSubmitButton(
          label: l10n.badgeSendCodeButton,
          busy: _busy,
          onPressed: _email.text.trim().isNotEmpty && !_busy
              ? () => unawaited(_start())
              : null,
        ),
      ];

  List<Widget> _codeStep(AppL10n l10n) => <Widget>[
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
            if (_canComplete) {
              unawaited(_complete());
            }
          },
          style: ksaInputTextStyle,
          decoration: ksaInputDecoration(),
        ),
        const SizedBox(height: 24),
        KsaSubmitButton(
          label: l10n.badgeActivateButton,
          busy: _busy,
          onPressed: _canComplete ? () => unawaited(_complete()) : null,
        ),
      ];

  bool get _canComplete =>
      _code.text.trim().isNotEmpty &&
      _password.text.isNotEmpty &&
      _confirm.text.isNotEmpty &&
      !_busy;
}
