import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/validation/email_validation.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/core/validation/required_validation.dart';
import 'package:simf_app/features/account/widgets/auth_bottom_bar.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';
import 'package:simf_app/features/account/widgets/auth_screen_scaffold.dart';
import 'package:simf_app/features/account/widgets/auth_scroll_body.dart';
import 'package:simf_app/features/account/widgets/navi_form_field.dart';
import 'package:simf_app/features/account/widgets/otp_code_boxes.dart';
import 'package:simf_app/features/account/widgets/remembered_sign_in_row.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Forgot password — نسيت كلمة المرور · route: RouteNames.forgotPassword
/// Figma 918:2341
/// Contract: Logic L-6 — the request is enumeration-resistant on the server
/// (always success-shaped), so the app always proceeds to the reset step with
/// the address carried forward.
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
    return AuthScreenScaffold(
      title: l10n.forgotPasswordTitle,
      onBack: _back,
      busy: _busy,
      body: AuthScrollBody(
        maxWidth: SimfTokens.forgotPasswordScreenMaxWidth,
        formKey: _formKey,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: SimfTokens.forgotPasswordScreenHeight),
          const Center(child: OtpMark(icon: Icons.lock_outline)),
          const SizedBox(height: SimfTokens.space6),
          Text(
            l10n.forgotPasswordBody,
            textAlign: TextAlign.center,
            style: SimfTokens.bodyBeige,
          ),
          const SizedBox(height: SimfTokens.space8),
          NaviFormField(
            label: l10n.emailLabel,
            controller: _email,
            enabled: !_busy,
            keyboardType: TextInputType.emailAddress,
            maxLength: FieldLimits.email,
            hintText: l10n.emailHintExample,
            // The mail glyph matches the hint colour (D-674); as a suffix
            // it renders at the inline-start (left under RTL), per the
            // frame.
            suffixIcon: const Icon(
              Icons.mail_outline,
              color: SimfTokens.greyText,
              size: SimfTokens.forgotPasswordScreenSize,
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
            const SizedBox(height: SimfTokens.space3),
            Text(
              _error!,
              style: SimfTokens.labelDangerSm,
            ),
          ],
          const SizedBox(height: SimfTokens.space6),
        ],
      ),
      // Bottom actions (918:2371): the gold send CTA + the "remembered? sign
      // in" foot.
      bottom: <Widget>[
        AuthBottomBar(
          maxWidth: SimfTokens.forgotPasswordScreenMaxWidth,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              AuthSubmitButton(
                label: l10n.sendRecoveryCodeButton,
                busy: _busy,
                onPressed: _canSubmit ? () => unawaited(_submit()) : null,
              ),
              const SizedBox(height: SimfTokens.space4),
              RememberedSignInRow(busy: _busy),
            ],
          ),
        ),
        const SizedBox(height: SimfTokens.space6),
      ],
    );
  }
}
