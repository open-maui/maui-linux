namespace ControlGallery.Pages;

public partial class SwipeViewPage : ContentPage
{
    public SwipeViewPage()
    {
        InitializeComponent();
    }

    private async void OnDeleteInvoked(object sender, EventArgs e)
    {
        await DisplayAlert("Delete", "Item would be deleted", "OK");
    }

    private async void OnArchiveInvoked(object sender, EventArgs e)
    {
        await DisplayAlert("Archive", "Item would be archived", "OK");
    }

    private async void OnFavoriteInvoked(object sender, EventArgs e)
    {
        await DisplayAlert("Favorite", "Item added to favorites", "OK");
    }

    private async void OnReplyInvoked(object sender, EventArgs e)
    {
        await DisplayAlert("Reply", "Opening reply composer", "OK");
    }

    private async void OnForwardInvoked(object sender, EventArgs e)
    {
        await DisplayAlert("Forward", "Opening forward dialog", "OK");
    }
}
