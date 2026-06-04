import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Page 006 — التحقق بالبريد · Email verification (Page_006 docs).
///
/// Sign-up **step 2**: the user enters the 6-digit code emailed after sign-up
/// and the app calls `POST /app/auth/verify-email { email, code }` — anonymous,
/// no token yet. On success the account moves Registered → EmailVerified; since
/// verify-email issues **no session**, the verified user is routed to sign-in to
/// continue (the profile step is authenticated — Page_006 Function). **Resend**
/// re-issues the code via `POST /app/auth/resend-code` and restarts a cooldown
/// from the returned `codeExpiresInSeconds`. The email is passed in from Page 005.
class SignUpEmailVerifyScreen extends ConsumerStatefulWidget {
  const SignUpEmailVerifyScreen({required this.email, super.key});

  /// The address the code was sent to (navigation argument from Page 005).
  final String email;

  @override
  ConsumerState<SignUpEmailVerifyScreen> createState() =>
      _SignUpEmailVerifyScreenState();
}

class _SignUpEmailVerifyScreenState
    extends ConsumerState<SignUpEmailVerifyScreen> {
  final TextEditingController _code = TextEditingController();
  bool _busy = false;
  String? _error;
  int _cooldown = 0;
  Timer? _timer;

  @override
  void dispose() {
    _timer?.cancel();
    _code.dispose();
    super.dispose();
  }

  bool get _canVerify => _code.text.trim().length == 6 && !_busy;
  bool get _canResend => _cooldown == 0 && !_busy;

  void _startCooldown(int seconds) {
    _timer?.cancel();
    setState(() => _cooldown = seconds);
    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }
      setState(() {
        _cooldown = _cooldown <= 1 ? 0 : _cooldown - 1;
      });
      if (_cooldown == 0) {
        timer.cancel();
      }
    });
  }

  Future<void> _verify() async {
    final code = _code.text.trim();
    if (code.length != 6) {
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(authControllerProvider.notifier).verifyEmail(
            email: widget.email,
            code: code,
          );
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.emailVerifiedToast)));
      // verify-email issues no session; the authenticated profile step needs a
      // token, so the verified user signs in next (Page_006 Function).
      context.goNamed(RouteNames.signIn);
    } on AuthFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = failure is NetworkUnavailable
            ? l10n.networkErrorBody
            : failure.source.message;
        _code.clear();
      });
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _resend() async {
    final l10n = AppL10n.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final seconds =
          await ref.read(authControllerProvider.notifier).resendCode(
                email: widget.email,
              );
      if (!mounted) {
        return;
      }
      _startCooldown(seconds > 0 ? seconds : 60);
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.emailVerifyTitle)),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              const SizedBox(height: SimfTokens.space5),
              Text(
                l10n.emailVerifySentTo,
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: SimfTokens.space1),
              Text(
                widget.email,
                textDirection: TextDirection.ltr,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textMd,
                ),
              ),
              const SizedBox(height: SimfTokens.space5),
              TextField(
                controller: _code,
                keyboardType: TextInputType.number,
                textDirection: TextDirection.ltr,
                maxLength: 6,
                enabled: !_busy,
                inputFormatters: <TextInputFormatter>[
                  FilteringTextInputFormatter.digitsOnly,
                ],
                onChanged: (_) => setState(() {}),
                onSubmitted: (_) {
                  if (_canVerify) {
                    unawaited(_verify());
                  }
                },
                decoration: InputDecoration(
                  labelText: l10n.otpLabel,
                  counterText: '',
                ),
              ),
              if (_error != null) ...<Widget>[
                const SizedBox(height: SimfTokens.space3),
                Text(
                  _error!,
                  style: const TextStyle(color: SimfTokens.danger),
                ),
              ],
              const SizedBox(height: SimfTokens.space5),
              FilledButton(
                onPressed: _canVerify ? () => unawaited(_verify()) : null,
                child: _busy
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(l10n.verifyButton),
              ),
              const SizedBox(height: SimfTokens.space2),
              TextButton(
                onPressed: _canResend ? () => unawaited(_resend()) : null,
                child: Text(
                  _cooldown > 0
                      ? l10n.resendCooldownText(_cooldown)
                      : l10n.resendCodeButton,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
