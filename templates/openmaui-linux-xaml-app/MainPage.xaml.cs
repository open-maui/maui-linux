namespace OpenMauiXamlApp;

public partial class MainPage : ContentPage
{
    private int _count = 0;

    public MainPage()
    {
        InitializeComponent();

        // Wire up slider value changed
        VolumeSlider.ValueChanged += OnVolumeChanged;
    }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        _count++;

        CounterBtn.Text = _count == 1
            ? $"Clicked {_count} time"
            : $"Clicked {_count} times";

        SemanticScreenReader.Announce(CounterBtn.Text);
    }

    private void OnVolumeChanged(object? sender, ValueChangedEventArgs e)
    {
        VolumeLabel.Text = $"Volume: {e.NewValue:F0}";
    }
}
