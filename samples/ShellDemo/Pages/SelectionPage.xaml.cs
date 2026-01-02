using System;
using Microsoft.Maui.Controls;

namespace ShellDemo.Pages;

public partial class SelectionPage : ContentPage
{
    private int _eventCount = 0;

    public SelectionPage()
    {
        InitializeComponent();
    }

    private void LogEvent(string message)
    {
        _eventCount++;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        EventLog.Text = $"[{timestamp}] {_eventCount}. {message}\n{EventLog.Text}";
    }

    private void OnCheckbox1Changed(object? sender, CheckedChangedEventArgs e)
    {
        LogEvent($"Checkbox 1: {(e.Value ? "Checked" : "Unchecked")}");
    }

    private void OnCheckbox2Changed(object? sender, CheckedChangedEventArgs e)
    {
        LogEvent($"Checkbox 2: {(e.Value ? "Checked" : "Unchecked")}");
    }

    private void OnColoredCheckboxChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox cb)
        {
            LogEvent($"{cb.Color} checkbox: {(e.Value ? "Checked" : "Unchecked")}");
        }
    }

    private void OnBasicSwitchToggled(object? sender, ToggledEventArgs e)
    {
        SwitchStatusLabel.Text = e.Value ? "On" : "Off";
        LogEvent($"Switch toggled: {(e.Value ? "ON" : "OFF")}");
    }

    private void OnColoredSwitchToggled(object? sender, ToggledEventArgs e)
    {
        if (sender is Switch sw)
        {
            LogEvent($"{sw.OnColor} switch: {(e.Value ? "ON" : "OFF")}");
        }
    }

    private void OnBasicSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        SliderValueLabel.Text = $"Value: {(int)e.NewValue}";
        LogEvent($"Slider value: {(int)e.NewValue}");
    }

    private void OnTempSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        TempLabel.Text = $"{(int)e.NewValue}C";
        LogEvent($"Temperature: {(int)e.NewValue}C");
    }

    private void OnColoredSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        LogEvent($"Colored slider: {(int)e.NewValue}");
    }
}
