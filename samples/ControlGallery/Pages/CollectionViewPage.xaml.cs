namespace ControlGallery.Pages;

public partial class CollectionViewPage : ContentPage
{
    public CollectionViewPage()
    {
        InitializeComponent();

        var items = new List<ItemModel>
        {
            new() { Title = "Product A", Description = "High quality item", Price = 29.99m, Color = Colors.Purple },
            new() { Title = "Product B", Description = "Best seller", Price = 49.99m, Color = Colors.Blue },
            new() { Title = "Product C", Description = "New arrival", Price = 19.99m, Color = Colors.Green },
            new() { Title = "Product D", Description = "Limited edition", Price = 99.99m, Color = Colors.Orange },
            new() { Title = "Product E", Description = "Customer favorite", Price = 39.99m, Color = Colors.Red },
            new() { Title = "Product F", Description = "Eco-friendly", Price = 24.99m, Color = Colors.Teal },
            new() { Title = "Product G", Description = "Premium quality", Price = 79.99m, Color = Colors.Indigo },
            new() { Title = "Product H", Description = "Budget friendly", Price = 9.99m, Color = Colors.Pink },
        };

        ItemsCollection.ItemsSource = items;
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ItemModel item)
        {
            await DisplayAlert("Selected", $"You selected {item.Title}", "OK");
            ((CollectionView)sender).SelectedItem = null;
        }
    }
}

public class ItemModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Color Color { get; set; } = Colors.Gray;
}
