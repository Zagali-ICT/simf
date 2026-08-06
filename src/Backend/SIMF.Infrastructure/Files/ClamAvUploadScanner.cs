// Tests: SIMF.Api.Tests/Files/ClamAvResponseParsingTests.cs (OK / FOUND / error
//        response parsing — protocol framing without a live daemon).
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Files;

/// <summary>
/// A real malware scanner backed by a <c>clamd</c> daemon over TCP using
/// the INSTREAM protocol. Registered as <see cref="IUploadScanner"/> when
/// <c>UploadScanning:Engine = "ClamAV"</c>; otherwise the EICAR
/// <c>DefaultUploadScanner</c> is used.
///
/// <para>Any connection / protocol failure returns
/// <see cref="UploadScanVerdict.Skipped"/> rather than throwing — the centralized
/// file pipeline is <b>fail-closed</b> (it rejects a Skipped verdict in
/// Production), so an unreachable daemon blocks the upload instead of storing it
/// unscanned (closes D-494).</para>
/// </summary>
internal sealed class ClamAvUploadScanner(
    IOptions<UploadScanningOptions> options,
    ILogger<ClamAvUploadScanner> logger) : IUploadScanner
{
    public async Task<UploadScanResult> ScanAsync(
        byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return UploadScanResult.Skipped;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.ClamAvTimeoutSeconds)));
            var token = timeout.Token;

            using var client = new TcpClient();
            await client.ConnectAsync(settings.ClamAvHost, settings.ClamAvPort, token);
            await using var stream = client.GetStream();

            // zINSTREAM = null-terminated command, followed by length-prefixed chunks
            // and a zero-length terminator.
            await stream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), token);

            var chunkSize = Math.Clamp(settings.ClamAvChunkBytes, 1024, 1024 * 1024);
            var lengthPrefix = new byte[4];
            for (var sent = 0; sent < content.Length; sent += chunkSize)
            {
                var count = Math.Min(chunkSize, content.Length - sent);
                BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, (uint)count);
                await stream.WriteAsync(lengthPrefix, token);
                await stream.WriteAsync(content.AsMemory(sent, count), token);
            }
            // Zero-length chunk terminates the stream.
            BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, 0);
            await stream.WriteAsync(lengthPrefix, token);
            await stream.FlushAsync(token);

            using var buffer = new MemoryStream();
            var readBuffer = new byte[256];
            int read;
            while ((read = await stream.ReadAsync(readBuffer, token)) > 0)
            {
                buffer.Write(readBuffer, 0, read);
                if (readBuffer[read - 1] == 0) { break; } // null terminator
            }

            var response = Encoding.ASCII.GetString(buffer.ToArray()).TrimEnd('\0', '\n', ' ');
            return ParseResponse(response, fileName);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("ClamAV scan of '{File}' timed out — verdict Skipped (fail-closed).", fileName);
            return UploadScanResult.Skipped;
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            logger.LogWarning(ex,
                "ClamAV unreachable while scanning '{File}' — verdict Skipped (fail-closed).", fileName);
            return UploadScanResult.Skipped;
        }
    }

    /// <summary>Parses a clamd INSTREAM reply: <c>stream: OK</c> = clean,
    /// <c>stream: &lt;name&gt; FOUND</c> = infected, anything else (e.g. a size-limit
    /// or parse error) = Skipped so the fail-closed layer decides.</summary>
    internal static UploadScanResult ParseResponse(string response, string fileName)
    {
        if (response.EndsWith("OK", StringComparison.Ordinal))
        {
            return UploadScanResult.Clean;
        }
        if (response.EndsWith("FOUND", StringComparison.Ordinal))
        {
            // "stream: Eicar-Test-Signature FOUND" → "Eicar-Test-Signature"
            var start = response.IndexOf(':') + 1;
            var end = response.LastIndexOf(" FOUND", StringComparison.Ordinal);
            var name = (start > 0 && end > start)
                ? response[start..end].Trim()
                : "Unknown";
            return UploadScanResult.Infected(name);
        }
        return UploadScanResult.Skipped;
    }
}
