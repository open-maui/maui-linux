using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public abstract class SkiaView : BindableObject, IDisposable
{
	private static readonly List<(SkiaView Owner, Action<SKCanvas> Draw)> _popupOverlays = new List<(SkiaView, Action<SKCanvas>)>();

	public static readonly BindableProperty IsVisibleProperty = BindableProperty.Create("IsVisible", typeof(bool), typeof(SkiaView), (object)true, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).OnVisibilityChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsEnabledProperty = BindableProperty.Create("IsEnabled", typeof(bool), typeof(SkiaView), (object)true, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).OnEnabledChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty OpacityProperty = BindableProperty.Create("Opacity", typeof(float), typeof(SkiaView), (object)1f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)((BindableObject b, object v) => Math.Clamp((float)v, 0f, 1f)), (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BackgroundColorProperty = BindableProperty.Create("BackgroundColor", typeof(SKColor), typeof(SkiaView), (object)SKColors.Transparent, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty WidthRequestProperty = BindableProperty.Create("WidthRequest", typeof(double), typeof(SkiaView), (object)(-1.0), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HeightRequestProperty = BindableProperty.Create("HeightRequest", typeof(double), typeof(SkiaView), (object)(-1.0), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MinimumWidthRequestProperty = BindableProperty.Create("MinimumWidthRequest", typeof(double), typeof(SkiaView), (object)0.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MinimumHeightRequestProperty = BindableProperty.Create("MinimumHeightRequest", typeof(double), typeof(SkiaView), (object)0.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsFocusableProperty = BindableProperty.Create("IsFocusable", typeof(bool), typeof(SkiaView), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MarginProperty = BindableProperty.Create("Margin", typeof(Thickness), typeof(SkiaView), (object)default(Thickness), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HorizontalOptionsProperty = BindableProperty.Create("HorizontalOptions", typeof(LayoutOptions), typeof(SkiaView), (object)LayoutOptions.Fill, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty VerticalOptionsProperty = BindableProperty.Create("VerticalOptions", typeof(LayoutOptions), typeof(SkiaView), (object)LayoutOptions.Fill, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty NameProperty = BindableProperty.Create("Name", typeof(string), typeof(SkiaView), (object)string.Empty, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ScaleProperty = BindableProperty.Create("Scale", typeof(double), typeof(SkiaView), (object)1.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ScaleXProperty = BindableProperty.Create("ScaleX", typeof(double), typeof(SkiaView), (object)1.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ScaleYProperty = BindableProperty.Create("ScaleY", typeof(double), typeof(SkiaView), (object)1.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty RotationProperty = BindableProperty.Create("Rotation", typeof(double), typeof(SkiaView), (object)0.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty RotationXProperty = BindableProperty.Create("RotationX", typeof(double), typeof(SkiaView), (object)0.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty RotationYProperty = BindableProperty.Create("RotationY", typeof(double), typeof(SkiaView), (object)0.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TranslationXProperty = BindableProperty.Create("TranslationX", typeof(double), typeof(SkiaView), (object)0.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TranslationYProperty = BindableProperty.Create("TranslationY", typeof(double), typeof(SkiaView), (object)0.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty AnchorXProperty = BindableProperty.Create("AnchorX", typeof(double), typeof(SkiaView), (object)0.5, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty AnchorYProperty = BindableProperty.Create("AnchorY", typeof(double), typeof(SkiaView), (object)0.5, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private bool _disposed;

	private SKRect _bounds;

	private SkiaView? _parent;

	private readonly List<SkiaView> _children = new List<SkiaView>();

	private SKColor _backgroundColor = SKColors.Transparent;

	public static bool HasActivePopup => _popupOverlays.Count > 0;

	public SKRect Bounds
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _bounds;
		}
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			if (_bounds != value)
			{
				_bounds = value;
				OnBoundsChanged();
			}
		}
	}

	public bool IsVisible
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsVisibleProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsVisibleProperty, (object)value);
		}
	}

	public bool IsEnabled
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsEnabledProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsEnabledProperty, (object)value);
		}
	}

	public float Opacity
	{
		get
		{
			return (float)((BindableObject)this).GetValue(OpacityProperty);
		}
		set
		{
			((BindableObject)this).SetValue(OpacityProperty, (object)value);
		}
	}

	public SKColor BackgroundColor
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _backgroundColor;
		}
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			if (_backgroundColor != value)
			{
				_backgroundColor = value;
				((BindableObject)this).SetValue(BackgroundColorProperty, (object)value);
				Invalidate();
			}
		}
	}

	public double WidthRequest
	{
		get
		{
			return (double)((BindableObject)this).GetValue(WidthRequestProperty);
		}
		set
		{
			((BindableObject)this).SetValue(WidthRequestProperty, (object)value);
		}
	}

	public double HeightRequest
	{
		get
		{
			return (double)((BindableObject)this).GetValue(HeightRequestProperty);
		}
		set
		{
			((BindableObject)this).SetValue(HeightRequestProperty, (object)value);
		}
	}

	public double MinimumWidthRequest
	{
		get
		{
			return (double)((BindableObject)this).GetValue(MinimumWidthRequestProperty);
		}
		set
		{
			((BindableObject)this).SetValue(MinimumWidthRequestProperty, (object)value);
		}
	}

	public double MinimumHeightRequest
	{
		get
		{
			return (double)((BindableObject)this).GetValue(MinimumHeightRequestProperty);
		}
		set
		{
			((BindableObject)this).SetValue(MinimumHeightRequestProperty, (object)value);
		}
	}

	public double RequestedWidth
	{
		get
		{
			return WidthRequest;
		}
		set
		{
			WidthRequest = value;
		}
	}

	public double RequestedHeight
	{
		get
		{
			return HeightRequest;
		}
		set
		{
			HeightRequest = value;
		}
	}

	public bool IsFocusable
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsFocusableProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsFocusableProperty, (object)value);
		}
	}

	public CursorType CursorType { get; set; }

	public Thickness Margin
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (Thickness)((BindableObject)this).GetValue(MarginProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(MarginProperty, (object)value);
		}
	}

	public LayoutOptions HorizontalOptions
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (LayoutOptions)((BindableObject)this).GetValue(HorizontalOptionsProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(HorizontalOptionsProperty, (object)value);
		}
	}

	public LayoutOptions VerticalOptions
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (LayoutOptions)((BindableObject)this).GetValue(VerticalOptionsProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(VerticalOptionsProperty, (object)value);
		}
	}

	public string Name
	{
		get
		{
			return (string)((BindableObject)this).GetValue(NameProperty);
		}
		set
		{
			((BindableObject)this).SetValue(NameProperty, (object)value);
		}
	}

	public double Scale
	{
		get
		{
			return (double)((BindableObject)this).GetValue(ScaleProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ScaleProperty, (object)value);
		}
	}

	public double ScaleX
	{
		get
		{
			return (double)((BindableObject)this).GetValue(ScaleXProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ScaleXProperty, (object)value);
		}
	}

	public double ScaleY
	{
		get
		{
			return (double)((BindableObject)this).GetValue(ScaleYProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ScaleYProperty, (object)value);
		}
	}

	public double Rotation
	{
		get
		{
			return (double)((BindableObject)this).GetValue(RotationProperty);
		}
		set
		{
			((BindableObject)this).SetValue(RotationProperty, (object)value);
		}
	}

	public double RotationX
	{
		get
		{
			return (double)((BindableObject)this).GetValue(RotationXProperty);
		}
		set
		{
			((BindableObject)this).SetValue(RotationXProperty, (object)value);
		}
	}

	public double RotationY
	{
		get
		{
			return (double)((BindableObject)this).GetValue(RotationYProperty);
		}
		set
		{
			((BindableObject)this).SetValue(RotationYProperty, (object)value);
		}
	}

	public double TranslationX
	{
		get
		{
			return (double)((BindableObject)this).GetValue(TranslationXProperty);
		}
		set
		{
			((BindableObject)this).SetValue(TranslationXProperty, (object)value);
		}
	}

	public double TranslationY
	{
		get
		{
			return (double)((BindableObject)this).GetValue(TranslationYProperty);
		}
		set
		{
			((BindableObject)this).SetValue(TranslationYProperty, (object)value);
		}
	}

	public double AnchorX
	{
		get
		{
			return (double)((BindableObject)this).GetValue(AnchorXProperty);
		}
		set
		{
			((BindableObject)this).SetValue(AnchorXProperty, (object)value);
		}
	}

	public double AnchorY
	{
		get
		{
			return (double)((BindableObject)this).GetValue(AnchorYProperty);
		}
		set
		{
			((BindableObject)this).SetValue(AnchorYProperty, (object)value);
		}
	}

	public bool IsFocused { get; internal set; }

	public SkiaView? Parent
	{
		get
		{
			return _parent;
		}
		internal set
		{
			_parent = value;
		}
	}

	public View? MauiView { get; set; }

	public SKRect ScreenBounds
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			SKRect bounds = Bounds;
			for (SkiaView parent = _parent; parent != null; parent = parent.Parent)
			{
				if (parent is SkiaScrollView skiaScrollView)
				{
					((SKRect)(ref bounds))._002Ector(((SKRect)(ref bounds)).Left - skiaScrollView.ScrollX, ((SKRect)(ref bounds)).Top - skiaScrollView.ScrollY, ((SKRect)(ref bounds)).Right - skiaScrollView.ScrollX, ((SKRect)(ref bounds)).Bottom - skiaScrollView.ScrollY);
				}
			}
			return bounds;
		}
	}

	public SKSize DesiredSize { get; protected set; }

	public IReadOnlyList<SkiaView> Children => _children;

	public event EventHandler? Invalidated;

	public static void RegisterPopupOverlay(SkiaView owner, Action<SKCanvas> drawAction)
	{
		_popupOverlays.RemoveAll(((SkiaView Owner, Action<SKCanvas> Draw) p) => p.Owner == owner);
		_popupOverlays.Add((owner, drawAction));
	}

	public static void UnregisterPopupOverlay(SkiaView owner)
	{
		_popupOverlays.RemoveAll(((SkiaView Owner, Action<SKCanvas> Draw) p) => p.Owner == owner);
	}

	public static void DrawPopupOverlays(SKCanvas canvas)
	{
		while (canvas.SaveCount > 1)
		{
			canvas.Restore();
		}
		foreach (var popupOverlay in _popupOverlays)
		{
			Action<SKCanvas> item = popupOverlay.Draw;
			canvas.Save();
			item(canvas);
			canvas.Restore();
		}
	}

	public static SkiaView? GetPopupOwnerAt(float x, float y)
	{
		for (int num = _popupOverlays.Count - 1; num >= 0; num--)
		{
			SkiaView item = _popupOverlays[num].Owner;
			if (item.HitTestPopupArea(x, y))
			{
				return item;
			}
		}
		return null;
	}

	protected virtual bool HitTestPopupArea(float x, float y)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		SKRect bounds = Bounds;
		return ((SKRect)(ref bounds)).Contains(x, y);
	}

	public SKRect GetAbsoluteBounds()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		SKRect bounds = Bounds;
		for (SkiaView parent = Parent; parent != null; parent = parent.Parent)
		{
			if (parent is SkiaScrollView skiaScrollView)
			{
				((SKRect)(ref bounds))._002Ector(((SKRect)(ref bounds)).Left - skiaScrollView.ScrollX, ((SKRect)(ref bounds)).Top - skiaScrollView.ScrollY, ((SKRect)(ref bounds)).Right - skiaScrollView.ScrollX, ((SKRect)(ref bounds)).Bottom - skiaScrollView.ScrollY);
			}
		}
		return bounds;
	}

	protected virtual void OnVisibilityChanged()
	{
		Invalidate();
	}

	protected virtual void OnEnabledChanged()
	{
		Invalidate();
	}

	protected override void OnBindingContextChanged()
	{
		((BindableObject)this).OnBindingContextChanged();
		foreach (SkiaView child in _children)
		{
			BindableObject.SetInheritedBindingContext((BindableObject)(object)child, ((BindableObject)this).BindingContext);
		}
	}

	public void AddChild(SkiaView child)
	{
		if (child._parent != null)
		{
			throw new InvalidOperationException("View already has a parent");
		}
		child._parent = this;
		_children.Add(child);
		if (((BindableObject)this).BindingContext != null)
		{
			BindableObject.SetInheritedBindingContext((BindableObject)(object)child, ((BindableObject)this).BindingContext);
		}
		Invalidate();
	}

	public void RemoveChild(SkiaView child)
	{
		if (child._parent == this)
		{
			child._parent = null;
			_children.Remove(child);
			Invalidate();
		}
	}

	public void InsertChild(int index, SkiaView child)
	{
		if (child._parent != null)
		{
			throw new InvalidOperationException("View already has a parent");
		}
		child._parent = this;
		_children.Insert(index, child);
		if (((BindableObject)this).BindingContext != null)
		{
			BindableObject.SetInheritedBindingContext((BindableObject)(object)child, ((BindableObject)this).BindingContext);
		}
		Invalidate();
	}

	public void ClearChildren()
	{
		foreach (SkiaView child in _children)
		{
			child._parent = null;
		}
		_children.Clear();
		Invalidate();
	}

	public void Invalidate()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		LinuxApplication.LogInvalidate(((object)this).GetType().Name);
		this.Invalidated?.Invoke(this, EventArgs.Empty);
		SKRect bounds = Bounds;
		if (((SKRect)(ref bounds)).Width > 0f)
		{
			bounds = Bounds;
			if (((SKRect)(ref bounds)).Height > 0f)
			{
				SkiaRenderingEngine.Current?.InvalidateRegion(Bounds);
			}
		}
		if (_parent != null)
		{
			_parent.Invalidate();
		}
		else
		{
			LinuxApplication.RequestRedraw();
		}
	}

	public void InvalidateMeasure()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		DesiredSize = SKSize.Empty;
		_parent?.InvalidateMeasure();
		Invalidate();
	}

	public virtual void Draw(SKCanvas canvas)
	{
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Expected O, but got Unknown
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Expected O, but got Unknown
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		if (!IsVisible || Opacity <= 0f)
		{
			return;
		}
		canvas.Save();
		if (Scale != 1.0 || ScaleX != 1.0 || ScaleY != 1.0 || Rotation != 0.0 || RotationX != 0.0 || RotationY != 0.0 || TranslationX != 0.0 || TranslationY != 0.0)
		{
			SKRect bounds = Bounds;
			float left = ((SKRect)(ref bounds)).Left;
			bounds = Bounds;
			float num = left + (float)((double)((SKRect)(ref bounds)).Width * AnchorX);
			bounds = Bounds;
			float top = ((SKRect)(ref bounds)).Top;
			bounds = Bounds;
			float num2 = top + (float)((double)((SKRect)(ref bounds)).Height * AnchorY);
			canvas.Translate(num, num2);
			if (TranslationX != 0.0 || TranslationY != 0.0)
			{
				canvas.Translate((float)TranslationX, (float)TranslationY);
			}
			if (Rotation != 0.0)
			{
				canvas.RotateDegrees((float)Rotation);
			}
			float num3 = (float)(Scale * ScaleX);
			float num4 = (float)(Scale * ScaleY);
			if (num3 != 1f || num4 != 1f)
			{
				canvas.Scale(num3, num4);
			}
			canvas.Translate(0f - num, 0f - num2);
		}
		if (Opacity < 1f)
		{
			canvas.SaveLayer(new SKPaint
			{
				Color = ((SKColor)(ref SKColors.White)).WithAlpha((byte)(Opacity * 255f))
			});
		}
		if (BackgroundColor != SKColors.Transparent)
		{
			SKPaint val = new SKPaint
			{
				Color = BackgroundColor
			};
			try
			{
				canvas.DrawRect(Bounds, val);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		OnDraw(canvas, Bounds);
		foreach (SkiaView child in _children)
		{
			child.Draw(canvas);
		}
		if (Opacity < 1f)
		{
			canvas.Restore();
		}
		canvas.Restore();
	}

	protected virtual void OnDraw(SKCanvas canvas, SKRect bounds)
	{
	}

	protected virtual void OnBoundsChanged()
	{
		Invalidate();
	}

	public SKSize Measure(SKSize availableSize)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		DesiredSize = MeasureOverride(availableSize);
		return DesiredSize;
	}

	protected virtual SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		float num = ((WidthRequest >= 0.0) ? ((float)WidthRequest) : 0f);
		float num2 = ((HeightRequest >= 0.0) ? ((float)HeightRequest) : 0f);
		return new SKSize(num, num2);
	}

	public virtual void Arrange(SKRect bounds)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		Bounds = ArrangeOverride(bounds);
	}

	protected virtual SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return bounds;
	}

	public virtual SkiaView? HitTest(SKPoint point)
	{
		return HitTest(((SKPoint)(ref point)).X, ((SKPoint)(ref point)).Y);
	}

	public virtual SkiaView? HitTest(float x, float y)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (!IsVisible || !IsEnabled)
		{
			return null;
		}
		SKRect bounds = Bounds;
		if (!((SKRect)(ref bounds)).Contains(x, y))
		{
			return null;
		}
		for (int num = _children.Count - 1; num >= 0; num--)
		{
			SkiaView skiaView = _children[num].HitTest(x, y);
			if (skiaView != null)
			{
				return skiaView;
			}
		}
		return this;
	}

	public virtual void OnPointerEntered(PointerEventArgs e)
	{
		if (MauiView != null)
		{
			GestureManager.ProcessPointerEntered(MauiView, e.X, e.Y);
		}
	}

	public virtual void OnPointerExited(PointerEventArgs e)
	{
		if (MauiView != null)
		{
			GestureManager.ProcessPointerExited(MauiView, e.X, e.Y);
		}
	}

	public virtual void OnPointerMoved(PointerEventArgs e)
	{
		if (MauiView != null)
		{
			GestureManager.ProcessPointerMove(MauiView, e.X, e.Y);
		}
	}

	public virtual void OnPointerPressed(PointerEventArgs e)
	{
		if (MauiView != null)
		{
			GestureManager.ProcessPointerDown(MauiView, e.X, e.Y);
		}
	}

	public virtual void OnPointerReleased(PointerEventArgs e)
	{
		Console.WriteLine("[SkiaView] OnPointerReleased on " + ((object)this).GetType().Name + ", MauiView=" + (((object)MauiView)?.GetType().Name ?? "null"));
		if (MauiView != null)
		{
			GestureManager.ProcessPointerUp(MauiView, e.X, e.Y);
		}
	}

	public virtual void OnScroll(ScrollEventArgs e)
	{
	}

	public virtual void OnKeyDown(KeyEventArgs e)
	{
	}

	public virtual void OnKeyUp(KeyEventArgs e)
	{
	}

	public virtual void OnTextInput(TextInputEventArgs e)
	{
	}

	public virtual void OnFocusGained()
	{
		IsFocused = true;
		Invalidate();
	}

	public virtual void OnFocusLost()
	{
		IsFocused = false;
		Invalidate();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}
		if (disposing)
		{
			foreach (SkiaView child in _children)
			{
				child.Dispose();
			}
			_children.Clear();
		}
		_disposed = true;
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
