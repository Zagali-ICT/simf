namespace SIMF.BadgeDesk;

/// <summary>
/// Entry point for the offline badge desk.
///
/// <para>Everything is resolved before the window opens: a desk that is not
/// correctly provisioned must say so to the person setting it up, not to the
/// first visitor in the queue.</para>
/// </summary>
internal static class Program
{
    /// <summary>Where the shift is recorded. Under ProgramData rather than beside
    /// the executable so it survives replacing the app, and so a desk PC with a
    /// locked-down Program Files can still write it.</summary>
    private static string StorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "SIMF", "BadgeDesk", "registrations.jsonl");

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        DeskConfig config;
        try
        {
            config = DeskConfig.Load(configPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException
                                      or System.Text.Json.JsonException)
        {
            Fail($"Could not read {configPath}.\r\n\r\n{ex.Message}");
            return;
        }

        if (config.Validate() is { Count: > 0 } problems)
        {
            Fail("This desk is not correctly provisioned:\r\n\r\n  • "
                + string.Join("\r\n  • ", problems));
            return;
        }

        DeskStore store;
        try
        {
            store = new DeskStore(StorePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or InvalidDataException)
        {
            // The local file is the only record a badge was handed out. Refusing
            // to start is the right answer: printing badges nothing can record
            // would be worse than not opening the desk.
            //
            // InvalidDataException is the encrypted store reporting that
            // NOTHING in the file decrypts on this machine, which means it came
            // from another desk. Opening anyway would reset the sequence counter
            // and reissue numbers already printed on paper.
            Fail($"Could not open the local record at {StorePath}.\r\n\r\n{ex.Message}");
            return;
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        Application.Run(new MainForm(config, store, new UploadClient(httpClient)));
    }

    private static void Fail(string message) =>
        MessageBox.Show(
            message, "SIMF badge desk", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
