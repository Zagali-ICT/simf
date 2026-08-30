using System.Drawing.Printing;
using SIMF.Common;
using SIMF.Common.Badges;
using SIMF.Contracts.Badges;

namespace SIMF.BadgeDesk;

/// <summary>
/// The badge desk window.
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
    private readonly PrinterSettings _printerSettings = new();

    private readonly TextBox _name = new();
    private readonly TextBox _nameArabic = new();
    private readonly ComboBox _profileType = new();
    private readonly TextBox _identityDocument = new();
    private readonly TextBox _mobile = new();
    private readonly PictureBox _preview = new();
    private readonly Label _status = new();
    private readonly Label _counters = new();

    /// <summary>The rows the LAST upload refused, newest first.
    ///
    /// <para>The server returns a reason per row and the console used to render
    /// only the totals, so an operator was told "3 rejected" and had no way to
    /// learn which three or why. The manual told them to read it off the upload
    /// report; there was no report. Without this the correction path could not
    /// even be started, and rejecting rows would have stranded printed badges -
    /// exactly the outcome the correction path exists to prevent.</para></summary>
    private readonly List<OfflineBadgeUploadResult> _rejections = [];

    /// <summary>Drains the backlog on its own once the network is back. It owns
    /// the token, the backoff and the in-flight guard; this form only asks it to
    /// try and renders what came back.</summary>
    private readonly BacklogUploader _backlog;

    /// <summary>Ticks once a second while a token is held.
    ///
    /// <para>A Forms timer, not a thread or a task loop: the tick arrives on the
    /// message loop, so the handler touches the store, the labels and the
    /// uploader with no marshalling and no locking of its own. One second is not
    /// a polling rate — the uploader decides when it is next due — it is how
    /// often the counters line can restate the wait honestly.</para></summary>
    private readonly System.Windows.Forms.Timer _uploadTimer = new() { Interval = 1000 };

    /// <summary>Cuts an upload in flight when the window closes. Safe mid-batch:
    /// a batch is idempotent by sequence, so rows the server took but never
    /// acknowledged come back as AlreadyUploaded on the next launch.</summary>
    private readonly CancellationTokenSource _closing = new();

    /// <summary>The failure already on screen.
    ///
    /// <para>An hour of dead venue wifi is ONE fact. Restating it every fifteen
    /// seconds would scroll away the upload report an operator is reading in
    /// order to correct the rows the server refused, and this label is not built
    /// to be transient — the counters line is, and that is where the countdown
    /// lives.</para></summary>
    private string? _reportedFailure;

    public MainForm(DeskConfig config, DeskStore store, UploadClient uploader)
    {
        _config = config;
        _store = store;

        // The form holds no upload logic: it hands the uploader the store and one
        // way to send a batch, and puts the answer on screen.
        _backlog = new BacklogUploader(
            store,
            (token, batch, cancellationToken) => uploader.UploadAsync(
                config.ApiBaseUrl, token, config.DeskLabel, batch, cancellationToken));
        _uploadTimer.Tick += OnUploadTick;

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
        // The upload report names every refused row, so this grows downwards.
        _status.MaximumSize = new Size(0, 220);
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
        + "F2 = reprint last  •  F3 = correct a rejected row  •  F5 = upload now",
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
            case Keys.F3:
                CorrectPending();
                args.Handled = true;
                break;
            case Keys.F5:
                StartUpload();
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
            // Minted here because the desk is disconnected: a Guid needs no
            // coordination, which is the same reason the printed sequence has
            // per-desk ranges. It is what the QR carries, so the badge in the
            // visitor's hand resolves the moment the shift uploads.
            ProfileId = Guid.NewGuid(),
            ProfileTypeCode = type.Code,
            Name = name,
            NameArabic = _nameArabic.Text.Trim() is { Length: > 0 } arabic ? arabic : null,
            SaudiMobile = mobile.StartsWith('0') ? mobile : null,
            InternationalMobile = mobile.StartsWith('+') ? mobile : null,
            RegisteredAt = SimfClock.Now,
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
            new EventBadgePayload(record.ProfileId, _config.EditionYear, type.Code),
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
    /// Corrects a registration the server rejected, without reprinting.
    ///
    /// <para>This is what makes server-side validation of an uploaded shift SAFE.
    /// Until it existed, rejecting a row for a mistyped ID meant a printed badge
    /// in a visitor's hand that no account backed and nobody could fix: the store
    /// is append-only and there was no edit path. So the choice was between
    /// accepting bad identity data — which defeats the duplicate-badge guard the
    /// desk exists to uphold — and stranding real visitors at a gate. With a
    /// correction path there is no trade: the row is rejected, named, fixed, and
    /// re-uploaded.</para>
    ///
    /// <para>The badge is NOT reprinted and the sequence does not change. The QR
    /// encodes only the profile-type code and the sequence, neither of which a
    /// correction touches, so the paper already issued stays valid.</para>
    /// </summary>
    private void CorrectPending()
    {
        var pending = _store.Records.Where(record => !record.Uploaded).ToList();
        if (pending.Count == 0)
        {
            ShowStatus("Nothing pending to correct.", isError: false);
            return;
        }

        var sequence = PromptForSequence(pending, _rejections);
        if (sequence is not { } target) { return; }

        var original = pending.FirstOrDefault(record => record.Sequence == target);
        if (original is null)
        {
            ShowStatus($"{target} is not a pending registration.", isError: true);
            return;
        }

        var name = _name.Text.Trim();
        var identityDocument = _identityDocument.Text.Trim();
        if (name.Length == 0 || identityDocument.Length == 0)
        {
            ShowStatus(
                "Type the corrected name and ID into the form first, then press F3.",
                isError: true);
            return;
        }

        // A BLANK box means "leave this alone", not "clear it".
        // The form is cleared after every registration and F3 does not repopulate
        // it, so an operator fixing a mistyped ID leaves the mobile and Arabic
        // name empty. Rebuilding the record from the form alone silently wiped
        // both - and the mobile is the only contact channel an offline-registered
        // visitor has. The corrected line supersedes the original on the next
        // load, so anything dropped here is unrecoverable.
        var mobile = _mobile.Text.Trim();
        var arabicName = _nameArabic.Text.Trim();
        var corrected = new StoredRegistration
        {
            // Carried over, never re-derived: these identify the printed badge.
            Sequence = original.Sequence,
            ProfileId = original.ProfileId,
            ProfileTypeCode = original.ProfileTypeCode,
            RegisteredAt = original.RegisteredAt,
            Email = original.Email,
            Name = name,
            NameArabic = arabicName.Length > 0 ? arabicName : original.NameArabic,
            SaudiMobile = mobile.Length > 0
                ? (mobile.StartsWith('0') ? mobile : null)
                : original.SaudiMobile,
            InternationalMobile = mobile.Length > 0
                ? (mobile.StartsWith('+') ? mobile : null)
                : original.InternationalMobile,
        };
        ApplyIdentityDocument(corrected, identityDocument);

        // The upload timer goes on ticking while the dialog above is open, so a
        // background batch can land in the middle of a correction. Harmless: the
        // store refuses to correct a row the server has already accepted, and
        // this is where that answer arrives.
        if (!_store.Correct(corrected))
        {
            ShowStatus(
                $"{target} has already been uploaded and cannot be corrected here.",
                isError: true);
            return;
        }

        // The reason this row was held out of the background loop has just been
        // dealt with, so it belongs in the next batch.
        _backlog.AllowRetry(target);

        // The badge prints the NAME as well as the QR. The QR is
        // unaffected by any correction (it carries only the badge type and the
        // sequence), but a corrected NAME makes the printed paper wrong. F2
        // reprints the SAME sequence, so it is the right answer when the name
        // changed and unnecessary when only the id did.
        var nameChanged = !string.Equals(
            original.Name, corrected.Name, StringComparison.Ordinal);
        ShowStatus(
            $"Corrected {target}. Press F5 to upload."
                + (nameChanged
                    ? " The printed NAME changed — press F2 to reprint the badge."
                    : " The printed badge is unchanged."),
            isError: false);
        ClearForm();
        RefreshCounters();
    }

    /// <summary>
    /// Asks which sequence to correct.
    ///
    /// <para>Defaults to the most recently REJECTED row, not the
    /// most recent pending one. The queue does not stop while an operator reads
    /// a rejection, so rows registered afterwards are also pending and sort
    /// last; a keyboard-only operator pressing Enter on that default would have
    /// overwritten an unrelated visitor's record with the corrected data of a
    /// different person, irreversibly. The dialog also names the holder, so what
    /// is about to be overwritten is visible before it happens.</para>
    /// </summary>
    private static long? PromptForSequence(
        IReadOnlyList<StoredRegistration> pending,
        IReadOnlyList<OfflineBadgeUploadResult> rejections)
    {
        var suggested = rejections
            .Select(rejection => rejection.Sequence)
            .FirstOrDefault(sequence =>
                pending.Any(record => record.Sequence == sequence));
        if (suggested == 0) { suggested = pending[^1].Sequence; }

        using var dialog = new Form
        {
            Text = "Correct which badge number?",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(420, 120),
            MinimizeBox = false,
            MaximizeBox = false,
        };
        var input = new TextBox
        {
            Dock = DockStyle.Top,
            Text = suggested.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        var holder = pending.FirstOrDefault(record => record.Sequence == suggested);
        var who = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 28,
            Text = holder is null
                ? string.Empty
                : $"This will overwrite: {holder.Name}",
        };
        var ok = new Button
        {
            Text = "Correct",
            Dock = DockStyle.Bottom,
            DialogResult = DialogResult.OK,
        };
        dialog.Controls.Add(who);
        dialog.Controls.Add(input);
        dialog.Controls.Add(ok);
        dialog.AcceptButton = ok;
        input.Select();
        input.SelectAll();

        if (dialog.ShowDialog() != DialogResult.OK) { return null; }
        return long.TryParse(
            input.Text.Trim(),
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Reprints the most recent badge.
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
            new EventBadgePayload(
                record.ProfileId, _config.EditionYear, record.ProfileTypeCode),
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

    /// <summary>
    /// F5: sign the shift in if it has not been, then upload NOW.
    ///
    /// <para>The operator is asked for a token once per launch. After that the
    /// desk uploads on its own and F5 is a "don't wait" button: it resets the
    /// backoff and releases the rows held back for correction, because a person
    /// pressing it is a person saying they have dealt with whatever the last
    /// report complained about.</para>
    /// </summary>
    private void StartUpload()
    {
        if (_store.PendingUploadCount == 0)
        {
            ShowStatus("Nothing to upload.", isError: false);
            return;
        }
        if (string.IsNullOrWhiteSpace(_config.ApiBaseUrl))
        {
            ShowStatus("No ApiBaseUrl is configured for this desk.", isError: true);
            return;
        }

        if (!_backlog.HasToken)
        {
            var token = PromptForToken();
            if (string.IsNullOrWhiteSpace(token)) { return; }
            _backlog.UseToken(token);
            // From here the shift uploads by itself; F5 only ever means "sooner".
            _uploadTimer.Start();
        }

        var sending = Math.Min(_store.PendingUploadCount, UploadClient.MaxBatchSize);
        ShowStatus($"Uploading {sending} registrations…", isError: false);
        _ = DrainAsync(force: true);
    }

    private void OnUploadTick(object? sender, EventArgs args) => _ = DrainAsync(force: false);

    /// <summary>
    /// One attempt at the backlog, and whatever it produced put on screen.
    ///
    /// <para>Every path out of the uploader ends here, including the ones the
    /// timer starts with nobody watching, which is why the catch is total. An
    /// autonomous loop must not fail more quietly than the manual action it
    /// replaced: an exception from a discarded task is swallowed by the runtime,
    /// and the desk would go on ticking, uploading nothing, looking perfectly
    /// healthy — the exact failure this loop exists to remove.</para>
    /// </summary>
    private async Task DrainAsync(bool force)
    {
        UploadAttempt attempt;
        try
        {
            attempt = await _backlog.TryUploadAsync(force, _closing.Token);
        }
        catch (Exception ex)
        {
            // The uploader classifies everything the desk can meet in a hall.
            // Anything left is a defect, and it is worth more on the operator's
            // screen than in a task nobody awaited.
            if (IsDisposed || Disposing) { return; }
            ShowStatus($"Upload stopped unexpectedly: {ex.Message}", isError: true);
            return;
        }

        // The window can be closed while a batch is on the wire.
        if (IsDisposed || Disposing) { return; }

        switch (attempt.Kind)
        {
            case UploadAttemptKind.Uploaded when attempt.Response is { } response:
                _reportedFailure = null;
                ShowUploadReport(response);
                break;

            case UploadAttemptKind.Failed:
                ReportFailure(attempt);
                break;

            case UploadAttemptKind.CredentialExpired:
                // Reported in the server's own words: 401 means the pasted token
                // has expired and 403 means the account cannot upload at all, and
                // an operator handed a single canned sentence would chase the
                // wrong one of those.
                _reportedFailure = null;
                _uploadTimer.Stop();
                ShowStatus(
                    $"The upload was refused: {attempt.FailureMessage}. Uploading is "
                    + "paused — press F5 to paste a fresh token. Nothing was lost; "
                    + "every registration is still recorded on this desk.",
                    isError: true);
                break;
        }

        RefreshCounters();
    }

    /// <summary>Says a retry is coming, once per distinct failure. The live
    /// countdown belongs to the counters line, which is rebuilt every tick by
    /// design; this label holds the report an operator reads.</summary>
    private void ReportFailure(UploadAttempt attempt)
    {
        if (_reportedFailure == attempt.FailureMessage) { return; }
        _reportedFailure = attempt.FailureMessage;
        ShowStatus(
            $"Upload failed: {attempt.FailureMessage}. Nothing was lost — "
            + $"retrying in {Describe(attempt.RetryIn)}.",
            isError: true);
    }

    /// <summary>
    /// The reconciliation report for one batch.
    ///
    /// <para>Rendered the same whether the operator pressed F5 or the timer did
    /// it unattended. That is deliberate for the key-version warning below: a
    /// desk keyed wrongly must learn it from whichever upload happens to be
    /// first, and once uploads run by themselves that is usually not a human's.
    /// </para>
    /// </summary>
    private void ShowUploadReport(OfflineBadgeBatchResponse response)
    {
        _rejections.Clear();
        _rejections.AddRange(response.Results
            .Where(result => result.Status == OfflineBadgeUploadStatus.Rejected)
            .Reverse());

        var summary =
            $"Uploaded {response.Submitted}: {response.Created} approved, "
            + $"{response.PendingApproval} awaiting approval, "
            + $"{response.AlreadyUploaded} already known, {response.Rejected} rejected.";
        if (_rejections.Count > 0)
        {
            // Name every refused row and why. An operator cannot correct what the
            // console will not tell them. They are also held out of the automatic
            // loop from here, so this text stays on screen to be acted on instead
            // of being overwritten by the same rejection a few seconds later.
            summary += Environment.NewLine
                + string.Join(Environment.NewLine, _rejections.Select(
                    result => $"  {result.Sequence}: {result.Message}"))
                + Environment.NewLine + "Press F3 to correct one.";
        }
        // A wrong badge key is the failure this desk cannot otherwise see: it
        // prints happily for three days and every badge is refused at the
        // gate with the same generic denial any garbage produces. The server
        // echoes which key version IT holds, so a mismatch surfaces here, on
        // the first upload, while the badges can still be reprinted.
        var keyMismatch = response.ServerBadgeKeyVersion != _config.BadgeKeyVersion;
        if (keyMismatch)
        {
            summary += Environment.NewLine
                + $"WARNING: this desk is keyed v{_config.BadgeKeyVersion}, the "
                + $"server holds v{response.ServerBadgeKeyVersion}. Badges printed "
                + "here will NOT scan. Stop and re-provision the key before "
                + "printing more.";
        }

        ShowStatus(summary, isError: response.Rejected > 0 || keyMismatch);
    }

    private static string? PromptForToken()
    {
        using var dialog = new Form
        {
            Text = "Paste a Control Panel bearer token",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(560, 160),
            MinimizeBox = false,
            MaximizeBox = false,
        };
        // Says what happens to it, because what happens to it is unusual: the
        // desk keeps this token for the shift so uploads can run unattended, and
        // still writes it nowhere. Being asked again after a restart is the
        // visible half of that bargain, and an operator who is not told assumes
        // something is broken.
        var explain = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 40,
            Text = "Kept in memory for this shift only — it is never written to "
                + "this machine, so it is asked for again after a restart.",
        };
        dialog.Controls.Add(explain);
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

    private void RefreshCounters()
    {
        var text =
            $"Registered this desk: {_store.Records.Count}   •   "
            + $"Waiting to upload: {_store.PendingUploadCount}   •   "
            + $"Uploads: {DescribeUploads()}   •   "
            + $"Range {_config.SequenceRangeStart}–{_config.SequenceRangeEnd}";

        // Assigned only when it has changed. This runs on every tick, and a Label
        // repaints on every assignment even when the text is identical.
        if (!string.Equals(_counters.Text, text, StringComparison.Ordinal))
        {
            _counters.Text = text;
        }
    }

    /// <summary>
    /// What the uploader is doing, in the line the operator already watches.
    ///
    /// <para>The honest answer to "is this desk uploading?", which a desk that
    /// uploads by itself has to be able to give at a glance. Deliberately NOT in
    /// the status label: that one holds the report being read to correct refused
    /// rows, and a countdown rewritten every second would scroll it away.</para>
    /// </summary>
    private string DescribeUploads()
    {
        if (_backlog.IsUploading) { return "sending…"; }

        var pending = _store.PendingUploadCount;
        if (pending == 0) { return "up to date"; }
        if (!_backlog.HasToken) { return "paused — press F5 to paste a token"; }
        if (_backlog.HeldBackCount >= pending)
        {
            return $"{_backlog.HeldBackCount} rejected — press F3 to correct";
        }

        var wait = _backlog.RetryIn;
        return wait > TimeSpan.Zero ? $"retrying in {Describe(wait)}" : "sending…";
    }

    /// <summary>A wait an operator can read at a glance: seconds under a minute,
    /// minutes and seconds above it.</summary>
    private static string Describe(TimeSpan wait) =>
        wait < TimeSpan.FromMinutes(1)
            ? $"{Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds))}s"
            : $"{(int)wait.TotalMinutes}:{wait.Seconds:D2}";

    private void ShowStatus(string message, bool isError)
    {
        _status.Text = message;
        _status.ForeColor = isError ? Color.Firebrick : Color.DarkGreen;
    }

    /// <summary>Stops the loop and cuts an upload in flight.
    ///
    /// <para>Closing the window mid-batch loses nothing. The batch is idempotent
    /// by sequence, so any row the server took but never acknowledged is reported
    /// as AlreadyUploaded the next time the desk sends it, and the local file —
    /// which is the record that a badge was handed out — was written before the
    /// badge was ever printed.</para></summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _uploadTimer.Stop();
            _uploadTimer.Dispose();
            _closing.Cancel();
            _closing.Dispose();
        }
        base.Dispose(disposing);
    }
}
