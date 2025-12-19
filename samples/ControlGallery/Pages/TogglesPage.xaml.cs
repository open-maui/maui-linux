namespace ControlGallery.Pages;

public partial class TogglesPage : ContentPage
{
    public TogglesPage()
    {
        InitializeComponent();
    }

    private void OnCheckBoxChanged(object sender, CheckedChangedEventArgs e)
    {
        CheckBoxLabel.Text = e.Value ? "Agreed!" : "Not agreed";
    }

    private void OnSwitchToggled(object sender, ToggledEventArgs e)
    {
        SwitchLabel.Text = $"Notifications: {(e.Value ? "On" : "Off")}";
    }

    private void OnRadioButtonChecked(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value && sender is RadioButton rb)
        {
            RadioLabel.Text = $"Selected: {rb.Content}";
        }
    }
}
