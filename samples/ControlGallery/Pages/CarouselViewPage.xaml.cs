namespace ControlGallery.Pages;

public partial class CarouselViewPage : ContentPage
{
    private readonly List<CarouselItem> _items;

    public CarouselViewPage()
    {
        InitializeComponent();

        _items = new List<CarouselItem>
        {
            new() { Title = "Welcome", Description = "Get started with OpenMaui", Icon = "👋", Color = Color.FromArgb("#512BD4") },
            new() { Title = "Controls", Description = "35+ beautiful controls", Icon = "🎨", Color = Color.FromArgb("#2196F3") },
            new() { Title = "Native", Description = "X11 & Wayland support", Icon = "🐧", Color = Color.FromArgb("#4CAF50") },
            new() { Title = "Fast", Description = "Hardware accelerated", Icon = "⚡", Color = Color.FromArgb("#FF9800") },
            new() { Title = "Accessible", Description = "Screen reader support", Icon = "♿", Color = Color.FromArgb("#9C27B0") },
        };

        Carousel.ItemsSource = _items;
        Carousel.IndicatorView = CarouselIndicator;

        UpdateCurrentItemLabel();
    }

    private void OnCurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        UpdateCurrentItemLabel();
    }

    private void UpdateCurrentItemLabel()
    {
        var index = _items.IndexOf(Carousel.CurrentItem as CarouselItem);
        CurrentItemLabel.Text = $"Slide {index + 1} of {_items.Count}";
    }

    private void OnPreviousClicked(object sender, EventArgs e)
    {
        var index = _items.IndexOf(Carousel.CurrentItem as CarouselItem);
        if (index > 0)
        {
            Carousel.ScrollTo(index - 1);
        }
        else
        {
            Carousel.ScrollTo(_items.Count - 1);
        }
    }

    private void OnNextClicked(object sender, EventArgs e)
    {
        var index = _items.IndexOf(Carousel.CurrentItem as CarouselItem);
        if (index < _items.Count - 1)
        {
            Carousel.ScrollTo(index + 1);
        }
        else
        {
            Carousel.ScrollTo(0);
        }
    }
}

public class CarouselItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public Color Color { get; set; } = Colors.Gray;
}
