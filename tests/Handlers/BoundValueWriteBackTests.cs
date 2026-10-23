// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.ComponentModel;
using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A control's bound value survives getting its handler: the handler applies the control's
/// properties to the Skia view, and a value the Skia view adjusts on the way (clamped to a range
/// it does not have yet, an index for items it does not have yet) must not be written back to
/// the control and through a TwoWay binding to the source (see <see cref="RangeValueWriteBackTests"/>).
/// </summary>
[Collection("LinuxApplication.Current")]
public class BoundValueWriteBackTests
{
    private sealed class Model<T> : INotifyPropertyChanged
    {
        private T _value = default!;

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                Writes++;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public int Writes { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>Binds <paramref name="property"/> TwoWay to a source holding <paramref name="value"/> and connects the handler.</summary>
    private static Model<T> Connect<T>(View view, BindableProperty property, T value)
    {
        var model = new Model<T> { Value = value };
        view.BindingContext = model;
        view.SetBinding(property, new Binding("Value", BindingMode.TwoWay));
        model.Writes = 0;
        using var host = new HeadlessMauiHost(new ContentPage());
        Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(view, host.MauiContext);
        return model;
    }

    private static void ShouldKeep<T>(Model<T> model, T expected)
    {
        model.Value.Should().Be(expected);
        model.Writes.Should().Be(0, "the handler should not write the source when it connects");
    }

    private static readonly string[] Items = { "a", "b", "c", "d" };

    [Fact]
    public void Picker_SelectedIndex()
    {
        var picker = new Picker { ItemsSource = Items };
        ShouldKeep(Connect(picker, Picker.SelectedIndexProperty, 2), 2);
    }

    [Fact]
    public void Picker_SelectedItem()
    {
        var picker = new Picker { ItemsSource = Items };
        ShouldKeep(Connect(picker, Picker.SelectedItemProperty, "c"), "c");
    }

    [Fact]
    public void DatePicker_Date_inside_its_range()
    {
        DateTime? date = new DateTime(2025, 6, 15);
        var picker = new DatePicker { MinimumDate = new DateTime(2020, 1, 1), MaximumDate = new DateTime(2030, 12, 31) };
        ShouldKeep(Connect(picker, DatePicker.DateProperty, date), date);
    }

    [Fact]
    public void DatePicker_Date_before_the_default_minimum()
    {
        DateTime? date = new DateTime(1850, 3, 1);
        var picker = new DatePicker { MinimumDate = new DateTime(1800, 1, 1) };
        ShouldKeep(Connect(picker, DatePicker.DateProperty, date), date);
    }

    [Fact]
    public void DatePicker_Date_after_the_default_maximum()
    {
        DateTime? date = new DateTime(2150, 3, 1);
        var picker = new DatePicker { MaximumDate = new DateTime(2200, 1, 1) };
        ShouldKeep(Connect(picker, DatePicker.DateProperty, date), date);
    }

    [Fact]
    public void TimePicker_Time()
    {
        TimeSpan? time = new TimeSpan(13, 45, 0);
        ShouldKeep(Connect(new TimePicker(), TimePicker.TimeProperty, time), time);
    }

    [Fact]
    public void Entry_Text_within_MaxLength()
    {
        var entry = new Entry { MaxLength = 20 };
        ShouldKeep(Connect(entry, Entry.TextProperty, "hello world"), "hello world");
    }

    [Fact]
    public void Editor_Text_with_line_breaks()
    {
        ShouldKeep(Connect(new Editor(), Editor.TextProperty, "one\ntwo"), "one\ntwo");
    }

    [Fact]
    public void SearchBar_Text()
    {
        ShouldKeep(Connect(new SearchBar(), SearchBar.TextProperty, "query"), "query");
    }

    [Fact]
    public void CheckBox_IsChecked()
    {
        ShouldKeep(Connect(new CheckBox(), CheckBox.IsCheckedProperty, true), true);
    }

    [Fact]
    public void Switch_IsToggled()
    {
        ShouldKeep(Connect(new Switch(), Switch.IsToggledProperty, true), true);
    }

    [Fact]
    public void RadioButton_IsChecked()
    {
        ShouldKeep(Connect(new RadioButton { Content = "x" }, RadioButton.IsCheckedProperty, true), true);
    }

    [Fact]
    public void Stepper_Value_off_the_increment()
    {
        var stepper = new Stepper { Minimum = 0, Maximum = 10, Increment = 2 };
        ShouldKeep(Connect(stepper, Stepper.ValueProperty, 3.0), 3.0);
    }

    [Fact]
    public void RefreshView_IsRefreshing()
    {
        var refresh = new RefreshView { Content = new Label() };
        ShouldKeep(Connect(refresh, RefreshView.IsRefreshingProperty, true), true);
    }

    [Fact]
    public void CarouselView_Position()
    {
        var carousel = new CarouselView { ItemsSource = new ObservableCollection<string>(Items) };
        ShouldKeep(Connect(carousel, CarouselView.PositionProperty, 2), 2);
    }

    [Fact]
    public void CarouselView_CurrentItem()
    {
        var carousel = new CarouselView { ItemsSource = new ObservableCollection<string>(Items) };
        ShouldKeep(Connect(carousel, CarouselView.CurrentItemProperty, "c"), "c");
        carousel.Position.Should().Be(2, "a CurrentItem places the carousel initially, as MAUI's handlers do");
    }

    [Fact]
    public void CollectionView_SelectedItem()
    {
        var collection = new CollectionView { ItemsSource = Items, SelectionMode = SelectionMode.Single };
        ShouldKeep(Connect(collection, SelectableItemsView.SelectedItemProperty, "c"), "c");
    }

    [Fact]
    public void IndicatorView_Position()
    {
        var indicator = new IndicatorView { Count = 4 };
        ShouldKeep(Connect(indicator, IndicatorView.PositionProperty, 2), 2);
    }
}
