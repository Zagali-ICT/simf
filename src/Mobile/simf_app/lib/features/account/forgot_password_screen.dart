import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../core/errors/api_error_l10n.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/validation/email_validation.dart';
import '../../core/validation/required_validation.dart';
import 'widgets/account_sub_header.dart';
import 'widgets/auth_chrome.dart';
import 'widgets/navi_form_field.dart';
import 'widgets/otp_code_boxes.dart';

/// Page 003 — نسيت كلمة المرور · Forgot password (Logic L-6). The KSA-Project
/// Figma design (node 918:2341): navy surface, the back + centred-title header,
/// the gold-ringed lock mark, the instruction body, the email field with a mail
/// glyph, the gold CTA pinned at the bottom, and the "remembered? sign in" foot.
/// Emails a reset code, then routes to the reset screen with the address carried
/// forward. The request is enumeration-resistant on the server (always
/// success-shaped), so the app always proceeds to the reset step.
class ForgotPasswordScreen extends ConsumerStatefulWidget {
  const ForgotPasswordScreen({this.email, super.key});

  /// Pre-fills the email field — passed when a signed-in user opens this from
  /// their profile (D-659), so they don't retype an address the app knows.
  final String? email;

  @override
  ConsumerState<ForgotPasswordScreen> createState() =>
      _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _email = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    final email = widget.email;
    if (email != null && email.isNotEmpty) {
      _email.text = email;
    }
  }

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  bool get _canSubmit => _email.text.trim().isNotEmpty && !_busy;

  String? _validateEmail(String? value) {
    final l10n = AppL10n.of(context);
    if (isBlank(value)) {
      return l10n.requiredField;
    }
    return isValidEmail(value!.trim()) ? null : l10n.invalidEmail;
  }

  Future<void> _submit() async {
    // Client-side validation (required + email shape) gates the round-trip; the
    // inline field error renders via the field's own error border.
    if (!_formKey.currentState!.validate()) {
      return;
    }
    final email = _email.text.trim();
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            AccountSubHeader(
              title: l10n.forgotPasswordTitle,
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
                l10n.forgotPasswordBody,
                textAlign: TextAlign.center,
                style: SimfTokens.bodyBeige,
              ),
              const SizedBox(height: 32),
              NaviFormField(
                label: l10n.emailLabel,
                controller: _email,
                enabled: !_busy,
                keyboardType: TextInputType.emailAddress,
                maxLength: 50,
                hintText: 'example@email.com',
                // The mail glyph matches the hint colour (D-674); as a suffix it
                // renders at the inline-start (left under RTL), per the frame.
                suffixIcon: const Icon(
                  Icons.mail_outline,
                  color: SimfTokens.greyText,
                  size: 18,
                ),
                validator: _validateEmail,
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

  /// Bottom actions (918:2371): the gold send CTA + the "remembered? sign in"
  /// foot.
  Widget _buildBottomActions(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: MaxWidthBody(
        maxWidth: 560,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            AuthSubmitButton(
              label: l10n.sendRecoveryCodeButton,
              busy: _busy,
              onPressed: _canSubmit ? () => unawaited(_submit()) : null,
            ),
            const SizedBox(height: 16),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Flexible(
                  child: Text(
                    l10n.rememberedPasswordQuestion,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 14,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ),
                const SizedBox(width: 6),
                Flexible(
                  child: TextButton(
                    onPressed: _busy ? null : () => context.goNamed(RouteNames.signIn),
                    style: authLinkButtonStyle(SimfTokens.accent),
                    child: Text(
                      l10n.signInTitle,
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
