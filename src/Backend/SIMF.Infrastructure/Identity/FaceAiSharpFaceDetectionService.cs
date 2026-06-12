// Tests: SIMF.Api.Tests/UserProfileFaceGateTests.cs (no-face 400 with the
// real model; disabled pass-through), SIMF.Api.Tests/UserProfileTests.cs
// (round-trip runs with the gate disabled in the factory).
using FaceAiSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.IdentityAccess;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SIMF.Infrastructure.Identity;

/// <summary>Binds the <c>FaceDetection</c> configuration section
/// (C7 — D-371). <see cref="Enabled"/> defaults to true — the production
/// posture; tests / constrained environments may disable the gate, which
/// turns the check into a logged pass-through.</summary>
public sealed class FaceDetectionOptions
{
    public const string SectionName = "FaceDetection";

    public bool Enabled { get; set; } = true;

    /// <summary>The minimum detector confidence for a hit to count as a
    /// face. FaceAiSharp's SCRFD scores real faces well above this.</summary>
    public float MinConfidence { get; set; } = 0.5f;
}

/// <summary>
/// C7 (D-371) — offline human-face detection over the FaceAiSharp SCRFD
/// ONNX model (bundled in the <c>FaceAiSharp.Bundle</c> package; no
/// external service, NCA-compatible). The detector is created lazily once
/// (the ONNX session is expensive) and guarded by a lock — uploads are
/// rare, so serialised inference is simpler than proving the session's
/// thread-safety. Registered as a singleton.
/// </summary>
public sealed class FaceAiSharpFaceDetectionService(
    IOptions<FaceDetectionOptions> options,
    ILogger<FaceAiSharpFaceDetectionService> logger) : IFaceDetectionService
{
    private readonly Lock _gate = new();
    private IFaceDetector? _detector;

    public Task<bool> ContainsHumanFaceAsync(
        byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            logger.LogWarning(
                "Face detection is disabled (FaceDetection:Enabled=false) — the human-face gate passed through.");
            return Task.FromResult(true);
        }

        // CPU-bound ONNX inference — run off the request thread.
        return Task.Run(() => Detect(imageBytes), cancellationToken);
    }

    private bool Detect(byte[] imageBytes)
    {
        Image<Rgb24> image;
        try
        {
            image = Image.Load<Rgb24>(imageBytes);
        }
        catch (ImageFormatException ex)
        {
            // An image the decoder cannot even parse certainly shows no
            // detectable face — reject as no-face (400) instead of failing
            // the request with a 500. Also hardens the endpoint against
            // crafted files that pass the magic-byte gate but are corrupt.
            logger.LogWarning(
                ex, "Face detection: the uploaded image could not be decoded.");
            return false;
        }

        using (image)
        lock (_gate)
        {
            _detector ??= FaceAiSharpBundleFactory.CreateFaceDetector();
            var faces = _detector.DetectFaces(image);
            var hit = faces.Any(face =>
                face.Confidence is null
                || face.Confidence.Value >= options.Value.MinConfidence);
            logger.LogInformation(
                "Face detection ran: {FaceCount} candidate(s), accepted={Accepted}.",
                faces.Count, hit);
            return hit;
        }
    }
}
