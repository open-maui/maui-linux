using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaAlertDialog : SkiaView
{
	private readonly string _title;

	private readonly string _message;

	private readonly string? _cancel;

	private readonly string? _accept;

	private readonly TaskCompletionSource<bool> _tcs;

	private SKRect _cancelButtonBounds;

	private SKRect _acceptButtonBounds;

	private bool _cancelHovered;

	private bool _acceptHovered;

	private static readonly SKColor OverlayColor = new SKColor((byte)0, (byte)0, (byte)0, (byte)128);

	private static readonly SKColor DialogBackground = SKColors.White;

	private static readonly SKColor TitleColor = new SKColor((byte)33, (byte)33, (byte)33);

	private static readonly SKColor MessageColor = new SKColor((byte)97, (byte)97, (byte)97);

	private static readonly SKColor ButtonColor = new SKColor((byte)33, (byte)150, (byte)243);

	private static readonly SKColor ButtonHoverColor = new SKColor((byte)25, (byte)118, (byte)210);

	private static readonly SKColor ButtonTextColor = SKColors.White;

	private static readonly SKColor CancelButtonColor = new SKColor((byte)158, (byte)158, (byte)158);

	private static readonly SKColor CancelButtonHoverColor = new SKColor((byte)117, (byte)117, (byte)117);

	private static readonly SKColor BorderColor = new SKColor((byte)224, (byte)224, (byte)224);

	private const float DialogWidth = 400f;

	private const float DialogPadding = 24f;

	private const float ButtonHeight = 44f;

	private const float ButtonSpacing = 12f;

	private const float CornerRadius = 12f;

	public Task<bool> Result => _tcs.Task;

	public SkiaAlertDialog(string title, string message, string? accept, string? cancel)
	{
		_title = title;
		_message = message;
		_accept = accept;
		_cancel = cancel;
		_tcs = new TaskCompletionSource<bool>();
		base.IsFocusable = true;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Expected O, but got Unknown
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Expected O, but got Unknown
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Expected O, but got Unknown
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Expected O, but got Unknown
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Expected O, but got Unknown
		//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_030a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Expected O, but got Unknown
		//IL_03b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03be: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0324: Unknown result type (might be due to invalid IL or missing references)
		//IL_031d: Unknown result type (might be due to invalid IL or missing references)
		//IL_041c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_0429: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Unknown result type (might be due to invalid IL or missing references)
		//IL_035b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_0443: Unknown result type (might be due to invalid IL or missing references)
		//IL_043c: Unknown result type (might be due to invalid IL or missing references)
		//IL_037d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0376: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = OverlayColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(bounds, val);
			List<string> list = WrapText(_message, 352f, 16f);
			float num = CalculateDialogHeight(list.Count);
			float num2 = ((SKRect)(ref bounds)).MidX - 200f;
			float num3 = ((SKRect)(ref bounds)).MidY - num / 2f;
			SKRect val2 = new SKRect(num2, num3, num2 + 400f, num3 + num);
			SKPaint val3 = new SKPaint
			{
				Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)60),
				MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, 8f),
				Style = (SKPaintStyle)0
			};
			try
			{
				SKRect val4 = new SKRect(((SKRect)(ref val2)).Left + 4f, ((SKRect)(ref val2)).Top + 4f, ((SKRect)(ref val2)).Right + 4f, ((SKRect)(ref val2)).Bottom + 4f);
				canvas.DrawRoundRect(val4, 12f, 12f, val3);
				SKPaint val5 = new SKPaint
				{
					Color = DialogBackground,
					Style = (SKPaintStyle)0,
					IsAntialias = true
				};
				try
				{
					canvas.DrawRoundRect(val2, 12f, 12f, val5);
					float num4 = ((SKRect)(ref val2)).Top + 24f;
					if (!string.IsNullOrEmpty(_title))
					{
						SKFont val6 = new SKFont(SKTypeface.Default, 20f, 1f, 0f)
						{
							Embolden = true
						};
						try
						{
							SKPaint val7 = new SKPaint(val6)
							{
								Color = TitleColor,
								IsAntialias = true
							};
							try
							{
								canvas.DrawText(_title, ((SKRect)(ref val2)).Left + 24f, num4 + 20f, val7);
								num4 += 36f;
							}
							finally
							{
								((IDisposable)val7)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)val6)?.Dispose();
						}
					}
					if (!string.IsNullOrEmpty(_message))
					{
						SKFont val8 = new SKFont(SKTypeface.Default, 16f, 1f, 0f);
						try
						{
							SKPaint val9 = new SKPaint(val8)
							{
								Color = MessageColor,
								IsAntialias = true
							};
							try
							{
								foreach (string item in list)
								{
									canvas.DrawText(item, ((SKRect)(ref val2)).Left + 24f, num4 + 16f, val9);
									num4 += 22f;
								}
								num4 += 8f;
							}
							finally
							{
								((IDisposable)val9)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)val8)?.Dispose();
						}
					}
					num4 = ((SKRect)(ref val2)).Bottom - 24f - 44f;
					float num5 = num4;
					int num6 = ((_accept != null) ? 1 : 0) + ((_cancel != null) ? 1 : 0);
					float num7 = 352f;
					if (num6 == 2)
					{
						float num8 = (num7 - 12f) / 2f;
						_cancelButtonBounds = new SKRect(((SKRect)(ref val2)).Left + 24f, num5, ((SKRect)(ref val2)).Left + 24f + num8, num5 + 44f);
						DrawButton(canvas, _cancelButtonBounds, _cancel, _cancelHovered ? CancelButtonHoverColor : CancelButtonColor);
						_acceptButtonBounds = new SKRect(((SKRect)(ref val2)).Right - 24f - num8, num5, ((SKRect)(ref val2)).Right - 24f, num5 + 44f);
						DrawButton(canvas, _acceptButtonBounds, _accept, _acceptHovered ? ButtonHoverColor : ButtonColor);
					}
					else if (_accept != null)
					{
						_acceptButtonBounds = new SKRect(((SKRect)(ref val2)).Left + 24f, num5, ((SKRect)(ref val2)).Right - 24f, num5 + 44f);
						DrawButton(canvas, _acceptButtonBounds, _accept, _acceptHovered ? ButtonHoverColor : ButtonColor);
					}
					else if (_cancel != null)
					{
						_cancelButtonBounds = new SKRect(((SKRect)(ref val2)).Left + 24f, num5, ((SKRect)(ref val2)).Right - 24f, num5 + 44f);
						DrawButton(canvas, _cancelButtonBounds, _cancel, _cancelHovered ? CancelButtonHoverColor : CancelButtonColor);
					}
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
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawButton(SKCanvas canvas, SKRect bounds, string text, SKColor bgColor)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = bgColor,
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			canvas.DrawRoundRect(bounds, 8f, 8f, val);
			SKFont val2 = new SKFont(SKTypeface.Default, 16f, 1f, 0f)
			{
				Embolden = true
			};
			try
			{
				SKPaint val3 = new SKPaint(val2)
				{
					Color = ButtonTextColor,
					IsAntialias = true
				};
				try
				{
					SKRect val4 = default(SKRect);
					val3.MeasureText(text, ref val4);
					float num = ((SKRect)(ref bounds)).MidX - ((SKRect)(ref val4)).MidX;
					float num2 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val4)).MidY;
					canvas.DrawText(text, num, num2, val3);
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
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private float CalculateDialogHeight(int messageLineCount)
	{
		float num = 48f;
		if (!string.IsNullOrEmpty(_title))
		{
			num += 36f;
		}
		if (!string.IsNullOrEmpty(_message))
		{
			num += (float)(messageLineCount * 22 + 8);
		}
		num += 44f;
		return Math.Max(num, 180f);
	}

	private List<string> WrapText(string text, float maxWidth, float fontSize)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		List<string> list = new List<string>();
		if (string.IsNullOrEmpty(text))
		{
			return list;
		}
		SKFont val = new SKFont(SKTypeface.Default, fontSize, 1f, 0f);
		try
		{
			SKPaint val2 = new SKPaint(val);
			try
			{
				string[] array = text.Split(' ');
				string text2 = "";
				string[] array2 = array;
				foreach (string text3 in array2)
				{
					string text4 = (string.IsNullOrEmpty(text2) ? text3 : (text2 + " " + text3));
					if (val2.MeasureText(text4) > maxWidth && !string.IsNullOrEmpty(text2))
					{
						list.Add(text2);
						text2 = text3;
					}
					else
					{
						text2 = text4;
					}
				}
				if (!string.IsNullOrEmpty(text2))
				{
					list.Add(text2);
				}
				return list;
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		bool num = _cancelHovered || _acceptHovered;
		_cancelHovered = _cancel != null && ((SKRect)(ref _cancelButtonBounds)).Contains(e.X, e.Y);
		_acceptHovered = _accept != null && ((SKRect)(ref _acceptButtonBounds)).Contains(e.X, e.Y);
		if (num != (_cancelHovered || _acceptHovered))
		{
			Invalidate();
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		if (_cancel != null && ((SKRect)(ref _cancelButtonBounds)).Contains(e.X, e.Y))
		{
			Dismiss(result: false);
		}
		else if (_accept != null && ((SKRect)(ref _acceptButtonBounds)).Contains(e.X, e.Y))
		{
			Dismiss(result: true);
		}
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (e.Key == Key.Escape && _cancel != null)
		{
			Dismiss(result: false);
			e.Handled = true;
		}
		else if (e.Key == Key.Enter && _accept != null)
		{
			Dismiss(result: true);
			e.Handled = true;
		}
	}

	private void Dismiss(bool result)
	{
		LinuxDialogService.HideDialog(this);
		_tcs.TrySetResult(result);
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return availableSize;
	}

	public override SkiaView? HitTest(float x, float y)
	{
		return this;
	}
}
