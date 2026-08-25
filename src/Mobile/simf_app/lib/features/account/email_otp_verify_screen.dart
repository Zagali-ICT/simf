import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_app/features/account/post_auth_route.dart';
import 'package:simf_app/features/account/widgets/auth_bottom_bar.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';
import 'package:simf_app/features/account/widgets/auth_screen_scaffold.dart';
import 'package:simf_app/features/account/widgets/auth_scroll_body.dart';
import 'package:simf_app/features/account/widgets/otp_code_boxes.dart';
import 'package:simf_app/features/account/widgets/otp_countdown_line.dart';
import 'package:simf_app/features/account/widgets/otp_sent_to.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Email-OTP second factor — route: RouteNames.verifyOtp · Figma 758:2616
/// (D-369)
/// Contract: Logic L-5, visitor-only (no TOTP path). The controller holds the
/// `otpToken`; resend re-issues the code IN PLACE via POST /app/auth/resend-otp
/// (#12 — keyed by the ticket, no re-authentication) and restarts the
/// countdown.
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
  // minutes (D-695); the server's ResendOtpResponse.cooldownSeconds overrides
  // it after a resend — the on-entry value has no server response yet.
  static const int _resendSeconds = 120;
  int _secondsLeft = _resendSeconds;
  Timer? _ticker;

  @override
  void initState() {
    super.initState();
    // The focused-box highlight follows the hidden field's focus.
    _codeFocus.addListener(() => setState(() {}));
    if (_hasOtpTicket) {
      _startCountdown(notify: false);
    } else {
      _secondsLeft = 0;
    }
  }

  void _startCountdown({int? seconds, bool notify = true}) {
    _ticker?.cancel();
    final cooldown = seconds ?? _resendSeconds;
    final next = cooldown <= 0 ? 0 : cooldown;
    if (notify && mounted) {
      setState(() => _secondsLeft = next);
    } else {
      _secondsLeft = next;
    }
    if (next == 0) {
      return;
    }
    _ticker = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }
      setState(() {
        _secondsLeft = _secondsLeft <= 1 ? 0 : _secondsLeft - 1;
      });
      if (_secondsLeft == 0) {
        timer.cancel();
      }
    });
  }

  String get _countdownLabel => formatCountdown(_secondsLeft);

  @override
  void dispose() {
    _ticker?.cancel();
    _codeFocus.dispose();
    _code.dispose();
    super.dispose();
  }

  bool get _hasOtpTicket =>
      ref.read(authControllerProvider) is AuthStateAwaitingOtp;

  bool get _canSubmit =>
      _code.text.trim().length == 6 && !_busy && _hasOtpTicket;

  Future<void> _submit() async {
    final code = _code.text.trim();
    if (code.isEmpty) {
      return;
    }
    final l10n = AppL10n.of(context);
    if (!_hasOtpTicket) {
      setState(() => _error = l10n.otpSessionExpired);
      return;
    }
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

  /// #12 — re-issue the emailed code in place (the controller keeps the
  /// ticket), restart the cooldown, and toast. A rate-limit / failure surfaces
  /// inline.
  Future<void> _resend() async {
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    if (!_hasOtpTicket) {
      _ticker?.cancel();
      setState(() {
        _secondsLeft = 0;
        _error = l10n.otpSessionExpired;
      });
      return;
    }
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
      if (cooldown <= 0) {
        _ticker?.cancel();
        setState(() {
          _secondsLeft = 0;
          _error = l10n.otpSessionExpired;
        });
        return;
      }
      _startCountdown(seconds: cooldown);
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
    final hasOtpTicket = authState is AuthStateAwaitingOtp;
    final email = hasOtpTicket ? authState.email : null;
    final errorText = _error ?? (hasOtpTicket ? null : l10n.otpSessionExpired);
    return AuthScreenScaffold(
      title: l10n.otpHeaderTitle,
      onBack: _back,
      busy: _busy,
      sweep: true,
      body: AuthScrollBody(
        maxWidth: SimfTokens.emailOtpVerifyScreenMaxWidth,
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
          if (errorText != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space3),
            Text(
              errorText,
              textAlign: TextAlign.center,
              style: SimfTokens.labelDangerSm,
            ),
          ],
          const SizedBox(height: SimfTokens.space6),
        ],
      ),
      bottom: <Widget>[
        AuthBottomBar(
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
        const SizedBox(height: SimfTokens.space4),
        _buildResendRow(l10n, hasOtpTicket: hasOtpTicket),
        const SizedBox(height: SimfTokens.space6),
      ],
    );
  }

  /// Resend row (frame 758:2616). When the countdown ends, the underlined
  /// "إعادة الإرسال" re-issues the code in place (resend-otp, #12) — it does
  /// not return to sign-in.
  Widget _buildResendRow(AppL10n l10n, {required bool hasOtpTicket}) {
    // Gate on !_busy as well as the countdown, so the resend can't fire a
    // second request on top of an in-flight verify (matches the sibling OTP
    // screens).
    final canResend = hasOtpTicket && _secondsLeft == 0 && !_busy;
    return Wrap(
      alignment: WrapAlignment.center,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: <Widget>[
        Text('${l10n.otpDidntReceive} ', style: SimfTokens.bodyWhiteMd),
        TextButton(
          onPressed: canResend ? () => unawaited(_resend()) : null,
          style: TextButton.styleFrom(
            foregroundColor: SimfTokens.accent,
            disabledForegroundColor: SimfTokens.beigeBorder,
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
            minimumSize: const Size(48, 40),
            tapTargetSize: MaterialTapTargetSize.shrinkWrap,
            textStyle: SimfTokens.bodyWhiteMd.copyWith(
              fontWeight: FontWeight.w700,
              decoration: TextDecoration.underline,
            ),
          ),
          child: Text(l10n.otpResendAction),
        ),
      ],
    );
  }
}
