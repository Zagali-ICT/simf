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
import '../../core/validation/required_validation.dart';
import '../../core/widgets/simf_field_label.dart';
import '../../core/widgets/simf_field_style.dart';
import 'biometric_auth.dart';
import 'post_auth_route.dart';
import 'widgets/account_sub_header.dart';
import 'widgets/auth_chrome.dart';
import 'widgets/navy_password_toggle.dart';

/// D-738 — the password step of badge-QR sign-in. A returning holder who scanned
/// their badge (an account that already has a password) finishes here with ONLY
/// their password: the resolved name + masked email are shown so no email is
/// typed, and the badge never bypasses the password (D-430). Submits
/// `{qrId, password}` to `POST /app/auth/badge-sign-in`, which runs the full
/// existing password + 2FA pipeline server-side; a 2FA account continues to the
/// shared email-OTP screen, otherwise it lands signed in (with the same Face-ID
/// nudge + post-auth routing as password sign-in).
///
/// Built on the navy auth family (D-659) — the same `Scaffold(navySurface)` +
/// [AccountSubHeader] + gold CTA as its sibling [BadgeActivationScreen]; no
/// dedicated Figma node.
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
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            AccountSubHeader(
              title: l10n.badgePasswordTitle,
              onBack: _back,
              busy: _busy,
            ),
            Expanded(child: _buildBody(l10n)),
            _buildSubmit(l10n),
            const SizedBox(height: SimfTokens.space6),
          ],
        ),
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    final name = widget.displayName;
    final masked = widget.maskedEmail;
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(
        maxWidth: 560,
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              const SizedBox(height: 48),
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
              SimfFieldLabel(l10n.passwordLabel, color: SimfTokens.surface),
              const SizedBox(height: SimfTokens.space2),
              TextFormField(
                controller: _password,
                obscureText: _obscure,
                maxLength: 128,
                enabled: !_busy,
                onChanged: (_) => setState(() {}),
                onFieldSubmitted: (_) {
                  if (_canSubmit) {
                    unawaited(_submit());
                  }
                },
                style: simfInputStyleOnNavy,
                decoration: simfFieldDecoration(
                  counterText: '',
                  suffixIcon: NavyPasswordToggle(
                    obscure: _obscure,
                    onToggle: () => setState(() => _obscure = !_obscure),
                  ),
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
                  style: const TextStyle(
                    color: SimfTokens.danger,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
              ],
              const SizedBox(height: SimfTokens.space6),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildSubmit(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(
        maxWidth: 560,
        child: AuthSubmitButton(
          label: l10n.signInButton,
          busy: _busy,
          onPressed: _canSubmit ? () => unawaited(_submit()) : null,
        ),
      ),
    );
  }
}
