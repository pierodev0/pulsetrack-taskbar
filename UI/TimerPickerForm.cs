namespace PulseTrack.Taskbar;

public class TimerPickerForm : Form
{
    private readonly NumericUpDown _minutes = new();
    private readonly NumericUpDown _seconds = new();
    private readonly Button _okBtn = new() { Text = "OK", DialogResult = DialogResult.OK };
    private readonly Button _cancelBtn = new() { Text = "Cancel", DialogResult = DialogResult.Cancel };

    public double SelectedSeconds => (double)_minutes.Value * 60 + (double)_seconds.Value;

    public TimerPickerForm(int initialSeconds)
    {
        Text = "PulseTrack - Set timer";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(278, 128);
        TopMost = true;
        ShowInTaskbar = false;
        Shown += (_, _) => Activate();

        initialSeconds = Math.Clamp(initialSeconds, 0, 180 * 60);

        var minLabel = new Label { Text = "Minutes", Location = new Point(14, 14), AutoSize = true };
        var secLabel = new Label { Text = "Seconds", Location = new Point(110, 14), AutoSize = true };

        _minutes.Location = new Point(14, 34);
        _minutes.Size = new Size(80, 23);
        _minutes.Minimum = 0;
        _minutes.Maximum = 180;
        _minutes.Value = initialSeconds / 60;

        _seconds.Location = new Point(110, 34);
        _seconds.Size = new Size(80, 23);
        _seconds.Minimum = 0;
        _seconds.Maximum = 59;
        _seconds.Value = initialSeconds % 60;

        _okBtn.Location = new Point(104, 76);
        _okBtn.Size = new Size(80, 28);
        _cancelBtn.Location = new Point(190, 76);
        _cancelBtn.Size = new Size(76, 28);

        _minutes.ValueChanged += (_, _) => SyncStart();
        _seconds.ValueChanged += (_, _) => SyncStart();

        Controls.AddRange(new Control[] { minLabel, secLabel, _minutes, _seconds, _okBtn, _cancelBtn });

        AcceptButton = _okBtn;
        CancelButton = _cancelBtn;

        SyncStart();
    }

    private void SyncStart() => _okBtn.Enabled = SelectedSeconds > 0;

    internal bool OkEnabled => _okBtn.Enabled;
}
