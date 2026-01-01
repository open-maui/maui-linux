using System;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class ScrollViewHandler : ViewHandler<IScrollView, SkiaScrollView>
{
	public static IPropertyMapper<IScrollView, ScrollViewHandler> Mapper = (IPropertyMapper<IScrollView, ScrollViewHandler>)(object)new PropertyMapper<IScrollView, ScrollViewHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Content"] = MapContent,
		["HorizontalScrollBarVisibility"] = MapHorizontalScrollBarVisibility,
		["VerticalScrollBarVisibility"] = MapVerticalScrollBarVisibility,
		["Orientation"] = MapOrientation
	};

	public static CommandMapper<IScrollView, ScrollViewHandler> CommandMapper = new CommandMapper<IScrollView, ScrollViewHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper) { ["RequestScrollTo"] = MapRequestScrollTo };

	public ScrollViewHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public ScrollViewHandler(IPropertyMapper? mapper)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(object)CommandMapper)
	{
	}

	protected override SkiaScrollView CreatePlatformView()
	{
		return new SkiaScrollView();
	}

	public static void MapContent(ScrollViewHandler handler, IScrollView scrollView)
	{
		if (((ViewHandler<IScrollView, SkiaScrollView>)(object)handler).PlatformView == null || ((ElementHandler)handler).MauiContext == null)
		{
			return;
		}
		IView presentedContent = ((IContentView)scrollView).PresentedContent;
		if (presentedContent != null)
		{
			Console.WriteLine("[ScrollViewHandler] MapContent: " + ((object)presentedContent).GetType().Name);
			if (presentedContent.Handler == null)
			{
				presentedContent.Handler = presentedContent.ToViewHandler(((ElementHandler)handler).MauiContext);
			}
			IViewHandler handler2 = presentedContent.Handler;
			if (((handler2 != null) ? ((IElementHandler)handler2).PlatformView : null) is SkiaView skiaView)
			{
				Console.WriteLine("[ScrollViewHandler] Setting content: " + ((object)skiaView).GetType().Name);
				((ViewHandler<IScrollView, SkiaScrollView>)(object)handler).PlatformView.Content = skiaView;
			}
		}
		else
		{
			((ViewHandler<IScrollView, SkiaScrollView>)(object)handler).PlatformView.Content = null;
		}
	}

	public static void MapHorizontalScrollBarVisibility(ScrollViewHandler handler, IScrollView scrollView)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Invalid comparison between Unknown and I4
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		SkiaScrollView platformView = ((ViewHandler<IScrollView, SkiaScrollView>)(object)handler).PlatformView;
		ScrollBarVisibility horizontalScrollBarVisibility = scrollView.HorizontalScrollBarVisibility;
		ScrollBarVisibility horizontalScrollBarVisibility2 = (((int)horizontalScrollBarVisibility == 1) ? ScrollBarVisibility.Always : (((int)horizontalScrollBarVisibility == 2) ? ScrollBarVisibility.Never : ScrollBarVisibility.Default));
		platformView.HorizontalScrollBarVisibility = horizontalScrollBarVisibility2;
	}

	public static void MapVerticalScrollBarVisibility(ScrollViewHandler handler, IScrollView scrollView)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Invalid comparison between Unknown and I4
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		SkiaScrollView platformView = ((ViewHandler<IScrollView, SkiaScrollView>)(object)handler).PlatformView;
		ScrollBarVisibility verticalScrollBarVisibility = scrollView.VerticalScrollBarVisibility;
		ScrollBarVisibility verticalScrollBarVisibility2 = (((int)verticalScrollBarVisibility == 1) ? ScrollBarVisibility.Always : (((int)verticalScrollBarVisibility == 2) ? ScrollBarVisibility.Never : ScrollBarVisibility.Default));
		platformView.VerticalScrollBarVisibility = verticalScrollBarVisibility2;
	}

	public static void MapOrientation(ScrollViewHandler handler, IScrollView scrollView)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected I4, but got Unknown
		SkiaScrollView platformView = ((ViewHandler<IScrollView, SkiaScrollView>)(object)handler).PlatformView;
		ScrollOrientation orientation = scrollView.Orientation;
		platformView.Orientation = (orientation - 1) switch
		{
			0 => ScrollOrientation.Horizontal, 
			1 => ScrollOrientation.Both, 
			2 => ScrollOrientation.Neither, 
			_ => ScrollOrientation.Vertical, 
		};
	}

	public static void MapRequestScrollTo(ScrollViewHandler handler, IScrollView scrollView, object? args)
	{
		ScrollToRequest val = (ScrollToRequest)((args is ScrollToRequest) ? args : null);
		if (val != null)
		{
			((ViewHandler<IScrollView, SkiaScrollView>)(object)handler).PlatformView.ScrollTo((float)val.HorizontalOffset, (float)val.VerticalOffset, !val.Instant);
		}
	}
}
