import 'dart:async';

import 'package:flutter/gestures.dart' show TapGestureRecognizer;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_svg_icon.dart';
import 'post_auth_route.dart';
import 'widgets/otp_code_boxes.dart';

const Color _sweepTint = Color(0x0AFFFFFF);

/// Page 003 — email-OTP second factor (Logic L-5), restyled to the KSA OTP
/// frame (D-369, reusing the D-364 pattern via [OtpCodeBoxes]/[OtpMark]; the
/// previous screen is parked in `_legacy_mockup/`). Reached after sign-in
/// when the account has 2FA on; the controller holds the `otpToken`. The user
/// enters the emailed code and the app calls `POST /app/auth/verify-otp`.
/// Visitor-only (no TOTP path). A resend countdown shows below the boxes; once
/// it elapses, "إعادة الإرسال" returns to sign-in (a 2FA code can only be
/// re-issued by re-authenticating — the password isn't held here). Frame 758:2616.
class EmailOtpVerifyScreen extends ConsumerStatefulWidget {
  const EmailOtpVerifyScreen({super.key});

  @override
  ConsumerState<EmailOtpVerifyScreen> createState() =>
      _EmailOtpVerifyScreenState();
}

class _EmailOtpVerifyScreenState extends ConsumerState<EmailOtpVerifyScreen> {
  final TextEditingController _code = TextEditingController();
  final FocusNode _codeFocus = FocusNode();
  bool _busy = false;
  String? _error;

  // Frame 758:2616 — the resend countdown ("إعادة الإرسال خلال 00:42").
  static const int _resendSeconds = 60;
  int _secondsLeft = _resendSeconds;
  Timer? _ticker;
  // Owned once (not rebuilt each tick) so it is disposed cleanly.
  late final TapGestureRecognizer _resendTap;

  @override
  void initState() {
    super.initState();
    // The focused-box highlight follows the hidden field's focus.
    _codeFocus.addListener(() => setState(() {}));
    _resendTap = TapGestureRecognizer()..onTap = _back;
    _startCountdown();
  }

  void _startCountdown() {
    _ticker?.cancel();
    setState(() => _secondsLeft = _resendSeconds);
    _ticker = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        return;
      }
      if (_secondsLeft <= 1) {
        timer.cancel();
        setState(() => _secondsLeft = 0);
      } else {
        setState(() => _secondsLeft -= 1);
      }
    });
  }

  String get _countdownLabel {
    final m = (_secondsLeft ~/ 60).toString().padLeft(2, '0');
    final s = (_secondsLeft % 60).toString().padLeft(2, '0');
    return '$m:$s';
  }

  @override
  void dispose() {
    _ticker?.cancel();
    _resendTap.dispose();
    _codeFocus.dispose();
    _code.dispose();
    super.dispose();
  }

  bool get _canSubmit => _code.text.trim().length >= 4 && !_busy;

  Future<void> _submit() async {
    final code = _code.text.trim();
    if (code.isEmpty) {
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(authControllerProvider.notifier).verifyOtp(code: code);
      if (!mounted) {
        return;
      }
      if (ref.read(authControllerProvider) is AuthStateSignedIn) {
        // D-374 — an incomplete profile goes to the add-profile stage first.
        routeAfterAuth(context, ref);
      }
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final authState = ref.watch(authControllerProvider);
    final email =
        authState is AuthStateAwaitingOtp ? authState.email : null;
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          // Decorative diagonal sweep (the shared OTP-frame backdrop).
          Positioned(
            top: -180,
            right: -80,
            child: Transform.rotate(
              angle: 0.4936, // 28.28°
              child: Container(
                width: 313,
                height: 323,
                decoration: BoxDecoration(
                  color: _sweepTint,
                  borderRadius: BorderRadius.circular(40),
                ),
              ),
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                // Header band: chevron left, centred title.
                SizedBox(
                  height: 56,
                  child: Stack(
                    alignment: Alignment.center,
                    children: <Widget>[
                      Align(
                        alignment: Alignment.centerLeft,
                        child: Padding(
                          padding: const EdgeInsets.only(left: 8),
                          child: IconButton(
                            onPressed: _busy ? null : _back,
                            icon: const SimfSvgIcon(
                              'assets/icons/ic_back.svg',
                              size: 24,
                              color: Colors.white,
                            ),
                          ),
                        ),
                      ),
                      Text(
                        // Frame 758:2616 header — "التحقق بالبريد".
                        l10n.otpHeaderTitle,
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 24,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 400),
                      child: Column(
                        children: <Widget>[
                          const SizedBox(height: 48),
                          const OtpMark(icon: Icons.mail_outline),
                          const SizedBox(height: 24),
                          Text(
                            l10n.enterOtpTitle,
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 20,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 24),
                          // Frame 758:2616 — "أرسلنا رمزاً الى" + the recipient
                          // email on a gold line (falls back to the generic
                          // sentence when the address isn't carried).
                          if (email != null && email.isNotEmpty) ...<Widget>[
                            Text(
                              l10n.otpSentToPrefix,
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                color: SimfTokens.beigeBorder,
                                fontSize: 14,
                              ),
                            ),
                            const SizedBox(height: 8),
                            Text(
                              email,
                              textAlign: TextAlign.center,
                              textDirection: TextDirection.ltr,
                              style: const TextStyle(
                                color: SimfTokens.accent,
                                fontSize: 14,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                          ] else
                            Text(
                              l10n.otpBody,
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                color: SimfTokens.beigeBorder,
                                fontSize: 14,
                              ),
                            ),
                          const SizedBox(height: 64),
                          OtpCodeBoxes(
                            controller: _code,
                            focusNode: _codeFocus,
                            enabled: !_busy,
                            onChanged: () => setState(() {}),
                            onSubmitted: () {
                              if (_canSubmit) {
                                unawaited(_submit());
                              }
                            },
                          ),
                          const SizedBox(height: 16),
                          // The resend countdown (frame 758:2616).
                          Text.rich(
                            TextSpan(
                              children: <InlineSpan>[
                                TextSpan(text: '${l10n.otpResendCountdown} '),
                                TextSpan(
                                  text: _countdownLabel,
                                  style: const TextStyle(
                                    color: SimfTokens.accent,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ],
                            ),
                            style: const TextStyle(
                              color: SimfTokens.beigeBorder,
                              fontSize: 14,
                            ),
                          ),
                          if (_error != null) ...<Widget>[
                            const SizedBox(height: 12),
                            Text(
                              _error!,
                              textAlign: TextAlign.center,
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
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  child: SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: _canSubmit ? () => unawaited(_submit()) : null,
                      child: _busy
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : Text(
                              l10n.verifyButton,
                              style: const TextStyle(
                                fontSize: 16,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                    ),
                  ),
                ),
                const SizedBox(height: 16),
                // Resend row (frame 758:2616). A 2FA OTP can only be re-issued
                // by re-authenticating (the password isn't on this screen), so
                // "إعادة الإرسال" returns to sign-in once the countdown ends.
                Text.rich(
                  TextSpan(
                    children: <InlineSpan>[
                      TextSpan(text: '${l10n.otpDidntReceive} '),
                      TextSpan(
                        text: l10n.otpResendAction,
                        style: TextStyle(
                          color: _secondsLeft == 0
                              ? SimfTokens.accent
                              : SimfTokens.beigeBorder,
                          fontWeight: FontWeight.w700,
                          decoration: TextDecoration.underline,
                        ),
                        recognizer: _secondsLeft == 0 ? _resendTap : null,
                      ),
                    ],
                  ),
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 14,
                  ),
                ),
                const SizedBox(height: 24),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
