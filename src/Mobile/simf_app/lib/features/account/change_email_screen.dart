import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../core/errors/api_error_l10n.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/validation/email_validation.dart';
import '../../core/validation/required_validation.dart';
import '../../core/widgets/simf_auth_sweep.dart';
import 'data/email_change_repository.dart';
import 'widgets/account_sub_header.dart';
import 'widgets/auth_chrome.dart';
import 'widgets/navi_form_field.dart';
import 'widgets/navy_password_toggle.dart';
import 'widgets/otp_code_boxes.dart';

/// Change email — تغيير البريد الإلكتروني · route: [RouteNames.changeEmail]
/// Purpose: signed-in self-service change of the login email (#24). Two phases in
///   one screen: (1) enter the new address → a code is emailed TO it; (2) enter
///   the code to confirm.
/// Data: [EmailChangeRepository] (`/app/auth/change-email/send-otp` + `/confirm`);
///   [authControllerProvider] for the current email + the forced sign-out.
/// Figma: no bound node — reuses the shared navy auth chrome (like biometric
///   step-up / reset-password, which have no dedicated frame either).
/// Perf: two lightweight forms, no lists.
/// Contract: confirm rolls the server security stamp + revokes sessions, so on
///   success the app MUST sign out and route to sign-in (mirrors reset-password).
class ChangeEmailScreen extends ConsumerStatefulWidget {
  const ChangeEmailScreen({super.key});

  @override
  ConsumerState<ChangeEmailScreen> createState() => _ChangeEmailScreenState();
}

enum _Phase { enterEmail, verifyCode }

class _ChangeEmailScreenState extends ConsumerState<ChangeEmailScreen> {
  final GlobalKey<FormState> _emailFormKey = GlobalKey<FormState>();
  final TextEditingController _newEmail = TextEditingController();
  final TextEditingController _code = TextEditingController();
  final TextEditingController _password = TextEditingController();
  final FocusNode _codeFocus = FocusNode();

  _Phase _phase = _Phase.enterEmail;
  bool _busy = false;
  bool _obscurePassword = true;
  String? _error;
  String _maskedEmail = '';

  int _resendSeconds = 60;
  int _secondsLeft = 0;
  Timer? _ticker;

  @override
  void dispose() {
    _ticker?.cancel();
    _newEmail.dispose();
    _code.dispose();
    _password.dispose();
    _codeFocus.dispose();
    super.dispose();
  }

  String get _currentEmail {
    final auth = ref.read(authControllerProvider);
    return auth is AuthStateSignedIn ? auth.session.user.email : '';
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

  bool get _canSubmit => _phase == _Phase.enterEmail
      ? _newEmail.text.trim().isNotEmpty && !_busy
      : _code.text.trim().length == 6 &&
          _password.text.isNotEmpty &&
          !_busy;

  /// Phase 1 — validate the new address and ask the server to email a code to it.
  /// Also the resend path: on resend the phase-1 Form is unmounted
  /// (`currentState == null`), and the address was already validated on the first
  /// send, so a null form is treated as valid and the same address is re-issued.
  Future<void> _sendCode() async {
    final l10n = AppL10n.of(context);
    if (_emailFormKey.currentState?.validate() == false) {
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final sent = await ref
          .read(emailChangeRepositoryProvider)
          .sendOtp(_newEmail.text.trim());
      if (!mounted) {
        return;
      }
      setState(() {
        _maskedEmail = sent.maskedNewEmail;
        _resendSeconds =
            sent.resendCooldownSeconds > 0 ? sent.resendCooldownSeconds : 60;
        _phase = _Phase.verifyCode;
      });
      _startCountdown();
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() => _error = failure.localizedMessage(l10n));
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  /// Re-request a code for the same new address once the countdown ends.
  Future<void> _resend() async {
    if (_secondsLeft != 0 || _busy) {
      return;
    }
    await _sendCode();
  }

  /// Phase 2 — confirm the code. On success the server rolls the security stamp +
  /// revokes sessions, so the old token is dead: sign out locally and go to
  /// sign-in for a genuine fresh login (mirrors reset-password, D-659).
  Future<void> _confirm() async {
    final code = _code.text.trim();
    if (code.length != 6 || _password.text.isEmpty) {
      return;
    }
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(emailChangeRepositoryProvider).confirm(
            newEmail: _newEmail.text.trim(),
            code: code,
            currentPassword: _password.text,
          );
      if (ref.read(authControllerProvider) is AuthStateSignedIn) {
        await ref.read(authControllerProvider.notifier).signOut();
      }
      if (!mounted) {
        return;
      }
      context.goNamed(RouteNames.signIn);
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.changeEmailSuccessToast)),
      );
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() => _error = failure.localizedMessage(l10n));
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  void _back() {
    if (_phase == _Phase.verifyCode) {
      setState(() {
        _phase = _Phase.enterEmail;
        _error = null;
        _code.clear();
        _password.clear();
      });
      return;
    }
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
                  title: l10n.changeEmailTitle,
                  onBack: _back,
                  busy: _busy,
                ),
                Expanded(
                  child: _phase == _Phase.enterEmail
                      ? _buildEmailPhase(l10n)
                      : _buildCodePhase(l10n),
                ),
                _buildSubmitButton(l10n),
                if (_phase == _Phase.verifyCode) ...<Widget>[
                  const SizedBox(height: 16),
                  _buildResendRow(l10n),
                ],
                const SizedBox(height: 24),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildEmailPhase(AppL10n l10n) {
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: MaxWidthBody(
        maxWidth: 560,
        child: Form(
          key: _emailFormKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              const SizedBox(height: 48),
              const Center(child: OtpMark(icon: Icons.alternate_email)),
              const SizedBox(height: 24),
              Text(
                l10n.changeEmailHeading,
                textAlign: TextAlign.center,
                style: SimfTokens.bodyBeige,
              ),
              const SizedBox(height: 32),
              Text(l10n.changeEmailCurrentLabel, style: SimfTokens.bodyBeige),
              const SizedBox(height: 8),
              Text(
                _currentEmail,
                textDirection: TextDirection.ltr,
                style: const TextStyle(
                  color: SimfTokens.accent,
                  fontSize: 15,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: 24),
              NaviFormField(
                label: l10n.changeEmailNewLabel,
                controller: _newEmail,
                enabled: !_busy,
                keyboardType: TextInputType.emailAddress,
                textDirection: TextDirection.ltr,
                maxLength: 256,
                validator: _validateNewEmail,
                onChanged: (_) => setState(() {}),
                onFieldSubmitted: (_) {
                  if (_canSubmit) {
                    unawaited(_sendCode());
                  }
                },
              ),
              if (_error != null) ...<Widget>[
                const SizedBox(height: 12),
                Text(
                  _error!,
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
    );
  }

  String? _validateNewEmail(String? value) {
    final l10n = AppL10n.of(context);
    if (isBlank(value)) {
      return l10n.requiredField;
    }
    final trimmed = value!.trim();
    if (!isValidEmail(trimmed)) {
      return l10n.invalidEmail;
    }
    if (trimmed.toLowerCase() == _currentEmail.trim().toLowerCase()) {
      return l10n.changeEmailSameAsCurrent;
    }
    return null;
  }

  Widget _buildCodePhase(AppL10n l10n) {
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: MaxWidthBody(
        maxWidth: 560,
        child: Column(
          children: <Widget>[
            const SizedBox(height: 48),
            const OtpMark(icon: Icons.mail_outline),
            const SizedBox(height: 24),
            Text(
              l10n.changeEmailVerifyHeading,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 20,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 24),
            if (_maskedEmail.isNotEmpty) ...<Widget>[
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
                _maskedEmail,
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
              enabled: !_busy,
              onChanged: () => setState(() {}),
              onSubmitted: () {
                if (_canSubmit) {
                  unawaited(_confirm());
                }
              },
            ),
            const SizedBox(height: 24),
            // Re-authenticate: the current password proves the account owner (not
            // just a held session) is making this identity change.
            NaviFormField(
              label: l10n.changeEmailPasswordLabel,
              controller: _password,
              enabled: !_busy,
              obscureText: _obscurePassword,
              maxLength: 128,
              suffixIcon: NavyPasswordToggle(
                obscure: _obscurePassword,
                onToggle: () =>
                    setState(() => _obscurePassword = !_obscurePassword),
              ),
              onChanged: (_) => setState(() {}),
              onFieldSubmitted: (_) {
                if (_canSubmit) {
                  unawaited(_confirm());
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
    final label = _phase == _Phase.enterEmail
        ? l10n.changeEmailSendButton
        : l10n.verifyButton;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: MaxWidthBody(
        maxWidth: 560,
        child: SizedBox(
          width: double.infinity,
          child: AuthSubmitButton(
            label: label,
            busy: _busy,
            onPressed: _canSubmit
                ? () => unawaited(
                      _phase == _Phase.enterEmail ? _sendCode() : _confirm(),
                    )
                : null,
          ),
        ),
      ),
    );
  }

  Widget _buildResendRow(AppL10n l10n) {
    final canResend = _secondsLeft == 0 && !_busy;
    return Text.rich(
      TextSpan(
        children: <InlineSpan>[
          TextSpan(text: '${l10n.otpDidntReceive} '),
          WidgetSpan(
            alignment: PlaceholderAlignment.middle,
            child: GestureDetector(
              onTap: canResend ? () => unawaited(_resend()) : null,
              child: Text(
                l10n.otpResendAction,
                style: TextStyle(
                  color:
                      canResend ? SimfTokens.accent : SimfTokens.beigeBorder,
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
