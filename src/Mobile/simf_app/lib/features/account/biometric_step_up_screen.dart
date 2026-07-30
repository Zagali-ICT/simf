import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../core/errors/api_error_l10n.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/widgets/simf_auth_sweep.dart';
import 'biometric_auth.dart';
import 'widgets/account_sub_header.dart';
import 'widgets/auth_chrome.dart';
import 'widgets/otp_code_boxes.dart';

/// #7a / D-738 — the banking-standard step-up confirming the user wants to
/// ENABLE biometric (Face-ID) sign-in. Reached (pushed) from the Face-ID toggle
/// and the post-sign-in enrol nudge. On open it requests an emailed code
/// (`POST /app/auth/device-keys/step-up`); entering it then requires an **OS
/// device-credential confirmation** (device PIN / biometric via `local_auth`)
/// before the device key is enrolled (`POST /app/auth/device-keys` with the
/// code). Two factors gate enrolment — email possession AND custody of the
/// unlocked device — so neither an emailed code alone nor a borrowed unlocked
/// phone alone can bind a biometric credential. Restyled to the shared KSA OTP
/// frame (D-369) via [OtpCodeBoxes]/[OtpMark], like the sign-in second factor.
///
/// Clean-code pass (D-554, Phase 3 — unbound auth screen, render preserved):
/// the lone sweep-tint const dropped for `SimfTokens.surfaceTint`; the long
/// `build` split into the shared [AccountSubHeader] (D-658) / `_buildContent` /
/// `_buildSubmitButton` / `_buildResendRow`; the body + CTA capped by
/// [MaxWidthBody].
class BiometricStepUpScreen extends ConsumerStatefulWidget {
  const BiometricStepUpScreen({super.key});

  @override
  ConsumerState<BiometricStepUpScreen> createState() =>
      _BiometricStepUpScreenState();
}

class _BiometricStepUpScreenState extends ConsumerState<BiometricStepUpScreen> {
  final TextEditingController _code = TextEditingController();
  final FocusNode _codeFocus = FocusNode();
  bool _verifying = false;
  bool _sending = false;
  String? _error;
  String? _maskedEmail;

  static const int _resendSeconds = 60;
  int _secondsLeft = _resendSeconds;
  Timer? _ticker;

  @override
  void initState() {
    super.initState();
    _codeFocus.addListener(() => setState(() {}));
    // Request the first code as the screen opens.
    unawaited(_send());
  }

  @override
  void dispose() {
    _ticker?.cancel();
    _codeFocus.dispose();
    _code.dispose();
    super.dispose();
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

  bool get _canSubmit => _code.text.trim().length == 6 && !_verifying;

  /// Asks the backend to email a step-up code; shows the masked recipient and
  /// (re)starts the resend countdown. A failure surfaces inline. Kicked off from
  /// initState, so it must not read inherited widgets (l10n) before the first
  /// await — the error text is resolved only after, in the catch.
  Future<void> _send() async {
    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      final masked =
          await ref.read(authControllerProvider.notifier).sendBiometricStepUp();
      if (!mounted) {
        return;
      }
      setState(() => _maskedEmail = masked);
      _startCountdown();
    } on AuthFailure catch (failure) {
      if (!mounted) {
        return;
      }
      final l10n = AppL10n.of(context);
      setState(() {
        _error = failure is NetworkUnavailable
            ? l10n.networkErrorBody
            : l10n.biometricStepUpSendFailed;
      });
    } finally {
      if (mounted) {
        setState(() => _sending = false);
      }
    }
  }

  /// Banking-standard enrolment (D-738): emailed OTP → **OS device-credential
  /// confirm** → enrol. The device-credential step proves custody of the
  /// unlocked device before a biometric credential is bound; on cancel/failure
  /// the code stays entered so the user can simply retry. The server then
  /// rejects a missing / wrong / expired OTP, surfaced inline.
  Future<void> _submit() async {
    final code = _code.text.trim();
    if (code.isEmpty) {
      return;
    }
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() {
      _verifying = true;
      _error = null;
    });
    final outcome = await ref
        .read(biometricAuthProvider)
        .confirmDeviceIdentity(l10n.biometricLocalConfirmReason);
    if (!mounted) {
      return;
    }
    if (outcome != LocalAuthOutcome.success) {
      setState(() {
        _verifying = false;
        // A user cancel maps to null; show the enrol-specific cancelled copy.
        _error = localizedBiometricError(l10n, outcome) ??
            l10n.biometricLocalConfirmCancelled;
      });
      return;
    }
    try {
      await ref
          .read(authControllerProvider.notifier)
          .enrolDeviceKey(stepUpCode: code);
      if (!mounted) {
        return;
      }
      // The toggle/nudge re-read this after the screen pops.
      ref.invalidate(biometricEnabledProvider);
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.biometricEnabledToast)),
      );
      if (context.canPop()) {
        context.pop();
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
        setState(() => _verifying = false);
      }
    }
  }

  void _back() {
    if (context.canPop()) {
      context.pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          const SimfAuthSweep(top: -180, left: null, right: -80),
          SafeArea(
            child: Column(
              children: <Widget>[
                AccountSubHeader(
                  title: l10n.biometricStepUpTitle,
                  onBack: _back,
                  busy: _verifying,
                ),
                Expanded(child: _buildContent(l10n)),
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

  Widget _buildContent(AppL10n l10n) {
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(
        maxWidth: 560,
        child: Column(
          children: <Widget>[
            const SizedBox(height: 48),
            const OtpMark(icon: Icons.fingerprint),
            const SizedBox(height: SimfTokens.space6),
            Text(
              l10n.biometricStepUpHeading,
              textAlign: TextAlign.center,
              style: SimfTokens.labelWhiteBoldXl,
            ),
            const SizedBox(height: SimfTokens.space6),
            if (_maskedEmail != null && _maskedEmail!.isNotEmpty) ...<Widget>[
              Text(
                l10n.otpSentToPrefix,
                textAlign: TextAlign.center,
                style: SimfTokens.bodyBeigeMd,
              ),
              const SizedBox(height: SimfTokens.space2),
              Text(
                _maskedEmail!,
                textAlign: TextAlign.center,
                textDirection: TextDirection.ltr,
                style: SimfTokens.labelGoldMedium,
              ),
            ],
            const SizedBox(height: 48),
            OtpCodeBoxes(
              controller: _code,
              focusNode: _codeFocus,
              enabled: !_verifying,
              onChanged: () => setState(() {}),
              onSubmitted: () {
                if (_canSubmit) {
                  unawaited(_submit());
                }
              },
            ),
            const SizedBox(height: SimfTokens.space4),
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
              style: SimfTokens.bodyBeigeMd,
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
        maxWidth: 560,
        child: SizedBox(
          width: double.infinity,
          child: AuthSubmitButton(
            label: l10n.verifyButton,
            busy: _verifying,
            onPressed: _canSubmit ? () => unawaited(_submit()) : null,
          ),
        ),
      ),
    );
  }

  /// Resend row — re-requests a code once the countdown ends.
  Widget _buildResendRow(AppL10n l10n) {
    final canResend = _secondsLeft == 0 && !_sending;
    return Text.rich(
      TextSpan(
        children: <InlineSpan>[
          TextSpan(text: '${l10n.otpDidntReceive} '),
          WidgetSpan(
            alignment: PlaceholderAlignment.middle,
            child: GestureDetector(
              onTap: canResend ? () => unawaited(_send()) : null,
              child: Text(
                l10n.otpResendAction,
                style: TextStyle(
                  color: canResend
                      ? SimfTokens.accent
                      : SimfTokens.beigeBorder,
                  fontWeight: FontWeight.w700,
                  decoration: TextDecoration.underline,
                ),
              ),
            ),
          ),
        ],
      ),
      textAlign: TextAlign.center,
      style: SimfTokens.bodyWhiteMd,
    );
  }
}
