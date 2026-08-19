// The log tail used to decode the whole file from byte zero to return the last N
// lines, so an admin clicking the CP logs page on a 500 MB rolling file allocated
// every line in it to keep a few hundred. It now scans backwards in blocks and
// decodes only the tail. These tests pin the part that is easy to get wrong: the
// answer must be identical to the whole-file read on both sides of the block
// boundary, with and without a trailing newline, and for multi-byte text.
using System.Text;
using Microsoft.Extensions.Options;
using SIMF.Common.Options;
using SIMF.Contracts.Logs;
using SIMF.Infrastructure.Logs;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class LogFileServiceTests : IDisposable
{
    private const string Project = "api";
    private const string FileName = "app.log";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "simf-log-tail-" + Guid.NewGuid().ToString("N"));

    public LogFileServiceTests() =>
        Directory.CreateDirectory(Path.Combine(_root, Project));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp directory the OS is still holding is not a test failure.
        }
    }

    [Fact]
    public async Task Tails_a_file_smaller_than_one_scan_block()
    {
        // Below the block size the reader still starts at byte zero, as it always did.
        var lines = Enumerate(20);
        await WriteAsync(lines, trailingNewline: true);

        var response = await TailAsync(5);

        Assert.Equal(5, response.LineCount);
        Assert.Equal(string.Join('\n', lines[^5..]), response.Content);
    }

    [Fact]
    public async Task Tails_a_file_spanning_many_scan_blocks()
    {
        // Comfortably past the 64 KB block, so the backwards scan crosses several
        // blocks and the answer must still match reading the whole file.
        var lines = Enumerate(20_000);
        await WriteAsync(lines, trailingNewline: true);

        var response = await TailAsync(200);

        Assert.Equal(200, response.LineCount);
        Assert.Equal(string.Join('\n', lines[^200..]), response.Content);
    }

    [Fact]
    public async Task Keeps_the_last_line_when_the_file_has_no_trailing_newline()
    {
        // Serilog is mid-write: the final line has no newline after it, and it is
        // the one the admin opened the page to read.
        var lines = Enumerate(20_000);
        await WriteAsync(lines, trailingNewline: false);

        var response = await TailAsync(3);

        Assert.Equal(3, response.LineCount);
        Assert.Equal(string.Join('\n', lines[^3..]), response.Content);
    }

    [Fact]
    public async Task Returns_the_whole_file_when_it_holds_fewer_lines_than_asked_for()
    {
        var lines = Enumerate(7);
        await WriteAsync(lines, trailingNewline: true);

        var response = await TailAsync(500);

        Assert.Equal(7, response.LineCount);
        Assert.Equal(string.Join('\n', lines), response.Content);
    }

    [Fact]
    public async Task Does_not_split_a_multi_byte_character_at_the_block_boundary()
    {
        // Arabic text: every letter is two UTF-8 bytes, so a scan that cut on a raw
        // byte count rather than on a newline would decode a replacement character.
        var lines = Enumerable.Range(0, 20_000)
            .Select(index => $"{index:D6} تسجيل الدخول إلى النظام")
            .ToArray();
        await WriteAsync(lines, trailingNewline: true);

        var response = await TailAsync(50);

        Assert.Equal(string.Join('\n', lines[^50..]), response.Content);
        Assert.DoesNotContain("�", response.Content);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<LogTailResponse> TailAsync(int lineCount)
    {
        var service = new LogFileService(
            Options.Create(new StorageOptions { LogDirectory = _root }));
        var response = await service.TailAsync(Project, FileName, lineCount);
        Assert.NotNull(response);
        return response!;
    }

    // An array, not a List: the assertions slice it with a range, which List does
    // not support.
    private static string[] Enumerate(int count) =>
        Enumerable.Range(0, count).Select(index => $"{index:D6} log line").ToArray();

    private async Task WriteAsync(IReadOnlyList<string> lines, bool trailingNewline)
    {
        var text = string.Join('\n', lines) + (trailingNewline ? "\n" : string.Empty);
        await File.WriteAllTextAsync(
            Path.Combine(_root, Project, FileName), text, new UTF8Encoding(false));
    }
}
