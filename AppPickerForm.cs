namespace PulseTrack.Taskbar;

public class AppPickerForm : Form
{
    private readonly ListBox _list = new();
    private readonly Button _okBtn = new() { Text = "OK", DialogResult = DialogResult.OK };
    private readonly Button _cancelBtn = new() { Text = "Cancel", DialogResult = DialogResult.Cancel };
    private readonly Button _refreshBtn = new() { Text = "Refresh" };

    private List<WindowInfo> _windows = new();

    public string? SelectedApp { get; private set; }

    public AppPickerForm(string? current)
    {
        Text = "PulseTrack - Select app";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 320);

        _list.Location = new Point(12, 12);
        _list.Size = new Size(396, 240);
        _list.DoubleClick += (_, _) => { if (_list.SelectedIndex >= 0) { AcceptSelection(); DialogResult = DialogResult.OK; Close(); } };
        Controls.Add(_list);

        _refreshBtn.Location = new Point(12, 260);
        _refreshBtn.Size = new Size(80, 28);
        _refreshBtn.Click += (_, _) => LoadWindows(current);
        Controls.Add(_refreshBtn);

        _okBtn.Location = new Point(248, 260);
        _okBtn.Size = new Size(80, 28);
        _okBtn.Click += (_, _) => AcceptSelection();
        Controls.Add(_okBtn);

        _cancelBtn.Location = new Point(334, 260);
        _cancelBtn.Size = new Size(80, 28);
        Controls.Add(_cancelBtn);

        AcceptButton = _okBtn;
        CancelButton = _cancelBtn;

        LoadWindows(current);
    }

    private void LoadWindows(string? current)
    {
        _windows = WindowWatcher.ListOpenWindows();
        _list.Items.Clear();
        int selectIdx = -1;
        for (int i = 0; i < _windows.Count; i++)
        {
            var w = _windows[i];
            _list.Items.Add($"{w.ProcessName} — {w.Title}");
            if (current != null && string.Equals(w.ProcessName, current, StringComparison.OrdinalIgnoreCase))
                selectIdx = i;
        }
        if (selectIdx >= 0) _list.SelectedIndex = selectIdx;
        else if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    private void AcceptSelection()
    {
        if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _windows.Count)
            SelectedApp = _windows[_list.SelectedIndex].ProcessName;
    }
}
