import 'dart:async';
import 'dart:io' show Platform;
import 'dart:typed_data';

import 'package:camera/camera.dart';
import 'package:flutter/foundation.dart'
    show defaultTargetPlatform, kDebugMode, kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show DeviceOrientation;
import 'package:google_mlkit_face_detection/google_mlkit_face_detection.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/liveness.dart';
import 'widgets/identity_capture_view.dart';
import 'widgets/identity_fallback_view.dart';

// The liveness step model + pure gate + prompt helper live in `data/liveness.dart`
// (unit-testable without a camera); re-exported so existing imports of this
// screen file keep resolving them.
export 'data/liveness.dart';

/// التحقق من الهوية — the guided face-capture / liveness screen (D-404, frames
/// 758:4180 → 758:4248 → 758:4316). A full-bleed navy screen with a live
/// front-camera preview and a prompt per step (ابتسم → أدر رأسك يمينًا → أدر
/// رأسك يسارًا). The user must actually smile, then turn right, then turn left;
/// the forward/smile frame is captured and returned as the new avatar selfie.
///
/// **Camera security rules (owner 2026-07-06, D-662):** the capture MUST verify
/// a live human via the liveness challenge and MUST use only a live camera
/// image — there is no gallery / manual-shutter path, so a static "studio"
/// photo can never be submitted. Where the live camera or the ML Kit face
/// detector is unavailable (web / test / no camera / permission denied / a
/// device without Google Play Services) the screen shows a "camera required"
/// message with a retry — never a gallery fallback.
class IdentityVerificationScreen extends StatefulWidget {
  const IdentityVerificationScreen({this.showConfirmation = false, super.key});

  final bool showConfirmation;

  @override
  State<IdentityVerificationScreen> createState() =>
      _IdentityVerificationScreenState();
}

class _IdentityVerificationScreenState
    extends State<IdentityVerificationScreen> {
  CameraController? _camera;
  FaceDetector? _detector;
  bool _processing = false;
  bool _cameraReady = false;
  bool _cameraFailed = false;

  // TEMP diagnostic (remove before merge, D-XXX): show the live headEulerAngleY
  // + its normalised value so the yaw-sign can be confirmed on-device in one
  // DEBUG build. Double-gated: it only renders when BOTH this flag is on AND
  // kDebugMode — so a release/production build never shows this security
  // control's internal telemetry. The readout rides its own ValueNotifier so
  // updating it does NOT rebuild the camera preview.
  static const bool _kShowYawDebug = true;
  final ValueNotifier<double?> _debugYaw = ValueNotifier<double?>(null);

  /// The challenge order is shuffled per session (D-422) so the sequence is not
  /// predictable — a fixed smile→right→left order is easier to defeat with a
  /// pre-recorded clip. The forward selfie is still grabbed on the smile step,
  /// wherever it lands in the order.
  late List<LivenessStep> _sequence;
  int _stepIndex = 0;
  LivenessStep get _step => _sequence[_stepIndex];
  Uint8List? _forwardFrame;
  String _forwardName = 'selfie.jpg';
  Uint8List? _preview;

  static const Map<DeviceOrientation, int> _orientationDegrees =
      <DeviceOrientation, int>{
    DeviceOrientation.portraitUp: 0,
    DeviceOrientation.landscapeLeft: 90,
    DeviceOrientation.portraitDown: 180,
    DeviceOrientation.landscapeRight: 270,
  };

  @override
  void initState() {
    super.initState();
    _sequence = List<LivenessStep>.of(LivenessStep.values)..shuffle();
    unawaited(_initCamera());
  }

  Future<void> _initCamera() async {
    if (kIsWeb) {
      // No live preview on web — show the "camera required" message (no gallery).
      setState(() => _cameraFailed = true);
      return;
    }
    try {
      final cameras = await availableCameras();
      final front = cameras.firstWhere(
        (c) => c.lensDirection == CameraLensDirection.front,
        orElse: () => cameras.first,
      );
      final controller = CameraController(
        front,
        ResolutionPreset.medium,
        enableAudio: false,
        imageFormatGroup: Platform.isAndroid
            ? ImageFormatGroup.nv21
            : ImageFormatGroup.bgra8888,
      );
      await controller.initialize();
      _detector = FaceDetector(
        options: FaceDetectorOptions(
          enableClassification: true,
          performanceMode: FaceDetectorMode.fast,
        ),
      );
      if (!mounted) {
        await controller.dispose();
        return;
      }
      _camera = controller;
      setState(() => _cameraReady = true);
      await controller.startImageStream(_onFrame);
    } catch (_) {
      // Camera / ML Kit unavailable or permission denied — show the "camera
      // required" message (no gallery: identity capture is live-image-only).
      if (mounted) {
        setState(() => _cameraFailed = true);
      }
    }
  }

  /// The active front camera's sensor orientation (degrees), used to normalise
  /// the ML Kit yaw sign per device (see [livenessInvertYaw]). Falls back to 0
  /// when the camera is not yet bound — only read while a frame or the overlay
  /// is live, so the fallback is never actually consumed.
  int get _frontCameraSensorOrientation =>
      _camera?.description.sensorOrientation ?? 0;

  Future<void> _onFrame(CameraImage image) async {
    if (_processing || !mounted) {
      return;
    }
    _processing = true;
    try {
      final input = _toInputImage(image);
      final detector = _detector;
      if (input == null || detector == null) {
        return;
      }
      final faces = await detector.processImage(input);
      if (faces.isEmpty || !mounted) {
        return;
      }
      final face = faces.first;
      if (_kShowYawDebug && kDebugMode) {
        // Update the readout's own notifier (a >=1° throttle); no setState, so
        // the camera preview is not rebuilt each frame.
        final yaw = face.headEulerAngleY;
        if (yaw != null &&
            (_debugYaw.value == null || (yaw - _debugYaw.value!).abs() >= 1)) {
          _debugYaw.value = yaw;
        }
      }
      if (!livenessStepSatisfied(
        _step,
        smilingProbability: face.smilingProbability,
        headEulerAngleY: face.headEulerAngleY,
        // The raw yaw sign for a physical turn depends on the platform AND the
        // front camera's sensor orientation (front-camera mirror + per-platform
        // input-image rotation); normalise so a positive yaw is always a
        // physical RIGHT turn.
        invertYaw: livenessInvertYaw(
          defaultTargetPlatform,
          _frontCameraSensorOrientation,
        ),
      )) {
        return;
      }
      await _advance();
    } catch (_) {
      // Transient frame error — ignore and keep streaming.
    } finally {
      _processing = false;
    }
  }

  /// Step passed — on the first (smile) step grab the forward selfie; advance to
  /// the next step, or finish on the last.
  Future<void> _advance() async {
    final controller = _camera;
    if (controller == null) {
      return;
    }
    if (_step == LivenessStep.smile && _forwardFrame == null) {
      try {
        await controller.stopImageStream();
        final shot = await controller.takePicture();
        _forwardFrame = await shot.readAsBytes();
        _forwardName = shot.name;
        await controller.startImageStream(_onFrame);
      } catch (_) {
        // The capture failed — the flow keeps verifying liveness; a null
        // forward frame is retaken from the live camera on finish.
      }
    }
    if (_stepIndex >= _sequence.length - 1) {
      await _finish();
      return;
    }
    if (mounted) {
      setState(() => _stepIndex++);
    }
  }

  /// Liveness passed. Return the live smile-frame; if it was not grabbed, take a
  /// final live shot now (the human is verified). There is NO gallery fallback —
  /// the identity photo must be a live camera image (owner 2026-07-06, D-662);
  /// if no live frame can be captured, show the "camera required" retry.
  Future<void> _finish() async {
    var bytes = _forwardFrame;
    final controller = _camera;
    if (bytes == null && controller != null) {
      try {
        if (controller.value.isStreamingImages) {
          await controller.stopImageStream();
        }
        final shot = await controller.takePicture();
        bytes = await shot.readAsBytes();
        _forwardName = shot.name;
      } catch (_) {
        // Fall through to the camera-required state below.
      }
    }
    await _stop();
    if (!mounted) {
      return;
    }
    if (bytes != null && widget.showConfirmation) {
      _preview = bytes;
      setState(() {});
      return;
    }
    if (bytes != null) {
      Navigator.of(context).pop<CapturedSelfie>(
        (bytes: bytes, filename: _forwardName),
      );
    } else {
      setState(() => _cameraFailed = true);
    }
  }

  void _retake() {
    _preview = null;
    _forwardFrame = null;
    _stepIndex = 0;
    _sequence = List<LivenessStep>.of(LivenessStep.values)..shuffle();
    unawaited(_initCamera());
  }

  /// Retry the live capture after the "camera required" message (e.g. once the
  /// camera permission is granted).
  void _retry() {
    setState(() {
      _cameraFailed = false;
      _cameraReady = false;
      _stepIndex = 0;
      _forwardFrame = null;
    });
    unawaited(_initCamera());
  }

  /// The command for the current liveness step (ابتسم / أدر رأسك لليمين / لليسار),
  /// shown big under the live preview so the user knows exactly what to do
  /// (D-683; over the Figma 758:4180 layout).
  ///
  /// The prompt always names the physical direction of the step — the
  /// per-platform yaw-sign difference is normalised in `livenessStepSatisfied`
  /// (`invertYaw`), NOT compensated here, so "turn right" reliably means a
  /// physical right turn on both iOS and Android (supersedes the D-684 / PR-103
  /// prompt swap).
  String _stepPrompt(AppL10n l10n) {
    switch (livenessPromptDirection(_step)) {
      case LivenessPromptDirection.none:
        return l10n.livenessSmilePrompt;
      case LivenessPromptDirection.right:
        return l10n.livenessTurnRightPrompt;
      case LivenessPromptDirection.left:
        return l10n.livenessTurnLeftPrompt;
    }
  }

  /// The directional cue for the current step: the 😊 emoji for the front step,
  /// a gold arrow for the right / left turns (Figma 758:4180 / 4248 / 4316). The
  /// arrow always matches the step's physical direction (see [_stepPrompt]).
  Widget _stepLeading() {
    switch (livenessPromptDirection(_step)) {
      case LivenessPromptDirection.none:
        return const Text('😊', style: TextStyle(fontSize: 30));
      case LivenessPromptDirection.right:
        return const Icon(Icons.east, color: SimfTokens.accent, size: 32);
      case LivenessPromptDirection.left:
        return const Icon(Icons.west, color: SimfTokens.accent, size: 32);
    }
  }

  InputImage? _toInputImage(CameraImage image) {
    final controller = _camera;
    if (controller == null) {
      return null;
    }
    final camera = controller.description;
    final sensor = camera.sensorOrientation;
    InputImageRotation? rotation;
    if (Platform.isIOS) {
      rotation = InputImageRotationValue.fromRawValue(sensor);
    } else {
      final deviceDegrees =
          _orientationDegrees[controller.value.deviceOrientation] ?? 0;
      final compensated = camera.lensDirection == CameraLensDirection.front
          ? (sensor + deviceDegrees) % 360
          : (sensor - deviceDegrees + 360) % 360;
      rotation = InputImageRotationValue.fromRawValue(compensated);
    }
    if (rotation == null) {
      return null;
    }
    final rawFormat = image.format.raw;
    final format =
        rawFormat is int ? InputImageFormatValue.fromRawValue(rawFormat) : null;
    if (format == null || image.planes.isEmpty) {
      return null;
    }
    final plane = image.planes.first;
    return InputImage.fromBytes(
      bytes: plane.bytes,
      metadata: InputImageMetadata(
        size: Size(image.width.toDouble(), image.height.toDouble()),
        rotation: rotation,
        format: format,
        bytesPerRow: plane.bytesPerRow,
      ),
    );
  }

  Future<void> _stop() async {
    final controller = _camera;
    _camera = null;
    try {
      if (controller != null) {
        if (controller.value.isStreamingImages) {
          await controller.stopImageStream();
        }
        await controller.dispose();
      }
    } catch (_) {
      // Already disposed — ignore.
    }
    await _detector?.close();
    _detector = null;
  }

  @override
  void dispose() {
    _debugYaw.dispose();
    unawaited(_stop());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final preview = _preview;
    if (preview != null) {
      return _previewView(preview, l10n);
    }
    return Scaffold(
      backgroundColor: SimfTokens.navy,
      appBar: AppBar(
        leading: const SimfBackButton(),
        backgroundColor: SimfTokens.navy,
        foregroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        title: Text(l10n.identityVerificationTitle),
      ),
      body: SafeArea(
        child: Stack(
          children: <Widget>[
            if (_cameraFailed)
              IdentityFallbackView(l10n: l10n, onRetry: _retry)
            else
              LiveCaptureView(
                ready: _cameraReady,
                preview: _cameraReady && _camera != null
                    ? CameraPreview(_camera!)
                    : null,
                humanCheckLabel: l10n.livenessHumanCheckTitle,
                promptText: _stepPrompt(l10n),
                promptLeading: _stepLeading(),
                stepIndex: _stepIndex,
                stepCount: _sequence.length,
              ),
            if (_kShowYawDebug && kDebugMode && !_cameraFailed)
              _yawDebugOverlay(),
          ],
        ),
      ),
    );
  }

  Widget _previewView(Uint8List bytes, AppL10n l10n) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        fit: StackFit.expand,
        children: <Widget>[
          Image.memory(bytes, fit: BoxFit.contain),
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            child: Container(
              padding: EdgeInsets.only(
                left: SimfTokens.space4,
                right: SimfTokens.space4,
                top: SimfTokens.space8,
                bottom: MediaQuery.of(context).padding.bottom + SimfTokens.space4,
              ),
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: [
                    SimfTokens.transparent,
                    SimfTokens.navy.withValues(alpha: 0.9),
                  ],
                ),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: () {
                        Navigator.of(context).pop<CapturedSelfie>(
                          (bytes: bytes, filename: _forwardName),
                        );
                      },
                      child: Text(l10n.saveLabel),
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space2),
                  SizedBox(
                    width: double.infinity,
                    child: TextButton(
                      onPressed: _retake,
                      style: TextButton.styleFrom(
                        foregroundColor: SimfTokens.beigeBorder,
                      ),
                      child: Text(l10n.retryLabel),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  // TEMP diagnostic (remove before merge, D-XXX); only ever built in a DEBUG
  // build (kDebugMode-gated at the call site). Shows the raw ML Kit yaw, its
  // normalised value (what the gate uses — positive should mean a physical RIGHT
  // turn), the current step and the platform. Turn RIGHT on the device: "norm"
  // should go positive toward +20. Only the readout text rebuilds (ValueNotifier),
  // not the camera preview.
  Widget _yawDebugOverlay() {
    final invert =
        livenessInvertYaw(defaultTargetPlatform, _frontCameraSensorOrientation);
    final platform =
        defaultTargetPlatform == TargetPlatform.iOS ? 'iOS' : 'Android';
    return Positioned(
      top: 8,
      left: 8,
      child: ValueListenableBuilder<double?>(
        valueListenable: _debugYaw,
        builder: (context, raw, _) {
          final norm = raw == null ? null : (invert ? -raw : raw);
          return Container(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
            color: Colors.black54,
            child: Text(
              'yaw ${raw?.toStringAsFixed(1) ?? "—"}  '
              'norm ${norm?.toStringAsFixed(1) ?? "—"}  '
              'step ${_step.name}  '
              '$platform s$_frontCameraSensorOrientation inv:$invert',
              style: const TextStyle(color: Colors.greenAccent, fontSize: 13),
            ),
          );
        },
      ),
    );
  }
}
