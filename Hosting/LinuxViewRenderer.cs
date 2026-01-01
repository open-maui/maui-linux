using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Hosting;

public class LinuxViewRenderer
{
	private readonly IMauiContext _mauiContext;

	public static Shell? CurrentMauiShell { get; private set; }

	public static SkiaShell? CurrentSkiaShell { get; private set; }

	public static LinuxViewRenderer? CurrentRenderer { get; set; }

	public static bool NavigateToRoute(string route)
	{
		if (CurrentSkiaShell == null)
		{
			Console.WriteLine("[NavigateToRoute] CurrentSkiaShell is null");
			return false;
		}
		string text = route.TrimStart('/');
		Console.WriteLine("[NavigateToRoute] Navigating to: " + text);
		for (int i = 0; i < CurrentSkiaShell.Sections.Count; i++)
		{
			ShellSection shellSection = CurrentSkiaShell.Sections[i];
			if (shellSection.Route.Equals(text, StringComparison.OrdinalIgnoreCase) || shellSection.Title.Equals(text, StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine($"[NavigateToRoute] Found section {i}: {shellSection.Title}");
				CurrentSkiaShell.NavigateToSection(i);
				return true;
			}
		}
		Console.WriteLine("[NavigateToRoute] Route not found: " + text);
		return false;
	}

	public static bool PushPage(Page page)
	{
		Console.WriteLine("[PushPage] Pushing page: " + ((object)page).GetType().Name);
		if (CurrentSkiaShell == null)
		{
			Console.WriteLine("[PushPage] CurrentSkiaShell is null");
			return false;
		}
		if (CurrentRenderer == null)
		{
			Console.WriteLine("[PushPage] CurrentRenderer is null");
			return false;
		}
		try
		{
			SkiaView skiaView = null;
			ContentPage val = (ContentPage)(object)((page is ContentPage) ? page : null);
			if (val != null && val.Content != null)
			{
				skiaView = CurrentRenderer.RenderView((IView)(object)val.Content);
			}
			if (skiaView == null)
			{
				Console.WriteLine("[PushPage] Failed to render page content");
				return false;
			}
			if (!(skiaView is SkiaScrollView))
			{
				skiaView = new SkiaScrollView
				{
					Content = skiaView
				};
			}
			CurrentSkiaShell.PushAsync(skiaView, page.Title ?? "Detail");
			Console.WriteLine("[PushPage] Successfully pushed page");
			return true;
		}
		catch (Exception ex)
		{
			Console.WriteLine("[PushPage] Error: " + ex.Message);
			return false;
		}
	}

	public static bool PopPage()
	{
		Console.WriteLine("[PopPage] Popping page");
		if (CurrentSkiaShell == null)
		{
			Console.WriteLine("[PopPage] CurrentSkiaShell is null");
			return false;
		}
		return CurrentSkiaShell.PopAsync();
	}

	public LinuxViewRenderer(IMauiContext mauiContext)
	{
		_mauiContext = mauiContext ?? throw new ArgumentNullException("mauiContext");
		CurrentRenderer = this;
	}

	public SkiaView? RenderPage(Page page)
	{
		if (page == null)
		{
			return null;
		}
		Shell val = (Shell)(object)((page is Shell) ? page : null);
		if (val != null)
		{
			return RenderShell(val);
		}
		IViewHandler handler = ((VisualElement)page).Handler;
		if (handler != null)
		{
			((IElementHandler)handler).DisconnectHandler();
		}
		if (((IElement)(object)page).ToHandler(_mauiContext).PlatformView is SkiaView skiaView)
		{
			ContentPage val2 = (ContentPage)(object)((page is ContentPage) ? page : null);
			if (val2 != null && val2.Content != null)
			{
				SkiaView skiaView2 = RenderView((IView)(object)val2.Content);
				if (skiaView is SkiaPage skiaPage && skiaView2 != null)
				{
					skiaPage.Content = skiaView2;
				}
			}
			return skiaView;
		}
		return null;
	}

	private SkiaShell RenderShell(Shell shell)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected I4, but got Unknown
		CurrentMauiShell = shell;
		SkiaShell skiaShell = new SkiaShell();
		skiaShell.Title = ((Page)shell).Title ?? "App";
		SkiaShell skiaShell2 = skiaShell;
		FlyoutBehavior flyoutBehavior = shell.FlyoutBehavior;
		skiaShell2.FlyoutBehavior = (int)flyoutBehavior switch
		{
			1 => ShellFlyoutBehavior.Flyout, 
			2 => ShellFlyoutBehavior.Locked, 
			0 => ShellFlyoutBehavior.Disabled, 
			_ => ShellFlyoutBehavior.Flyout, 
		};
		skiaShell.MauiShell = shell;
		SkiaShell skiaShell3 = skiaShell;
		ApplyShellColors(skiaShell3, shell);
		object flyoutHeader = shell.FlyoutHeader;
		View val = (View)((flyoutHeader is View) ? flyoutHeader : null);
		if (val != null)
		{
			SkiaView skiaView = RenderView((IView)(object)val);
			if (skiaView != null)
			{
				skiaShell3.FlyoutHeaderView = skiaView;
				skiaShell3.FlyoutHeaderHeight = (float)((((VisualElement)val).HeightRequest > 0.0) ? ((VisualElement)val).HeightRequest : 140.0);
			}
		}
		Version version = Assembly.GetEntryAssembly()?.GetName().Version;
		skiaShell3.FlyoutFooterText = $"Version {version?.Major ?? 1}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
		foreach (ShellItem item in shell.Items)
		{
			ProcessShellItem(skiaShell3, item);
		}
		CurrentSkiaShell = skiaShell3;
		skiaShell3.ContentRenderer = CreateShellContentPage;
		skiaShell3.ColorRefresher = ApplyShellColors;
		shell.Navigated += OnShellNavigated;
		shell.Navigating += delegate(object? s, ShellNavigatingEventArgs e)
		{
			Console.WriteLine($"[Navigation] Navigating: {e.Target}");
		};
		Console.WriteLine($"[Navigation] Shell navigation events subscribed. Sections: {skiaShell3.Sections.Count}");
		for (int num = 0; num < skiaShell3.Sections.Count; num++)
		{
			Console.WriteLine($"[Navigation] Section {num}: Route='{skiaShell3.Sections[num].Route}', Title='{skiaShell3.Sections[num].Title}'");
		}
		return skiaShell3;
	}

	private static void ApplyShellColors(SkiaShell skiaShell, Shell shell)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		Application current = Application.Current;
		bool flag = current != null && (int)current.UserAppTheme == 2;
		Console.WriteLine("[ApplyShellColors] Theme is: " + (flag ? "Dark" : "Light"));
		if (shell.FlyoutBackgroundColor != null && shell.FlyoutBackgroundColor != Colors.Transparent)
		{
			Color flyoutBackgroundColor = shell.FlyoutBackgroundColor;
			skiaShell.FlyoutBackgroundColor = new SKColor((byte)(flyoutBackgroundColor.Red * 255f), (byte)(flyoutBackgroundColor.Green * 255f), (byte)(flyoutBackgroundColor.Blue * 255f), (byte)(flyoutBackgroundColor.Alpha * 255f));
			Console.WriteLine($"[ApplyShellColors] FlyoutBackgroundColor from MAUI: {skiaShell.FlyoutBackgroundColor}");
		}
		else
		{
			skiaShell.FlyoutBackgroundColor = (flag ? new SKColor((byte)30, (byte)30, (byte)30) : new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue));
			Console.WriteLine($"[ApplyShellColors] Using default FlyoutBackgroundColor: {skiaShell.FlyoutBackgroundColor}");
		}
		skiaShell.FlyoutTextColor = (flag ? new SKColor((byte)224, (byte)224, (byte)224) : new SKColor((byte)33, (byte)33, (byte)33));
		Console.WriteLine($"[ApplyShellColors] FlyoutTextColor: {skiaShell.FlyoutTextColor}");
		skiaShell.ContentBackgroundColor = (flag ? new SKColor((byte)18, (byte)18, (byte)18) : new SKColor((byte)250, (byte)250, (byte)250));
		Console.WriteLine($"[ApplyShellColors] ContentBackgroundColor: {skiaShell.ContentBackgroundColor}");
		if (((VisualElement)shell).BackgroundColor != null && ((VisualElement)shell).BackgroundColor != Colors.Transparent)
		{
			Color backgroundColor = ((VisualElement)shell).BackgroundColor;
			skiaShell.NavBarBackgroundColor = new SKColor((byte)(backgroundColor.Red * 255f), (byte)(backgroundColor.Green * 255f), (byte)(backgroundColor.Blue * 255f), (byte)(backgroundColor.Alpha * 255f));
		}
		else
		{
			skiaShell.NavBarBackgroundColor = new SKColor((byte)33, (byte)150, (byte)243);
		}
	}

	private static void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(70, 3);
		defaultInterpolatedStringHandler.AppendLiteral("[Navigation] OnShellNavigated called - Source: ");
		defaultInterpolatedStringHandler.AppendFormatted<ShellNavigationSource>(e.Source);
		defaultInterpolatedStringHandler.AppendLiteral(", Current: ");
		ShellNavigationState current = e.Current;
		defaultInterpolatedStringHandler.AppendFormatted((current != null) ? current.Location : null);
		defaultInterpolatedStringHandler.AppendLiteral(", Previous: ");
		ShellNavigationState previous = e.Previous;
		defaultInterpolatedStringHandler.AppendFormatted((previous != null) ? previous.Location : null);
		Console.WriteLine(defaultInterpolatedStringHandler.ToStringAndClear());
		if (CurrentSkiaShell == null || CurrentMauiShell == null)
		{
			Console.WriteLine("[Navigation] CurrentSkiaShell or CurrentMauiShell is null");
			return;
		}
		ShellNavigationState currentState = CurrentMauiShell.CurrentState;
		string text = ((currentState == null) ? null : currentState.Location?.OriginalString) ?? "";
		Console.WriteLine($"[Navigation] Location: {text}, Sections: {CurrentSkiaShell.Sections.Count}");
		for (int i = 0; i < CurrentSkiaShell.Sections.Count; i++)
		{
			ShellSection shellSection = CurrentSkiaShell.Sections[i];
			Console.WriteLine($"[Navigation] Checking section {i}: Route='{shellSection.Route}', Title='{shellSection.Title}'");
			if (!string.IsNullOrEmpty(shellSection.Route) && text.Contains(shellSection.Route, StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine($"[Navigation] Match found by route! Navigating to section {i}");
				if (i != CurrentSkiaShell.CurrentSectionIndex)
				{
					CurrentSkiaShell.NavigateToSection(i);
				}
				return;
			}
			if (!string.IsNullOrEmpty(shellSection.Title) && text.Contains(shellSection.Title, StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine($"[Navigation] Match found by title! Navigating to section {i}");
				if (i != CurrentSkiaShell.CurrentSectionIndex)
				{
					CurrentSkiaShell.NavigateToSection(i);
				}
				return;
			}
		}
		Console.WriteLine("[Navigation] No matching section found for location: " + text);
	}

	private void ProcessShellItem(SkiaShell skiaShell, ShellItem item)
	{
		FlyoutItem val = (FlyoutItem)(object)((item is FlyoutItem) ? item : null);
		if (val != null)
		{
			ShellSection shellSection = new ShellSection
			{
				Title = (((BaseShellItem)val).Title ?? ""),
				Route = (((BaseShellItem)val).Route ?? ((BaseShellItem)val).Title ?? "")
			};
			foreach (ShellSection item2 in ((ShellItem)val).Items)
			{
				foreach (ShellContent item3 in item2.Items)
				{
					ShellContent shellContent = new ShellContent
					{
						Title = (((BaseShellItem)item3).Title ?? ((BaseShellItem)item2).Title ?? ((BaseShellItem)val).Title ?? ""),
						Route = (((BaseShellItem)item3).Route ?? ""),
						MauiShellContent = item3
					};
					SkiaView skiaView = CreateShellContentPage(item3);
					if (skiaView != null)
					{
						shellContent.Content = skiaView;
					}
					shellSection.Items.Add(shellContent);
				}
			}
			if (shellSection.Items.Count == 1)
			{
				shellSection.Title = shellSection.Items[0].Title;
			}
			skiaShell.AddSection(shellSection);
			return;
		}
		TabBar val2 = (TabBar)(object)((item is TabBar) ? item : null);
		if (val2 != null)
		{
			foreach (ShellSection item4 in ((ShellItem)val2).Items)
			{
				ShellSection shellSection2 = new ShellSection
				{
					Title = (((BaseShellItem)item4).Title ?? ""),
					Route = (((BaseShellItem)item4).Route ?? "")
				};
				foreach (ShellContent item5 in item4.Items)
				{
					ShellContent shellContent2 = new ShellContent
					{
						Title = (((BaseShellItem)item5).Title ?? ((BaseShellItem)item4).Title ?? ""),
						Route = (((BaseShellItem)item5).Route ?? ""),
						MauiShellContent = item5
					};
					SkiaView skiaView2 = CreateShellContentPage(item5);
					if (skiaView2 != null)
					{
						shellContent2.Content = skiaView2;
					}
					shellSection2.Items.Add(shellContent2);
				}
				skiaShell.AddSection(shellSection2);
			}
			return;
		}
		ShellSection shellSection3 = new ShellSection
		{
			Title = (((BaseShellItem)item).Title ?? ""),
			Route = (((BaseShellItem)item).Route ?? "")
		};
		foreach (ShellSection item6 in item.Items)
		{
			foreach (ShellContent item7 in item6.Items)
			{
				ShellContent shellContent3 = new ShellContent
				{
					Title = (((BaseShellItem)item7).Title ?? ""),
					Route = (((BaseShellItem)item7).Route ?? ""),
					MauiShellContent = item7
				};
				SkiaView skiaView3 = CreateShellContentPage(item7);
				if (skiaView3 != null)
				{
					shellContent3.Content = skiaView3;
				}
				shellSection3.Items.Add(shellContent3);
			}
		}
		skiaShell.AddSection(shellSection3);
	}

	private SkiaView? CreateShellContentPage(ShellContent content)
	{
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Page val = null;
			if (content.ContentTemplate != null)
			{
				object obj = ((ElementTemplate)content.ContentTemplate).CreateContent();
				val = (Page)((obj is Page) ? obj : null);
			}
			if (val == null)
			{
				object content2 = content.Content;
				Page val2 = (Page)((content2 is Page) ? content2 : null);
				if (val2 != null)
				{
					val = val2;
				}
			}
			ContentPage val3 = (ContentPage)(object)((val is ContentPage) ? val : null);
			if (val3 != null && val3.Content != null)
			{
				SkiaView skiaView = RenderView((IView)(object)val3.Content);
				if (skiaView != null)
				{
					SKColor? value = null;
					if (((VisualElement)val3).BackgroundColor != null && ((VisualElement)val3).BackgroundColor != Colors.Transparent)
					{
						Color backgroundColor = ((VisualElement)val3).BackgroundColor;
						value = new SKColor((byte)(backgroundColor.Red * 255f), (byte)(backgroundColor.Green * 255f), (byte)(backgroundColor.Blue * 255f), (byte)(backgroundColor.Alpha * 255f));
						Console.WriteLine($"[CreateShellContentPage] Page BackgroundColor: {value}");
					}
					if (skiaView is SkiaScrollView skiaScrollView)
					{
						if (value.HasValue)
						{
							skiaScrollView.BackgroundColor = value.Value;
						}
						return skiaScrollView;
					}
					SkiaScrollView skiaScrollView2 = new SkiaScrollView
					{
						Content = skiaView
					};
					if (value.HasValue)
					{
						skiaScrollView2.BackgroundColor = value.Value;
					}
					return skiaScrollView2;
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	public SkiaView? RenderView(IView view)
	{
		if (view == null)
		{
			return null;
		}
		try
		{
			Element val = (Element)(object)((view is Element) ? view : null);
			if (val != null && val.Handler != null)
			{
				val.Handler.DisconnectHandler();
			}
			IElementHandler obj = ((IElement)(object)view).ToHandler(_mauiContext);
			if (!(((obj != null) ? obj.PlatformView : null) is SkiaView result))
			{
				return CreateFallbackView(view);
			}
			return result;
		}
		catch (Exception)
		{
			return CreateFallbackView(view);
		}
	}

	private SkiaView CreateFallbackView(IView view)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		return new SkiaLabel
		{
			Text = "[" + ((object)view).GetType().Name + "]",
			TextColor = SKColors.Gray,
			FontSize = 12f
		};
	}
}
