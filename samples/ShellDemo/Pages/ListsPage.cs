// ListsPage - CollectionView and ListView Demo

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ShellDemo;

public class ListsPage : ContentPage
{
    private readonly Label _eventLog;
    private int _eventCount = 0;

    public ListsPage()
    {
        Title = "Lists";

        _eventLog = new Label
        {
            Text = "Events will appear here...",
            FontSize = 11,
            TextColor = Colors.Gray,
            LineBreakMode = LineBreakMode.WordWrap
        };

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(120) }
            },
            Children =
            {
                CreateMainContent(),
                CreateEventLogPanel()
            }
        };

        Grid.SetRow((View)((Grid)Content).Children[0], 0);
        Grid.SetRow((View)((Grid)Content).Children[1], 1);
    }

    private View CreateMainContent()
    {
        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 20,
                Children =
                {
                    new Label { Text = "List Controls", FontSize = 24, FontAttributes = FontAttributes.Bold },

                    CreateSection("CollectionView - Fruits", CreateFruitsCollectionView()),
                    CreateSection("CollectionView - Colors", CreateColorsCollectionView()),
                    CreateSection("CollectionView - Contacts", CreateContactsCollectionView())
                }
            }
        };
    }

    private View CreateFruitsCollectionView()
    {
        var layout = new VerticalStackLayout { Spacing = 10 };

        var fruits = new List<string>
        {
            "Apple", "Banana", "Cherry", "Date", "Elderberry",
            "Fig", "Grape", "Honeydew", "Kiwi", "Lemon",
            "Mango", "Nectarine", "Orange", "Papaya", "Quince"
        };

        var selectedLabel = new Label { Text = "Tap a fruit to select", TextColor = Colors.Gray };

        var collectionView = new CollectionView
        {
            ItemsSource = fruits,
            HeightRequest = 200,
            SelectionMode = SelectionMode.Single,
            BackgroundColor = Color.FromArgb("#FAFAFA")
        };

        collectionView.SelectionChanged += (s, e) =>
        {
            if (e.CurrentSelection.Count > 0)
            {
                var item = e.CurrentSelection[0]?.ToString();
                selectedLabel.Text = $"Selected: {item}";
                LogEvent($"Fruit selected: {item}");
            }
        };

        layout.Children.Add(collectionView);
        layout.Children.Add(selectedLabel);

        return layout;
    }

    private View CreateColorsCollectionView()
    {
        var layout = new VerticalStackLayout { Spacing = 10 };

        var colors = new List<ColorItem>
        {
            new("Red", "#F44336"),
            new("Pink", "#E91E63"),
            new("Purple", "#9C27B0"),
            new("Deep Purple", "#673AB7"),
            new("Indigo", "#3F51B5"),
            new("Blue", "#2196F3"),
            new("Cyan", "#00BCD4"),
            new("Teal", "#009688"),
            new("Green", "#4CAF50"),
            new("Light Green", "#8BC34A"),
            new("Lime", "#CDDC39"),
            new("Yellow", "#FFEB3B"),
            new("Amber", "#FFC107"),
            new("Orange", "#FF9800"),
            new("Deep Orange", "#FF5722")
        };

        var collectionView = new CollectionView
        {
            ItemsSource = colors,
            HeightRequest = 180,
            SelectionMode = SelectionMode.Single,
            BackgroundColor = Colors.White
        };

        collectionView.SelectionChanged += (s, e) =>
        {
            if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is ColorItem item)
            {
                LogEvent($"Color selected: {item.Name} ({item.Hex})");
            }
        };

        layout.Children.Add(collectionView);
        layout.Children.Add(new Label { Text = "Scroll to see all colors", FontSize = 11, TextColor = Colors.Gray });

        return layout;
    }

    private View CreateContactsCollectionView()
    {
        var layout = new VerticalStackLayout { Spacing = 10 };

        var contacts = new List<ContactItem>
        {
            new("Alice Johnson", "alice@example.com", "Engineering"),
            new("Bob Smith", "bob@example.com", "Marketing"),
            new("Carol Williams", "carol@example.com", "Design"),
            new("David Brown", "david@example.com", "Sales"),
            new("Eva Martinez", "eva@example.com", "Engineering"),
            new("Frank Lee", "frank@example.com", "Support"),
            new("Grace Kim", "grace@example.com", "HR"),
            new("Henry Wilson", "henry@example.com", "Finance")
        };

        var collectionView = new CollectionView
        {
            ItemsSource = contacts,
            HeightRequest = 200,
            SelectionMode = SelectionMode.Single,
            BackgroundColor = Colors.White
        };

        collectionView.SelectionChanged += (s, e) =>
        {
            if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is ContactItem contact)
            {
                LogEvent($"Contact: {contact.Name} - {contact.Department}");
            }
        };

        layout.Children.Add(collectionView);

        // Action buttons
        var buttonRow = new HorizontalStackLayout { Spacing = 10 };
        var addBtn = new Button { Text = "Add Contact", BackgroundColor = Colors.Green, TextColor = Colors.White };
        addBtn.Clicked += (s, e) => LogEvent("Add contact clicked");
        var deleteBtn = new Button { Text = "Delete Selected", BackgroundColor = Colors.Red, TextColor = Colors.White };
        deleteBtn.Clicked += (s, e) => LogEvent("Delete contact clicked");
        buttonRow.Children.Add(addBtn);
        buttonRow.Children.Add(deleteBtn);
        layout.Children.Add(buttonRow);

        return layout;
    }

    private Frame CreateSection(string title, View content)
    {
        return new Frame
        {
            CornerRadius = 8,
            Padding = new Thickness(15),
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold },
                    content
                }
            }
        };
    }

    private View CreateEventLogPanel()
    {
        return new Frame
        {
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Padding = new Thickness(10),
            CornerRadius = 0,
            Content = new VerticalStackLayout
            {
                Children =
                {
                    new Label { Text = "Event Log:", FontSize = 12, FontAttributes = FontAttributes.Bold },
                    new ScrollView
                    {
                        HeightRequest = 80,
                        Content = _eventLog
                    }
                }
            }
        };
    }

    private void LogEvent(string message)
    {
        _eventCount++;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _eventLog.Text = $"[{timestamp}] {_eventCount}. {message}\n{_eventLog.Text}";
    }
}

public record ColorItem(string Name, string Hex)
{
    public override string ToString() => Name;
}

public record ContactItem(string Name, string Email, string Department)
{
    public override string ToString() => $"{Name} ({Department})";
}
