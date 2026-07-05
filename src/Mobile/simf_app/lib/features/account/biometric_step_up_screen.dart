import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/app_assets.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_svg_icon.dart';
import '../../core/errors/api_error_l10n.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/widgets/simf_auth_sweep.dart';
import 'biometric_auth.dart';
import 'widgets/otp_code_boxes.dart';

/// #7a — the emailed-OTP step-up confirming the user wants to ENABLE biometric
/// (Face-ID) sign-in. Reached (pushed) from the Face-ID toggle and the
/// post-sign-in enrol nudge. On open it requests a code
/// (`POST /app/auth/device-keys/step-up`); entering it enrols the device key
/// (`POST /app/auth/device-keys` with the code), which the server rejects
/// without a fresh code — so a borrowed-but-unlocked phone can't silently bind a
/// biometric credential. Restyled to the shared KSA OTP frame (D-369) via
/// [OtpCodeBoxes]/[OtpMark], like the sign-in second factor.
///
/// Clean-code frozen (D-554, Phase 3 — unbound auth screen, render preserved):
/// the lone sweep-tint const dropped for `SimfTokens.surfaceTint`; the long
/// `build` split into `_buildHeader` / `_buildContent` / `_buildSubmitButton` /
/// `_buildResendRow`; the body + CTA capped by [MaxWidthBody].
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

  /// Enrols the device key with the entered code. The server rejects a missing /
  /// wrong / expired code, which surfaces here as an inline error.
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
                _buildHeader(l10n),
                Expanded(child: _buildContent(l10n)),
                _buildSubmitButton(l10n),
                const SizedBox(height: 16),
                _buildResendRow(l10n),
                const SizedBox(height: 24),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildHeader(AppL10n l10n) {
    return SizedBox(
      height: 56,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          Align(
            alignment: Alignment.centerLeft,
            child: Padding(
              padding: const EdgeInsets.only(left: 8),
              child: IconButton(
                onPressed: _verifying ? null : _back,
                icon: const SimfSvgIcon(
                  AppAssets.icBack,
                  size: 24,
                  color: Colors.white,
                ),
              ),
            ),
          ),
          Text(
            l10n.biometricStepUpTitle,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 24,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildContent(AppL10n l10n) {
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: MaxWidthBody(
        maxWidth: 560,
        child: Column(
          children: <Widget>[
            const SizedBox(height: 48),
            const OtpMark(icon: Icons.fingerprint),
            const SizedBox(height: 24),
            Text(
              l10n.biometricStepUpHeading,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 20,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 24),
            if (_maskedEmail != null && _maskedEmail!.isNotEmpty) ...<Widget>[
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
                _maskedEmail!,
                textAlign: TextAlign.center,
                textDirection: TextDirection.ltr,
                style: const TextStyle(
                  color: SimfTokens.accent,
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                ),
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
            const SizedBox(height: 16),
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
    );
  }

  Widget _buildSubmitButton(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: MaxWidthBody(
        maxWidth: 560,
        child: SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: _canSubmit ? () => unawaited(_submit()) : null,
            child: _verifying
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
      style: const TextStyle(color: Colors.white, fontSize: 14),
    );
  }
}
