// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Handlers;
using Syncfusion.Maui.Core.Rotator;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Handler for <c>SfRotator</c> (<see cref="IRotator"/>), replacing the
/// platform-neutral <c>RotatorHandler</c> whose <c>CreatePlatformView</c>
/// throws. Builds the item views as the native builds do (an item's content
/// or image and its caption, the item template, or the item's text), hands
/// them to <see cref="SkiaSfRotator"/>, and reports back: <c>SelectedIndex</c>
/// and <c>SelectedIndexChanged</c> for every change, <c>ItemTapped</c> (with
/// <c>Command</c>) on a click. <c>EnableAutoPlay</c> moves to the next item
/// every <c>NavigationDelay</c>.
/// </summary>
public class SfRotatorBridgeHandler : LinuxViewHandler<IRotator, SkiaSfRotator>
{
    public static IPropertyMapper<IRotator, SfRotatorBridgeHandler> Mapper =
        new PropertyMapper<IRotator, SfRotatorBridgeHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IRotator.ItemsSource)] = MapItems,
            [nameof(IRotator.ItemTemplate)] = MapItems,
            [nameof(IRotator.SelectedIndex)] = MapSelectedIndex,
            [nameof(IRotator.NavigationStripMode)] = MapAppearance,
            [nameof(IRotator.SelectedDotColor)] = MapAppearance,
            [nameof(IRotator.UnselectedDotColor)] = MapAppearance,
            [nameof(IRotator.DotsStroke)] = MapAppearance,
            [nameof(IRotator.DotPlacement)] = MapAppearance,
            [nameof(IRotator.NavigationDirection)] = MapAppearance,
            [nameof(IRotator.NavigationStripPosition)] = MapAppearance,
            [nameof(IRotator.EnableSwiping)] = MapAppearance,
            [nameof(IRotator.EnableLooping)] = MapAppearance,
            [nameof(IRotator.IsTextVisible)] = MapAppearance,
            [nameof(IRotator.ShowNavigationButton)] = MapAppearance,
            [nameof(IRotator.SelectedThumbnailStroke)] = MapAppearance,
            [nameof(IRotator.UnselectedThumbnailStroke)] = MapAppearance,
            [nameof(IRotator.NavigationButtonBackgroundColor)] = MapAppearance,
            [nameof(IRotator.NavigationButtonIconColor)] = MapAppearance,
            [nameof(IRotator.EnableAutoPlay)] = MapAutoPlay,
            [nameof(IRotator.NavigationDelay)] = MapAutoPlay,
        };

    public static CommandMapper<IRotator, SfRotatorBridgeHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            [nameof(IRotator.Next)] = (h, _, _) => h.PlatformView?.RequestNext(),
            [nameof(IRotator.Previous)] = (h, _, _) => h.PlatformView?.RequestPrevious(),
            [nameof(IRotator.Refresh)] = (h, _, _) => h.RebuildItems(),
        };

    private readonly List<(object? Item, View View, bool Adopted)> _views = new();
    private IDispatcherTimer? _autoPlay;
    private bool _connected;

    public SfRotatorBridgeHandler() : base(Mapper, CommandMapper)
    {
    }

    protected override SkiaSfRotator CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaSfRotator platformView)
    {
        base.ConnectHandler(platformView);
        platformView.MauiView = VirtualView as View;
        platformView.SelectionRequested += OnSelectionRequested;
        platformView.ItemTapped += OnItemTapped;
        _connected = true;
    }

    protected override void DisconnectHandler(SkiaSfRotator platformView)
    {
        _connected = false;
        StopAutoPlay();
        platformView.SelectionRequested -= OnSelectionRequested;
        platformView.ItemTapped -= OnItemTapped;
        ReleaseViews(_views.ToList());
        _views.Clear();
        platformView.SetItems(Array.Empty<SkiaView>(), Array.Empty<string?>());
        platformView.MauiView = null;
        base.DisconnectHandler(platformView);
    }

    public static void MapAppearance(SfRotatorBridgeHandler handler, IRotator rotator)
    {
        var view = handler.PlatformView;
        if (view == null)
            return;
        view.StripMode = rotator.NavigationStripMode;
        view.DotPlacement = rotator.DotPlacement;
        view.StripPosition = rotator.NavigationStripPosition;
        view.Direction = rotator.NavigationDirection;
        view.EnableSwiping = rotator.EnableSwiping;
        view.EnableLooping = rotator.EnableLooping;
        // Captions are an item-control feature (SfRotatorItem.ItemText).
        view.ShowText = rotator.IsTextVisible && rotator.ItemsSource?.FirstOrDefault() is IRotatorItem;
        view.ShowNavigationButton = rotator.ShowNavigationButton;
        if (rotator.SelectedDotColor != null) view.SelectedDotColor = rotator.SelectedDotColor;
        if (rotator.UnselectedDotColor != null) view.UnselectedDotColor = rotator.UnselectedDotColor;
        if (rotator.DotsStroke != null) view.DotsStroke = rotator.DotsStroke;
        if (rotator.SelectedThumbnailStroke != null) view.SelectedThumbnailStroke = rotator.SelectedThumbnailStroke;
        if (rotator.UnselectedThumbnailStroke != null) view.UnselectedThumbnailStroke = rotator.UnselectedThumbnailStroke;
        if (rotator.NavigationButtonBackgroundColor != null) view.NavigationButtonBackgroundColor = rotator.NavigationButtonBackgroundColor;
        if (rotator.NavigationButtonIconColor != null) view.NavigationButtonIconColor = rotator.NavigationButtonIconColor;
        view.InvalidateMeasure();
        view.Invalidate();
    }

    /// <summary>
    /// Shows the new selection and raises SelectedIndexChanged, as the native
    /// builds do for every change (the user's and the app's).
    /// </summary>
    public static void MapSelectedIndex(SfRotatorBridgeHandler handler, IRotator rotator)
    {
        var view = handler.PlatformView;
        if (view == null)
            return;
        int index = rotator.SelectedIndex;
        bool changed = view.ItemCount > 0 && index != view.SelectedIndex && index >= 0 && index < view.ItemCount;
        view.SetSelectedIndex(index, animate: handler._connected);
        if (changed)
        {
            var args = new global::Syncfusion.Maui.Core.Rotator.SelectedIndexChangedEventArgs();
            SfReflect.Set(args, nameof(args.Rotator), rotator);
            SfReflect.Set(args, nameof(args.Index), (double)index);
            rotator.RaiseSelectionChanged(args);
        }
        handler.RestartAutoPlay();
    }

    public static void MapItems(SfRotatorBridgeHandler handler, IRotator rotator)
    {
        handler.RebuildItems();
        MapAppearance(handler, rotator);
    }

    public static void MapAutoPlay(SfRotatorBridgeHandler handler, IRotator rotator) => handler.RestartAutoPlay();

    private void RebuildItems()
    {
        if (PlatformView is not { } view || MauiContext is not { } context || VirtualView is not { } rotator)
            return;

        var owner = (Element)rotator;
        var previous = _views.ToList();
        _views.Clear();
        var texts = new List<string?>();
        foreach (var item in rotator.ItemsSource ?? Enumerable.Empty<object>())
        {
            int reuse = previous.FindIndex(p => ReferenceEquals(p.Item, item) || (p.Item is ValueType && Equals(p.Item, item)));
            if (reuse >= 0)
            {
                _views.Add(previous[reuse]);
                previous.RemoveAt(reuse);
            }
            else if (ViewFor(item, rotator) is { } itemView)
            {
                _views.Add((item, itemView, SfItemViews.Adopt(itemView, owner)));
            }
            else
            {
                continue;
            }
            texts.Add((item as IRotatorItem)?.ItemText);
        }
        ReleaseViews(previous);

        var platformItems = new List<SkiaView>();
        var platformTexts = new List<string?>();
        for (int i = 0; i < _views.Count; i++)
        {
            if (SfItemViews.PlatformOf(_views[i].View, context) is { } skia)
            {
                platformItems.Add(skia);
                platformTexts.Add(texts[i]);
            }
        }
        view.SetItems(platformItems, platformTexts);
        view.SetSelectedIndex(Math.Clamp(rotator.SelectedIndex, 0, Math.Max(0, platformItems.Count - 1)), animate: false);
    }

    /// <summary>The view for one item, as the native builds' item mapping builds it.</summary>
    private static View? ViewFor(object item, IRotator rotator)
    {
        if (item is IRotatorItem rotatorItem)
        {
            if (rotatorItem.ItemContent != null)
                return rotatorItem.ItemContent;
            if (!string.IsNullOrEmpty(rotatorItem.Image))
                return SfItemViews.ForImage(rotatorItem.Image);
            return new ContentView();
        }
        return SfItemViews.ForData(item, rotator.ItemTemplate, (BindableObject)rotator);
    }

    private static void ReleaseViews(List<(object? Item, View View, bool Adopted)> stale)
    {
        foreach (var (_, view, adopted) in stale)
        {
            view.Handler?.DisconnectHandler();
            if (adopted)
                view.Parent = null;
        }
    }

    private void OnSelectionRequested(object? sender, int index)
    {
        if (VirtualView is { } rotator)
            rotator.SelectedIndex = index;
    }

    private void OnItemTapped(object? sender, EventArgs e) => VirtualView?.RaiseItemTapped(EventArgs.Empty);

    /// <summary>
    /// (Re)starts the autoplay timer, so each item gets the full delay after a
    /// manual change too.
    /// </summary>
    private void RestartAutoPlay()
    {
        StopAutoPlay();
        if (!_connected || VirtualView is not { EnableAutoPlay: true } rotator || rotator.NavigationDelay <= 0)
            return;
        var dispatcher = (rotator as BindableObject)?.Dispatcher ?? Dispatcher.GetForCurrentThread();
        if (dispatcher == null)
            return;
        _autoPlay = dispatcher.CreateTimer();
        _autoPlay.Interval = TimeSpan.FromMilliseconds(rotator.NavigationDelay);
        _autoPlay.IsRepeating = true;
        _autoPlay.Tick += OnAutoPlayTick;
        _autoPlay.Start();
    }

    private void StopAutoPlay()
    {
        if (_autoPlay == null)
            return;
        _autoPlay.Stop();
        _autoPlay.Tick -= OnAutoPlayTick;
        _autoPlay = null;
    }

    private void OnAutoPlayTick(object? sender, EventArgs e)
    {
        if (PlatformView is not { } view)
            return;
        int next = view.NextOf(view.SelectedIndex);
        if (next < 0)
            StopAutoPlay(); // the last item, without looping
        else
            view.RequestNext();
    }

    /// <summary>The autoplay timer, when running (diagnostics and tests).</summary>
    internal IDispatcherTimer? AutoPlayTimer => _autoPlay;
}
