namespace ControlGallery.Pages;

public partial class ProgressPage : ContentPage
{
    public ProgressPage()
    {
        InitializeComponent();
    }

    private void OnProgress0(object sender, EventArgs e) => DemoProgress.Progress = 0;
    private void OnProgress50(object sender, EventArgs e) => DemoProgress.Progress = 0.5;
    private void OnProgress100(object sender, EventArgs e) => DemoProgress.Progress = 1.0;

    private void OnStartIndicator(object sender, EventArgs e) => ControlledIndicator.IsRunning = true;
    private void OnStopIndicator(object sender, EventArgs e) => ControlledIndicator.IsRunning = false;

    private async void OnAnimateProgress(object sender, EventArgs e)
    {
        AnimatedProgress.Progress = 0;
        await AnimatedProgress.ProgressTo(1.0, 2000, Easing.Linear);
    }

    private async void OnShowLoading(object sender, EventArgs e)
    {
        ContentPanel.IsVisible = false;
        LoadingPanel.IsVisible = true;

        await Task.Delay(2000);

        LoadingPanel.IsVisible = false;
        ContentPanel.IsVisible = true;
    }
}
