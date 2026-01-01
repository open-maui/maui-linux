using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform.Linux.Services;

public static class MauiIconGenerator
{
	private const int DefaultIconSize = 256;

	public static string? GenerateIcon(string metaFilePath)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Expected O, but got Unknown
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		if (!File.Exists(metaFilePath))
		{
			Console.WriteLine("[MauiIconGenerator] Metadata file not found: " + metaFilePath);
			return null;
		}
		try
		{
			string? path = Path.GetDirectoryName(metaFilePath) ?? "";
			Dictionary<string, string> dictionary = ParseMetadata(File.ReadAllText(metaFilePath));
			Path.Combine(path, "appicon_bg.svg");
			string text = Path.Combine(path, "appicon_fg.svg");
			string text2 = Path.Combine(path, "appicon.png");
			string value;
			int result;
			int num = ((dictionary.TryGetValue("Size", out value) && int.TryParse(value, out result)) ? result : 256);
			string value2;
			SKColor val = (dictionary.TryGetValue("Color", out value2) ? ParseColor(value2) : SKColors.Purple);
			string value3;
			float result2;
			float num2 = ((dictionary.TryGetValue("Scale", out value3) && float.TryParse(value3, out result2)) ? result2 : 0.65f);
			Console.WriteLine($"[MauiIconGenerator] Generating {num}x{num} icon");
			Console.WriteLine($"[MauiIconGenerator]   Color: {val}");
			Console.WriteLine($"[MauiIconGenerator]   Scale: {num2}");
			SKSurface val2 = SKSurface.Create(new SKImageInfo(num, num, (SKColorType)4, (SKAlphaType)2));
			try
			{
				SKCanvas canvas = val2.Canvas;
				canvas.Clear(val);
				if (File.Exists(text))
				{
					SKSvg val3 = new SKSvg();
					try
					{
						if (val3.Load(text) != null && val3.Picture != null)
						{
							SKRect cullRect = val3.Picture.CullRect;
							float num3 = (float)num * num2 / Math.Max(((SKRect)(ref cullRect)).Width, ((SKRect)(ref cullRect)).Height);
							float num4 = ((float)num - ((SKRect)(ref cullRect)).Width * num3) / 2f;
							float num5 = ((float)num - ((SKRect)(ref cullRect)).Height * num3) / 2f;
							canvas.Save();
							canvas.Translate(num4, num5);
							canvas.Scale(num3);
							canvas.DrawPicture(val3.Picture, (SKPaint)null);
							canvas.Restore();
						}
					}
					finally
					{
						((IDisposable)val3)?.Dispose();
					}
				}
				SKImage val4 = val2.Snapshot();
				try
				{
					SKData val5 = val4.Encode((SKEncodedImageFormat)4, 100);
					try
					{
						using FileStream fileStream = File.OpenWrite(text2);
						val5.SaveTo((Stream)fileStream);
						Console.WriteLine("[MauiIconGenerator] Generated: " + text2);
						return text2;
					}
					finally
					{
						((IDisposable)val5)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[MauiIconGenerator] Error: " + ex.Message);
			return null;
		}
	}

	private static Dictionary<string, string> ParseMetadata(string content)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		string[] array = content.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split('=', 2);
			if (array2.Length == 2)
			{
				dictionary[array2[0].Trim()] = array2[1].Trim();
			}
		}
		return dictionary;
	}

	private static SKColor ParseColor(string colorStr)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_023d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrEmpty(colorStr))
		{
			return SKColors.Purple;
		}
		colorStr = colorStr.Trim();
		if (colorStr.StartsWith("#"))
		{
			string text = colorStr.Substring(1);
			if (text.Length == 3)
			{
				text = $"{text[0]}{text[0]}{text[1]}{text[1]}{text[2]}{text[2]}";
			}
			uint result2;
			if (text.Length == 6)
			{
				if (uint.TryParse(text, NumberStyles.HexNumber, null, out var result))
				{
					return new SKColor((byte)((result >> 16) & 0xFF), (byte)((result >> 8) & 0xFF), (byte)(result & 0xFF));
				}
			}
			else if (text.Length == 8 && uint.TryParse(text, NumberStyles.HexNumber, null, out result2))
			{
				return new SKColor((byte)((result2 >> 16) & 0xFF), (byte)((result2 >> 8) & 0xFF), (byte)(result2 & 0xFF), (byte)((result2 >> 24) & 0xFF));
			}
		}
		return (SKColor)(colorStr.ToLowerInvariant() switch
		{
			"red" => SKColors.Red, 
			"green" => SKColors.Green, 
			"blue" => SKColors.Blue, 
			"purple" => SKColors.Purple, 
			"orange" => SKColors.Orange, 
			"white" => SKColors.White, 
			"black" => SKColors.Black, 
			_ => SKColors.Purple, 
		});
	}
}
