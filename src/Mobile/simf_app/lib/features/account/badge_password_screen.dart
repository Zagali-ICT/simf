import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/core/validation/required_validation.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_app/features/account/post_auth_route.dart';
import 'package:simf_app/features/account/widgets/auth_bottom_bar.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';
import 'package:simf_app/features/account/widgets/auth_screen_scaffold.dart';
import 'package:simf_app/features/account/widgets/auth_scroll_body.dart';
import 'package:simf_app/features/account/widgets/navi_form_field.dart';
import 'package:simf_app/features/account/widgets/navy_password_toggle.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Badge password — إكمال تسجيل الدخول · route: RouteNames.badgePassword
/// Figma: no bound node (the navy auth family, D-659).
/// Contract: the badge NEVER bypasses the password (D-430/D-738). The server
/// runs the full password + 2FA pipeline, so a 2FA account continues to the
/// shared email-OTP screen rather than landing signed in here.
class BadgePasswordScreen extends ConsumerStatefulWidget {
  const BadgePasswordScreen({
    required this.qrId,
    this.displayName,
    this.maskedEmail,
    super.key,
  });

  final String qrId;
  final String? displayName;
  final String? maskedEmail;

  @override
  ConsumerState<BadgePasswordScreen> createState() =>
      _BadgePasswordScreenState();
}

class _BadgePasswordScreenState extends ConsumerState<BadgePasswordScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _password = TextEditingController();
  bool _obscure = true;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _password.dispose();
    super.dispose();
  }

  bool get _canSubmit => _password.text.isNotEmpty && !_busy;

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(authControllerProvider.notifier).signInWithBadge(
            qrId: widget.qrId,
            password: _password.text,
            displayEmail: widget.maskedEmail,
          );
      if (!mounted) {
        return;
      }
      final state = ref.read(authControllerProvider);
      if (state is AuthStateAwaitingOtp) {
        context.goNamed(RouteNames.verifyOtp);
      } else if (state is AuthStateSignedIn) {
        await maybeOfferBiometricEnrolment(context, ref);
        if (mounted) {
          routeAfterAuth(context, ref);
        }
      }
    } on AuthFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = failure.source.localizedMessage(l10n);
        _password.clear();
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
    final name = widget.displayName;
    final masked = widget.maskedEmail;
    return AuthScreenScaffold(
      title: l10n.badgePasswordTitle,
      onBack: _back,
      busy: _busy,
      body: AuthScrollBody(
        maxWidth: SimfTokens.badgePasswordScreenMaxWidth,
        formKey: _formKey,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: SimfTokens.badgePasswordScreenHeight),
          if (name != null && name.isNotEmpty) ...<Widget>[
            Text(
              l10n.badgeWelcomeName(name),
              textAlign: TextAlign.center,
              style: SimfTokens.labelWhiteBoldXxl,
            ),
            const SizedBox(height: SimfTokens.space2),
          ],
          if (masked != null && masked.isNotEmpty) ...<Widget>[
            Text(
              l10n.badgeSignInAccountLine(masked),
              textAlign: TextAlign.center,
              textDirection: TextDirection.ltr,
              style: SimfTokens.bodyBeige,
            ),
            const SizedBox(height: SimfTokens.space2),
          ],
          const SizedBox(height: SimfTokens.space6),
          NaviFormField(
            label: l10n.passwordLabel,
            controller: _password,
            obscureText: _obscure,
            maxLength: FieldLimits.password,
            enabled: !_busy,
            autovalidateMode: AutovalidateMode.disabled,
            onChanged: (_) => setState(() {}),
            onFieldSubmitted: (_) {
              if (_canSubmit) {
                unawaited(_submit());
              }
            },
            suffixIcon: NavyPasswordToggle(
              obscure: _obscure,
              onToggle: () => setState(() => _obscure = !_obscure),
            ),
            validator: (value) => isBlank(value) ? l10n.requiredField : null,
          ),
          const SizedBox(height: SimfTokens.space3),
          Align(
            alignment: AlignmentDirectional.centerEnd,
            child: TextButton(
              onPressed: _busy
                  ? null
                  : () => context.goNamed(RouteNames.forgotPassword),
              child: Text(l10n.forgotPasswordLink),
            ),
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
      bottom: <Widget>[
        AuthBottomBar(
          maxWidth: SimfTokens.badgePasswordScreenMaxWidth,
          child: AuthSubmitButton(
            label: l10n.signInButton,
            busy: _busy,
            onPressed: _canSubmit ? () => unawaited(_submit()) : null,
          ),
        ),
        const SizedBox(height: SimfTokens.space6),
      ],
    );
  }
}
