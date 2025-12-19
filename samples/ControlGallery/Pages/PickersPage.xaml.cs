namespace ControlGallery.Pages;

public partial class PickersPage : ContentPage
{
    public PickersPage()
    {
        InitializeComponent();

        // Set date range
        RangeDatePicker.MinimumDate = DateTime.Today;
        RangeDatePicker.MaximumDate = DateTime.Today.AddDays(30);
    }

    private void OnColorPickerChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        if (picker.SelectedIndex >= 0)
        {
            ColorResultLabel.Text = $"Selected: {picker.Items[picker.SelectedIndex]}";
        }
    }

    private void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        DateResultLabel.Text = $"Selected: {e.NewDate:MMMM dd, yyyy}";
    }

    private void OnTimeChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimePicker.Time))
        {
            var picker = (TimePicker)sender;
            TimeResultLabel.Text = $"Selected: {picker.Time:hh\\:mm}";
        }
    }
}
