// PickersPage - Picker, DatePicker, TimePicker Demo

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ShellDemo;

public class PickersPage : ContentPage
{
    private readonly Label _eventLog;
    private int _eventCount = 0;

    public PickersPage()
    {
        Title = "Pickers";

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
                    new Label { Text = "Picker Controls", FontSize = 24, FontAttributes = FontAttributes.Bold },

                    CreateSection("Picker", CreatePickerDemo()),
                    CreateSection("DatePicker", CreateDatePickerDemo()),
                    CreateSection("TimePicker", CreateTimePickerDemo())
                }
            }
        };
    }

    private View CreatePickerDemo()
    {
        var layout = new VerticalStackLayout { Spacing = 15 };

        // Basic picker
        var selectedLabel = new Label { Text = "Selected: (none)", TextColor = Colors.Gray };
        var picker1 = new Picker { Title = "Select a fruit" };
        picker1.Items.Add("Apple");
        picker1.Items.Add("Banana");
        picker1.Items.Add("Cherry");
        picker1.Items.Add("Date");
        picker1.Items.Add("Elderberry");
        picker1.Items.Add("Fig");
        picker1.Items.Add("Grape");
        picker1.SelectedIndexChanged += (s, e) =>
        {
            if (picker1.SelectedIndex >= 0)
            {
                var item = picker1.Items[picker1.SelectedIndex];
                selectedLabel.Text = $"Selected: {item}";
                LogEvent($"Fruit selected: {item}");
            }
        };
        layout.Children.Add(picker1);
        layout.Children.Add(selectedLabel);

        // Picker with default selection
        layout.Children.Add(new Label { Text = "With Default Selection:", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        var picker2 = new Picker { Title = "Select a color" };
        picker2.Items.Add("Red");
        picker2.Items.Add("Green");
        picker2.Items.Add("Blue");
        picker2.Items.Add("Yellow");
        picker2.Items.Add("Purple");
        picker2.SelectedIndex = 2; // Blue
        picker2.SelectedIndexChanged += (s, e) =>
        {
            if (picker2.SelectedIndex >= 0)
                LogEvent($"Color selected: {picker2.Items[picker2.SelectedIndex]}");
        };
        layout.Children.Add(picker2);

        // Styled picker
        layout.Children.Add(new Label { Text = "Styled Picker:", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        var picker3 = new Picker
        {
            Title = "Select size",
            TextColor = Colors.DarkBlue,
            TitleColor = Colors.Gray
        };
        picker3.Items.Add("Small");
        picker3.Items.Add("Medium");
        picker3.Items.Add("Large");
        picker3.Items.Add("Extra Large");
        picker3.SelectedIndexChanged += (s, e) =>
        {
            if (picker3.SelectedIndex >= 0)
                LogEvent($"Size selected: {picker3.Items[picker3.SelectedIndex]}");
        };
        layout.Children.Add(picker3);

        return layout;
    }

    private View CreateDatePickerDemo()
    {
        var layout = new VerticalStackLayout { Spacing = 15 };

        // Basic date picker
        var dateLabel = new Label { Text = $"Selected: {DateTime.Today:d}" };
        var datePicker1 = new DatePicker { Date = DateTime.Today };
        datePicker1.DateSelected += (s, e) =>
        {
            dateLabel.Text = $"Selected: {e.NewDate:d}";
            LogEvent($"Date selected: {e.NewDate:d}");
        };
        layout.Children.Add(datePicker1);
        layout.Children.Add(dateLabel);

        // Date picker with range
        layout.Children.Add(new Label { Text = "With Date Range (this month only):", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
        var datePicker2 = new DatePicker
        {
            MinimumDate = startOfMonth,
            MaximumDate = endOfMonth,
            Date = DateTime.Today
        };
        datePicker2.DateSelected += (s, e) => LogEvent($"Date (limited): {e.NewDate:d}");
        layout.Children.Add(datePicker2);

        // Styled date picker
        layout.Children.Add(new Label { Text = "Styled DatePicker:", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        var datePicker3 = new DatePicker
        {
            Date = DateTime.Today.AddDays(7),
            TextColor = Colors.DarkGreen
        };
        datePicker3.DateSelected += (s, e) => LogEvent($"Styled date: {e.NewDate:d}");
        layout.Children.Add(datePicker3);

        return layout;
    }

    private View CreateTimePickerDemo()
    {
        var layout = new VerticalStackLayout { Spacing = 15 };

        // Basic time picker
        var timeLabel = new Label { Text = $"Selected: {DateTime.Now:t}" };
        var timePicker1 = new TimePicker { Time = DateTime.Now.TimeOfDay };
        timePicker1.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TimePicker.Time))
            {
                var time = timePicker1.Time;
                timeLabel.Text = $"Selected: {time:hh\\:mm}";
                LogEvent($"Time selected: {time:hh\\:mm}");
            }
        };
        layout.Children.Add(timePicker1);
        layout.Children.Add(timeLabel);

        // Styled time picker
        layout.Children.Add(new Label { Text = "Styled TimePicker:", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        var timePicker2 = new TimePicker
        {
            Time = new TimeSpan(14, 30, 0),
            TextColor = Colors.DarkBlue
        };
        timePicker2.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TimePicker.Time))
                LogEvent($"Styled time: {timePicker2.Time:hh\\:mm}");
        };
        layout.Children.Add(timePicker2);

        // Morning alarm example
        layout.Children.Add(new Label { Text = "Alarm Time:", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        var alarmRow = new HorizontalStackLayout { Spacing = 10 };
        var alarmPicker = new TimePicker { Time = new TimeSpan(7, 0, 0) };
        var alarmBtn = new Button { Text = "Set Alarm", BackgroundColor = Colors.Orange, TextColor = Colors.White };
        alarmBtn.Clicked += (s, e) => LogEvent($"Alarm set for {alarmPicker.Time:hh\\:mm}");
        alarmRow.Children.Add(alarmPicker);
        alarmRow.Children.Add(alarmBtn);
        layout.Children.Add(alarmRow);

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
