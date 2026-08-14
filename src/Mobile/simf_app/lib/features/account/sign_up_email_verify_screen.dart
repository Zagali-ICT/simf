import 'dart:async';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/widgets/account_sub_header.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';
import 'package:simf_app/features/account/widgets/otp_code_boxes.dart';
import 'package:simf_app/features/account/widgets/otp_sent_to.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Page 006 — التحقق بالبريد · Email verification. The KSA-Project Figma
/// design (node 505:837 — D-364): navy surface with the decorative sweep, a
/// gold-ringed mail mark, six segmented code boxes, the gold تحقق button and
/// the "لم يصلك الرمز؟ إعادة الإرسال" footer. The previous screen is parked in
/// `_legacy_mockup/`.
///
/// Contract unchanged — sign-up **step 2**: `POST /app/auth/verify-email
/// { email, code }` (anonymous); success routes to sign-in (verify-email
/// issues no session). **Resend** re-issues via `POST /app/auth/resend-code`
/// and restarts the cooldown from the returned `codeExpiresInSeconds`.
///
/// Clean-code pass (D-553, Phase 3): the lone sweep-tint const dropped for
/// `SimfTokens.surfaceTint`; the long `build` split into the shared
/// [AccountSubHeader] (D-658) / `_buildContent` / `_buildBottomActions`; the
/// resend link reuses the shared
/// [authLinkButtonStyle]; the body + actions capped by [MaxWidthBody].
/// Behaviour + render unchanged — the 505:837 golden locks it.
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
  final FocusNode _codeFocus = FocusNode();
  bool _busy = false;
  String? _error;
  int _cooldown = 0;
  Timer? _timer;

  // The resend cooldown, 2 minutes (D-695). A fixed client cooldown — NOT the
  // code lifetime the resend endpoint returns (600s); the button just paces
  // re-requests, matching the 2FA OTP screen's rhythm.
  static const int _resendCooldownSeconds = 120;

  @override
  void initState() {
    super.initState();
    // The focused-box highlight follows the hidden field's focus.
    _codeFocus.addListener(() => setState(() {}));
    // D-695 — show the countdown from entry (the code was just sent), so resend
    // is disabled until it elapses, like the 2FA OTP screen.
    _startCooldown(_resendCooldownSeconds);
  }

  @override
  void dispose() {
    _timer?.cancel();
    _codeFocus.dispose();
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
      // The account is verified — prefill THIS address on the sign-in screen
      // the user lands on next, so login shows the email they just registered,
      // not a stale one. Native only: the web PoC shares kiosk localStorage
      // (D-384), matching the sign-in screen's own remember-email gate.
      if (!kIsWeb) {
        await ref
            .read(simfPrefsStorageProvider)
            .setString(StorageKeys.lastEmail, widget.email);
        if (!mounted) {
          return;
        }
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
        _error = failure.source.localizedMessage(l10n);
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
      await ref.read(authControllerProvider.notifier).resendCode(
            email: widget.email,
          );
      if (!mounted) {
        return;
      }
      // D-695 — a fixed 2-minute cooldown; the returned value is the code
      // LIFETIME (600s), not a button cooldown, so we do not use it here.
      _startCooldown(_resendCooldownSeconds);
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
    context.goNamed(RouteNames.signUpForm);
  }

  String _formatCooldown(int seconds) => formatCountdown(seconds);

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
                  title: l10n.emailVerifyTitle,
                  onBack: _back,
                  busy: _busy,
                ),
                Expanded(child: _buildContent(l10n)),
                _buildBottomActions(l10n),
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
        maxWidth: SimfTokens.signUpEmailVerifyScreenMaxWidth,
        child: Column(
          children: <Widget>[
            const SizedBox(height: SimfTokens.signUpEmailVerifyScreenHeight),
            // Gold-ringed mail mark (Figma 505:969).
            const OtpMark(icon: Icons.mail_outline),
            const SizedBox(height: SimfTokens.space6),
            Text(
              l10n.enterOtpTitle,
              style: SimfTokens.labelWhiteBoldXl,
            ),
            const SizedBox(height: SimfTokens.space6),
            OtpSentTo(
              prefix: l10n.emailVerifySentTo,
              recipient: widget.email,
            ),
            const SizedBox(height: SimfTokens.signUpEmailVerifyScreenHeight),
            OtpCodeBoxes(
              controller: _code,
              focusNode: _codeFocus,
              enabled: !_busy,
              onChanged: () => setState(() {}),
              onSubmitted: () {
                if (_canVerify) {
                  unawaited(_verify());
                }
              },
            ),
            const SizedBox(height: SimfTokens.space4),
            if (_cooldown > 0) _buildCooldownRow(l10n),
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

  Widget _buildCooldownRow(AppL10n l10n) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Text(
          l10n.resendInLabel,
          style: const TextStyle(color: otpMutedBlue, fontSize: SimfTokens.textMd),
        ),
        const SizedBox(width: SimfTokens.signUpEmailVerifyScreenWidth),
        Text(
          _formatCooldown(_cooldown),
          textDirection: TextDirection.ltr,
          style: SimfTokens.labelGoldBold,
        ),
      ],
    );
  }

  /// Bottom actions (Figma 505:1003): the gold verify CTA + the resend row.
  Widget _buildBottomActions(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(
        maxWidth: SimfTokens.signUpEmailVerifyScreenMaxWidth,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            AuthSubmitButton(
              label: l10n.verifyButton,
              busy: _busy,
              onPressed: _canVerify ? () => unawaited(_verify()) : null,
            ),
            const SizedBox(height: SimfTokens.space4),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(
                  l10n.noCodeQuestion,
                  style: const TextStyle(
                    color: SimfTokens.surface,
                    fontSize: SimfTokens.textMd,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(width: SimfTokens.signUpEmailVerifyScreenWidth),
                TextButton(
                  onPressed: _canResend ? () => unawaited(_resend()) : null,
                  style: authLinkButtonStyle(SimfTokens.accent),
                  child: Text(
                    l10n.resendAction,
                    style: const TextStyle(
                      fontSize: SimfTokens.textMd,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
