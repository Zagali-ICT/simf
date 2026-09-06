import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/widgets/auth_bottom_bar.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';
import 'package:simf_app/features/account/widgets/auth_screen_scaffold.dart';
import 'package:simf_app/features/account/widgets/auth_scroll_body.dart';
import 'package:simf_app/features/account/widgets/otp_code_boxes.dart';
import 'package:simf_app/features/account/widgets/otp_countdown_line.dart';
import 'package:simf_app/features/account/widgets/otp_sent_to.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Delete-account confirmation — route: RouteNames.deleteAccountCode.
///
/// Contract: erasure is irreversible, so it takes an emailed code the way
/// enrolling a credential does. The screen SUBMITS the deletion rather than
/// handing a verified code back, because there is no verify-only endpoint: the
/// code is checked inside the DELETE. Popping back to the tile on every wrong
/// digit would re-enter here and send a fresh code, and five sends an hour is
/// the cap - four typos would lock the holder out of deleting for an hour.
class DeleteAccountCodeScreen extends ConsumerStatefulWidget {
  const DeleteAccountCodeScreen({required this.erase, super.key});

  /// The tile's own erase call, handed over so the tile stays the thing that
  /// knows what deleting means and this screen stays reusable. Null after an
  /// Android process-death restore drops the route's extra, in which case the
  /// screen refuses rather than guessing at a repository.
  final Future<void> Function(String code)? erase;

  @override
  ConsumerState<DeleteAccountCodeScreen> createState() =>
      _DeleteAccountCodeScreenState();
}

class _DeleteAccountCodeScreenState
    extends ConsumerState<DeleteAccountCodeScreen> {
  final TextEditingController _code = TextEditingController();
  final FocusNode _codeFocus = FocusNode();
  bool _sending = false;
  bool _deleting = false;
  String? _error;
  String? _maskedEmail;

  static const int _resendSeconds = 60;
  int _secondsLeft = _resendSeconds;
  Timer? _ticker;

  @override
  void initState() {
    super.initState();
    _codeFocus.addListener(() => setState(() {}));
    if (widget.erase != null) {
      unawaited(_send());
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // l10n needs an inherited widget, so the restore message is resolved here
    // rather than in initState.
    if (widget.erase == null && _error == null) {
      _error = AppL10n.of(context).deleteAccountCodeUnavailable;
    }
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

  String get _countdownLabel => formatCountdown(_secondsLeft);

  bool get _canSubmit =>
      widget.erase != null && _code.text.trim().length == 6 && !_deleting;

  /// Asks the server to email a code, then shows the masked recipient and
  /// restarts the countdown. Kicked off from initState, so it must not read
  /// inherited widgets before the first await.
  Future<void> _send() async {
    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      final sent = await ref.read(profileRepositoryProvider).sendDeletionCode();
      if (!mounted) {
        return;
      }
      setState(() => _maskedEmail = sent.maskedEmail);
      _startCountdown();
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      final l10n = AppL10n.of(context);
      setState(() => _error = _sendError(failure, l10n));
    } finally {
      if (mounted) {
        setState(() => _sending = false);
      }
    }
  }

  String _sendError(ApiFailure failure, AppL10n l10n) {
    if (failure.code == ApiErrorCodes.rateLimitExceeded) {
      return l10n.deleteAccountCodeTooMany;
    }
    final message = failure.localizedMessage(l10n).trim();
    return message.isEmpty ? l10n.deleteAccountCodeSendFailed : message;
  }

  /// Submits the deletion. A refusal keeps the holder HERE with the reason, so
  /// a mistyped digit costs one attempt rather than a whole new code.
  Future<void> _submit() async {
    final code = _code.text.trim();
    final erase = widget.erase;
    if (code.isEmpty || erase == null) {
      return;
    }
    setState(() {
      _deleting = true;
      _error = null;
    });
    try {
      await erase(code);
      if (!mounted) {
        return;
      }
      context.pop(true);
      return;
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      final l10n = AppL10n.of(context);
      setState(() => _error = failure.localizedMessage(l10n));
      // An expired code is not a typo: free the resend immediately rather than
      // showing "request a new one" against a timer that has not run down.
      if (failure.code == _expiredCode) {
        _ticker?.cancel();
        setState(() => _secondsLeft = 0);
      }
    } finally {
      if (mounted) {
        setState(() => _deleting = false);
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
    return AuthScreenScaffold(
      title: l10n.deleteAccountCodeTitle,
      onBack: _back,
      busy: _deleting,
      sweep: true,
      body: AuthScrollBody(
        maxWidth: SimfTokens.biometricStepUpScreenMaxWidth,
        children: <Widget>[
          const SizedBox(height: SimfTokens.biometricStepUpScreenHeight),
          const OtpMark(icon: Icons.delete_outline),
          const SizedBox(height: SimfTokens.space6),
          Text(
            l10n.deleteAccountCodeHeading,
            textAlign: TextAlign.center,
            style: SimfTokens.labelWhiteBoldXl,
          ),
          const SizedBox(height: SimfTokens.space6),
          OtpSentTo(
            prefix: l10n.otpSentToPrefix,
            recipient: _maskedEmail ?? '',
            fallback: l10n.deleteAccountCodeBody,
          ),
          const SizedBox(height: SimfTokens.space6),
          OtpCodeBoxes(
            controller: _code,
            focusNode: _codeFocus,
            enabled: !_deleting && widget.erase != null,
            onChanged: () => setState(() {}),
            onSubmitted: () {
              if (_canSubmit) {
                unawaited(_submit());
              }
            },
          ),
          const SizedBox(height: SimfTokens.space4),
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
      bottom: <Widget>[
        AuthBottomBar(
          maxWidth: SimfTokens.biometricStepUpScreenMaxWidth,
          child: SizedBox(
            width: double.infinity,
            // This button IS the irreversible delete, so it carries the
            // destructive wording rather than a neutral "Verify".
            child: AuthSubmitButton(
              label: l10n.deleteAccountConfirmAction,
              busy: _deleting,
              onPressed: _canSubmit ? () => unawaited(_submit()) : null,
            ),
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        _buildResendRow(l10n),
        const SizedBox(height: SimfTokens.space6),
      ],
    );
  }

  Widget _buildResendRow(AppL10n l10n) {
    final canResend =
        _secondsLeft == 0 && !_sending && !_deleting && widget.erase != null;
    return Center(
      child: TextButton(
        onPressed: canResend ? () => unawaited(_send()) : null,
        child: Text(
          '${l10n.otpDidntReceive} ${l10n.otpResendAction}',
          textAlign: TextAlign.center,
          style: SimfTokens.bodyGreySm.copyWith(
            color: canResend ? SimfTokens.accent : SimfTokens.greyText,
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
    );
  }
}

const String _expiredCode = 'ACCOUNT_DELETION_CODE_EXPIRED';
