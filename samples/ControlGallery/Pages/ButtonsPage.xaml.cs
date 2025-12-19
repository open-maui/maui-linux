namespace ControlGallery.Pages;

public partial class ButtonsPage : ContentPage
{
    private int _clickCount = 0;

    public ButtonsPage()
    {
        InitializeComponent();
    }

    private void OnButtonClicked(object sender, EventArgs e)
    {
        _clickCount++;
        ButtonResultLabel.Text = $"Button clicked {_clickCount} time(s)";
    }

    private async void OnImageButtonClicked(object sender, EventArgs e)
    {
        await DisplayAlert("ImageButton", "You clicked the ImageButton!", "OK");
    }
}
