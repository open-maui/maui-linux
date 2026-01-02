using System;
using Microsoft.Maui.Controls;

namespace ShellDemo.Pages;

public partial class PickersPage : ContentPage
{
    private int _eventCount = 0;

    public PickersPage()
    {
        InitializeComponent();

        // Set date range for the range picker
        var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
        RangeDatePicker.MinimumDate = startOfMonth;
        RangeDatePicker.MaximumDate = endOfMonth;
    }

    private void LogEvent(string message)
    {
        _eventCount++;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        EventLog.Text = $"[{timestamp}] {_eventCount}. {message}\n{EventLog.Text}";
    }

    private void OnFruitPickerChanged(object? sender, EventArgs e)
    {
        if (FruitPicker.SelectedIndex >= 0)
        {
            var item = FruitPicker.ItemsSource[FruitPicker.SelectedIndex]?.ToString();
            FruitSelectedLabel.Text = $"Selected: {item}";
            LogEvent($"Fruit selected: {item}");
        }
    }

    private void OnColorPickerChanged(object? sender, EventArgs e)
    {
        if (ColorPicker.SelectedIndex >= 0)
        {
            LogEvent($"Color selected: {ColorPicker.ItemsSource[ColorPicker.SelectedIndex]}");
        }
    }

    private void OnSizePickerChanged(object? sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            LogEvent($"Size selected: {picker.ItemsSource[picker.SelectedIndex]}");
        }
    }

    private void OnDateSelected(object? sender, DateChangedEventArgs e)
    {
        DateSelectedLabel.Text = $"Selected: {e.NewDate:d}";
        LogEvent($"Date selected: {e.NewDate:d}");
    }

    private void OnRangeDateSelected(object? sender, DateChangedEventArgs e)
    {
        LogEvent($"Date (limited): {e.NewDate:d}");
    }

    private void OnStyledDateSelected(object? sender, DateChangedEventArgs e)
    {
        LogEvent($"Styled date: {e.NewDate:d}");
    }

    private void OnTimeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimePicker.Time))
        {
            var time = BasicTimePicker.Time;
            TimeSelectedLabel.Text = $"Selected: {time:hh\\:mm}";
            LogEvent($"Time selected: {time:hh\\:mm}");
        }
    }

    private void OnStyledTimeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimePicker.Time) && sender is TimePicker picker)
        {
            LogEvent($"Styled time: {picker.Time:hh\\:mm}");
        }
    }

    private void OnSetAlarmClicked(object? sender, EventArgs e)
    {
        LogEvent($"Alarm set for {AlarmTimePicker.Time:hh\\:mm}");
    }
}
