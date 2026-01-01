using System;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaContentPresenter : SkiaView
{
	public static readonly BindableProperty ContentProperty = BindableProperty.Create("Content", typeof(SkiaView), typeof(SkiaContentPresenter), (object)null, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaContentPresenter)(object)b).OnContentChanged((SkiaView)o, (SkiaView)n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HorizontalContentAlignmentProperty = BindableProperty.Create("HorizontalContentAlignment", typeof(LayoutAlignment), typeof(SkiaContentPresenter), (object)LayoutAlignment.Fill, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaContentPresenter)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty VerticalContentAlignmentProperty = BindableProperty.Create("VerticalContentAlignment", typeof(LayoutAlignment), typeof(SkiaContentPresenter), (object)LayoutAlignment.Fill, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaContentPresenter)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PaddingProperty = BindableProperty.Create("Padding", typeof(SKRect), typeof(SkiaContentPresenter), (object)SKRect.Empty, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaContentPresenter)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public SkiaView? Content
	{
		get
		{
			return (SkiaView)((BindableObject)this).GetValue(ContentProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ContentProperty, (object)value);
		}
	}

	public LayoutAlignment HorizontalContentAlignment
	{
		get
		{
			return (LayoutAlignment)((BindableObject)this).GetValue(HorizontalContentAlignmentProperty);
		}
		set
		{
			((BindableObject)this).SetValue(HorizontalContentAlignmentProperty, (object)value);
		}
	}

	public LayoutAlignment VerticalContentAlignment
	{
		get
		{
			return (LayoutAlignment)((BindableObject)this).GetValue(VerticalContentAlignmentProperty);
		}
		set
		{
			((BindableObject)this).SetValue(VerticalContentAlignmentProperty, (object)value);
		}
	}

	public SKRect Padding
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKRect)((BindableObject)this).GetValue(PaddingProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(PaddingProperty, (object)value);
		}
	}

	private void OnContentChanged(SkiaView? oldContent, SkiaView? newContent)
	{
		if (oldContent != null)
		{
			oldContent.Parent = null;
		}
		if (newContent != null)
		{
			newContent.Parent = this;
			if (((BindableObject)this).BindingContext != null)
			{
				BindableObject.SetInheritedBindingContext((BindableObject)(object)newContent, ((BindableObject)this).BindingContext);
			}
		}
		InvalidateMeasure();
	}

	protected override void OnBindingContextChanged()
	{
		base.OnBindingContextChanged();
		if (Content != null)
		{
			BindableObject.SetInheritedBindingContext((BindableObject)(object)Content, ((BindableObject)this).BindingContext);
		}
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (base.BackgroundColor != SKColors.Transparent)
		{
			SKPaint val = new SKPaint
			{
				Color = base.BackgroundColor,
				Style = (SKPaintStyle)0
			};
			try
			{
				canvas.DrawRect(bounds, val);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		Content?.Draw(canvas);
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		SKRect padding = Padding;
		if (Content == null)
		{
			return new SKSize(((SKRect)(ref padding)).Left + ((SKRect)(ref padding)).Right, ((SKRect)(ref padding)).Top + ((SKRect)(ref padding)).Bottom);
		}
		float num = ((HorizontalContentAlignment == LayoutAlignment.Fill) ? Math.Max(0f, ((SKSize)(ref availableSize)).Width - ((SKRect)(ref padding)).Left - ((SKRect)(ref padding)).Right) : float.PositiveInfinity);
		float num2 = ((VerticalContentAlignment == LayoutAlignment.Fill) ? Math.Max(0f, ((SKSize)(ref availableSize)).Height - ((SKRect)(ref padding)).Top - ((SKRect)(ref padding)).Bottom) : float.PositiveInfinity);
		SKSize val = Content.Measure(new SKSize(num, num2));
		return new SKSize(((SKSize)(ref val)).Width + ((SKRect)(ref padding)).Left + ((SKRect)(ref padding)).Right, ((SKSize)(ref val)).Height + ((SKRect)(ref padding)).Top + ((SKRect)(ref padding)).Bottom);
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		if (Content != null)
		{
			SKRect padding = Padding;
			SKRect availableBounds = new SKRect(((SKRect)(ref bounds)).Left + ((SKRect)(ref padding)).Left, ((SKRect)(ref bounds)).Top + ((SKRect)(ref padding)).Top, ((SKRect)(ref bounds)).Right - ((SKRect)(ref padding)).Right, ((SKRect)(ref bounds)).Bottom - ((SKRect)(ref padding)).Bottom);
			SKSize desiredSize = Content.DesiredSize;
			SKRect bounds2 = ApplyAlignment(availableBounds, desiredSize, HorizontalContentAlignment, VerticalContentAlignment);
			Content.Arrange(bounds2);
		}
		return bounds;
	}

	private static SKRect ApplyAlignment(SKRect availableBounds, SKSize contentSize, LayoutAlignment horizontal, LayoutAlignment vertical)
	{
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		float num = ((SKRect)(ref availableBounds)).Left;
		float num2 = ((SKRect)(ref availableBounds)).Top;
		float num3 = ((horizontal == LayoutAlignment.Fill) ? ((SKRect)(ref availableBounds)).Width : ((SKSize)(ref contentSize)).Width);
		float num4 = ((vertical == LayoutAlignment.Fill) ? ((SKRect)(ref availableBounds)).Height : ((SKSize)(ref contentSize)).Height);
		switch (horizontal)
		{
		case LayoutAlignment.Center:
			num = ((SKRect)(ref availableBounds)).Left + (((SKRect)(ref availableBounds)).Width - num3) / 2f;
			break;
		case LayoutAlignment.End:
			num = ((SKRect)(ref availableBounds)).Right - num3;
			break;
		}
		switch (vertical)
		{
		case LayoutAlignment.Center:
			num2 = ((SKRect)(ref availableBounds)).Top + (((SKRect)(ref availableBounds)).Height - num4) / 2f;
			break;
		case LayoutAlignment.End:
			num2 = ((SKRect)(ref availableBounds)).Bottom - num4;
			break;
		}
		return new SKRect(num, num2, num + num3, num2 + num4);
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
				if (Content != null)
				{
					SkiaView skiaView = Content.HitTest(x, y);
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
		Content?.OnPointerPressed(e);
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		Content?.OnPointerMoved(e);
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		Content?.OnPointerReleased(e);
	}
}
