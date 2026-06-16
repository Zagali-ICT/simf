import 'dart:async';
import 'dart:io' show Platform;
import 'dart:typed_data';

import 'package:camera/camera.dart';
import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show DeviceOrientation;
import 'package:google_mlkit_face_detection/google_mlkit_face_detection.dart';
import 'package:image_picker/image_picker.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';

/// The selfie the flow returns to the My-Area caller, which uploads it via the
/// existing `POST /app/account/avatar`.
typedef CapturedSelfie = ({Uint8List bytes, String filename});

/// One step of the guided liveness flow (frames 758:4180 / 758:4248 / 758:4316).
enum LivenessStep { smile, turnRight, turnLeft }

/// The smile threshold (ML Kit `smilingProbability`, 0..1) that satisfies the
/// "ابتسم" step.
const double kSmileProbability = 0.7;

/// The minimum absolute head yaw (ML Kit `headEulerAngleY`, degrees) that
/// satisfies a head-turn step.
const double kTurnYawDegrees = 20;

/// Pure step gate — true when the detected face satisfies [step]. Kept top-level
/// so the liveness logic is unit-testable without a camera or the native plugin.
///
/// Note on sign: ML Kit's `headEulerAngleY` is positive when the head turns to
/// the image's right. The two turn steps require a yaw beyond ±[kTurnYawDegrees]
/// in opposite directions; front-camera mirroring can swap which way the user
/// perceives as "right", but a turn in each direction is still required.
bool livenessStepSatisfied(
  LivenessStep step, {
  double? smilingProbability,
  double? headEulerAngleY,
}) {
  switch (step) {
    case LivenessStep.smile:
      return smilingProbability != null &&
          smilingProbability >= kSmileProbability;
    case LivenessStep.turnRight:
      return headEulerAngleY != null && headEulerAngleY >= kTurnYawDegrees;
    case LivenessStep.turnLeft:
      return headEulerAngleY != null && headEulerAngleY <= -kTurnYawDegrees;
  }
}

/// التحقق من الهوية — the guided face-capture / liveness screen (D-404, frames
/// 758:4180 → 758:4248 → 758:4316). A full-bleed navy screen with a framed live
/// front-camera preview, a prompt per step (ابتسم → ادر راسك لليمين → ادر راسك
/// لليسار) and a gold 3-dot progress. The user must actually smile, then turn
/// right, then turn left; the forward/smile frame is captured and returned as
/// the new avatar selfie.
///
/// Where the live camera or the ML Kit plugin is unavailable (web / test /
/// emulator without a working camera / permission denied) the screen shows a
/// graceful fallback with a "choose from gallery" action so the avatar can
/// still be set — that path skips the liveness check (documented, D-404).
class IdentityVerificationScreen extends StatefulWidget {
  const IdentityVerificationScreen({super.key});

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
  /// The challenge order is shuffled per session (D-422) so the sequence is not
  /// predictable — a fixed smile→right→left order is easier to defeat with a
  /// pre-recorded clip. The forward selfie is still grabbed on the smile step,
  /// wherever it lands in the order.
  late final List<LivenessStep> _sequence;
  int _stepIndex = 0;
  LivenessStep get _step => _sequence[_stepIndex];
  Uint8List? _forwardFrame;
  String _forwardName = 'selfie.jpg';

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
      // No live preview on web — go straight to the gallery fallback.
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
        imageFormatGroup:
            Platform.isAndroid ? ImageFormatGroup.nv21 : ImageFormatGroup.bgra8888,
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
      // Camera/plugin unavailable or permission denied — fall back to gallery.
      if (mounted) {
        setState(() => _cameraFailed = true);
      }
    }
  }

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
      if (!livenessStepSatisfied(
        _step,
        smilingProbability: face.smilingProbability,
        headEulerAngleY: face.headEulerAngleY,
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
        // If the capture fails, keep going — the flow still verifies liveness;
        // a null forward frame falls back to the gallery on finish.
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

  Future<void> _finish() async {
    final bytes = _forwardFrame;
    await _stop();
    if (!mounted) {
      return;
    }
    if (bytes != null) {
      Navigator.of(context).pop<CapturedSelfie>(
        (bytes: bytes, filename: _forwardName),
      );
    } else {
      // No forward frame captured — let the user pick from the gallery instead.
      await _pickFromGallery();
    }
  }

  Future<void> _pickFromGallery() async {
    final picked = await ImagePicker().pickImage(
      source: ImageSource.gallery,
      maxWidth: 1024,
      imageQuality: 85,
    );
    if (picked == null || !mounted) {
      return;
    }
    final bytes = await picked.readAsBytes();
    if (!mounted) {
      return;
    }
    Navigator.of(context).pop<CapturedSelfie>(
      (bytes: bytes, filename: picked.name),
    );
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
    unawaited(_stop());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
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
        child: _cameraFailed
            ? _Fallback(l10n: l10n, onPick: () => unawaited(_pickFromGallery()))
            : _LiveCapture(
                l10n: l10n,
                step: _step,
                activeIndex: _stepIndex,
                ready: _cameraReady,
                preview: _cameraReady && _camera != null
                    ? CameraPreview(_camera!)
                    : null,
              ),
      ),
    );
  }
}

/// The prompt text for a step (the frame's "ابتسم" / "ادر راسك لليمين" / "ادر
/// راسك لليسار").
String livenessPrompt(AppL10n l10n, LivenessStep step) {
  switch (step) {
    case LivenessStep.smile:
      return l10n.stepSmilePrompt;
    case LivenessStep.turnRight:
      return l10n.stepTurnRightPrompt;
    case LivenessStep.turnLeft:
      return l10n.stepTurnLeftPrompt;
  }
}

class _LiveCapture extends StatelessWidget {
  const _LiveCapture({
    required this.l10n,
    required this.step,
    required this.activeIndex,
    required this.ready,
    required this.preview,
  });

  final AppL10n l10n;
  final LivenessStep step;

  /// Position in the shuffled sequence (0-based) — drives the progress dots,
  /// not the enum index (which no longer matches the displayed order).
  final int activeIndex;
  final bool ready;
  final Widget? preview;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        // The framed face preview (the figma's bordered square) — Expanded so
        // it scales to the available height without overflowing.
        Expanded(
          child: Center(
            child: Padding(
              padding:
                  const EdgeInsets.symmetric(horizontal: SimfTokens.space6),
              child: AspectRatio(
                aspectRatio: 3 / 4,
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    color: SimfTokens.navyDeep,
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
                    border: Border.all(color: SimfTokens.accent, width: 2),
                  ),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
                    child: ready && preview != null
                        ? FittedBox(fit: BoxFit.cover, child: _sized(preview!))
                        : const Center(
                            child: CircularProgressIndicator(
                              color: SimfTokens.accent,
                            ),
                          ),
                  ),
                ),
              ),
            ),
          ),
        ),
        const SizedBox(height: SimfTokens.space6),
        Text(
          livenessPrompt(l10n, step),
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: Colors.white,
            fontSize: SimfTokens.textLg,
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        _ProgressDots(activeIndex: step.index),
        const SizedBox(height: SimfTokens.space6),
      ],
    );
  }

  // CameraPreview needs a finite size inside the FittedBox/BoxFit.cover.
  Widget _sized(Widget child) =>
      SizedBox(width: 1080, height: 1440, child: child);
}

/// The gold 3-dot step indicator (the frame's bottom progress).
class _ProgressDots extends StatelessWidget {
  const _ProgressDots({required this.activeIndex});

  final int activeIndex;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        for (var i = 0; i < LivenessStep.values.length; i++)
          Container(
            margin: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
            width: i <= activeIndex ? 28 : 20,
            height: 6,
            decoration: BoxDecoration(
              color: i <= activeIndex
                  ? SimfTokens.accent
                  : SimfTokens.beigeBorder.withValues(alpha: 0.4),
              borderRadius: BorderRadius.circular(3),
            ),
          ),
      ],
    );
  }
}

/// Shown when no live camera is available (web / test / emulator / permission
/// denied): the user picks a photo from the gallery instead (no liveness).
class _Fallback extends StatelessWidget {
  const _Fallback({required this.l10n, required this.onPick});

  final AppL10n l10n;
  final VoidCallback onPick;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.photo_camera_front_outlined,
              size: 56,
              color: SimfTokens.beigeBorder,
            ),
            const SizedBox(height: SimfTokens.space4),
            Text(
              l10n.identityCameraUnavailable,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white),
            ),
            const SizedBox(height: SimfTokens.space6),
            FilledButton.icon(
              onPressed: onPick,
              style: FilledButton.styleFrom(
                backgroundColor: SimfTokens.accent,
                foregroundColor: SimfTokens.navy,
                minimumSize: const Size.fromHeight(48),
              ),
              icon: const Icon(Icons.photo_library_outlined),
              label: Text(l10n.chooseFromGallery),
            ),
          ],
        ),
      ),
    );
  }
}
