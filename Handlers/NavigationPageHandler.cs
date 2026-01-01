using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class NavigationPageHandler : ViewHandler<NavigationPage, SkiaNavigationPage>
{
	public static IPropertyMapper<NavigationPage, NavigationPageHandler> Mapper = (IPropertyMapper<NavigationPage, NavigationPageHandler>)(object)new PropertyMapper<NavigationPage, NavigationPageHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["BarBackgroundColor"] = MapBarBackgroundColor,
		["BarBackground"] = MapBarBackground,
		["BarTextColor"] = MapBarTextColor,
		["Background"] = MapBackground
	};

	public static CommandMapper<NavigationPage, NavigationPageHandler> CommandMapper = new CommandMapper<NavigationPage, NavigationPageHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper) { ["RequestNavigation"] = MapRequestNavigation };

	private readonly Dictionary<Page, (SkiaPage, INotifyCollectionChanged)> _toolbarSubscriptions = new Dictionary<Page, (SkiaPage, INotifyCollectionChanged)>();

	public NavigationPageHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public NavigationPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaNavigationPage CreatePlatformView()
	{
		return new SkiaNavigationPage();
	}

	protected override void ConnectHandler(SkiaNavigationPage platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Pushed += OnPushed;
		platformView.Popped += OnPopped;
		platformView.PoppedToRoot += OnPoppedToRoot;
		if (base.VirtualView != null)
		{
			base.VirtualView.Pushed += OnVirtualViewPushed;
			base.VirtualView.Popped += OnVirtualViewPopped;
			base.VirtualView.PoppedToRoot += OnVirtualViewPoppedToRoot;
			SetupNavigationStack();
		}
	}

	protected override void DisconnectHandler(SkiaNavigationPage platformView)
	{
		platformView.Pushed -= OnPushed;
		platformView.Popped -= OnPopped;
		platformView.PoppedToRoot -= OnPoppedToRoot;
		if (base.VirtualView != null)
		{
			base.VirtualView.Pushed -= OnVirtualViewPushed;
			base.VirtualView.Popped -= OnVirtualViewPopped;
			base.VirtualView.PoppedToRoot -= OnVirtualViewPoppedToRoot;
		}
		base.DisconnectHandler(platformView);
	}

	private void SetupNavigationStack()
	{
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		if (base.VirtualView == null || base.PlatformView == null || ((ElementHandler)this).MauiContext == null)
		{
			return;
		}
		List<Page> list = ((NavigableElement)base.VirtualView).Navigation.NavigationStack.ToList();
		Console.WriteLine($"[NavigationPageHandler] Setting up {list.Count} pages");
		if (list.Count == 0 && base.VirtualView.CurrentPage != null)
		{
			Console.WriteLine("[NavigationPageHandler] No pages in stack, using CurrentPage: " + base.VirtualView.CurrentPage.Title);
			list.Add(base.VirtualView.CurrentPage);
		}
		foreach (Page item in list)
		{
			if (((VisualElement)item).Handler == null)
			{
				Console.WriteLine("[NavigationPageHandler] Creating handler for: " + item.Title);
				((VisualElement)item).Handler = ((IView)(object)item).ToViewHandler(((ElementHandler)this).MauiContext);
			}
			Console.WriteLine("[NavigationPageHandler] Page handler type: " + ((object)((VisualElement)item).Handler)?.GetType().Name);
			IViewHandler handler = ((VisualElement)item).Handler;
			Console.WriteLine("[NavigationPageHandler] Page PlatformView type: " + ((handler == null) ? null : ((IElementHandler)handler).PlatformView?.GetType().Name));
			IViewHandler handler2 = ((VisualElement)item).Handler;
			if (((handler2 != null) ? ((IElementHandler)handler2).PlatformView : null) is SkiaPage skiaPage)
			{
				skiaPage.ShowNavigationBar = true;
				skiaPage.TitleBarColor = base.PlatformView.BarBackgroundColor;
				skiaPage.TitleTextColor = base.PlatformView.BarTextColor;
				skiaPage.Title = item.Title ?? "";
				Console.WriteLine("[NavigationPageHandler] SkiaPage content: " + (((object)skiaPage.Content)?.GetType().Name ?? "null"));
				if (skiaPage.Content == null)
				{
					ContentPage val = (ContentPage)(object)((item is ContentPage) ? item : null);
					if (val != null && val.Content != null)
					{
						Console.WriteLine("[NavigationPageHandler] Content is null, manually creating handler for: " + ((object)val.Content).GetType().Name);
						if (((VisualElement)val.Content).Handler == null)
						{
							((VisualElement)val.Content).Handler = ((IView)(object)val.Content).ToViewHandler(((ElementHandler)this).MauiContext);
						}
						IViewHandler handler3 = ((VisualElement)val.Content).Handler;
						if (((handler3 != null) ? ((IElementHandler)handler3).PlatformView : null) is SkiaView skiaView)
						{
							skiaPage.Content = skiaView;
							Console.WriteLine("[NavigationPageHandler] Set content to: " + ((object)skiaView).GetType().Name);
						}
					}
				}
				MapToolbarItems(skiaPage, item);
				if (base.PlatformView.StackDepth == 0)
				{
					Console.WriteLine("[NavigationPageHandler] Setting root page: " + item.Title);
					base.PlatformView.SetRootPage(skiaPage);
				}
				else
				{
					Console.WriteLine("[NavigationPageHandler] Pushing page: " + item.Title);
					base.PlatformView.Push(skiaPage, animated: false);
				}
			}
			else
			{
				Console.WriteLine("[NavigationPageHandler] Failed to get SkiaPage for: " + item.Title);
			}
		}
	}

	private void MapToolbarItems(SkiaPage skiaPage, Page page)
	{
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Invalid comparison between Unknown and I4
		if (!(skiaPage is SkiaContentPage skiaContentPage))
		{
			return;
		}
		Console.WriteLine($"[NavigationPageHandler] MapToolbarItems for '{page.Title}', count={page.ToolbarItems.Count}");
		skiaContentPage.ToolbarItems.Clear();
		foreach (ToolbarItem toolbarItem2 in page.ToolbarItems)
		{
			Console.WriteLine($"[NavigationPageHandler] Adding toolbar item: '{((MenuItem)toolbarItem2).Text}', IconImageSource={((MenuItem)toolbarItem2).IconImageSource}, Order={toolbarItem2.Order}");
			SkiaToolbarItemOrder order = (((int)toolbarItem2.Order == 2) ? SkiaToolbarItemOrder.Secondary : SkiaToolbarItemOrder.Primary);
			ToolbarItem toolbarItem = toolbarItem2;
			RelayCommand command = new RelayCommand(delegate
			{
				Console.WriteLine("[NavigationPageHandler] ToolbarItem '" + ((MenuItem)toolbarItem).Text + "' clicked, invoking...");
				IMenuItemController val2 = (IMenuItemController)(object)toolbarItem;
				if (val2 != null)
				{
					val2.Activate();
				}
				else
				{
					((MenuItem)toolbarItem).Command?.Execute(((MenuItem)toolbarItem).CommandParameter);
				}
			});
			SKBitmap icon = null;
			ImageSource iconImageSource = ((MenuItem)toolbarItem2).IconImageSource;
			FileImageSource val = (FileImageSource)(object)((iconImageSource is FileImageSource) ? iconImageSource : null);
			if (val != null && !string.IsNullOrEmpty(val.File))
			{
				icon = LoadToolbarIcon(val.File);
			}
			skiaContentPage.ToolbarItems.Add(new SkiaToolbarItem
			{
				Text = (((MenuItem)toolbarItem2).Text ?? ""),
				Icon = icon,
				Order = order,
				Command = command
			});
		}
		if (page.ToolbarItems is INotifyCollectionChanged notifyCollectionChanged && !_toolbarSubscriptions.ContainsKey(page))
		{
			Console.WriteLine("[NavigationPageHandler] Subscribing to ToolbarItems changes for '" + page.Title + "'");
			notifyCollectionChanged.CollectionChanged += delegate(object? s, NotifyCollectionChangedEventArgs e)
			{
				Console.WriteLine($"[NavigationPageHandler] ToolbarItems changed for '{page.Title}', action={e.Action}");
				MapToolbarItems(skiaPage, page);
				skiaPage.Invalidate();
			};
			_toolbarSubscriptions[page] = (skiaPage, notifyCollectionChanged);
		}
	}

	private SKBitmap? LoadToolbarIcon(string fileName)
	{
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Expected O, but got Unknown
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Expected O, but got Unknown
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Expected O, but got Unknown
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			string baseDirectory = AppContext.BaseDirectory;
			string text = Path.Combine(baseDirectory, fileName);
			string text2 = Path.Combine(baseDirectory, Path.ChangeExtension(fileName, ".svg"));
			Console.WriteLine("[NavigationPageHandler] LoadToolbarIcon: Looking for " + fileName);
			Console.WriteLine($"[NavigationPageHandler]   Trying PNG: {text} (exists: {File.Exists(text)})");
			Console.WriteLine($"[NavigationPageHandler]   Trying SVG: {text2} (exists: {File.Exists(text2)})");
			if (File.Exists(text2))
			{
				SKSvg val = new SKSvg();
				try
				{
					val.Load(text2);
					if (val.Picture != null)
					{
						SKRect cullRect = val.Picture.CullRect;
						float num = 24f / Math.Max(((SKRect)(ref cullRect)).Width, ((SKRect)(ref cullRect)).Height);
						SKBitmap val2 = new SKBitmap(24, 24, false);
						SKCanvas val3 = new SKCanvas(val2);
						try
						{
							val3.Clear(SKColors.Transparent);
							val3.Scale(num);
							val3.DrawPicture(val.Picture, (SKPaint)null);
							Console.WriteLine("[NavigationPageHandler] Loaded SVG icon: " + text2);
							return val2;
						}
						finally
						{
							((IDisposable)val3)?.Dispose();
						}
					}
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			}
			if (File.Exists(text))
			{
				using (FileStream fileStream = File.OpenRead(text))
				{
					SKBitmap result = SKBitmap.Decode((Stream)fileStream);
					Console.WriteLine("[NavigationPageHandler] Loaded PNG icon: " + text);
					return result;
				}
			}
			Console.WriteLine("[NavigationPageHandler] Icon not found: " + fileName);
			return null;
		}
		catch (Exception ex)
		{
			Console.WriteLine("[NavigationPageHandler] Error loading icon " + fileName + ": " + ex.Message);
			return null;
		}
	}

	private void OnVirtualViewPushed(object? sender, NavigationEventArgs e)
	{
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Page page = e.Page;
			Console.WriteLine("[NavigationPageHandler] VirtualView Pushed: " + ((page != null) ? page.Title : null));
			if (e.Page == null || base.PlatformView == null || ((ElementHandler)this).MauiContext == null)
			{
				return;
			}
			if (((VisualElement)e.Page).Handler == null)
			{
				Console.WriteLine("[NavigationPageHandler] Creating handler for page: " + ((object)e.Page).GetType().Name);
				((VisualElement)e.Page).Handler = ((IView)(object)e.Page).ToViewHandler(((ElementHandler)this).MauiContext);
				Console.WriteLine("[NavigationPageHandler] Handler created: " + ((object)((VisualElement)e.Page).Handler)?.GetType().Name);
			}
			IViewHandler handler = ((VisualElement)e.Page).Handler;
			if (((handler != null) ? ((IElementHandler)handler).PlatformView : null) is SkiaPage skiaPage)
			{
				Console.WriteLine("[NavigationPageHandler] Setting up skiaPage, content: " + (((object)skiaPage.Content)?.GetType().Name ?? "null"));
				skiaPage.ShowNavigationBar = true;
				skiaPage.TitleBarColor = base.PlatformView.BarBackgroundColor;
				skiaPage.TitleTextColor = base.PlatformView.BarTextColor;
				skiaPage.Title = e.Page.Title ?? "";
				if (skiaPage.Content == null)
				{
					Page page2 = e.Page;
					ContentPage val = (ContentPage)(object)((page2 is ContentPage) ? page2 : null);
					if (val != null && val.Content != null)
					{
						Console.WriteLine("[NavigationPageHandler] Content is null, creating handler for: " + ((object)val.Content).GetType().Name);
						if (((VisualElement)val.Content).Handler == null)
						{
							((VisualElement)val.Content).Handler = ((IView)(object)val.Content).ToViewHandler(((ElementHandler)this).MauiContext);
						}
						IViewHandler handler2 = ((VisualElement)val.Content).Handler;
						if (((handler2 != null) ? ((IElementHandler)handler2).PlatformView : null) is SkiaView skiaView)
						{
							skiaPage.Content = skiaView;
							Console.WriteLine("[NavigationPageHandler] Set content to: " + ((object)skiaView).GetType().Name);
						}
					}
				}
				Console.WriteLine("[NavigationPageHandler] Mapping toolbar items");
				MapToolbarItems(skiaPage, e.Page);
				Console.WriteLine("[NavigationPageHandler] Pushing page to platform");
				base.PlatformView.Push(skiaPage, animated: false);
				Console.WriteLine($"[NavigationPageHandler] Push complete, thread={Environment.CurrentManagedThreadId}");
			}
			Console.WriteLine("[NavigationPageHandler] OnVirtualViewPushed returning");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[NavigationPageHandler] EXCEPTION in OnVirtualViewPushed: " + ex.GetType().Name + ": " + ex.Message);
			Console.WriteLine("[NavigationPageHandler] Stack trace: " + ex.StackTrace);
			throw;
		}
	}

	private void OnVirtualViewPopped(object? sender, NavigationEventArgs e)
	{
		Page page = e.Page;
		Console.WriteLine("[NavigationPageHandler] VirtualView Popped: " + ((page != null) ? page.Title : null));
		base.PlatformView?.Pop();
	}

	private void OnVirtualViewPoppedToRoot(object? sender, NavigationEventArgs e)
	{
		Console.WriteLine("[NavigationPageHandler] VirtualView PoppedToRoot");
		base.PlatformView?.PopToRoot();
	}

	private void OnPushed(object? sender, NavigationEventArgs e)
	{
	}

	private void OnPopped(object? sender, NavigationEventArgs e)
	{
		NavigationPage virtualView = base.VirtualView;
		if (virtualView != null && ((NavigableElement)virtualView).Navigation.NavigationStack.Count > 1)
		{
			((NavigableElement)base.VirtualView).Navigation.RemovePage(((NavigableElement)base.VirtualView).Navigation.NavigationStack.Last());
		}
	}

	private void OnPoppedToRoot(object? sender, NavigationEventArgs e)
	{
	}

	public static void MapBarBackgroundColor(NavigationPageHandler handler, NavigationPage navigationPage)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView != null && navigationPage.BarBackgroundColor != null)
		{
			((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView.BarBackgroundColor = navigationPage.BarBackgroundColor.ToSKColor();
		}
	}

	public static void MapBarBackground(NavigationPageHandler handler, NavigationPage navigationPage)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView != null)
		{
			Brush barBackground = navigationPage.BarBackground;
			SolidColorBrush val = (SolidColorBrush)(object)((barBackground is SolidColorBrush) ? barBackground : null);
			if (val != null)
			{
				((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView.BarBackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapBarTextColor(NavigationPageHandler handler, NavigationPage navigationPage)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView != null && navigationPage.BarTextColor != null)
		{
			((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView.BarTextColor = navigationPage.BarTextColor.ToSKColor();
		}
	}

	public static void MapBackground(NavigationPageHandler handler, NavigationPage navigationPage)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView != null)
		{
			Brush background = ((VisualElement)navigationPage).Background;
			SolidColorBrush val = (SolidColorBrush)(object)((background is SolidColorBrush) ? background : null);
			if (val != null)
			{
				((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapRequestNavigation(NavigationPageHandler handler, NavigationPage navigationPage, object? args)
	{
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView == null || ((ElementHandler)handler).MauiContext == null)
		{
			return;
		}
		NavigationRequest val = (NavigationRequest)((args is NavigationRequest) ? args : null);
		if (val == null)
		{
			return;
		}
		Console.WriteLine($"[NavigationPageHandler] MapRequestNavigation: {val.NavigationStack.Count} pages");
		foreach (IView item in val.NavigationStack)
		{
			Page val2 = (Page)(object)((item is Page) ? item : null);
			if (val2 == null)
			{
				continue;
			}
			if (((VisualElement)val2).Handler == null)
			{
				((VisualElement)val2).Handler = ((IView)(object)val2).ToViewHandler(((ElementHandler)handler).MauiContext);
			}
			IViewHandler handler2 = ((VisualElement)val2).Handler;
			if (((handler2 != null) ? ((IElementHandler)handler2).PlatformView : null) is SkiaPage skiaPage)
			{
				skiaPage.ShowNavigationBar = true;
				skiaPage.TitleBarColor = ((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView.BarBackgroundColor;
				skiaPage.TitleTextColor = ((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView.BarTextColor;
				handler.MapToolbarItems(skiaPage, val2);
				if (((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView.StackDepth == 0)
				{
					((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView.SetRootPage(skiaPage);
				}
				else
				{
					((ViewHandler<NavigationPage, SkiaNavigationPage>)(object)handler).PlatformView.Push(skiaPage, val.Animated);
				}
			}
		}
	}
}
