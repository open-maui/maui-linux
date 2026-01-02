using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace ShellDemo.Pages;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
        ThemeSwitch.IsToggled = Application.Current?.UserAppTheme == AppTheme.Dark;
    }

    private void OnThemeToggled(object? sender, ToggledEventArgs e)
    {
        if (Application.Current != null)
        {
            Application.Current.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;
        }
    }

    private async void OnButtonsCardTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//Buttons");
    }

    private async void OnTextInputCardTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//TextInput");
    }

    private async void OnSelectionCardTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//Selection");
    }

    private async void OnPickersCardTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//Pickers");
    }

    private async void OnListsCardTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//Lists");
    }

    private async void OnProgressCardTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//Progress");
    }

    private async void OnTryButtonsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Buttons");
    }

    private async void OnTryListsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Lists");
    }
}
