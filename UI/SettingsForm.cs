namespace PulseTrack.Taskbar;

public class SettingsForm : Form
{
    private readonly SettingsViewModel _vm;

    private readonly ComboBox _fontBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _sizeBox = new() { Minimum = 6, Maximum = 32, DecimalPlaces = 1, Increment = 0.5m };
    private readonly CheckBox _boldCheck = new() { Text = "Bold" };
    private readonly CheckBox _italicCheck = new() { Text = "Italic" };
    private readonly CheckBox _bgCheck = new() { Text = "Show background" };
    private readonly Button _textColorBtn = new() { Text = "Text color..." };
    private readonly Button _bgColorBtn = new() { Text = "Background..." };
    private readonly Label _preview = new() { BorderStyle = BorderStyle.FixedSingle, TextAlign = ContentAlignment.MiddleCenter };
    private Font? _previewFont;
    private readonly Button _okBtn = new() { Text = "OK", DialogResult = DialogResult.OK };
    private readonly Button _cancelBtn = new() { Text = "Cancel", DialogResult = DialogResult.Cancel };

    public SettingsForm(SettingsViewModel vm)
    {
        _vm = vm;
        _vm.Load();

        Text = "PulseTrack - Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 300);

        var y = 12;
        AddLabel("Font:", 12, y);
        _fontBox.Location = new Point(110, y - 3);
        _fontBox.Size = new Size(230, 24);
        foreach (var family in FontFamily.Families.Select(f => f.Name).OrderBy(n => n))
            _fontBox.Items.Add(family);
        _fontBox.SelectedItem = _vm.FontFamily;
        if (_fontBox.SelectedIndex < 0 && _fontBox.Items.Count > 0)
            _fontBox.SelectedIndex = 0;
        _fontBox.SelectedIndexChanged += (_, _) => { _vm.FontFamily = _fontBox.SelectedItem?.ToString() ?? _vm.FontFamily; UpdatePreview(); };
        Controls.Add(_fontBox);
        y += 34;

        AddLabel("Size:", 12, y);
        _sizeBox.Location = new Point(110, y - 3);
        _sizeBox.Size = new Size(80, 24);
        _sizeBox.Value = (decimal)_vm.FontSize;
        _sizeBox.ValueChanged += (_, _) => { _vm.FontSize = (float)_sizeBox.Value; UpdatePreview(); };
        Controls.Add(_sizeBox);
        y += 34;

        _boldCheck.Location = new Point(110, y);
        _boldCheck.Checked = _vm.Bold;
        _boldCheck.CheckedChanged += (_, _) => { _vm.Bold = _boldCheck.Checked; UpdatePreview(); };
        Controls.Add(_boldCheck);

        _italicCheck.Location = new Point(200, y);
        _italicCheck.Checked = _vm.Italic;
        _italicCheck.CheckedChanged += (_, _) => { _vm.Italic = _italicCheck.Checked; UpdatePreview(); };
        Controls.Add(_italicCheck);
        y += 32;

        _textColorBtn.Location = new Point(12, y);
        _textColorBtn.Size = new Size(150, 28);
        _textColorBtn.Click += (_, _) => PickColor("Text color", _vm.TextColor, c => { _vm.TextColor = c; UpdatePreview(); });
        Controls.Add(_textColorBtn);

        _bgColorBtn.Location = new Point(190, y);
        _bgColorBtn.Size = new Size(150, 28);
        _bgColorBtn.Click += (_, _) => PickColor("Background", _vm.BackgroundColor, c => { _vm.BackgroundColor = c; UpdatePreview(); });
        Controls.Add(_bgColorBtn);
        y += 38;

        _bgCheck.Location = new Point(12, y);
        _bgCheck.Checked = _vm.ShowBackground;
        _bgCheck.CheckedChanged += (_, _) => _vm.ShowBackground = _bgCheck.Checked;
        Controls.Add(_bgCheck);
        y += 32;

        _preview.Location = new Point(12, y);
        _preview.Size = new Size(336, 44);
        Controls.Add(_preview);
        y += 54;

        _okBtn.Location = new Point(180, y);
        _okBtn.Size = new Size(80, 28);
        Controls.Add(_okBtn);

        _cancelBtn.Location = new Point(268, y);
        _cancelBtn.Size = new Size(80, 28);
        Controls.Add(_cancelBtn);

        AcceptButton = _okBtn;
        CancelButton = _cancelBtn;

        UpdatePreview();
    }

    private void AddLabel(string text, int x, int y)
    {
        Controls.Add(new Label { Text = text, Location = new Point(x, y + 3), AutoSize = true });
    }

    private void PickColor(string title, Color current, Action<Color> apply)
    {
        using var dlg = new ColorDialog { Color = current, FullOpen = true };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            apply(dlg.Color);
    }

    private void UpdatePreview()
    {
        _preview.Text = _vm.PreviewText;
        try
        {
            var style = FontStyle.Regular;
            if (_vm.Bold) style |= FontStyle.Bold;
            if (_vm.Italic) style |= FontStyle.Italic;
            var next = new Font(_vm.FontFamily, Math.Min(_vm.FontSize, 16f), style);
            _preview.Font = next;
            _previewFont?.Dispose();
            _previewFont = next;
        }
        catch { }
        _preview.ForeColor = _vm.TextColor;
        _preview.BackColor = _vm.ShowBackground ? _vm.BackgroundColor : SystemColors.Control;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _previewFont?.Dispose();
        _previewFont = null;
        base.OnFormClosed(e);
    }
}
