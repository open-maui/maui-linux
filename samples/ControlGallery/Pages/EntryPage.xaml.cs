namespace ControlGallery.Pages;

public partial class EntryPage : ContentPage
{
    public EntryPage()
    {
        InitializeComponent();
    }

    private void OnEntryTextChanged(object sender, TextChangedEventArgs e)
    {
        BoundLabel.Text = $"You typed: {e.NewTextValue}";
    }
}
