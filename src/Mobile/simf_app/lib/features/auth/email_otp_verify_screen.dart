import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import 'widgets/otp_code_boxes.dart';

const Color _sweepTint = Color(0x0AFFFFFF);

/// Page 003 — email-OTP second factor (Logic L-5), restyled to the KSA OTP
/// frame (D-369, reusing the D-364 pattern via [OtpCodeBoxes]/[OtpMark]; the
/// previous screen is parked in `_legacy_mockup/`). Reached after sign-in
/// when the account has 2FA on; the controller holds the `otpToken`. The user
/// enters the emailed code and the app calls `POST /app/auth/verify-otp`.
/// Visitor-only (no TOTP path); no resend on this step.
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

  @override
  void initState() {
    super.initState();
    // The focused-box highlight follows the hidden field's focus.
    _codeFocus.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
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
        context.go('/');
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
                            icon: const Icon(
                              Icons.arrow_back_ios_new,
                              color: Colors.white,
                              size: 20,
                              textDirection: TextDirection.ltr,
                            ),
                          ),
                        ),
                      ),
                      Text(
                        l10n.otpTitle,
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
                          Text(
                            l10n.otpBody,
                            textAlign: TextAlign.center,
                            style: const TextStyle(
                              color: SimfTokens.beigeBorder,
                              fontSize: 14,
                            ),
                          ),
                          const SizedBox(height: 48),
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
                const SizedBox(height: 24),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
