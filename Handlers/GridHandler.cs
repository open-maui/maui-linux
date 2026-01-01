using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class GridHandler : LayoutHandler
{
	public new static IPropertyMapper<IGridLayout, GridHandler> Mapper = (IPropertyMapper<IGridLayout, GridHandler>)(object)new PropertyMapper<IGridLayout, GridHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)LayoutHandler.Mapper })
	{
		["RowSpacing"] = MapRowSpacing,
		["ColumnSpacing"] = MapColumnSpacing,
		["RowDefinitions"] = MapRowDefinitions,
		["ColumnDefinitions"] = MapColumnDefinitions
	};

	public GridHandler()
		: base((IPropertyMapper?)(object)Mapper)
	{
	}

	protected override SkiaLayoutView CreatePlatformView()
	{
		return new SkiaGrid();
	}

	protected override void ConnectHandler(SkiaLayoutView platformView)
	{
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			ILayout virtualView = ((ViewHandler<ILayout, SkiaLayoutView>)(object)this).VirtualView;
			IGridLayout val = (IGridLayout)(object)((virtualView is IGridLayout) ? virtualView : null);
			if (val == null || ((ElementHandler)this).MauiContext == null || !(platformView is SkiaGrid skiaGrid))
			{
				return;
			}
			Console.WriteLine($"[GridHandler] ConnectHandler: {((ICollection<IView>)val).Count} children, {val.RowDefinitions.Count} rows, {val.ColumnDefinitions.Count} cols");
			ILayout virtualView2 = ((ViewHandler<ILayout, SkiaLayoutView>)(object)this).VirtualView;
			VisualElement val2 = (VisualElement)(object)((virtualView2 is VisualElement) ? virtualView2 : null);
			if (val2 != null && val2.BackgroundColor != null)
			{
				platformView.BackgroundColor = val2.BackgroundColor.ToSKColor();
			}
			IPadding virtualView3 = (IPadding)(object)((ViewHandler<ILayout, SkiaLayoutView>)(object)this).VirtualView;
			if (virtualView3 != null)
			{
				Thickness padding = virtualView3.Padding;
				platformView.Padding = new SKRect((float)((Thickness)(ref padding)).Left, (float)((Thickness)(ref padding)).Top, (float)((Thickness)(ref padding)).Right, (float)((Thickness)(ref padding)).Bottom);
				Console.WriteLine($"[GridHandler] Applied Padding: L={((Thickness)(ref padding)).Left}, T={((Thickness)(ref padding)).Top}, R={((Thickness)(ref padding)).Right}, B={((Thickness)(ref padding)).Bottom}");
			}
			MapRowDefinitions(this, val);
			MapColumnDefinitions(this, val);
			for (int i = 0; i < ((ICollection<IView>)val).Count; i++)
			{
				IView val3 = ((IList<IView>)val)[i];
				if (val3 != null)
				{
					Console.WriteLine($"[GridHandler] Processing child {i}: {((object)val3).GetType().Name}");
					if (val3.Handler == null)
					{
						val3.Handler = val3.ToViewHandler(((ElementHandler)this).MauiContext);
					}
					int num = 0;
					int num2 = 0;
					int rowSpan = 1;
					int columnSpan = 1;
					View val4 = (View)(object)((val3 is View) ? val3 : null);
					if (val4 != null)
					{
						num = Grid.GetRow((BindableObject)(object)val4);
						num2 = Grid.GetColumn((BindableObject)(object)val4);
						rowSpan = Grid.GetRowSpan((BindableObject)(object)val4);
						columnSpan = Grid.GetColumnSpan((BindableObject)(object)val4);
					}
					Console.WriteLine($"[GridHandler] Child {i} at row={num}, col={num2}, handler={((object)val3.Handler)?.GetType().Name}");
					IViewHandler handler = val3.Handler;
					if (((handler != null) ? ((IElementHandler)handler).PlatformView : null) is SkiaView child)
					{
						skiaGrid.AddChild(child, num, num2, rowSpan, columnSpan);
						Console.WriteLine($"[GridHandler] Added child {i} to grid");
					}
				}
			}
			Console.WriteLine("[GridHandler] ConnectHandler complete");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[GridHandler] EXCEPTION in ConnectHandler: " + ex.GetType().Name + ": " + ex.Message);
			Console.WriteLine("[GridHandler] Stack trace: " + ex.StackTrace);
			throw;
		}
	}

	public static void MapRowSpacing(GridHandler handler, IGridLayout layout)
	{
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView is SkiaGrid skiaGrid)
		{
			skiaGrid.RowSpacing = (float)layout.RowSpacing;
		}
	}

	public static void MapColumnSpacing(GridHandler handler, IGridLayout layout)
	{
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView is SkiaGrid skiaGrid)
		{
			skiaGrid.ColumnSpacing = (float)layout.ColumnSpacing;
		}
	}

	public static void MapRowDefinitions(GridHandler handler, IGridLayout layout)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (!(((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView is SkiaGrid skiaGrid))
		{
			return;
		}
		skiaGrid.RowDefinitions.Clear();
		foreach (IGridRowDefinition rowDefinition in layout.RowDefinitions)
		{
			GridLength height = rowDefinition.Height;
			if (((GridLength)(ref height)).IsAbsolute)
			{
				skiaGrid.RowDefinitions.Add(new GridLength((float)((GridLength)(ref height)).Value));
			}
			else if (((GridLength)(ref height)).IsAuto)
			{
				skiaGrid.RowDefinitions.Add(GridLength.Auto);
			}
			else
			{
				skiaGrid.RowDefinitions.Add(new GridLength((float)((GridLength)(ref height)).Value, GridUnitType.Star));
			}
		}
	}

	public static void MapColumnDefinitions(GridHandler handler, IGridLayout layout)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (!(((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView is SkiaGrid skiaGrid))
		{
			return;
		}
		skiaGrid.ColumnDefinitions.Clear();
		foreach (IGridColumnDefinition columnDefinition in layout.ColumnDefinitions)
		{
			GridLength width = columnDefinition.Width;
			if (((GridLength)(ref width)).IsAbsolute)
			{
				skiaGrid.ColumnDefinitions.Add(new GridLength((float)((GridLength)(ref width)).Value));
			}
			else if (((GridLength)(ref width)).IsAuto)
			{
				skiaGrid.ColumnDefinitions.Add(GridLength.Auto);
			}
			else
			{
				skiaGrid.ColumnDefinitions.Add(new GridLength((float)((GridLength)(ref width)).Value, GridUnitType.Star));
			}
		}
	}
}
