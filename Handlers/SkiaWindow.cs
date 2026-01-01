using System;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class SkiaWindow
{
	private SkiaView? _content;

	private string _title = "MAUI Application";

	private int _x;

	private int _y;

	private int _width = 800;

	private int _height = 600;

	private int _minWidth = 100;

	private int _minHeight = 100;

	private int _maxWidth = int.MaxValue;

	private int _maxHeight = int.MaxValue;

	public SkiaView? Content
	{
		get
		{
			return _content;
		}
		set
		{
			_content = value;
			this.ContentChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public string Title
	{
		get
		{
			return _title;
		}
		set
		{
			_title = value;
			this.TitleChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public int X
	{
		get
		{
			return _x;
		}
		set
		{
			_x = value;
			this.PositionChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public int Y
	{
		get
		{
			return _y;
		}
		set
		{
			_y = value;
			this.PositionChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public int Width
	{
		get
		{
			return _width;
		}
		set
		{
			_width = Math.Clamp(value, _minWidth, _maxWidth);
			this.SizeChanged?.Invoke(this, new SizeChangedEventArgs(_width, _height));
		}
	}

	public int Height
	{
		get
		{
			return _height;
		}
		set
		{
			_height = Math.Clamp(value, _minHeight, _maxHeight);
			this.SizeChanged?.Invoke(this, new SizeChangedEventArgs(_width, _height));
		}
	}

	public int MinWidth
	{
		get
		{
			return _minWidth;
		}
		set
		{
			_minWidth = value;
		}
	}

	public int MinHeight
	{
		get
		{
			return _minHeight;
		}
		set
		{
			_minHeight = value;
		}
	}

	public int MaxWidth
	{
		get
		{
			return _maxWidth;
		}
		set
		{
			_maxWidth = value;
		}
	}

	public int MaxHeight
	{
		get
		{
			return _maxHeight;
		}
		set
		{
			_maxHeight = value;
		}
	}

	public event EventHandler? ContentChanged;

	public event EventHandler? TitleChanged;

	public event EventHandler? PositionChanged;

	public event EventHandler<SizeChangedEventArgs>? SizeChanged;

	public event EventHandler? CloseRequested;

	public void Render(SKCanvas canvas)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		canvas.Clear(SKColors.White);
		if (_content != null)
		{
			_content.Measure(new SKSize((float)_width, (float)_height));
			_content.Arrange(new SKRect(0f, 0f, (float)_width, (float)_height));
			_content.Draw(canvas);
		}
		SkiaView.DrawPopupOverlays(canvas);
	}

	public void Close()
	{
		this.CloseRequested?.Invoke(this, EventArgs.Empty);
	}
}
