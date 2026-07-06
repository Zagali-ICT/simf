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
import '../../app/widgets/simf_page_shell.dart';
import 'data/liveness.dart';
import 'widgets/identity_capture_view.dart';
import 'widgets/identity_fallback_view.dart';

// The liveness step model + pure gate + prompt helper live in `data/liveness.dart`
// (unit-testable without a camera); re-exported so existing imports of this
// screen file keep resolving them.
export 'data/liveness.dart';

/// التحقق من الهوية — the guided face-capture / liveness screen (D-404, frames
/// 758:4180 → 758:4248 → 758:4316). A full-bleed navy screen with a framed live
/// front-camera preview, a prompt per step (ابتسم → ادر راسك لليمين → ادر راسك
/// لليسار) and a gold progress bar. The user must actually smile, then turn
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
  bool _capturing = false;

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

  /// Manual shutter (owner 2026-07-06) — grabs the current frame and returns it,
  /// bypassing the smile → turn liveness so the photo can always be taken
  /// (the auto check can't complete without Google Play Services / ML Kit).
  Future<void> _captureNow() async {
    final controller = _camera;
    if (controller == null || _capturing) {
      return;
    }
    setState(() => _capturing = true);
    try {
      if (controller.value.isStreamingImages) {
        await controller.stopImageStream();
      }
      final shot = await controller.takePicture();
      final bytes = await shot.readAsBytes();
      await _stop();
      if (!mounted) {
        return;
      }
      Navigator.of(context).pop<CapturedSelfie>(
        (bytes: bytes, filename: shot.name),
      );
    } catch (_) {
      // Capture failed — let the user retry or pick from the gallery.
      if (mounted) {
        setState(() => _capturing = false);
      }
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
            ? IdentityFallbackView(
                l10n: l10n,
                onPick: () => unawaited(_pickFromGallery()),
              )
            : LiveCaptureView(
                ready: _cameraReady,
                preview: _cameraReady && _camera != null
                    ? CameraPreview(_camera!)
                    : null,
                promptText: l10n.identityCapturePrompt,
                captureLabel: l10n.capturePhotoLabel,
                galleryLabel: l10n.chooseFromGallery,
                capturing: _capturing,
                onCapture: () => unawaited(_captureNow()),
                onGallery: () => unawaited(_pickFromGallery()),
              ),
      ),
    );
  }
}
