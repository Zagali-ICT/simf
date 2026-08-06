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
/// Offline human-face detection over the FaceAiSharp SCRFD
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

    // Security finding H3 — reject decompression-bomb uploads before they
    // allocate. A small file can declare an enormous canvas that decodes to
    // multiple GB of Rgb24; Image.Identify reads only the header (no pixel
    // allocation), so an over-large image is rejected before Image.Load
    // commits the memory. _decodeLimiter bounds concurrent decodes so a burst
    // of large-but-valid uploads cannot pile up buffers ahead of the
    // serialised inference lock.
    // ~60 MP — above any mainstream phone/camera sensor (so a legitimate
    // full-res ID photo is never rejected) yet far below the hundreds-of-
    // megapixels / gigapixel canvases a decompression bomb declares.
    private const long MaxDecodePixels = 60_000_000;
    private readonly SemaphoreSlim _decodeLimiter = new(2);

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
        return Task.Run(() => Detect(imageBytes, cancellationToken), cancellationToken);
    }

    private bool Detect(byte[] imageBytes, CancellationToken cancellationToken)
    {
        // Header-only inspection (no pixel allocation) — reject an over-large
        // canvas before the full decode commits memory (security finding H3).
        try
        {
            var info = Image.Identify(imageBytes);
            if ((long)info.Width * info.Height > MaxDecodePixels)
            {
                logger.LogWarning(
                    "Face detection: image {Width}x{Height} exceeds the {Cap}px decode cap — rejected as no-face.",
                    info.Width, info.Height, MaxDecodePixels);
                return false;
            }
        }
        catch (ImageFormatException ex)
        {
            logger.LogWarning(
                ex, "Face detection: the uploaded image header could not be identified.");
            return false;
        }

        // Bound concurrent decodes so a burst of valid uploads cannot allocate
        // unboundedly ahead of the serialised inference lock.
        _decodeLimiter.Wait(cancellationToken);
        try
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
        finally
        {
            _decodeLimiter.Release();
        }
    }
}
