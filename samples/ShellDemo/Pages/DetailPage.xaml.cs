using System;
using Microsoft.Maui.Controls;

namespace ShellDemo.Pages;

[QueryProperty(nameof(ItemName), "item")]
public partial class DetailPage : ContentPage
{
    private string _itemName = "Detail Item";

    public string ItemName
    {
        get => _itemName;
        set
        {
            _itemName = value;
            Title = $"Detail: {value}";
            if (ItemLabel != null)
            {
                ItemLabel.Text = $"You navigated to: {value}";
            }
        }
    }

    public DetailPage()
    {
        InitializeComponent();
    }

    private async void OnGoBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
