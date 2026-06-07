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
    // Mockup frame 4-01 (.otp): a fully-navy, centred column — gold-ringed
    // envelope mark, the "sent to <email>" caption, the tinted 6-digit entry,
    // a full-width gold Verify button, and the accent resend line below it.
    return Scaffold(
      appBar: AppBar(title: Text(l10n.emailVerifyTitle)),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space6,
            SimfTokens.space8,
            SimfTokens.space6,
            SimfTokens.space8,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              const _MailMark(),
              const SizedBox(height: SimfTokens.space5),
              // Caption (.otp p) + the bold white LTR address it was sent to.
              Text(
                l10n.emailVerifySentTo,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: SimfTokens.txtSecondary,
                  fontSize: SimfTokens.textSm,
                  height: 1.85,
                ),
              ),
              const SizedBox(height: SimfTokens.space1),
              Text(
                widget.email,
                textDirection: TextDirection.ltr,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textMd,
                  letterSpacing: 0.4,
                ),
              ),
              const SizedBox(height: SimfTokens.space5),
              // The 6-digit code entry — a single tinted box (.cell-d look):
              // accent border, faint accent fill, centred LTR spaced digits.
              TextField(
                controller: _code,
                keyboardType: TextInputType.number,
                textDirection: TextDirection.ltr,
                textAlign: TextAlign.center,
                maxLength: 6,
                enabled: !_busy,
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontSize: SimfTokens.textXl,
                  fontWeight: FontWeight.w700,
                  letterSpacing: 8,
                ),
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
                  filled: true,
                  fillColor: SimfTokens.accent.withValues(alpha: 0.06),
                  enabledBorder: OutlineInputBorder(
                    borderRadius:
                        BorderRadius.circular(SimfTokens.radiusSmall + 2),
                    borderSide: const BorderSide(color: SimfTokens.line),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderRadius:
                        BorderRadius.circular(SimfTokens.radiusSmall + 2),
                    borderSide:
                        const BorderSide(color: SimfTokens.accent, width: 1.5),
                  ),
                ),
              ),
              if (_error != null) ...<Widget>[
                const SizedBox(height: SimfTokens.space3),
                Text(
                  _error!,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: SimfTokens.danger),
                ),
              ],
              const SizedBox(height: SimfTokens.space5),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _canVerify ? () => unawaited(_verify()) : null,
                  child: _busy
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Text(l10n.verifyButton),
                ),
              ),
              const SizedBox(height: SimfTokens.space2),
              // Resend line (.otp-foot / .otp-timer) — accent text; shows the
              // cooldown countdown until it expires, then the resend action.
              TextButton(
                onPressed: _canResend ? () => unawaited(_resend()) : null,
                style: TextButton.styleFrom(
                  foregroundColor: SimfTokens.accent,
                ),
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

/// The gold-ringed envelope mark at the top of the OTP frame (.otp-ic): a
/// 64px circle with an accent border and a faint accent fill.
class _MailMark extends StatelessWidget {
  const _MailMark();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 64,
      height: 64,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: SimfTokens.accent.withValues(alpha: 0.06),
        border: Border.all(color: SimfTokens.accent),
      ),
      alignment: Alignment.center,
      child: const Icon(
        Icons.mail_outline,
        color: SimfTokens.accent,
        size: 30,
      ),
    );
  }
}
