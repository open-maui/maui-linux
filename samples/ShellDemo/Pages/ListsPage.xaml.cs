using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;

namespace ShellDemo.Pages;

public partial class ListsPage : ContentPage
{
    private int _eventCount = 0;

    public ListsPage()
    {
        InitializeComponent();
        LoadData();
    }

    private void LoadData()
    {
        // Fruits
        var fruits = new List<string>
        {
            "Apple", "Banana", "Cherry", "Date", "Elderberry",
            "Fig", "Grape", "Honeydew", "Kiwi", "Lemon",
            "Mango", "Nectarine", "Orange", "Papaya", "Quince"
        };
        FruitsCollectionView.ItemsSource = fruits;

        // Colors
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
        ColorsCollectionView.ItemsSource = colors;

        // Contacts
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
        ContactsCollectionView.ItemsSource = contacts;
    }

    private void LogEvent(string message)
    {
        _eventCount++;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        EventLog.Text = $"[{timestamp}] {_eventCount}. {message}\n{EventLog.Text}";
    }

    private void OnFruitSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0)
        {
            var item = e.CurrentSelection[0]?.ToString();
            FruitSelectedLabel.Text = $"Selected: {item}";
            LogEvent($"Fruit selected: {item}");
        }
    }

    private void OnColorSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is ColorItem item)
        {
            LogEvent($"Color selected: {item.Name} ({item.Hex})");
        }
    }

    private void OnContactSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is ContactItem contact)
        {
            LogEvent($"Contact: {contact.Name} - {contact.Department}");
        }
    }

    private void OnAddContactClicked(object? sender, EventArgs e)
    {
        LogEvent("Add contact clicked");
    }

    private void OnDeleteContactClicked(object? sender, EventArgs e)
    {
        LogEvent("Delete contact clicked");
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
