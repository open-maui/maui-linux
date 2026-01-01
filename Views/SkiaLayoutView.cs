using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public abstract class SkiaLayoutView : SkiaView
{
	public static readonly BindableProperty SpacingProperty = BindableProperty.Create("Spacing", typeof(float), typeof(SkiaLayoutView), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLayoutView)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PaddingProperty = BindableProperty.Create("Padding", typeof(SKRect), typeof(SkiaLayoutView), (object)SKRect.Empty, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLayoutView)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ClipToBoundsProperty = BindableProperty.Create("ClipToBounds", typeof(bool), typeof(SkiaLayoutView), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLayoutView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private readonly List<SkiaView> _children = new List<SkiaView>();

	public new IReadOnlyList<SkiaView> Children => _children;

	public float Spacing
	{
		get
		{
			return (float)((BindableObject)this).GetValue(SpacingProperty);
		}
		set
		{
			((BindableObject)this).SetValue(SpacingProperty, (object)value);
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

	public bool ClipToBounds
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(ClipToBoundsProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ClipToBoundsProperty, (object)value);
		}
	}

	protected override void OnBindingContextChanged()
	{
		base.OnBindingContextChanged();
		foreach (SkiaView child in _children)
		{
			BindableObject.SetInheritedBindingContext((BindableObject)(object)child, ((BindableObject)this).BindingContext);
		}
	}

	public new virtual void AddChild(SkiaView child)
	{
		if (child.Parent != null)
		{
			throw new InvalidOperationException("View already has a parent");
		}
		_children.Add(child);
		child.Parent = this;
		if (((BindableObject)this).BindingContext != null)
		{
			BindableObject.SetInheritedBindingContext((BindableObject)(object)child, ((BindableObject)this).BindingContext);
		}
		InvalidateMeasure();
		Invalidate();
	}

	public new virtual void RemoveChild(SkiaView child)
	{
		if (_children.Remove(child))
		{
			child.Parent = null;
			InvalidateMeasure();
			Invalidate();
		}
	}

	public virtual void RemoveChildAt(int index)
	{
		if (index >= 0 && index < _children.Count)
		{
			SkiaView skiaView = _children[index];
			_children.RemoveAt(index);
			skiaView.Parent = null;
			InvalidateMeasure();
			Invalidate();
		}
	}

	public new virtual void InsertChild(int index, SkiaView child)
	{
		if (child.Parent != null)
		{
			throw new InvalidOperationException("View already has a parent");
		}
		_children.Insert(index, child);
		child.Parent = this;
		if (((BindableObject)this).BindingContext != null)
		{
			BindableObject.SetInheritedBindingContext((BindableObject)(object)child, ((BindableObject)this).BindingContext);
		}
		InvalidateMeasure();
		Invalidate();
	}

	public new virtual void ClearChildren()
	{
		foreach (SkiaView child in _children)
		{
			child.Parent = null;
		}
		_children.Clear();
		InvalidateMeasure();
		Invalidate();
	}

	protected virtual SKRect GetContentBounds()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return GetContentBounds(base.Bounds);
	}

	protected SKRect GetContentBounds(SKRect bounds)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		float left = ((SKRect)(ref bounds)).Left;
		SKRect padding = Padding;
		float num = left + ((SKRect)(ref padding)).Left;
		float top = ((SKRect)(ref bounds)).Top;
		padding = Padding;
		float num2 = top + ((SKRect)(ref padding)).Top;
		float right = ((SKRect)(ref bounds)).Right;
		padding = Padding;
		float num3 = right - ((SKRect)(ref padding)).Right;
		float bottom = ((SKRect)(ref bounds)).Bottom;
		padding = Padding;
		return new SKRect(num, num2, num3, bottom - ((SKRect)(ref padding)).Bottom);
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
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
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
		if (this is SkiaStackLayout)
		{
			bool flag = false;
			foreach (SkiaView child in _children)
			{
				if (child is SkiaCollectionView)
				{
					flag = true;
				}
			}
			if (flag)
			{
				Console.WriteLine($"[SkiaStackLayout+CV] OnDraw - bounds={bounds}, children={_children.Count}");
				foreach (SkiaView child2 in _children)
				{
					Console.WriteLine($"[SkiaStackLayout+CV] Child: {((object)child2).GetType().Name}, IsVisible={child2.IsVisible}, Bounds={child2.Bounds}");
				}
			}
		}
		foreach (SkiaView child3 in _children)
		{
			if (child3.IsVisible)
			{
				child3.Draw(canvas);
			}
		}
	}

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsVisible && base.IsEnabled)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(new SKPoint(x, y)))
			{
				for (int num = _children.Count - 1; num >= 0; num--)
				{
					SkiaView skiaView = _children[num].HitTest(x, y);
					if (skiaView != null)
					{
						if (this is SkiaBorder)
						{
							Console.WriteLine($"[SkiaBorder.HitTest] Hit child - x={x}, y={y}, Bounds={base.Bounds}, child={((object)skiaView).GetType().Name}");
						}
						return skiaView;
					}
				}
				if (this is SkiaBorder)
				{
					Console.WriteLine($"[SkiaBorder.HitTest] Hit self - x={x}, y={y}, Bounds={base.Bounds}, children={_children.Count}");
				}
				return this;
			}
		}
		if (this is SkiaBorder)
		{
			Console.WriteLine($"[SkiaBorder.HitTest] Miss - x={x}, y={y}, Bounds={base.Bounds}, IsVisible={base.IsVisible}, IsEnabled={base.IsEnabled}");
		}
		return null;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		SkiaView skiaView = HitTest(e.X, e.Y);
		if (skiaView != null && skiaView != this)
		{
			skiaView.OnPointerPressed(e);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		SkiaView skiaView = HitTest(e.X, e.Y);
		if (skiaView != null && skiaView != this)
		{
			skiaView.OnPointerReleased(e);
		}
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		SkiaView skiaView = HitTest(e.X, e.Y);
		if (skiaView != null && skiaView != this)
		{
			skiaView.OnPointerMoved(e);
		}
	}

	public override void OnScroll(ScrollEventArgs e)
	{
		SkiaView skiaView = HitTest(e.X, e.Y);
		if (skiaView != null && skiaView != this)
		{
			skiaView.OnScroll(e);
		}
	}
}
