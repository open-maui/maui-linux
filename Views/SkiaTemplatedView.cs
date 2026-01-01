using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public abstract class SkiaTemplatedView : SkiaView
{
	private SkiaView? _templateRoot;

	private bool _templateApplied;

	public static readonly BindableProperty ControlTemplateProperty = BindableProperty.Create("ControlTemplate", typeof(ControlTemplate), typeof(SkiaTemplatedView), (object)null, (BindingMode)2, (ValidateValueDelegate)null, new BindingPropertyChangedDelegate(OnControlTemplateChanged), (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public ControlTemplate? ControlTemplate
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Expected O, but got Unknown
			return (ControlTemplate)((BindableObject)this).GetValue(ControlTemplateProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ControlTemplateProperty, (object)value);
		}
	}

	protected SkiaView? TemplateRoot => _templateRoot;

	protected bool IsTemplateApplied => _templateApplied;

	private static void OnControlTemplateChanged(BindableObject bindable, object oldValue, object newValue)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_001c: Expected O, but got Unknown
		if (bindable is SkiaTemplatedView skiaTemplatedView)
		{
			skiaTemplatedView.OnControlTemplateChanged((ControlTemplate)oldValue, (ControlTemplate)newValue);
		}
	}

	protected virtual void OnControlTemplateChanged(ControlTemplate? oldTemplate, ControlTemplate? newTemplate)
	{
		_templateApplied = false;
		_templateRoot = null;
		if (newTemplate != null)
		{
			ApplyTemplate();
		}
		InvalidateMeasure();
	}

	protected virtual void ApplyTemplate()
	{
		if (ControlTemplate == null || _templateApplied)
		{
			return;
		}
		try
		{
			object obj = ((ElementTemplate)ControlTemplate).CreateContent();
			Element val = (Element)((obj is Element) ? obj : null);
			if (val != null)
			{
				_templateRoot = ConvertElementToSkiaView(val);
			}
			else if (obj is SkiaView templateRoot)
			{
				_templateRoot = templateRoot;
			}
			if (_templateRoot != null)
			{
				_templateRoot.Parent = this;
				OnTemplateApplied();
			}
			_templateApplied = true;
		}
		catch (Exception)
		{
		}
	}

	protected virtual void OnTemplateApplied()
	{
		SkiaContentPresenter skiaContentPresenter = FindTemplateChild<SkiaContentPresenter>("PART_ContentPresenter");
		if (skiaContentPresenter != null)
		{
			OnContentPresenterFound(skiaContentPresenter);
		}
	}

	protected virtual void OnContentPresenterFound(SkiaContentPresenter presenter)
	{
	}

	protected T? FindTemplateChild<T>(string name) where T : SkiaView
	{
		if (_templateRoot == null)
		{
			return null;
		}
		return FindChild<T>(_templateRoot, name);
	}

	private static T? FindChild<T>(SkiaView root, string name) where T : SkiaView
	{
		if (root is T result && root.Name == name)
		{
			return result;
		}
		if (root is SkiaLayoutView skiaLayoutView)
		{
			foreach (SkiaView child in skiaLayoutView.Children)
			{
				T val = FindChild<T>(child, name);
				if (val != null)
				{
					return val;
				}
			}
		}
		else if (root is SkiaContentPresenter { Content: not null } skiaContentPresenter)
		{
			return FindChild<T>(skiaContentPresenter.Content, name);
		}
		return null;
	}

	protected virtual SkiaView? ConvertElementToSkiaView(Element element)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		StackLayout val = (StackLayout)(object)((element is StackLayout) ? element : null);
		if (val == null)
		{
			Grid val2 = (Grid)(object)((element is Grid) ? element : null);
			if (val2 == null)
			{
				Border val3 = (Border)(object)((element is Border) ? element : null);
				if (val3 == null)
				{
					Label val4 = (Label)(object)((element is Label) ? element : null);
					if (val4 == null)
					{
						if (element is ContentPresenter)
						{
							return new SkiaContentPresenter();
						}
						return new SkiaLabel
						{
							Text = "[" + ((object)element).GetType().Name + "]",
							TextColor = SKColors.Gray
						};
					}
					return CreateSkiaLabel(val4);
				}
				return CreateSkiaBorder(val3);
			}
			return CreateSkiaGrid(val2);
		}
		return CreateSkiaStackLayout(val);
	}

	private SkiaStackLayout CreateSkiaStackLayout(StackLayout sl)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		SkiaStackLayout skiaStackLayout = new SkiaStackLayout
		{
			Orientation = (((int)sl.Orientation != 0) ? StackOrientation.Horizontal : StackOrientation.Vertical),
			Spacing = (float)((StackBase)sl).Spacing
		};
		foreach (IView child in ((Layout)sl).Children)
		{
			Element val = (Element)(object)((child is Element) ? child : null);
			if (val != null)
			{
				SkiaView skiaView = ConvertElementToSkiaView(val);
				if (skiaView != null)
				{
					skiaStackLayout.AddChild(skiaView);
				}
			}
		}
		return skiaStackLayout;
	}

	private SkiaGrid CreateSkiaGrid(Grid grid)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Expected O, but got Unknown
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Expected O, but got Unknown
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Expected O, but got Unknown
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Expected O, but got Unknown
		SkiaGrid skiaGrid = new SkiaGrid();
		GridLength val;
		foreach (RowDefinition item3 in (DefinitionCollection<RowDefinition>)(object)grid.RowDefinitions)
		{
			val = item3.Height;
			GridLength gridLength;
			if (!((GridLength)(ref val)).IsAuto)
			{
				val = item3.Height;
				if (!((GridLength)(ref val)).IsStar)
				{
					val = item3.Height;
					gridLength = new GridLength((float)((GridLength)(ref val)).Value);
				}
				else
				{
					val = item3.Height;
					gridLength = new GridLength((float)((GridLength)(ref val)).Value, GridUnitType.Star);
				}
			}
			else
			{
				gridLength = GridLength.Auto;
			}
			GridLength item = gridLength;
			skiaGrid.RowDefinitions.Add(item);
		}
		foreach (ColumnDefinition item4 in (DefinitionCollection<ColumnDefinition>)(object)grid.ColumnDefinitions)
		{
			val = item4.Width;
			GridLength gridLength2;
			if (!((GridLength)(ref val)).IsAuto)
			{
				val = item4.Width;
				if (!((GridLength)(ref val)).IsStar)
				{
					val = item4.Width;
					gridLength2 = new GridLength((float)((GridLength)(ref val)).Value);
				}
				else
				{
					val = item4.Width;
					gridLength2 = new GridLength((float)((GridLength)(ref val)).Value, GridUnitType.Star);
				}
			}
			else
			{
				gridLength2 = GridLength.Auto;
			}
			GridLength item2 = gridLength2;
			skiaGrid.ColumnDefinitions.Add(item2);
		}
		foreach (IView child in ((Layout)grid).Children)
		{
			Element val2 = (Element)(object)((child is Element) ? child : null);
			if (val2 != null)
			{
				SkiaView skiaView = ConvertElementToSkiaView(val2);
				if (skiaView != null)
				{
					int row = Grid.GetRow((BindableObject)child);
					int column = Grid.GetColumn((BindableObject)child);
					int rowSpan = Grid.GetRowSpan((BindableObject)child);
					int columnSpan = Grid.GetColumnSpan((BindableObject)child);
					skiaGrid.AddChild(skiaView, row, column, rowSpan, columnSpan);
				}
			}
		}
		return skiaGrid;
	}

	private SkiaBorder CreateSkiaBorder(Border border)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		float cornerRadius = 0f;
		IShape strokeShape = border.StrokeShape;
		RoundRectangle val = (RoundRectangle)(object)((strokeShape is RoundRectangle) ? strokeShape : null);
		if (val != null)
		{
			CornerRadius cornerRadius2 = val.CornerRadius;
			cornerRadius = (float)((CornerRadius)(ref cornerRadius2)).TopLeft;
		}
		SkiaBorder skiaBorder = new SkiaBorder
		{
			CornerRadius = cornerRadius,
			StrokeThickness = (float)border.StrokeThickness
		};
		Brush stroke = border.Stroke;
		SolidColorBrush val2 = (SolidColorBrush)(object)((stroke is SolidColorBrush) ? stroke : null);
		if (val2 != null)
		{
			skiaBorder.Stroke = val2.Color.ToSKColor();
		}
		Brush background = ((VisualElement)border).Background;
		SolidColorBrush val3 = (SolidColorBrush)(object)((background is SolidColorBrush) ? background : null);
		if (val3 != null)
		{
			skiaBorder.BackgroundColor = val3.Color.ToSKColor();
		}
		Element content = (Element)(object)border.Content;
		if (content != null)
		{
			SkiaView skiaView = ConvertElementToSkiaView(content);
			if (skiaView != null)
			{
				skiaBorder.AddChild(skiaView);
			}
		}
		return skiaBorder;
	}

	private SkiaLabel CreateSkiaLabel(Label label)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		SkiaLabel skiaLabel = new SkiaLabel
		{
			Text = (label.Text ?? ""),
			FontSize = (float)label.FontSize
		};
		if (label.TextColor != null)
		{
			skiaLabel.TextColor = label.TextColor.ToSKColor();
		}
		return skiaLabel;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (_templateRoot != null && _templateApplied)
		{
			_templateRoot.Draw(canvas);
		}
		else
		{
			DrawDefaultAppearance(canvas, bounds);
		}
	}

	protected abstract void DrawDefaultAppearance(SKCanvas canvas, SKRect bounds);

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		if (_templateRoot != null && _templateApplied)
		{
			return _templateRoot.Measure(availableSize);
		}
		return MeasureDefaultAppearance(availableSize);
	}

	protected virtual SKSize MeasureDefaultAppearance(SKSize availableSize)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		return new SKSize(100f, 40f);
	}

	public new void Arrange(SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		base.Arrange(bounds);
		if (_templateRoot != null && _templateApplied)
		{
			_templateRoot.Arrange(bounds);
		}
	}

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsVisible)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(x, y))
			{
				if (_templateRoot != null && _templateApplied)
				{
					SkiaView skiaView = _templateRoot.HitTest(x, y);
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
}
