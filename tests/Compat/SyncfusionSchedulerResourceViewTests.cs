// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.Scheduler;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SfScheduler's desktop horizontal resource view, as its Windows and Mac builds lay it out: with
/// resources, the day, week and work-week views show the resources side by side under a resource
/// header, beside a time ruler that stays put while the time slots scroll both ways, and the
/// month view shows a month per resource. Each test builds a real scheduler with
/// <c>ResourceView.Resources</c>, renders it and reads the frame and the view tree.
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionSchedulerResourceViewTests
{
    private const int Width = 900, Height = 600;

    // Resource header backgrounds, and the appointment colors (distinct from them).
    private static readonly Color[] ResourceColors = [Color.FromRgb(0xF4, 0xC2, 0xC2), Color.FromRgb(0xC2, 0xE0, 0xF4), Color.FromRgb(0xC8, 0xF0, 0xC0), Color.FromRgb(0xF0, 0xE6, 0xA0), Color.FromRgb(0xE0, 0xC8, 0xF0)];
    private static readonly Color[] AppointmentColors = [Color.FromRgb(0xFF, 0x8C, 0x00), Color.FromRgb(0x1E, 0x60, 0xD0), Color.FromRgb(0x10, 0x90, 0x30), Color.FromRgb(0xB0, 0x20, 0xB0), Color.FromRgb(0x90, 0x60, 0x20)];

    private static readonly MethodInfo FireHolds = typeof(LinuxSyncfusionBuilderExtensions).Assembly
        .GetType("Microsoft.Maui.Platform.Linux.Syncfusion.SfSchedulerPatches")!
        .GetMethod("FireHolds", BindingFlags.NonPublic | BindingFlags.Static)!;

    private sealed class Fixture : IDisposable
    {
        public required CompatHost Host { get; init; }
        public required SfScheduler Scheduler { get; init; }
        public required ObservableCollection<SchedulerAppointment> Appointments { get; init; }
        public List<string> Log { get; } = new();
        public void Dispose() => Host.Dispose();
    }

    /// <summary>
    /// A scheduler with <paramref name="resources"/> resources and one timed appointment per resource
    /// today (resource i from 01:00 + i % 5 hours, for an hour), scrolled to the top of the day.
    /// </summary>
    private static Fixture Build(SchedulerView view, int resources = 3, Action<SfScheduler>? configure = null,
        IEnumerable<SchedulerAppointment>? extra = null, bool withResources = true)
    {
        var scheduler = new SfScheduler { View = view, DisplayDate = DateTime.Today, AllowAppointmentDrag = true, AllowAppointmentResize = true };
        if (withResources)
            scheduler.ResourceView.Resources = Resources(resources);
        configure?.Invoke(scheduler);
        var appointments = new ObservableCollection<SchedulerAppointment>();
        for (int i = 0; i < resources; i++)
        {
            var start = DateTime.Today.AddHours(1 + i % 5);
            appointments.Add(new SchedulerAppointment
            {
                StartTime = start, EndTime = start.AddHours(1), Subject = "A" + (i + 1), Background = AppointmentColors[i % 5],
                ResourceIds = withResources ? new ObservableCollection<object> { i + 1 } : null,
            });
        }
        foreach (var appointment in extra ?? [])
            appointments.Add(appointment);
        var fixture = new Fixture
        {
            Host = new CompatHost(new ContentPage { Content = scheduler }, b => b.UseLinuxSyncfusion(), Width, Height),
            Scheduler = scheduler,
            Appointments = appointments,
        };
        scheduler.Tapped += (_, e) => fixture.Log.Add($"tapped {e.Element} {e.Resource?.Name} {e.Date:dd HH:mm}");
        scheduler.AppointmentDrop += (_, e) => fixture.Log.Add($"drop {e.TargetResource?.Name} allday={e.IsDroppingToAllDay}");
        Pump(fixture.Host);
        scheduler.AppointmentsSource = appointments;
        Pump(fixture.Host);
        // The compat host runs the scheduler's posted appointment update inline, before its views
        // exist; a resize lays them out with the appointments (as in the mouse tests).
        fixture.Host.DisplayWindow.RaiseResized(Width + 1, Height);
        Pump(fixture.Host);
        fixture.Log.Clear();
        return fixture;
    }

    private static ObservableCollection<SchedulerResource> Resources(int count)
    {
        var list = new ObservableCollection<SchedulerResource>();
        for (int i = 0; i < count; i++)
            list.Add(new SchedulerResource { Id = i + 1, Name = "Res" + (i + 1), Background = ResourceColors[i % 5] });
        return list;
    }

    private static void Pump(CompatHost host, int frames = 8)
    {
        for (int i = 0; i < frames; i++)
        {
            Thread.Sleep(10);
            Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll();
            host.Render();
        }
    }

    private static IEnumerable<Element> Descendants(Element root)
    {
        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
        {
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }

    private static bool OnScreen(VisualElement view)
    {
        var b = CompatHost.PlatformOf(view).ScreenBounds;
        return b.Left >= 0 && b.Left < Width;
    }

    /// <summary>The visible view of a type (the snap layout keeps the previous and next ones beside it).</summary>
    private static VisualElement Visible(Element root, string typeName) =>
        Descendants(root).OfType<VisualElement>().First(e => e.GetType().Name == typeName && OnScreen(e));

    private static VisualElement Child(Element root, string typeName) =>
        Descendants(root).OfType<VisualElement>().Single(e => e.GetType().Name == typeName);

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static (int Left, int Top, int Right, int Bottom)? Find(CompatHost host, Color color, SKRectI area)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        int r0 = (int)(color.Red * 255), g0 = (int)(color.Green * 255), b0 = (int)(color.Blue * 255);
        for (int y = Math.Max(0, area.Top); y < Math.Min(Height, area.Bottom); y++)
        {
            for (int x = Math.Max(0, area.Left); x < Math.Min(Width, area.Right); x++)
            {
                var (r, g, b, _) = host.DisplayWindow.PixelAt(x, y);
                if (Math.Abs(r - r0) + Math.Abs(g - g0) + Math.Abs(b - b0) <= 12)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
        }
        return right < 0 ? null : (left, top, right, bottom);
    }

    private static int LayoutPassesWhileIdle(CompatHost host)
    {
        var view = typeof(LinuxSyncfusionBuilderExtensions).Assembly.GetType("Microsoft.Maui.Platform.Linux.Syncfusion.SfSchedulerResourceView")!;
        var passes = view.GetProperty("LayoutPasses", BindingFlags.NonPublic | BindingFlags.Static)!;
        int before = (int)passes.GetValue(null)!;
        Pump(host, 20);
        return (int)passes.GetValue(null)! - before;
    }

    // ---- Layout -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(SchedulerView.Day)]
    [InlineData(SchedulerView.Week)]
    [InlineData(SchedulerView.WorkWeek)]
    public void The_day_views_show_the_resources_side_by_side_with_their_header_and_time_ruler(SchedulerView view)
    {
        if (view == SchedulerView.WorkWeek && DateTime.Today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return; // the appointments are today, which a work week does not show
        using var f = Build(view);
        var day = Visible(f.Scheduler, "DayViewControl");
        var header = Child(day, "ResourceHeaderLayout");
        var ruler = Child(day, "SfSchedulerTimeRulerView");
        var slots = Child(day, "DayViewLayout");

        float rulerWidth = (float)f.Scheduler.DaysView.TimeRulerWidth;
        var rulerBounds = CompatHost.PlatformOf(ruler).ScreenBounds;
        var slotBounds = CompatHost.PlatformOf(slots).ScreenBounds;
        var headerBounds = CompatHost.PlatformOf(header).ScreenBounds;
        rulerBounds.Left.Should().BeApproximately(0, 1, "the time ruler is a column of its own at the left");
        rulerBounds.Width.Should().BeApproximately(rulerWidth, 1);
        slotBounds.Left.Should().BeApproximately(rulerWidth, 1, "the time slots start after the ruler");
        headerBounds.Left.Should().BeApproximately(rulerWidth, 1, "the resource header spans the time slots");
        headerBounds.Bottom.Should().BeLessThanOrEqualTo(slotBounds.Top + 1);

        // Three resources fill the viewport: each a third of it, under its own header.
        float column = (Width - rulerWidth) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int x0 = (int)(rulerWidth + i * column), x1 = (int)(rulerWidth + (i + 1) * column);
            var headerCell = Find(f.Host, ResourceColors[i], new SKRectI(x0 - 2, (int)headerBounds.Top, x1 + 2, (int)headerBounds.Bottom));
            headerCell.Should().NotBeNull($"Res{i + 1}'s header is drawn over its third");
            headerCell!.Value.Left.Should().BeLessThanOrEqualTo(x0 + 2);
            headerCell.Value.Right.Should().BeGreaterThanOrEqualTo(x1 - 3);

            var box = Find(f.Host, AppointmentColors[i], new SKRectI((int)rulerWidth, (int)slotBounds.Top, Width, Height));
            box.Should().NotBeNull($"A{i + 1} is drawn");
            box!.Value.Left.Should().BeGreaterThanOrEqualTo(x0 - 1, $"A{i + 1} is in Res{i + 1}'s column");
            box.Value.Right.Should().BeLessThanOrEqualTo(x1 + 1);
        }
        LayoutPassesWhileIdle(f.Host).Should().Be(0, "the resource view settles");
    }

    [Fact]
    public void The_month_view_shows_a_month_per_resource()
    {
        using var f = Build(SchedulerView.Month);
        var month = Visible(f.Scheduler, "MonthViewLayout");
        var header = Child(month, "ResourceHeaderLayout");
        var headerBounds = CompatHost.PlatformOf(header).ScreenBounds;
        float column = Width / 3f;
        for (int i = 0; i < 3; i++)
        {
            int x0 = (int)(i * column), x1 = (int)((i + 1) * column);
            Find(f.Host, ResourceColors[i], new SKRectI(x0, (int)headerBounds.Top, x1, (int)headerBounds.Bottom))
                .Should().NotBeNull($"Res{i + 1}'s header is over its month");
            var box = Find(f.Host, AppointmentColors[i], new SKRectI(0, (int)headerBounds.Bottom, Width, Height));
            box.Should().NotBeNull($"A{i + 1} is drawn");
            box!.Value.Left.Should().BeGreaterThanOrEqualTo(x0 - 1, $"A{i + 1} is in Res{i + 1}'s month");
            box.Value.Right.Should().BeLessThanOrEqualTo(x1 + 1);
        }
        LayoutPassesWhileIdle(f.Host).Should().Be(0);
    }

    [Fact]
    public void Grouping_by_date_puts_the_day_header_above_the_resource_names()
    {
        using var f = Build(SchedulerView.Day, configure: s => s.ResourceView.ResourceGroupType = SchedulerResourceGroupType.Date);
        var day = Visible(f.Scheduler, "DayViewControl");
        var names = Child(day, "ResourceHeaderView");
        var dates = Child(day, "DayHeaderView");
        names.Bounds.Y.Should().BeGreaterThan(dates.Bounds.Y, "with date grouping each day lists its resources");

        f.Scheduler.ResourceView.ResourceGroupType = SchedulerResourceGroupType.Resource;
        Pump(f.Host);
        names.Bounds.Y.Should().BeLessThan(dates.Bounds.Y, "with resource grouping each resource lists its days");
    }

    [Fact]
    public void The_time_ruler_and_the_resource_header_follow_the_time_slots()
    {
        using var f = Build(SchedulerView.Week, resources: 5);
        var day = Visible(f.Scheduler, "DayViewControl");
        var slots = Field<ScrollView>(day, "scrollView");
        var resourceScroll = (ScrollView)Child(day, "ResourceHeaderLayout").Parent;
        var ruler = (ScrollView)Child(day, "SfSchedulerTimeRulerView").Parent;

        double viewport = slots.Width;
        slots.ContentSize.Width.Should().BeApproximately(viewport / 3 * 5, 1, "three of the five resources fit the viewport");

        f.Host.DisplayWindow.RaiseScroll(Width / 2f, 400, 3);
        Pump(f.Host);
        slots.ScrollY.Should().BeGreaterThan(0, "the wheel scrolls the time slots");
        ruler.ScrollY.Should().Be(slots.ScrollY, "the time ruler follows vertically");

        _ = slots.ScrollToAsync(400, slots.ScrollY, false);
        Pump(f.Host);
        slots.ScrollX.Should().Be(400);
        resourceScroll.ScrollX.Should().Be(400, "the resource header follows horizontally");
        ruler.ScrollY.Should().Be(slots.ScrollY);

        f.Scheduler.ResourceView.VisibleResourceCount = 2;
        Pump(f.Host);
        day = Visible(f.Scheduler, "DayViewControl");
        Field<ScrollView>(day, "scrollView").ContentSize.Width.Should().BeApproximately(viewport / 2 * 5, 1, "two resources per viewport");
    }

    [Fact]
    public void Columns_wider_than_three_viewports_are_drawn_in_a_window_that_follows_the_scroll_offset()
    {
        // Fifteen resources, three per viewport: five viewports of columns, more than the three
        // the Windows build draws at once.
        using var f = Build(SchedulerView.Day, resources: 15);
        var day = Visible(f.Scheduler, "DayViewControl");
        var slots = Field<ScrollView>(day, "scrollView");
        var header = Child(day, "ResourceHeaderLayout");
        var names = Child(day, "ResourceHeaderView");
        var dates = Child(day, "DayHeaderView");
        var dayView = Child(day, "DayView");
        var info = typeof(SfScheduler).GetProperty("IsVirtualizationNeeded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        float rulerWidth = (float)f.Scheduler.DaysView.TimeRulerWidth;
        double viewport = slots.Width;
        double column = viewport / 3;

        ((bool)info.GetValue(f.Scheduler)!).Should().BeTrue("the columns are five viewports wide");
        slots.ContentSize.Width.Should().BeApproximately(viewport * 5, 1, "the time slots scroll across every column");
        header.Width.Should().BeApproximately(viewport * 5, 1);
        foreach (var window in new[] { names, dates, dayView })
        {
            window.Width.Should().BeApproximately(viewport * 3, 1, $"{window.GetType().Name} draws a window three viewports wide");
            window.X.Should().Be(0);
        }

        // Scrolled to Res11 (the eleventh column), the window starts a viewport before it.
        _ = slots.ScrollToAsync(column * 10, 0, false);
        Pump(f.Host);
        slots.ScrollX.Should().BeApproximately(column * 10, 1);
        foreach (var window in new[] { names, dates, dayView })
            window.X.Should().BeApproximately(column * 10 - viewport, 1, $"{window.GetType().Name} follows the scroll offset");

        // Res11 to Res13 are drawn in the viewport, each over its own appointment.
        var slotBounds = CompatHost.PlatformOf(Child(day, "DayViewLayout")).ScreenBounds;
        var headerBounds = CompatHost.PlatformOf(names).ScreenBounds;
        for (int i = 0; i < 3; i++)
        {
            int x0 = (int)(rulerWidth + i * column), x1 = (int)(rulerWidth + (i + 1) * column);
            var cell = Find(f.Host, ResourceColors[i], new SKRectI(x0 - 2, (int)Math.Max(0, headerBounds.Top), x1 + 2, (int)slotBounds.Top));
            cell.Should().NotBeNull($"Res{i + 11}'s header is drawn over its column");
            cell!.Value.Left.Should().BeLessThanOrEqualTo(x0 + 2);
            cell.Value.Right.Should().BeGreaterThanOrEqualTo(x1 - 3);
            var box = Find(f.Host, AppointmentColors[i], new SKRectI((int)rulerWidth, (int)slotBounds.Top, Width, Height));
            box.Should().NotBeNull($"A{i + 11} is drawn");
            box!.Value.Left.Should().BeGreaterThanOrEqualTo(x0 - 1, $"A{i + 11} is in Res{i + 11}'s column");
            box.Value.Right.Should().BeLessThanOrEqualTo(x1 + 1);
        }

        f.Host.Tap(rulerWidth + (float)column * 1.5f, (float)headerBounds.Center.Y);
        Pump(f.Host);
        f.Log.Should().Contain(l => l.StartsWith("tapped ResourceHeader Res12"));
        LayoutPassesWhileIdle(f.Host).Should().Be(0, "the virtualized view settles");

        // Fewer resources fit: back to the full width.
        f.Scheduler.ResourceView.Resources = Resources(6);
        Pump(f.Host);
        ((bool)info.GetValue(f.Scheduler)!).Should().BeFalse("six resources are two viewports wide");
    }

    [Fact]
    public void Removing_and_adding_the_resources_switches_between_the_layouts()
    {
        using var f = Build(SchedulerView.Week);
        var day = Visible(f.Scheduler, "DayViewControl");
        Descendants(day).Should().Contain(e => e.GetType().Name == "ResourceHeaderLayout");

        var resources = f.Scheduler.ResourceView.Resources;
        f.Scheduler.ResourceView.Resources = null;
        Pump(f.Host);
        Descendants(day).Should().NotContain(e => e.GetType().Name == "ResourceHeaderLayout");
        Descendants(day).Should().NotContain(e => e.GetType().Name == "SfSchedulerTimeRulerView");
        Field<object?>(day, "allDayAppointmentsLayout").Should().NotBeNull("the plain week view has its own all-day panel again");

        f.Scheduler.ResourceView.Resources = resources;
        Pump(f.Host);
        Descendants(day).Should().Contain(e => e.GetType().Name == "ResourceHeaderLayout");
        Field<object?>(day, "allDayAppointmentsLayout").Should().BeNull();
        LayoutPassesWhileIdle(f.Host).Should().Be(0);

        f.Scheduler.View = SchedulerView.TimelineWeek;
        Pump(f.Host, 30);
        f.Scheduler.View = SchedulerView.Week;
        Pump(f.Host, 30);
        day = Visible(f.Scheduler, "DayViewControl");
        Child(day, "ResourceHeaderView").Height.Should().BeGreaterThan(0, "a view switch builds and lays out the resource view again");
        LayoutPassesWhileIdle(f.Host).Should().Be(0);
    }

    // ---- Interaction ------------------------------------------------------------------------------------

    [Fact]
    public void Taps_report_the_resource_under_the_pointer()
    {
        using var f = Build(SchedulerView.Day);
        var day = Visible(f.Scheduler, "DayViewControl");
        var nameBounds = CompatHost.PlatformOf(Child(day, "ResourceHeaderView")).ScreenBounds;
        var slotBounds = CompatHost.PlatformOf(Child(day, "DayViewLayout")).ScreenBounds;
        float rulerWidth = (float)f.Scheduler.DaysView.TimeRulerWidth;
        float column = (Width - rulerWidth) / 3f;

        f.Host.Tap(rulerWidth + column * 2.5f, (float)nameBounds.Center.Y);
        Pump(f.Host);
        f.Log.Should().Contain(l => l.StartsWith("tapped ResourceHeader Res3"));

        f.Log.Clear();
        // Res2's column, an hour and a half below the top (the day starts at the top).
        f.Host.Tap(rulerWidth + column * 1.5f, (float)slotBounds.Top + 75);
        Pump(f.Host);
        f.Log.Should().Contain($"tapped SchedulerCell Res2 {DateTime.Today.AddHours(1):dd HH:mm}");
    }

    [Fact]
    public void An_appointment_dragged_to_another_resource_moves_to_it()
    {
        using var f = Build(SchedulerView.Day);
        var slotBounds = CompatHost.PlatformOf(Visible(f.Scheduler, "DayViewLayout")).ScreenBounds;
        var box = Find(f.Host, AppointmentColors[0], new SKRectI((int)slotBounds.Left, (int)slotBounds.Top, Width, Height))!.Value;
        float x = box.Left + 30, y = box.Top + 10;
        float column = (Width - (float)f.Scheduler.DaysView.TimeRulerWidth) / 3f;
        f.Host.DisplayWindow.RaisePointerMoved(x, y);
        f.Host.DisplayWindow.RaisePointerPressed(x, y);
        ((int)FireHolds.Invoke(null, null)!).Should().BeGreaterThan(0);
        for (int i = 1; i <= 10; i++)
        {
            f.Host.DisplayWindow.RaisePointerMoved(x + i * column * 2 / 10f, y);
            Pump(f.Host, 1);
        }
        f.Host.DisplayWindow.RaisePointerReleased(x + column * 2, y);
        Pump(f.Host);

        f.Log.Should().Contain("drop Res3 allday=False");
        f.Appointments[0].ResourceIds.Should().Equal(3);
        f.Appointments[0].StartTime.Should().Be(DateTime.Today.AddHours(1));
    }

    [Fact]
    public void Templates_in_a_virtualized_view_are_laid_out_across_the_full_width()
    {
        static DataTemplate LabelFor(string path) => new(() =>
        {
            var label = new Label { FontSize = 11 };
            label.SetBinding(Label.TextProperty, path);
            return label;
        });
        var trip = new SchedulerAppointment
        {
            StartTime = DateTime.Today, EndTime = DateTime.Today, IsAllDay = true, Subject = "Trip11", ResourceIds = new ObservableCollection<object> { 11 },
        };
        using var f = Build(SchedulerView.Day, resources: 15, extra: [trip], configure: s =>
        {
            s.ResourceView.HeaderTemplate = LabelFor("Name");
            s.DaysView.AppointmentTemplate = LabelFor("Subject");
            s.DaysView.AllDayAppointmentTemplate = LabelFor("Subject");
            s.DaysView.ViewHeaderTemplate = new DataTemplate(() => new Label { Text = "Day", FontSize = 9 });
            s.DaysView.TimeRegionTemplate = LabelFor("Text");
            s.DaysView.TimeRegions =
            [
                new SchedulerTimeRegion
                {
                    StartTime = DateTime.Today.AddHours(3), EndTime = DateTime.Today.AddHours(4), Text = "Busy12",
                    // The scheduler matches a region's resource by reference (as on Windows): the resource's own Id.
                    ResourceIds = new ObservableCollection<object> { ((System.Collections.IEnumerable)s.ResourceView.Resources!).Cast<SchedulerResource>().ElementAt(11).Id },
                },
            ];
        });
        var day = Visible(f.Scheduler, "DayViewControl");
        var slots = Field<ScrollView>(day, "scrollView");
        double viewport = slots.Width, column = viewport / 3;
        float rulerWidth = (float)f.Scheduler.DaysView.TimeRulerWidth;

        // The templates are laid out by views across every column.
        foreach (var name in new[] { "ResourceLayoutTemplateView", "DayViewViewHeaderTemplateView", "AllDayAppointmentsTemplateView", "SpecialTimeRegionTemplateView", "DayAppointmentsView" })
            Child(day, name).Width.Should().BeApproximately(viewport * 5, 1, $"{name} spans every column");
        Child(day, "ResourceHeaderView").Width.Should().BeApproximately(viewport * 3, 1, "the drawn header is still a window");

        _ = slots.ScrollToAsync(column * 10, 0, false);
        Pump(f.Host);
        Label LabelOf(string text) => Descendants(day).OfType<Label>().Single(l => l.Text == text);
        for (int i = 0; i < 3; i++)
        {
            float x0 = rulerWidth + i * (float)column, x1 = x0 + (float)column;
            var name = CompatHost.PlatformOf(LabelOf("Res" + (i + 11))).ScreenBounds;
            name.Left.Should().BeGreaterThanOrEqualTo(x0 - 1, $"Res{i + 11}'s template is over its column");
            name.Right.Should().BeLessThanOrEqualTo(x1 + 1);
            var subject = CompatHost.PlatformOf(LabelOf("A" + (i + 11))).ScreenBounds;
            subject.Left.Should().BeGreaterThanOrEqualTo(x0 - 1, $"A{i + 11}'s template is in Res{i + 11}'s column");
            subject.Right.Should().BeLessThanOrEqualTo(x1 + 1);
        }
        var allDay = CompatHost.PlatformOf(LabelOf("Trip11")).ScreenBounds;
        allDay.Left.Should().BeGreaterThanOrEqualTo(rulerWidth - 1, "the all-day template is in Res11's column");
        allDay.Right.Should().BeLessThanOrEqualTo(rulerWidth + (float)column + 1);
        var region = CompatHost.PlatformOf(LabelOf("Busy12")).ScreenBounds;
        region.Left.Should().BeGreaterThanOrEqualTo(rulerWidth + (float)column - 1, "the time region template is in Res12's column");
        region.Right.Should().BeLessThanOrEqualTo(rulerWidth + 2 * (float)column + 1);
        LayoutPassesWhileIdle(f.Host).Should().Be(0);

        // Six resources are not virtualized: the resource header holds its templates again.
        f.Scheduler.ResourceView.Resources = Resources(6);
        Pump(f.Host);
        day = Visible(f.Scheduler, "DayViewControl");
        foreach (var name in new[] { "ResourceLayoutTemplateView", "DayViewViewHeaderTemplateView", "AllDayAppointmentsTemplateView", "SpecialTimeRegionTemplateView" })
            Descendants(day).Should().NotContain(e => e.GetType().Name == name);
        Descendants(Child(day, "DayHeaderView")).OfType<Label>().Should().HaveCount(6, "the day header holds a template per resource again");
        var header = Child(day, "ResourceHeaderView");
        Descendants(header).OfType<Label>().Select(l => l.Text).Should().Contain("Res1").And.Contain("Res6");
    }

    [Fact]
    public void A_drag_in_a_virtualized_view_drops_on_the_resource_under_the_pointer()
    {
        using var f = Build(SchedulerView.Day, resources: 15);
        var day = Visible(f.Scheduler, "DayViewControl");
        var slots = Field<ScrollView>(day, "scrollView");
        float column = (float)slots.Width / 3f;
        _ = slots.ScrollToAsync(column * 10, 0, false);
        Pump(f.Host);

        // A11, at the left of the viewport, dragged two columns right onto Res13.
        var slotBounds = CompatHost.PlatformOf(Child(day, "DayViewLayout")).ScreenBounds;
        float rulerWidth = (float)f.Scheduler.DaysView.TimeRulerWidth;
        var box = Find(f.Host, AppointmentColors[0], new SKRectI((int)rulerWidth, (int)Math.Max(0, slotBounds.Top), Width, Height))!.Value;
        float x = box.Left + 30, y = box.Top + 10;
        f.Host.DisplayWindow.RaisePointerMoved(x, y);
        f.Host.DisplayWindow.RaisePointerPressed(x, y);
        ((int)FireHolds.Invoke(null, null)!).Should().BeGreaterThan(0);
        for (int i = 1; i <= 10; i++)
        {
            f.Host.DisplayWindow.RaisePointerMoved(x + i * column * 2 / 10f, y);
            Pump(f.Host, 1);
        }
        f.Host.DisplayWindow.RaisePointerReleased(x + column * 2, y);
        Pump(f.Host);

        f.Log.Should().Contain("drop Res13 allday=False");
        f.Appointments[10].ResourceIds.Should().Equal(13);
        f.Appointments[10].StartTime.Should().Be(DateTime.Today.AddHours(1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_timed_appointment_dragged_onto_the_all_day_panel_becomes_all_day(bool withResources)
    {
        // An all-day appointment on another day gives the week view its all-day panel.
        var other = DateTime.Today.AddDays(DateTime.Today.DayOfWeek == DayOfWeek.Saturday ? -1 : 1);
        var allDay = new SchedulerAppointment
        {
            StartTime = other, EndTime = other, IsAllDay = true, Subject = "Trip", Background = AppointmentColors[3],
            ResourceIds = withResources ? new ObservableCollection<object> { 1 } : null,
        };
        using var f = Build(SchedulerView.Week, resources: 1, extra: [allDay], withResources: withResources);
        var slotBounds = CompatHost.PlatformOf(Visible(f.Scheduler, "DayViewLayout")).ScreenBounds;
        var panel = Find(f.Host, AppointmentColors[3], new SKRectI(0, 0, Width, (int)slotBounds.Top))!.Value;
        var box = Find(f.Host, AppointmentColors[0], new SKRectI((int)slotBounds.Left, (int)slotBounds.Top, Width, Height))!.Value;
        float x = box.Left + 10, y = box.Top + 15, target = (panel.Top + panel.Bottom) / 2f;
        f.Host.DisplayWindow.RaisePointerMoved(x, y);
        f.Host.DisplayWindow.RaisePointerPressed(x, y);
        FireHolds.Invoke(null, null);
        for (int i = 1; i <= 10; i++)
        {
            f.Host.DisplayWindow.RaisePointerMoved(x, y + (target - y) * i / 10f);
            Pump(f.Host, 1);
        }
        f.Host.DisplayWindow.RaisePointerReleased(x, target);
        Pump(f.Host);

        f.Log.Should().Contain(l => l.StartsWith("drop") && l.EndsWith("allday=True"));
        f.Appointments[0].IsAllDay.Should().BeTrue("a drop on the all-day panel makes the appointment all-day");
    }

    [Fact]
    public void An_all_day_appointment_dragged_into_the_time_slots_of_another_resource_becomes_timed()
    {
        var trip = new SchedulerAppointment
        {
            StartTime = DateTime.Today, EndTime = DateTime.Today, IsAllDay = true, Subject = "Trip", Background = AppointmentColors[3],
            ResourceIds = new ObservableCollection<object> { 1 },
        };
        using var f = Build(SchedulerView.Day, extra: [trip]);
        var day = Visible(f.Scheduler, "DayViewControl");
        var slotBounds = CompatHost.PlatformOf(Child(day, "DayViewLayout")).ScreenBounds;
        float column = (Width - (float)f.Scheduler.DaysView.TimeRulerWidth) / 3f;
        var box = Find(f.Host, AppointmentColors[3], new SKRectI(0, 0, Width, (int)slotBounds.Top))!.Value;
        float x = box.Left + 20, y = (box.Top + box.Bottom) / 2f;
        // Res2's column, two and a half hours down.
        float targetX = x + column, targetY = (float)slotBounds.Top + 125;
        f.Host.DisplayWindow.RaisePointerMoved(x, y);
        f.Host.DisplayWindow.RaisePointerPressed(x, y);
        FireHolds.Invoke(null, null);
        for (int i = 1; i <= 10; i++)
        {
            f.Host.DisplayWindow.RaisePointerMoved(x + (targetX - x) * i / 10f, y + (targetY - y) * i / 10f);
            Pump(f.Host, 1);
        }
        f.Host.DisplayWindow.RaisePointerReleased(targetX, targetY);
        Pump(f.Host);

        f.Log.Should().Contain("drop Res2 allday=False");
        var dropped = f.Appointments.Single(a => a.Subject == "Trip");
        dropped.IsAllDay.Should().BeFalse("a drop in the time slots makes the appointment timed");
        dropped.ResourceIds.Should().Equal(2);
    }

    [Fact]
    public void The_all_day_expander_in_the_ruler_column_expands_the_panel()
    {
        // Four all-day appointments in one resource's day: the panel shows three rows (as on
        // Windows, with resources side by side) and a chevron.
        var allDay = Enumerable.Range(0, 4).Select(i => new SchedulerAppointment
        {
            StartTime = DateTime.Today, EndTime = DateTime.Today, IsAllDay = true, Subject = "Trip" + i, Background = AppointmentColors[3],
            ResourceIds = new ObservableCollection<object> { 1 },
        }).ToList();
        using var f = Build(SchedulerView.Week, extra: allDay);
        var day = Visible(f.Scheduler, "DayViewControl");
        var expander = Child(day, "SfSchedulerAllDayExpanderView");
        var interaction = day.GetType().GetInterfaces().Single(i => i.Name == "IDaysViewInteraction");
        bool Expanded() => (bool)interaction.GetMethod("IsExpanded")!.Invoke(day, null)!;
        ((bool)interaction.GetMethod("IsExpandable")!.Invoke(day, null)!).Should().BeTrue();
        Expanded().Should().BeFalse();
        double collapsed = expander.Height;
        var bounds = CompatHost.PlatformOf(expander).ScreenBounds;
        bounds.Width.Should().BeApproximately((float)f.Scheduler.DaysView.TimeRulerWidth, 1, "the chevron sits in the ruler column");

        f.Host.Tap((float)bounds.Center.X, (float)bounds.Bottom - 5);
        Pump(f.Host, 40);
        Expanded().Should().BeTrue();
        expander.Height.Should().BeGreaterThan(collapsed, "the expanded panel shows every all-day row");
    }
}
