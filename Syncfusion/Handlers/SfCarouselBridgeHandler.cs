// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Handlers;
using Syncfusion.Maui.Core.Carousel;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Handler for <c>SfCarousel</c> (<see cref="ICarousel"/>), replacing the
/// platform-neutral <c>CarouselHandler</c> whose <c>CreatePlatformView</c>
/// throws. Builds the item views as the native builds do (item controls'
/// content or image, the item template, or the item's text), hands them to
/// <see cref="SkiaSfCarousel"/>, and reports the user's selection back to the
/// control: <c>SelectedIndex</c>, <c>SelectionChanged</c>, <c>SwipeStarted</c>
/// and <c>SwipeEnded</c>. With <c>AllowLoadMore</c> the items come
/// <c>LoadMoreItemsCount</c> at a time behind a "Load More" item. With
/// <c>EnableVirtualization</c> (Default view mode, no load-more) only the items
/// the carousel can show around the selection get views, as the Windows
/// build's <c>MapEnableVirtualization</c> makes its panel realize only the
/// visible range; the others hold an empty place until they come into view.
/// </summary>
public class SfCarouselBridgeHandler : LinuxViewHandler<ICarousel, SkiaSfCarousel>
{
    public static IPropertyMapper<ICarousel, SfCarouselBridgeHandler> Mapper =
        new PropertyMapper<ICarousel, SfCarouselBridgeHandler>(ViewHandler.ViewMapper)
        {
            [nameof(ICarousel.ItemsSource)] = MapItems,
            [nameof(ICarousel.ItemTemplate)] = MapItems,
            [nameof(ICarousel.AllowLoadMore)] = MapItems,
            [nameof(ICarousel.LoadMoreItemsCount)] = MapItems,
            [nameof(ICarousel.LoadMoreView)] = MapItems,
            [nameof(ICarousel.EnableVirtualization)] = MapItems,
            [nameof(ICarousel.SelectedIndex)] = MapSelectedIndex,
            [nameof(ICarousel.ViewMode)] = MapLayout,
            [nameof(ICarousel.ItemSpacing)] = MapLayout,
            [nameof(ICarousel.RotationAngle)] = MapLayout,
            [nameof(ICarousel.Offset)] = MapLayout,
            [nameof(ICarousel.ScaleOffset)] = MapLayout,
            [nameof(ICarousel.SelectedItemOffset)] = MapLayout,
            [nameof(ICarousel.Duration)] = MapLayout,
            [nameof(ICarousel.ItemWidth)] = MapLayout,
            [nameof(ICarousel.ItemHeight)] = MapLayout,
            [nameof(ICarousel.EnableInteraction)] = MapLayout,
            [nameof(ICarousel.SwipeMovementMode)] = MapLayout,
        };

    public static CommandMapper<ICarousel, SfCarouselBridgeHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            [nameof(ICarousel.MoveNext)] = (h, _, _) => h.Move(+1),
            [nameof(ICarousel.MovePrevious)] = (h, _, _) => h.Move(-1),
            [nameof(ICarousel.LoadMore)] = (h, _, _) => h.LoadMore(),
        };

    /// <summary>One item: its view once realized (always, without virtualization).</summary>
    private sealed class Slot
    {
        public Slot(object? item) => Item = item;
        public object? Item { get; }
        public View? View;
        public bool Adopted;
        public SkiaView? Placeholder;
    }

    private readonly List<Slot> _views = new();
    private bool _virtualizing;
    private View? _loadMoreView;
    private bool _loadMoreAdopted;
    private int _shownCount;
    private INotifyCollectionChanged? _observed;
    private bool _connected;

    public SfCarouselBridgeHandler() : base(Mapper, CommandMapper)
    {
    }

    protected override SkiaSfCarousel CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaSfCarousel platformView)
    {
        base.ConnectHandler(platformView);
        platformView.MauiView = VirtualView as View;
        platformView.SelectionRequested += OnSelectionRequested;
        platformView.ItemTapped += OnItemTapped;
        platformView.SwipeStarted += OnSwipeStarted;
        platformView.SwipeEnded += OnSwipeEnded;
        platformView.ShownRangeChanged += OnShownRangeChanged;
        _connected = true;
    }

    protected override void DisconnectHandler(SkiaSfCarousel platformView)
    {
        _connected = false;
        platformView.SelectionRequested -= OnSelectionRequested;
        platformView.ItemTapped -= OnItemTapped;
        platformView.SwipeStarted -= OnSwipeStarted;
        platformView.SwipeEnded -= OnSwipeEnded;
        platformView.ShownRangeChanged -= OnShownRangeChanged;
        Observe(null);
        ReleaseViews();
        platformView.SetItems(Array.Empty<SkiaView>());
        platformView.MauiView = null;
        base.DisconnectHandler(platformView);
    }

    public static void MapLayout(SfCarouselBridgeHandler handler, ICarousel carousel)
    {
        var view = handler.PlatformView;
        if (view == null)
            return;
        view.Mode = carousel.ViewMode == ViewMode.Linear ? CarouselViewMode.Linear : CarouselViewMode.Default;
        view.ItemWidth = carousel.ItemWidth;
        view.ItemHeight = carousel.ItemHeight;
        view.ItemSpacing = carousel.ItemSpacing;
        view.RotationAngle = carousel.RotationAngle;
        view.ItemOffset = carousel.Offset;
        view.ScaleOffset = carousel.ScaleOffset;
        view.SelectedItemOffset = carousel.SelectedItemOffset;
        view.DurationMs = carousel.Duration;
        view.EnableInteraction = carousel.EnableInteraction;
        view.MultipleItemSwipe = carousel.SwipeMovementMode == SwipeMovementMode.MultipleItems;
        view.InvalidateMeasure();
        view.Invalidate();
    }

    public static void MapSelectedIndex(SfCarouselBridgeHandler handler, ICarousel carousel)
        => handler.PlatformView?.SetSelectedIndex(carousel.SelectedIndex, animate: handler._connected);

    public static void MapItems(SfCarouselBridgeHandler handler, ICarousel carousel) => handler.RebuildItems();

    private void RebuildItems()
    {
        if (PlatformView is not { } view || MauiContext is not { } context || VirtualView is not { } carousel)
            return;

        Observe(carousel.ItemsSource as INotifyCollectionChanged);
        var source = carousel.ItemsSource?.ToList() ?? new List<object>();
        int step = Math.Max(1, carousel.LoadMoreItemsCount);
        if (!carousel.AllowLoadMore)
            _shownCount = source.Count;
        else if (_shownCount <= 0 || _shownCount > source.Count)
            _shownCount = Math.Min(step, source.Count);

        _virtualizing = carousel.EnableVirtualization && !carousel.AllowLoadMore && carousel.ViewMode == ViewMode.Default;
        var previous = _views.ToList();
        _views.Clear();
        for (int i = 0; i < _shownCount; i++)
        {
            var item = source[i];
            int reuse = previous.FindIndex(p => ReferenceEquals(p.Item, item) || (p.Item is ValueType && Equals(p.Item, item)));
            if (reuse >= 0)
            {
                _views.Add(previous[reuse]);
                previous.RemoveAt(reuse);
                continue;
            }
            _views.Add(new Slot(item));
        }
        foreach (var stale in previous)
            Release(stale);

        var range = _virtualizing ? view.ShownRange(_views.Count) : (0, _views.Count - 1);
        var platformItems = Realize(range, context, carousel);

        if (carousel.AllowLoadMore && _shownCount < source.Count)
        {
            var loadMore = carousel.LoadMoreView ?? DefaultLoadMore(carousel);
            if (!ReferenceEquals(loadMore, _loadMoreView))
            {
                ReleaseLoadMore();
                _loadMoreView = loadMore;
                _loadMoreAdopted = SfItemViews.Adopt(loadMore, (Element)carousel);
            }
            if (SfItemViews.PlatformOf(loadMore, context) is { } skia)
                platformItems.Add(skia);
        }
        else
        {
            ReleaseLoadMore();
        }

        view.SetItems(platformItems);
        view.SetSelectedIndex(carousel.SelectedIndex, animate: false);
    }

    /// <summary>
    /// Gives the items in <paramref name="range"/> (one more on each side, as
    /// a swipe brings them in) their views and the others an empty place, and
    /// returns the platform views in item order.
    /// </summary>
    private List<SkiaView> Realize((int First, int Last) range, IMauiContext context, ICarousel carousel)
    {
        var owner = (Element)carousel;
        int first = range.First - 1, last = range.Last + 1;
        var platformItems = new List<SkiaView>(_views.Count + 1);
        for (int i = 0; i < _views.Count; i++)
        {
            var slot = _views[i];
            bool wanted = !_virtualizing || (i >= first && i <= last);
            if (wanted && slot.View == null && slot.Item is { } item && ViewFor(item, carousel) is { } itemView)
            {
                slot.View = itemView;
                slot.Adopted = SfItemViews.Adopt(itemView, owner);
            }
            else if (!wanted && slot.View != null)
            {
                Release(slot);
            }
            if (slot.View != null && SfItemViews.PlatformOf(slot.View, context) is { } skia)
            {
                platformItems.Add(skia);
                continue;
            }
            slot.Placeholder ??= new SkiaSfCarouselPlaceholder();
            platformItems.Add(slot.Placeholder);
        }
        return platformItems;
    }

    private static void Release(Slot slot)
    {
        if (slot.View == null)
            return;
        slot.View.Handler?.DisconnectHandler();
        if (slot.Adopted)
            slot.View.Parent = null;
        slot.View = null;
        slot.Adopted = false;
    }

    /// <summary>The selection or a drag moved: realize the items now in view.</summary>
    private void OnShownRangeChanged(object? sender, EventArgs e)
    {
        if (!_virtualizing || PlatformView is not { } view || MauiContext is not { } context || VirtualView is not { } carousel)
            return;
        view.SetItems(Realize(view.ShownRange(_views.Count), context, carousel));
    }

    /// <summary>The number of items with a view (all of them unless virtualizing).</summary>
    internal int RealizedCount => _views.Count(slot => slot.View != null);

    /// <summary>The view for one item, as the native builds' item mapping builds it.</summary>
    private static View? ViewFor(object item, ICarousel carousel)
    {
        if (item is ICarouselItem carouselItem)
        {
            if (carouselItem.ItemContent != null)
                return carouselItem.ItemContent;
            if (!string.IsNullOrEmpty(carouselItem.ImageName))
                return SfItemViews.ForImage(carouselItem.ImageName);
            return new ContentView();
        }
        return SfItemViews.ForData(item, carousel.ItemTemplate, (BindableObject)carousel);
    }

    /// <summary>The "Load More" item the native builds show when no LoadMoreView is set.</summary>
    private static View DefaultLoadMore(ICarousel carousel) => new Border
    {
        Stroke = Color.FromArgb("#CAC4D0"),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 8 },
        BackgroundColor = Color.FromArgb("#FFFBFE"),
        WidthRequest = carousel.ItemWidth,
        HeightRequest = carousel.ItemHeight,
        Content = new Label
        {
            Text = "Load More",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        },
    };

    private void ReleaseViews()
    {
        foreach (var slot in _views)
            Release(slot);
        _views.Clear();
        ReleaseLoadMore();
    }

    private void ReleaseLoadMore()
    {
        if (_loadMoreView == null)
            return;
        _loadMoreView.Handler?.DisconnectHandler();
        if (_loadMoreAdopted)
            _loadMoreView.Parent = null;
        _loadMoreView = null;
        _loadMoreAdopted = false;
    }

    private void Observe(INotifyCollectionChanged? source)
    {
        if (ReferenceEquals(source, _observed))
            return;
        if (_observed != null)
            _observed.CollectionChanged -= OnCollectionChanged;
        _observed = source;
        if (_observed != null)
            _observed.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems();

    private bool IsLoadMoreIndex(int index) => _loadMoreView != null && index == _views.Count;

    private void LoadMore()
    {
        if (VirtualView is not { AllowLoadMore: true } carousel)
            return;
        _shownCount += Math.Max(1, carousel.LoadMoreItemsCount);
        int total = carousel.ItemsSource?.Count() ?? 0;
        _shownCount = Math.Min(_shownCount, total);
        RebuildItems();
    }

    private void Move(int delta)
    {
        if (VirtualView is not { } carousel)
            return;
        int target = carousel.SelectedIndex + delta;
        if (target < 0 || target >= _views.Count)
            return;
        Select(target);
    }

    private void OnSelectionRequested(object? sender, int index)
    {
        if (IsLoadMoreIndex(index))
        {
            LoadMore();
            return;
        }
        Select(index);
    }

    private void OnItemTapped(object? sender, int index)
    {
        if (IsLoadMoreIndex(index))
            LoadMore();
    }

    /// <summary>Selects an item for the user and raises SelectionChanged, as the native builds do.</summary>
    private void Select(int index)
    {
        if (VirtualView is not { } carousel || index == carousel.SelectedIndex)
            return;
        var args = new global::Syncfusion.Maui.Core.Carousel.SelectionChangedEventArgs();
        SfReflect.Set(args, nameof(args.OldItem), ItemAt(carousel.SelectedIndex));
        carousel.SelectedIndex = index;
        SfReflect.Set(args, nameof(args.NewItem), ItemAt(index));
        carousel.RaiseSelectionChanged(args);
    }

    private object? ItemAt(int index)
        => index >= 0 && index < _views.Count ? _views[index].Item : null;

    private void OnSwipeStarted(object? sender, bool left)
    {
        var args = new global::Syncfusion.Maui.Core.Carousel.SwipeStartedEventArgs();
        SfReflect.Set(args, nameof(args.IsSwipedLeft), left);
        VirtualView?.RaiseSwipeStarted(args);
    }

    private void OnSwipeEnded(object? sender, EventArgs e) => VirtualView?.RaiseSwipeEnded(EventArgs.Empty);
}
