namespace ControlGallery.Pages;

public partial class SlidersPage : ContentPage
{
    public SlidersPage()
    {
        InitializeComponent();
    }

    private void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        SliderValueLabel.Text = $"Value: {e.NewValue:F0}";
    }

    private void OnStepperValueChanged(object sender, ValueChangedEventArgs e)
    {
        StepperValueLabel.Text = $"Value: {e.NewValue:F0}";
    }

    private void OnDecimalStepperValueChanged(object sender, ValueChangedEventArgs e)
    {
        DecimalStepperLabel.Text = $"Value: {e.NewValue:F1}";
    }

    private void OnSizeSliderChanged(object sender, ValueChangedEventArgs e)
    {
        DemoBox.WidthRequest = e.NewValue;
        DemoBox.HeightRequest = e.NewValue;
    }
}
