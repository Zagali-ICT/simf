using System.Drawing.Printing;
using SIMF.Common.Badges;

namespace SIMF.BadgeDesk;

/// <summary>
/// D-809 — the badge desk window.
///
/// <para>Built in code rather than with the designer so the whole layout is
/// reviewable in one file and no generated <c>.Designer.cs</c> / <c>.resx</c>
/// boilerplate ships.</para>
///
/// <para><b>Keyboard only.</b> An operator facing a queue never touches the
/// mouse: Enter moves down the fields and prints from the last one, Escape
/// clears the form, F5 uploads. Nothing on this screen requires a pointer.</para>
/// </summary>
public sealed class MainForm : Form
{
    private readonly DeskConfig _config;
    private readonly DeskStore _store;
    private readonly UploadClient _uploader;
    private readonly PrinterSettings _printerSettings = new();

    private readonly TextBox _name = new();
    private readonly TextBox _nameArabic = new();
    private readonly ComboBox _profileType = new();
    private readonly TextBox _identityDocument = new();
    private readonly TextBox _mobile = new();
    private readonly PictureBox _preview = new();
    private readonly Label _status = new();
    private readonly Label _counters = new();

    public MainForm(DeskConfig config, DeskStore store, UploadClient uploader)
    {
        _config = config;
        _store = store;
        _uploader = uploader;

        Text = $"SIMF badge desk — {config.DeskLabel}";
        MinimumSize = new Size(760, 620);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        Font = new Font(FontFamily.GenericSansSerif, 11f);

        BuildLayout();
        RefreshCounters();
        _name.Select();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(16),
            AutoSize = false,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        AddField(fields, "Name / الاسم", _name);
        AddField(fields, "Arabic name", _nameArabic);

        _profileType.DropDownStyle = ComboBoxStyle.DropDownList;
        _profileType.Dock = DockStyle.Fill;
        foreach (var type in _config.ProfileTypes) { _profileType.Items.Add(type); }
        if (_profileType.Items.Count > 0) { _profileType.SelectedIndex = 0; }
        AddField(fields, "Badge type", _profileType);

        AddField(fields, "ID / Iqama / passport", _identityDocument);
        AddField(fields, "Mobile", _mobile);

        _status.Dock = DockStyle.Fill;
        _status.AutoSize = true;
        _status.Padding = new Padding(0, 12, 0, 0);
        fields.Controls.Add(_status);
        fields.SetColumnSpan(_status, 2);

        _counters.Dock = DockStyle.Bottom;
        _counters.AutoSize = true;

        _preview.Dock = DockStyle.Fill;
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        _preview.BorderStyle = BorderStyle.FixedSingle;

        layout.Controls.Add(fields, 0, 0);
        layout.Controls.Add(_preview, 1, 0);
        Controls.Add(layout);
        Controls.Add(_counters);
        Controls.Add(BuildHintBar());

        KeyDown += OnKeyDown;
        foreach (Control control in new Control[]
                 { _name, _nameArabic, _profileType, _identityDocument, _mobile })
        {
            control.KeyDown += OnFieldKeyDown;
        }
    }

    private static void AddField(TableLayoutPanel panel, string caption, Control control)
    {
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Height = 32,
        });
        panel.Controls.Add(control);
    }

    private static Control BuildHintBar() => new Label
    {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        Text = "Enter = next field, print from the last  •  Esc = clear  •  "
        + "F2 = reprint the last badge  •  F5 = upload",
    };

    private void OnFieldKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.KeyCode != Keys.Enter) { return; }
        args.Handled = true;
        args.SuppressKeyPress = true;

        // Enter walks the queue-facing path: down the fields, then print. The
        // last field is the mobile, so an operator can register somebody without
        // ever leaving the keyboard.
        if (ReferenceEquals(sender, _mobile)) { RegisterAndPrint(); return; }
        SelectNextControl((Control)sender!, forward: true, tabStopOnly: true,
            nested: true, wrap: true);
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        switch (args.KeyCode)
        {
            case Keys.Escape:
                ClearForm();
                args.Handled = true;
                break;
            case Keys.F2:
                ReprintLast();
                args.Handled = true;
                break;
            case Keys.F5:
                _ = UploadAsync();
                args.Handled = true;
                break;
        }
    }

    private void RegisterAndPrint()
    {
        if (_profileType.SelectedItem is not DeskProfileType type)
        {
            ShowStatus("Pick a badge type.", isError: true);
            return;
        }
        var name = _name.Text.Trim();
        if (name.Length == 0)
        {
            ShowStatus("A name is required.", isError: true);
            _name.Select();
            return;
        }
        var identityDocument = _identityDocument.Text.Trim();
        if (identityDocument.Length == 0)
        {
            // The one field the reduced desk keeps. It is the only thing that
            // stops one person collecting several badges, and it cannot be
            // recovered after the event.
            ShowStatus("An ID, Iqama or passport number is required.", isError: true);
            _identityDocument.Select();
            return;
        }

        var sequence = _store.NextSequence(_config);
        if (sequence > _config.SequenceRangeEnd)
        {
            ShowStatus(
                $"This desk has used its whole range ({_config.SequenceRangeStart}–"
                + $"{_config.SequenceRangeEnd}). Stop and get a new desk number.",
                isError: true);
            return;
        }

        var mobile = _mobile.Text.Trim();
        var record = new StoredRegistration
        {
            Sequence = sequence,
            ProfileTypeCode = type.Code,
            Name = name,
            NameArabic = _nameArabic.Text.Trim() is { Length: > 0 } arabic ? arabic : null,
            SaudiMobile = mobile.StartsWith('0') ? mobile : null,
            InternationalMobile = mobile.StartsWith('+') ? mobile : null,
            RegisteredAt = DateTimeOffset.Now,
        };
        ApplyIdentityDocument(record, identityDocument);

        // STORE BEFORE PRINT, always. If the printer jams, the visitor is still
        // registered and the badge is reprinted from the list; if it were the
        // other way round a paper badge could exist that no record knows about.
        try
        {
            _store.Append(record);
        }
        catch (IOException ex)
        {
            ShowStatus($"Could not write the local file — STOP. {ex.Message}", isError: true);
            return;
        }

        var payload = EventBadgeCodec.Encode(
            new EventBadgePayload(type.Code, sequence),
            _config.BadgeKeyBytes!,
            _config.BadgeKeyVersion);

        _preview.Image?.Dispose();
        _preview.Image = BadgePrinter.RenderQr(payload);

        try
        {
            BadgePrinter.Print(_printerSettings, payload, name, type.Name, sequence);
            ShowStatus($"Printed badge {sequence}.", isError: false);
        }
        catch (Exception ex) when (ex is InvalidPrinterException or SystemException)
        {
            ShowStatus(
                $"Registered as {sequence} but printing failed: {ex.Message}. "
                + "Press F2 to reprint — the visitor is recorded.",
                isError: true);
        }

        ClearForm();
        RefreshCounters();
    }

    /// <summary>
    /// D-813 — reprints the most recent badge.
    ///
    /// <para>The status text already told the operator to reprint after a
    /// printer jam, and there was nothing to reprint WITH: no list, no action.
    /// A jam at a live desk left them with a registered visitor, no badge, and
    /// on-screen guidance pointing at a feature that did not exist.</para>
    ///
    /// <para>Reprints from the stored record rather than re-reading the form,
    /// so the reissued badge carries the same sequence as the one that jammed.
    /// Printing a NEW sequence would put two numbers on one visitor and break
    /// the reconciliation the whole upload depends on.</para>
    /// </summary>
    private void ReprintLast()
    {
        if (_store.Records.Count == 0)
        {
            ShowStatus("Nothing to reprint yet.", isError: false);
            return;
        }
        var record = _store.Records[^1];
        var type = _config.ProfileTypes
            .FirstOrDefault(candidate => candidate.Code == record.ProfileTypeCode);

        var payload = EventBadgeCodec.Encode(
            new EventBadgePayload(record.ProfileTypeCode, record.Sequence),
            _config.BadgeKeyBytes!,
            _config.BadgeKeyVersion);

        _preview.Image?.Dispose();
        _preview.Image = BadgePrinter.RenderQr(payload);

        try
        {
            BadgePrinter.Print(
                _printerSettings, payload, record.Name,
                type?.Name ?? string.Empty, record.Sequence);
            ShowStatus($"Reprinted badge {record.Sequence}.", isError: false);
        }
        catch (Exception ex) when (ex is InvalidPrinterException or SystemException)
        {
            ShowStatus($"Reprint failed: {ex.Message}", isError: true);
        }
    }

    /// <summary>
    /// Files the one captured document into the right column.
    ///
    /// <para>Saudi identity numbers are 10 digits and the first digit says which
    /// kind: <c>1</c> is a national ID, <c>2</c> is an Iqama. Anything else is a
    /// passport. Getting this right matters — each column carries its own
    /// filtered unique index, so a resident's Iqama recorded as a passport would
    /// not collide with the same Iqama entered correctly at another desk, and
    /// that person could collect two badges.</para>
    ///
    /// <para>Shape only: the desk does not run the Luhn check, because a queue
    /// is the wrong place to argue with somebody's identity card. The server
    /// validates it on upload and reports the row.</para>
    /// </summary>
    private static void ApplyIdentityDocument(StoredRegistration record, string value)
    {
        var isTenDigits = value.Length == 10 && value.All(char.IsAsciiDigit);
        if (isTenDigits && value[0] == '1')
        {
            record.NationalId = value;
            return;
        }
        if (isTenDigits && value[0] == '2')
        {
            record.IqamaNumber = value;
            return;
        }
        record.PassportNumber = value;
    }

    private async Task UploadAsync()
    {
        var pending = _store.BuildPendingBatch(UploadClient.MaxBatchSize);
        if (pending.Count == 0)
        {
            ShowStatus("Nothing to upload.", isError: false);
            return;
        }
        if (string.IsNullOrWhiteSpace(_config.ApiBaseUrl))
        {
            ShowStatus("No ApiBaseUrl is configured for this desk.", isError: true);
            return;
        }

        var token = PromptForToken();
        if (string.IsNullOrWhiteSpace(token)) { return; }

        ShowStatus($"Uploading {pending.Count} registrations…", isError: false);
        try
        {
            var response = await _uploader.UploadAsync(
                _config.ApiBaseUrl, token, _config.DeskLabel, pending);

            // Only rows the server actually accounted for are marked done. A
            // rejected row stays pending so it is retried or chased by hand,
            // which is what makes the reconciliation reach zero.
            _store.MarkUploaded(response.Results
                .Where(result => result.Status
                    is Contracts.Badges.OfflineBadgeUploadStatus.Created
                    or Contracts.Badges.OfflineBadgeUploadStatus.CreatedPendingApproval
                    or Contracts.Badges.OfflineBadgeUploadStatus.AlreadyUploaded)
                .Select(result => result.Sequence));

            ShowStatus(
                $"Uploaded {response.Submitted}: {response.Created} approved, "
                + $"{response.PendingApproval} awaiting approval, "
                + $"{response.AlreadyUploaded} already known, {response.Rejected} rejected.",
                isError: response.Rejected > 0);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException
                                      or TaskCanceledException)
        {
            ShowStatus($"Upload failed: {ex.Message}. Nothing was lost — retry.", isError: true);
        }
        RefreshCounters();
    }

    private static string? PromptForToken()
    {
        using var dialog = new Form
        {
            Text = "Paste a Control Panel bearer token",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(560, 120),
            MinimizeBox = false,
            MaximizeBox = false,
        };
        var input = new TextBox
        {
            Dock = DockStyle.Top,
            UseSystemPasswordChar = true,
            Margin = new Padding(12),
        };
        var ok = new Button { Text = "Upload", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };
        dialog.Controls.Add(input);
        dialog.Controls.Add(ok);
        dialog.AcceptButton = ok;
        input.Select();

        return dialog.ShowDialog() == DialogResult.OK ? input.Text.Trim() : null;
    }

    private void ClearForm()
    {
        _name.Clear();
        _nameArabic.Clear();
        _identityDocument.Clear();
        _mobile.Clear();
        _name.Select();
    }

    private void RefreshCounters() =>
        _counters.Text =
            $"Registered this desk: {_store.Records.Count}   •   "
            + $"Waiting to upload: {_store.PendingUploadCount}   •   "
            + $"Range {_config.SequenceRangeStart}–{_config.SequenceRangeEnd}";

    private void ShowStatus(string message, bool isError)
    {
        _status.Text = message;
        _status.ForeColor = isError ? Color.Firebrick : Color.DarkGreen;
    }
}
