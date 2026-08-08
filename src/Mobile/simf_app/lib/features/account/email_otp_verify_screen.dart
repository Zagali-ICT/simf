import 'dart:async';

import 'package:flutter/gestures.dart' show TapGestureRecognizer;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_app/features/account/post_auth_route.dart';
import 'package:simf_app/features/account/widgets/account_sub_header.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';
import 'package:simf_app/features/account/widgets/otp_code_boxes.dart';
import 'package:simf_app/features/account/widgets/otp_countdown_line.dart';
import 'package:simf_app/features/account/widgets/otp_sent_to.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Page 003 — email-OTP second factor (Logic L-5), restyled to the KSA OTP
/// frame (D-369, reusing the D-364 pattern via [OtpCodeBoxes]/[OtpMark]; the
/// previous screen is parked in `_legacy_mockup/`). Reached after sign-in
/// when the account has 2FA on; the controller holds the `otpToken`. The user
/// enters the emailed code and the app calls `POST /app/auth/verify-otp`.
/// Visitor-only (no TOTP path). A resend countdown shows below the boxes; once
/// it elapses, "إعادة الإرسال" re-issues the code **in place** via
/// `POST /app/auth/resend-otp` (#12 — keyed by the ticket, no re-authentication)
/// and restarts the countdown. Frame 758:2616.
///
/// Clean-code pass (D-552, Phase 3): the lone sweep-tint const dropped for
/// `SimfTokens.surfaceTint`; the long `build` split into the shared
/// [AccountSubHeader] (D-658) / `_buildContent` / `_buildSubmitButton` /
/// `_buildResendRow`; the body + CTA
/// capped by [MaxWidthBody]. Behaviour + render unchanged — the 758:2616 golden
/// locks it.
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

  // Frame 758:2616 — the resend countdown ("إعادة الإرسال خلال 01:59"). Two
  // minutes (D-695); the server's ResendOtpResponse.cooldownSeconds overrides it
  // after a resend — the on-entry value has no server response yet.
  static const int _resendSeconds = 120;
  int _secondsLeft = _resendSeconds;
  Timer? _ticker;
  // Owned once (not rebuilt each tick) so it is disposed cleanly.
  late final TapGestureRecognizer _resendTap;

  @override
  void initState() {
    super.initState();
    // The focused-box highlight follows the hidden field's focus.
    _codeFocus.addListener(() => setState(() {}));
    _resendTap = TapGestureRecognizer()..onTap = () => unawaited(_resend());
    _startCountdown();
  }

  void _startCountdown({int? seconds}) {
    _ticker?.cancel();
    setState(() => _secondsLeft = seconds ?? _resendSeconds);
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

  bool get _canSubmit => _code.text.trim().length == 6 && !_busy;

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
        // Offer Face-ID enrolment on the OTP path too (D-441) — closes the gap
        // where only the direct password sign-in enrolled. D-374 — an
        // incomplete profile goes to the add-profile stage first.
        await maybeOfferBiometricEnrolment(context, ref);
        if (!mounted) {
          return;
        }
        routeAfterAuth(context, ref);
      }
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

  /// #12 — re-issue the emailed code in place (the controller keeps the ticket),
  /// restart the cooldown, and toast. A rate-limit / failure surfaces inline.
  Future<void> _resend() async {
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      // D-695 — restart from the server's advised cooldown (falls back to the
      // 2-minute default when the response omits it).
      final cooldown =
          await ref.read(authControllerProvider.notifier).resendOtp();
      if (!mounted) {
        return;
      }
      _startCountdown(seconds: cooldown > 0 ? cooldown : _resendSeconds);
      messenger.showSnackBar(SnackBar(content: Text(l10n.otpResentToast)));
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
    final authState = ref.watch(authControllerProvider);
    final email = authState is AuthStateAwaitingOtp ? authState.email : null;
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          const SimfAuthSweep(top: -180, left: null, right: -80),
          SafeArea(
            child: Column(
              children: <Widget>[
                AccountSubHeader(
                  title: l10n.otpHeaderTitle,
                  onBack: _back,
                  busy: _busy,
                ),
                Expanded(child: _buildContent(l10n, email)),
                _buildSubmitButton(l10n),
                const SizedBox(height: SimfTokens.space4),
                _buildResendRow(l10n),
                const SizedBox(height: SimfTokens.space6),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildContent(AppL10n l10n, String? email) {
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(
        maxWidth: SimfTokens.emailOtpVerifyScreenMaxWidth,
        child: Column(
          children: <Widget>[
            const SizedBox(height: SimfTokens.emailOtpVerifyScreenHeightSm),
            const OtpMark(icon: Icons.mail_outline),
            const SizedBox(height: SimfTokens.space6),
            Text(
              l10n.enterOtpTitle,
              style: SimfTokens.labelWhiteBoldXl,
            ),
            const SizedBox(height: SimfTokens.space6),
            // Frame 758:2616 — "أرسلنا رمزاً الى" + the recipient
            // email on a gold line (falls back to the generic
            // sentence when no address is carried).
            OtpSentTo(
              prefix: l10n.otpSentToPrefix,
              recipient: email,
              fallback: l10n.otpBody,
            ),
            const SizedBox(height: SimfTokens.emailOtpVerifyScreenHeightMd),
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
            const SizedBox(height: SimfTokens.space4),
            // The resend countdown (frame 758:2616).
            OtpCountdownLine(
              prefix: l10n.otpResendCountdown,
              remaining: _countdownLabel,
            ),
            if (_error != null) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              Text(
                _error!,
                textAlign: TextAlign.center,
                style: SimfTokens.labelDangerSm,
              ),
            ],
            const SizedBox(height: SimfTokens.space6),
          ],
        ),
      ),
    );
  }

  Widget _buildSubmitButton(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(
        maxWidth: SimfTokens.emailOtpVerifyScreenMaxWidth,
        child: SizedBox(
          width: double.infinity,
          child: AuthSubmitButton(
            label: l10n.verifyButton,
            busy: _busy,
            onPressed: _canSubmit ? () => unawaited(_submit()) : null,
          ),
        ),
      ),
    );
  }

  /// Resend row (frame 758:2616). When the countdown ends, the underlined
  /// "إعادة الإرسال" re-issues the code in place (resend-otp, #12) — it does
  /// not return to sign-in.
  Widget _buildResendRow(AppL10n l10n) {
    // Gate on !_busy as well as the countdown, so the resend can't fire a second
    // request on top of an in-flight verify (matches the sibling OTP screens).
    final canResend = _secondsLeft == 0 && !_busy;
    return Text.rich(
      TextSpan(
        children: <InlineSpan>[
          TextSpan(text: '${l10n.otpDidntReceive} '),
          TextSpan(
            text: l10n.otpResendAction,
            style: TextStyle(
              color: canResend ? SimfTokens.accent : SimfTokens.beigeBorder,
              fontWeight: FontWeight.w700,
              decoration: TextDecoration.underline,
            ),
            recognizer: canResend ? _resendTap : null,
          ),
        ],
      ),
      textAlign: TextAlign.center,
      style: SimfTokens.bodyWhiteMd,
    );
  }
}
