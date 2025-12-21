// TextInputPage - Demonstrates text input controls

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ShellDemo;

public class TextInputPage : ContentPage
{
    private Label _entryOutput;
    private Label _searchOutput;
    private Label _editorOutput;

    public TextInputPage()
    {
        Title = "Text Input";

        _entryOutput = new Label { TextColor = Colors.Gray, FontSize = 12 };
        _searchOutput = new Label { TextColor = Colors.Gray, FontSize = 12 };
        _editorOutput = new Label { TextColor = Colors.Gray, FontSize = 12 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 15,
                Children =
                {
                    new Label
                    {
                        Text = "Text Input Controls",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = "Click on any field and start typing. All keyboard input is handled by the framework.",
                        FontSize = 14,
                        TextColor = Colors.Gray
                    },

                    // Entry Section
                    new BoxView { HeightRequest = 1, Color = Colors.LightGray },
                    new Label { Text = "Entry (Single Line)", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    CreateEntry("Enter your name...", e => _entryOutput.Text = $"You typed: {e.Text}"),
                    _entryOutput,

                    CreateEntry("Enter your email...", null, Keyboard.Email),
                    new Label { Text = "Email keyboard type", FontSize = 12, TextColor = Colors.Gray },

                    CreatePasswordEntry("Enter password..."),
                    new Label { Text = "Password field (text hidden)", FontSize = 12, TextColor = Colors.Gray },

                    // SearchBar Section
                    new BoxView { HeightRequest = 1, Color = Colors.LightGray },
                    new Label { Text = "SearchBar", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    CreateSearchBar(),
                    _searchOutput,

                    // Editor Section
                    new BoxView { HeightRequest = 1, Color = Colors.LightGray },
                    new Label { Text = "Editor (Multi-line)", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    CreateEditor(),
                    _editorOutput,

                    // Instructions
                    new BoxView { HeightRequest = 1, Color = Colors.LightGray },
                    new Frame
                    {
                        BackgroundColor = Color.FromArgb("#E3F2FD"),
                        CornerRadius = 8,
                        Padding = new Thickness(15),
                        Content = new VerticalStackLayout
                        {
                            Spacing = 5,
                            Children =
                            {
                                new Label
                                {
                                    Text = "Keyboard Shortcuts",
                                    FontAttributes = FontAttributes.Bold
                                },
                                new Label { Text = "Ctrl+A: Select all" },
                                new Label { Text = "Ctrl+C: Copy" },
                                new Label { Text = "Ctrl+V: Paste" },
                                new Label { Text = "Ctrl+X: Cut" },
                                new Label { Text = "Home/End: Move to start/end" },
                                new Label { Text = "Shift+Arrow: Select text" }
                            }
                        }
                    }
                }
            }
        };
    }

    private Entry CreateEntry(string placeholder, Action<Entry>? onTextChanged, Keyboard? keyboard = null)
    {
        var entry = new Entry
        {
            Placeholder = placeholder,
            FontSize = 14
        };

        if (keyboard != null)
        {
            entry.Keyboard = keyboard;
        }

        if (onTextChanged != null)
        {
            entry.TextChanged += (s, e) => onTextChanged(entry);
        }

        return entry;
    }

    private Entry CreatePasswordEntry(string placeholder)
    {
        return new Entry
        {
            Placeholder = placeholder,
            FontSize = 14,
            IsPassword = true
        };
    }

    private SearchBar CreateSearchBar()
    {
        var searchBar = new SearchBar
        {
            Placeholder = "Search for items..."
        };

        searchBar.TextChanged += (s, e) =>
        {
            _searchOutput.Text = $"Searching: {e.NewTextValue}";
        };

        searchBar.SearchButtonPressed += (s, e) =>
        {
            _searchOutput.Text = $"Search submitted: {searchBar.Text}";
        };

        return searchBar;
    }

    private Editor CreateEditor()
    {
        var editor = new Editor
        {
            Placeholder = "Enter multiple lines of text here...\nPress Enter to create new lines.",
            HeightRequest = 120,
            FontSize = 14
        };

        editor.TextChanged += (s, e) =>
        {
            var lineCount = string.IsNullOrEmpty(e.NewTextValue) ? 0 : e.NewTextValue.Split('\n').Length;
            _editorOutput.Text = $"Lines: {lineCount}, Characters: {e.NewTextValue?.Length ?? 0}";
        };

        return editor;
    }
}
