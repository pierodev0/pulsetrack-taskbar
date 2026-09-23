using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class TimerPickerFormTests
{
    [Fact]
    public void InitialSeconds_SplitIntoMinutesAndSeconds()
    {
        using var form = new TimerPickerForm(150);

        Assert.Equal(150, form.SelectedSeconds);
        Assert.True(form.OkEnabled);
    }

    [Fact]
    public void ZeroSeconds_DisablesStart()
    {
        using var form = new TimerPickerForm(0);

        Assert.Equal(0, form.SelectedSeconds);
        Assert.False(form.OkEnabled);
    }

    [Fact]
    public void OversizedInitial_ClampsToMaximum()
    {
        using var form = new TimerPickerForm(999_999);

        Assert.Equal(180 * 60, form.SelectedSeconds);
    }
}
