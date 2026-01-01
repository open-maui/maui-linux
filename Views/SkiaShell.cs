using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaShell : SkiaLayoutView
{
	public static readonly BindableProperty FlyoutIsPresentedProperty = BindableProperty.Create("FlyoutIsPresented", typeof(bool), typeof(SkiaShell), (object)false, (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).OnFlyoutIsPresentedChanged((bool)n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FlyoutBehaviorProperty = BindableProperty.Create("FlyoutBehavior", typeof(ShellFlyoutBehavior), typeof(SkiaShell), (object)ShellFlyoutBehavior.Flyout, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FlyoutWidthProperty = BindableProperty.Create("FlyoutWidth", typeof(float), typeof(SkiaShell), (object)280f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)((BindableObject b, object v) => Math.Max(100f, (float)v)), (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FlyoutBackgroundColorProperty = BindableProperty.Create("FlyoutBackgroundColor", typeof(SKColor), typeof(SkiaShell), (object)SKColors.White, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FlyoutTextColorProperty = BindableProperty.Create("FlyoutTextColor", typeof(SKColor), typeof(SkiaShell), (object)new SKColor((byte)33, (byte)33, (byte)33), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty NavBarBackgroundColorProperty = BindableProperty.Create("NavBarBackgroundColor", typeof(SKColor), typeof(SkiaShell), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty NavBarTextColorProperty = BindableProperty.Create("NavBarTextColor", typeof(SKColor), typeof(SkiaShell), (object)SKColors.White, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty NavBarHeightProperty = BindableProperty.Create("NavBarHeight", typeof(float), typeof(SkiaShell), (object)56f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TabBarHeightProperty = BindableProperty.Create("TabBarHeight", typeof(float), typeof(SkiaShell), (object)56f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty NavBarIsVisibleProperty = BindableProperty.Create("NavBarIsVisible", typeof(bool), typeof(SkiaShell), (object)true, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TabBarIsVisibleProperty = BindableProperty.Create("TabBarIsVisible", typeof(bool), typeof(SkiaShell), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ContentPaddingProperty = BindableProperty.Create("ContentPadding", typeof(float), typeof(SkiaShell), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ContentBackgroundColorProperty = BindableProperty.Create("ContentBackgroundColor", typeof(SKColor), typeof(SkiaShell), (object)new SKColor((byte)250, (byte)250, (byte)250), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TitleProperty = BindableProperty.Create("Title", typeof(string), typeof(SkiaShell), (object)string.Empty, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaShell)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private readonly List<ShellSection> _sections = new List<ShellSection>();

	private SkiaView? _currentContent;

	private float _flyoutAnimationProgress;

	private int _selectedSectionIndex;

	private int _selectedItemIndex;

	private readonly Stack<(SkiaView Content, string Title)> _navigationStack = new Stack<(SkiaView, string)>();

	private float _flyoutScrollOffset;

	private readonly Dictionary<string, Func<SkiaView?>> _registeredRoutes = new Dictionary<string, Func<SkiaView>>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, string> _routeTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public bool FlyoutIsPresented
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(FlyoutIsPresentedProperty);
		}
		set
		{
			((BindableObject)this).SetValue(FlyoutIsPresentedProperty, (object)value);
		}
	}

	public ShellFlyoutBehavior FlyoutBehavior
	{
		get
		{
			return (ShellFlyoutBehavior)((BindableObject)this).GetValue(FlyoutBehaviorProperty);
		}
		set
		{
			((BindableObject)this).SetValue(FlyoutBehaviorProperty, (object)value);
		}
	}

	public float FlyoutWidth
	{
		get
		{
			return (float)((BindableObject)this).GetValue(FlyoutWidthProperty);
		}
		set
		{
			((BindableObject)this).SetValue(FlyoutWidthProperty, (object)value);
		}
	}

	public SKColor FlyoutBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(FlyoutBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(FlyoutBackgroundColorProperty, (object)value);
		}
	}

	public SKColor FlyoutTextColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(FlyoutTextColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(FlyoutTextColorProperty, (object)value);
		}
	}

	public SkiaView? FlyoutHeaderView { get; set; }

	public float FlyoutHeaderHeight { get; set; } = 140f;

	public string? FlyoutFooterText { get; set; }

	public float FlyoutFooterHeight { get; set; } = 40f;

	public SKColor NavBarBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(NavBarBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(NavBarBackgroundColorProperty, (object)value);
		}
	}

	public SKColor NavBarTextColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(NavBarTextColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(NavBarTextColorProperty, (object)value);
		}
	}

	public float NavBarHeight
	{
		get
		{
			return (float)((BindableObject)this).GetValue(NavBarHeightProperty);
		}
		set
		{
			((BindableObject)this).SetValue(NavBarHeightProperty, (object)value);
		}
	}

	public float TabBarHeight
	{
		get
		{
			return (float)((BindableObject)this).GetValue(TabBarHeightProperty);
		}
		set
		{
			((BindableObject)this).SetValue(TabBarHeightProperty, (object)value);
		}
	}

	public bool NavBarIsVisible
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(NavBarIsVisibleProperty);
		}
		set
		{
			((BindableObject)this).SetValue(NavBarIsVisibleProperty, (object)value);
		}
	}

	public bool TabBarIsVisible
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(TabBarIsVisibleProperty);
		}
		set
		{
			((BindableObject)this).SetValue(TabBarIsVisibleProperty, (object)value);
		}
	}

	public float ContentPadding
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ContentPaddingProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ContentPaddingProperty, (object)value);
		}
	}

	public SKColor ContentBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ContentBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ContentBackgroundColorProperty, (object)value);
		}
	}

	public string Title
	{
		get
		{
			return (string)((BindableObject)this).GetValue(TitleProperty);
		}
		set
		{
			((BindableObject)this).SetValue(TitleProperty, (object)value);
		}
	}

	public IReadOnlyList<ShellSection> Sections => _sections;

	public int CurrentSectionIndex => _selectedSectionIndex;

	public Func<ShellContent, SkiaView?>? ContentRenderer { get; set; }

	public Action<SkiaShell, Shell>? ColorRefresher { get; set; }

	public Shell? MauiShell { get; set; }

	public bool CanGoBack => _navigationStack.Count > 0;

	public int NavigationStackDepth => _navigationStack.Count;

	public event EventHandler? FlyoutIsPresentedChanged;

	public event EventHandler<ShellNavigationEventArgs>? Navigated;

	private void OnFlyoutIsPresentedChanged(bool newValue)
	{
		_flyoutAnimationProgress = (newValue ? 1f : 0f);
		this.FlyoutIsPresentedChanged?.Invoke(this, EventArgs.Empty);
		Invalidate();
	}

	public void RefreshTheme()
	{
		Console.WriteLine("[SkiaShell] RefreshTheme called - refreshing all pages");
		if (MauiShell != null && ColorRefresher != null)
		{
			Console.WriteLine("[SkiaShell] Refreshing shell colors");
			ColorRefresher(this, MauiShell);
		}
		if (ContentRenderer != null)
		{
			foreach (ShellSection section in _sections)
			{
				foreach (ShellContent item in section.Items)
				{
					if (item.MauiShellContent != null)
					{
						Console.WriteLine("[SkiaShell] Re-rendering: " + item.Title);
						SkiaView skiaView = ContentRenderer(item.MauiShellContent);
						if (skiaView != null)
						{
							item.Content = skiaView;
						}
					}
				}
			}
		}
		if (_selectedSectionIndex >= 0 && _selectedSectionIndex < _sections.Count)
		{
			ShellSection shellSection = _sections[_selectedSectionIndex];
			if (_selectedItemIndex >= 0 && _selectedItemIndex < shellSection.Items.Count)
			{
				ShellContent shellContent = shellSection.Items[_selectedItemIndex];
				SetCurrentContent(shellContent.Content);
			}
		}
		InvalidateMeasure();
		Invalidate();
	}

	public void AddSection(ShellSection section)
	{
		_sections.Add(section);
		if (_sections.Count == 1)
		{
			NavigateToSection(0);
		}
		Invalidate();
	}

	public void RemoveSection(ShellSection section)
	{
		_sections.Remove(section);
		Invalidate();
	}

	public void NavigateToSection(int sectionIndex, int itemIndex = 0)
	{
		if (sectionIndex >= 0 && sectionIndex < _sections.Count)
		{
			ShellSection shellSection = _sections[sectionIndex];
			if (itemIndex >= 0 && itemIndex < shellSection.Items.Count)
			{
				_navigationStack.Clear();
				_selectedSectionIndex = sectionIndex;
				_selectedItemIndex = itemIndex;
				ShellContent shellContent = shellSection.Items[itemIndex];
				SetCurrentContent(shellContent.Content);
				Title = shellContent.Title;
				this.Navigated?.Invoke(this, new ShellNavigationEventArgs(shellSection, shellContent));
				Invalidate();
			}
		}
	}

	public void GoToAsync(string route)
	{
		GoToAsync(route, null);
	}

	public void GoToAsync(string route, IDictionary<string, object>? parameters)
	{
		if (string.IsNullOrEmpty(route))
		{
			return;
		}
		string text = route;
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		int num = route.IndexOf('?');
		if (num >= 0)
		{
			text = route.Substring(0, num);
			dictionary = ParseQueryString(route.Substring(num + 1));
		}
		Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
		foreach (KeyValuePair<string, string> item in dictionary)
		{
			dictionary2[item.Key] = item.Value;
		}
		if (parameters != null)
		{
			foreach (KeyValuePair<string, object> parameter in parameters)
			{
				dictionary2[parameter.Key] = parameter.Value;
			}
		}
		string[] array = text.TrimStart('/').Split('/');
		if (array.Length == 0)
		{
			return;
		}
		if (_registeredRoutes.TryGetValue(text.TrimStart('/'), out Func<SkiaView> value))
		{
			SkiaView skiaView = value();
			if (skiaView != null)
			{
				ApplyQueryParameters(skiaView, dictionary2);
				PushAsync(skiaView, GetRouteTitle(text.TrimStart('/')));
				return;
			}
		}
		for (int i = 0; i < _sections.Count; i++)
		{
			ShellSection shellSection = _sections[i];
			if (!shellSection.Route.Equals(array[0], StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (array.Length > 1)
			{
				for (int j = 0; j < shellSection.Items.Count; j++)
				{
					if (shellSection.Items[j].Route.Equals(array[1], StringComparison.OrdinalIgnoreCase))
					{
						NavigateToSection(i, j);
						if (shellSection.Items[j].Content != null && dictionary2.Count > 0)
						{
							ApplyQueryParameters(shellSection.Items[j].Content, dictionary2);
						}
						return;
					}
				}
			}
			NavigateToSection(i);
			if (shellSection.Items.Count > 0 && shellSection.Items[0].Content != null && dictionary2.Count > 0)
			{
				ApplyQueryParameters(shellSection.Items[0].Content, dictionary2);
			}
			break;
		}
	}

	private static Dictionary<string, string> ParseQueryString(string queryString)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrEmpty(queryString))
		{
			return dictionary;
		}
		string[] array = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split('=', 2);
			if (array2.Length == 2)
			{
				string key = Uri.UnescapeDataString(array2[0]);
				string value = Uri.UnescapeDataString(array2[1]);
				dictionary[key] = value;
			}
			else if (array2.Length == 1)
			{
				dictionary[Uri.UnescapeDataString(array2[0])] = string.Empty;
			}
		}
		return dictionary;
	}

	private static void ApplyQueryParameters(SkiaView content, IDictionary<string, object> parameters)
	{
		if (parameters.Count == 0)
		{
			return;
		}
		if (content is ISkiaQueryAttributable skiaQueryAttributable)
		{
			skiaQueryAttributable.ApplyQueryAttributes(parameters);
		}
		Type type = ((object)content).GetType();
		foreach (KeyValuePair<string, object> parameter in parameters)
		{
			PropertyInfo property = type.GetProperty(parameter.Key, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
			if (property != null && property.CanWrite)
			{
				try
				{
					object value = Convert.ChangeType(parameter.Value, property.PropertyType);
					property.SetValue(content, value);
				}
				catch
				{
				}
			}
		}
	}

	public void RegisterRoute(string route, Func<SkiaView?> contentFactory, string? title = null)
	{
		string key = route.TrimStart('/');
		_registeredRoutes[key] = contentFactory;
		if (!string.IsNullOrEmpty(title))
		{
			_routeTitles[key] = title;
		}
	}

	public void UnregisterRoute(string route)
	{
		string key = route.TrimStart('/');
		_registeredRoutes.Remove(key);
		_routeTitles.Remove(key);
	}

	private string GetRouteTitle(string route)
	{
		if (_routeTitles.TryGetValue(route, out string value))
		{
			return value;
		}
		return route.Split('/').LastOrDefault() ?? route;
	}

	public void PushAsync(SkiaView page, string title)
	{
		if (_currentContent != null)
		{
			_navigationStack.Push((_currentContent, Title));
		}
		SetCurrentContent(page);
		Title = title;
		Invalidate();
	}

	public bool PopAsync()
	{
		if (_navigationStack.Count == 0)
		{
			return false;
		}
		var (currentContent, title) = _navigationStack.Pop();
		SetCurrentContent(currentContent);
		Title = title;
		Invalidate();
		return true;
	}

	public void PopToRootAsync()
	{
		if (_navigationStack.Count != 0)
		{
			(SkiaView, string) tuple = default((SkiaView, string));
			while (_navigationStack.Count > 0)
			{
				tuple = _navigationStack.Pop();
			}
			SetCurrentContent(tuple.Item1);
			Title = tuple.Item2;
			Invalidate();
		}
	}

	private void SetCurrentContent(SkiaView? content)
	{
		if (_currentContent != null)
		{
			RemoveChild(_currentContent);
		}
		_currentContent = content;
		if (_currentContent != null)
		{
			AddChild(_currentContent);
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		if (_currentContent != null)
		{
			float num = (NavBarIsVisible ? NavBarHeight : 0f);
			float num2 = (TabBarIsVisible ? TabBarHeight : 0f);
			float width = ((SKSize)(ref availableSize)).Width;
			SKRect padding = base.Padding;
			float num3 = width - ((SKRect)(ref padding)).Left;
			padding = base.Padding;
			float num4 = num3 - ((SKRect)(ref padding)).Right;
			float num5 = ((SKSize)(ref availableSize)).Height - num - num2;
			padding = base.Padding;
			float num6 = num5 - ((SKRect)(ref padding)).Top;
			padding = base.Padding;
			SKSize availableSize2 = default(SKSize);
			((SKSize)(ref availableSize2))._002Ector(num4, num6 - ((SKRect)(ref padding)).Bottom);
			_currentContent.Measure(availableSize2);
		}
		return availableSize;
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		Console.WriteLine($"[SkiaShell] ArrangeOverride - bounds={bounds}");
		if (_currentContent != null)
		{
			float num = ((SKRect)(ref bounds)).Top + (NavBarIsVisible ? NavBarHeight : 0f) + ContentPadding;
			float num2 = ((SKRect)(ref bounds)).Bottom - (TabBarIsVisible ? TabBarHeight : 0f) - ContentPadding;
			SKRect val = default(SKRect);
			((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Left + ContentPadding, num, ((SKRect)(ref bounds)).Right - ContentPadding, num2);
			Console.WriteLine($"[SkiaShell] Arranging content with bounds={val}, padding={ContentPadding}");
			_currentContent.Arrange(val);
		}
		return bounds;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Expected O, but got Unknown
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		canvas.Save();
		canvas.ClipRect(bounds, (SKClipOperation)1, false);
		float num = ((SKRect)(ref bounds)).Top + (NavBarIsVisible ? NavBarHeight : 0f);
		float num2 = ((SKRect)(ref bounds)).Bottom - (TabBarIsVisible ? TabBarHeight : 0f);
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Left, num, ((SKRect)(ref bounds)).Right, num2);
		SKPaint val2 = new SKPaint
		{
			Color = ContentBackgroundColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(val, val2);
			_currentContent?.Draw(canvas);
			if (NavBarIsVisible)
			{
				DrawNavBar(canvas, bounds);
			}
			if (TabBarIsVisible)
			{
				DrawTabBar(canvas, bounds);
			}
			if (_flyoutAnimationProgress > 0f)
			{
				DrawFlyout(canvas, bounds);
			}
			canvas.Restore();
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void DrawNavBar(SKCanvas canvas, SKRect bounds)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Expected O, but got Unknown
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Expected O, but got Unknown
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Expected O, but got Unknown
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + NavBarHeight);
		SKPaint val2 = new SKPaint
		{
			Color = NavBarBackgroundColor,
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			canvas.DrawRect(val, val2);
			SKPaint val3 = new SKPaint
			{
				Color = NavBarTextColor,
				Style = (SKPaintStyle)1,
				StrokeWidth = 2f,
				StrokeCap = (SKStrokeCap)1,
				IsAntialias = true
			};
			try
			{
				float num = ((SKRect)(ref val)).Left + 16f;
				float midY = ((SKRect)(ref val)).MidY;
				if (CanGoBack)
				{
					SKPaint val4 = new SKPaint
					{
						Color = NavBarTextColor,
						Style = (SKPaintStyle)1,
						StrokeWidth = 2.5f,
						StrokeCap = (SKStrokeCap)1,
						StrokeJoin = (SKStrokeJoin)1,
						IsAntialias = true
					};
					try
					{
						float num2 = num + 6f;
						float num3 = 10f;
						canvas.DrawLine(num2 + num3, midY - num3, num2, midY, val4);
						canvas.DrawLine(num2, midY, num2 + num3, midY + num3, val4);
					}
					finally
					{
						((IDisposable)val4)?.Dispose();
					}
				}
				else if (FlyoutBehavior == ShellFlyoutBehavior.Flyout)
				{
					canvas.DrawLine(num, midY - 8f, num + 18f, midY - 8f, val3);
					canvas.DrawLine(num, midY, num + 18f, midY, val3);
					canvas.DrawLine(num, midY + 8f, num + 18f, midY + 8f, val3);
				}
				SKPaint val5 = new SKPaint
				{
					Color = NavBarTextColor,
					TextSize = 20f,
					IsAntialias = true,
					FakeBoldText = true
				};
				try
				{
					float num4 = ((CanGoBack || FlyoutBehavior == ShellFlyoutBehavior.Flyout) ? (((SKRect)(ref val)).Left + 56f) : (((SKRect)(ref val)).Left + 16f));
					float num5 = ((SKRect)(ref val)).MidY + 6f;
					canvas.DrawText(Title, num4, num5, val5);
				}
				finally
				{
					((IDisposable)val5)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void DrawTabBar(SKCanvas canvas, SKRect bounds)
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Expected O, but got Unknown
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Expected O, but got Unknown
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		if (_selectedSectionIndex < 0 || _selectedSectionIndex >= _sections.Count)
		{
			return;
		}
		ShellSection shellSection = _sections[_selectedSectionIndex];
		if (shellSection.Items.Count <= 1)
		{
			return;
		}
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Bottom - TabBarHeight, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom);
		SKPaint val2 = new SKPaint
		{
			Color = SKColors.White,
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			canvas.DrawRect(val, val2);
			SKPaint val3 = new SKPaint
			{
				Color = new SKColor((byte)224, (byte)224, (byte)224),
				Style = (SKPaintStyle)1,
				StrokeWidth = 1f
			};
			try
			{
				canvas.DrawLine(((SKRect)(ref val)).Left, ((SKRect)(ref val)).Top, ((SKRect)(ref val)).Right, ((SKRect)(ref val)).Top, val3);
				float num = ((SKRect)(ref val)).Width / (float)shellSection.Items.Count;
				SKPaint val4 = new SKPaint
				{
					TextSize = 12f,
					IsAntialias = true
				};
				try
				{
					for (int i = 0; i < shellSection.Items.Count; i++)
					{
						ShellContent shellContent = shellSection.Items[i];
						bool flag = i == _selectedItemIndex;
						val4.Color = (SKColor)(flag ? NavBarBackgroundColor : new SKColor((byte)117, (byte)117, (byte)117));
						SKRect val5 = default(SKRect);
						val4.MeasureText(shellContent.Title, ref val5);
						float num2 = ((SKRect)(ref val)).Left + (float)i * num + num / 2f - ((SKRect)(ref val5)).MidX;
						float num3 = ((SKRect)(ref val)).MidY - ((SKRect)(ref val5)).MidY;
						canvas.DrawText(shellContent.Title, num2, num3, val4);
					}
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void DrawFlyout(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected O, but got Unknown
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Expected O, but got Unknown
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0342: Unknown result type (might be due to invalid IL or missing references)
		//IL_0344: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_034f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0359: Unknown result type (might be due to invalid IL or missing references)
		//IL_0360: Unknown result type (might be due to invalid IL or missing references)
		//IL_036d: Expected O, but got Unknown
		//IL_0432: Unknown result type (might be due to invalid IL or missing references)
		//IL_0437: Unknown result type (might be due to invalid IL or missing references)
		//IL_0439: Unknown result type (might be due to invalid IL or missing references)
		//IL_043e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0444: Unknown result type (might be due to invalid IL or missing references)
		//IL_044e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0455: Unknown result type (might be due to invalid IL or missing references)
		//IL_045e: Expected O, but got Unknown
		//IL_0393: Unknown result type (might be due to invalid IL or missing references)
		//IL_0398: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Expected O, but got Unknown
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Expected O, but got Unknown
		//IL_049d: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b3: Expected O, but got Unknown
		//IL_03c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)(100f * _flyoutAnimationProgress)),
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(bounds, val);
			float num = ((SKRect)(ref bounds)).Left - FlyoutWidth + FlyoutWidth * _flyoutAnimationProgress;
			SKRect val2 = new SKRect(num, ((SKRect)(ref bounds)).Top, num + FlyoutWidth, ((SKRect)(ref bounds)).Bottom);
			SKPaint val3 = new SKPaint
			{
				Color = FlyoutBackgroundColor,
				Style = (SKPaintStyle)0,
				IsAntialias = true
			};
			try
			{
				canvas.DrawRect(val2, val3);
				float num2 = ((FlyoutHeaderView != null) ? FlyoutHeaderHeight : 0f);
				float num3 = ((!string.IsNullOrEmpty(FlyoutFooterText)) ? FlyoutFooterHeight : 0f);
				float num4 = 48f;
				float num5 = (float)_sections.Count * num4;
				float num6 = ((SKRect)(ref val2)).Height - num2 - num3;
				float num7 = Math.Max(0f, num5 - num6);
				_flyoutScrollOffset = Math.Max(0f, Math.Min(_flyoutScrollOffset, num7));
				if (FlyoutHeaderView != null)
				{
					canvas.Save();
					canvas.ClipRect(new SKRect(((SKRect)(ref val2)).Left, ((SKRect)(ref val2)).Top, ((SKRect)(ref val2)).Right, ((SKRect)(ref val2)).Top + num2), (SKClipOperation)1, false);
					canvas.Translate(((SKRect)(ref val2)).Left, ((SKRect)(ref val2)).Top);
					SKRect bounds2 = default(SKRect);
					((SKRect)(ref bounds2))._002Ector(0f, 0f, FlyoutWidth, num2);
					FlyoutHeaderView.Measure(new SKSize(FlyoutWidth, num2));
					FlyoutHeaderView.Arrange(bounds2);
					FlyoutHeaderView.Draw(canvas);
					canvas.Restore();
				}
				float num8 = ((SKRect)(ref val2)).Top + num2;
				float num9 = ((SKRect)(ref val2)).Bottom - num3;
				canvas.Save();
				canvas.ClipRect(new SKRect(((SKRect)(ref val2)).Left, num8, ((SKRect)(ref val2)).Right, num9), (SKClipOperation)1, false);
				SKPaint val4 = new SKPaint
				{
					TextSize = 14f,
					IsAntialias = true
				};
				try
				{
					float num10 = num8 - _flyoutScrollOffset;
					for (int i = 0; i < _sections.Count; i++)
					{
						if (num10 + num4 < num8)
						{
							num10 += num4;
							continue;
						}
						if (num10 > num9)
						{
							break;
						}
						ShellSection shellSection = _sections[i];
						bool flag = i == _selectedSectionIndex;
						if (flag)
						{
							SKPaint val5 = new SKPaint
							{
								Color = new SKColor((byte)33, (byte)150, (byte)243, (byte)30),
								Style = (SKPaintStyle)0
							};
							try
							{
								SKRect val6 = new SKRect(((SKRect)(ref val2)).Left, num10, ((SKRect)(ref val2)).Right, num10 + num4);
								canvas.DrawRect(val6, val5);
							}
							finally
							{
								((IDisposable)val5)?.Dispose();
							}
						}
						val4.Color = (flag ? NavBarBackgroundColor : FlyoutTextColor);
						canvas.DrawText(shellSection.Title, ((SKRect)(ref val2)).Left + 16f, num10 + 30f, val4);
						num10 += num4;
					}
					canvas.Restore();
					SKColor flyoutTextColor;
					if (!string.IsNullOrEmpty(FlyoutFooterText))
					{
						float num11 = ((SKRect)(ref val2)).Bottom - num3;
						SKPaint val7 = new SKPaint();
						flyoutTextColor = FlyoutTextColor;
						val7.Color = ((SKColor)(ref flyoutTextColor)).WithAlpha((byte)50);
						val7.Style = (SKPaintStyle)1;
						val7.StrokeWidth = 1f;
						SKPaint val8 = val7;
						try
						{
							canvas.DrawLine(((SKRect)(ref val2)).Left + 16f, num11, ((SKRect)(ref val2)).Right - 16f, num11, val8);
							SKPaint val9 = new SKPaint
							{
								TextSize = 12f
							};
							flyoutTextColor = FlyoutTextColor;
							val9.Color = ((SKColor)(ref flyoutTextColor)).WithAlpha((byte)150);
							val9.IsAntialias = true;
							SKPaint val10 = val9;
							try
							{
								SKRect val11 = default(SKRect);
								val10.MeasureText(FlyoutFooterText, ref val11);
								canvas.DrawText(FlyoutFooterText, ((SKRect)(ref val2)).Left + 16f, num11 + (num3 + ((SKRect)(ref val11)).Height) / 2f, val10);
							}
							finally
							{
								((IDisposable)val10)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)val8)?.Dispose();
						}
					}
					if (num7 > 0f)
					{
						SKPaint val12 = new SKPaint();
						flyoutTextColor = FlyoutTextColor;
						val12.Color = ((SKColor)(ref flyoutTextColor)).WithAlpha((byte)80);
						val12.Style = (SKPaintStyle)0;
						val12.IsAntialias = true;
						SKPaint val13 = val12;
						try
						{
							float num12 = ((SKRect)(ref val2)).Right - 6f;
							float num13 = num6 * (num6 / num5);
							float num14 = num8 + _flyoutScrollOffset / num7 * (num6 - num13);
							canvas.DrawRoundRect(new SKRoundRect(new SKRect(num12, num14, num12 + 4f, num14 + num13), 2f), val13);
							return;
						}
						finally
						{
							((IDisposable)val13)?.Dispose();
						}
					}
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsVisible)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(x, y))
			{
				if (_flyoutAnimationProgress > 0f)
				{
					bounds = base.Bounds;
					float num = ((SKRect)(ref bounds)).Left - FlyoutWidth + FlyoutWidth * _flyoutAnimationProgress;
					bounds = base.Bounds;
					float top = ((SKRect)(ref bounds)).Top;
					float num2 = num + FlyoutWidth;
					bounds = base.Bounds;
					SKRect val = default(SKRect);
					((SKRect)(ref val))._002Ector(num, top, num2, ((SKRect)(ref bounds)).Bottom);
					if (((SKRect)(ref val)).Contains(x, y))
					{
						return this;
					}
					if (FlyoutIsPresented)
					{
						return this;
					}
				}
				if (NavBarIsVisible)
				{
					bounds = base.Bounds;
					if (y < ((SKRect)(ref bounds)).Top + NavBarHeight)
					{
						return this;
					}
				}
				if (TabBarIsVisible)
				{
					bounds = base.Bounds;
					if (y > ((SKRect)(ref bounds)).Bottom - TabBarHeight)
					{
						return this;
					}
				}
				if (_currentContent != null)
				{
					SkiaView skiaView = _currentContent.HitTest(x, y);
					if (skiaView != null)
					{
						return skiaView;
					}
				}
				return this;
			}
		}
		return null;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		SKRect bounds;
		if (_flyoutAnimationProgress > 0f)
		{
			bounds = base.Bounds;
			float num = ((SKRect)(ref bounds)).Left - FlyoutWidth + FlyoutWidth * _flyoutAnimationProgress;
			bounds = base.Bounds;
			float top = ((SKRect)(ref bounds)).Top;
			float num2 = num + FlyoutWidth;
			bounds = base.Bounds;
			SKRect val = default(SKRect);
			((SKRect)(ref val))._002Ector(num, top, num2, ((SKRect)(ref bounds)).Bottom);
			if (((SKRect)(ref val)).Contains(e.X, e.Y))
			{
				float num3 = ((FlyoutHeaderView != null) ? FlyoutHeaderHeight : 0f);
				if (e.Y < ((SKRect)(ref val)).Top + num3)
				{
					e.Handled = true;
					return;
				}
				float num4 = ((!string.IsNullOrEmpty(FlyoutFooterText)) ? FlyoutFooterHeight : 0f);
				float num5 = ((SKRect)(ref val)).Top + num3 - _flyoutScrollOffset;
				float num6 = 48f;
				for (int i = 0; i < _sections.Count; i++)
				{
					if (e.Y >= num5 && e.Y < num5 + num6 && e.Y < ((SKRect)(ref val)).Bottom - num4)
					{
						NavigateToSection(i);
						FlyoutIsPresented = false;
						e.Handled = true;
						return;
					}
					num5 += num6;
				}
			}
			else if (FlyoutIsPresented)
			{
				FlyoutIsPresented = false;
				e.Handled = true;
				return;
			}
		}
		if (NavBarIsVisible)
		{
			float y = e.Y;
			bounds = base.Bounds;
			if (y < ((SKRect)(ref bounds)).Top + NavBarHeight && e.X < 56f)
			{
				if (CanGoBack)
				{
					PopAsync();
					e.Handled = true;
					return;
				}
				if (FlyoutBehavior == ShellFlyoutBehavior.Flyout)
				{
					FlyoutIsPresented = !FlyoutIsPresented;
					e.Handled = true;
					return;
				}
			}
		}
		if (TabBarIsVisible)
		{
			float y2 = e.Y;
			bounds = base.Bounds;
			if (y2 > ((SKRect)(ref bounds)).Bottom - TabBarHeight && _selectedSectionIndex >= 0 && _selectedSectionIndex < _sections.Count)
			{
				ShellSection shellSection = _sections[_selectedSectionIndex];
				bounds = base.Bounds;
				float num7 = ((SKRect)(ref bounds)).Width / (float)shellSection.Items.Count;
				float x = e.X;
				bounds = base.Bounds;
				int value = (int)((x - ((SKRect)(ref bounds)).Left) / num7);
				value = Math.Clamp(value, 0, shellSection.Items.Count - 1);
				if (value != _selectedItemIndex)
				{
					NavigateToSection(_selectedSectionIndex, value);
				}
				e.Handled = true;
				return;
			}
		}
		base.OnPointerPressed(e);
	}

	public override void OnScroll(ScrollEventArgs e)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		if (FlyoutIsPresented && _flyoutAnimationProgress > 0f)
		{
			SKRect bounds = base.Bounds;
			float num = ((SKRect)(ref bounds)).Left - FlyoutWidth + FlyoutWidth * _flyoutAnimationProgress;
			bounds = base.Bounds;
			float top = ((SKRect)(ref bounds)).Top;
			float num2 = num + FlyoutWidth;
			bounds = base.Bounds;
			SKRect val = default(SKRect);
			((SKRect)(ref val))._002Ector(num, top, num2, ((SKRect)(ref bounds)).Bottom);
			if (((SKRect)(ref val)).Contains(e.X, e.Y))
			{
				float num3 = ((FlyoutHeaderView != null) ? FlyoutHeaderHeight : 0f);
				float num4 = ((!string.IsNullOrEmpty(FlyoutFooterText)) ? FlyoutFooterHeight : 0f);
				float num5 = 48f;
				float num6 = (float)_sections.Count * num5;
				float num7 = ((SKRect)(ref val)).Height - num3 - num4;
				float val2 = Math.Max(0f, num6 - num7);
				_flyoutScrollOffset -= e.DeltaY * 30f;
				_flyoutScrollOffset = Math.Max(0f, Math.Min(_flyoutScrollOffset, val2));
				Invalidate();
				e.Handled = true;
				return;
			}
		}
		base.OnScroll(e);
	}
}
