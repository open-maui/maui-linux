using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Microsoft.Maui.Platform.Linux.Services;

public class HiDpiService
{
	private const float DefaultDpi = 96f;

	private float _scaleFactor = 1f;

	private float _dpi = 96f;

	private bool _initialized;

	public float ScaleFactor => _scaleFactor;

	public float Dpi => _dpi;

	public event EventHandler<ScaleChangedEventArgs>? ScaleChanged;

	public void Initialize()
	{
		if (!_initialized)
		{
			_initialized = true;
			DetectScaleFactor();
		}
	}

	public void DetectScaleFactor()
	{
		float scale = 1f;
		float dpi = 96f;
		float scale3;
		float dpi2;
		float scale4;
		float scale5;
		float dpi3;
		float scale6;
		if (TryGetEnvironmentScale(out var scale2))
		{
			scale = scale2;
		}
		else if (TryGetGnomeScale(out scale3, out dpi2))
		{
			scale = scale3;
			dpi = dpi2;
		}
		else if (TryGetKdeScale(out scale4))
		{
			scale = scale4;
		}
		else if (TryGetX11Scale(out scale5, out dpi3))
		{
			scale = scale5;
			dpi = dpi3;
		}
		else if (TryGetXrandrScale(out scale6))
		{
			scale = scale6;
		}
		UpdateScale(scale, dpi);
	}

	private void UpdateScale(float scale, float dpi)
	{
		if (Math.Abs(_scaleFactor - scale) > 0.01f || Math.Abs(_dpi - dpi) > 0.01f)
		{
			float scaleFactor = _scaleFactor;
			_scaleFactor = scale;
			_dpi = dpi;
			this.ScaleChanged?.Invoke(this, new ScaleChangedEventArgs(scaleFactor, scale, dpi));
		}
	}

	private static bool TryGetEnvironmentScale(out float scale)
	{
		scale = 1f;
		string environmentVariable = Environment.GetEnvironmentVariable("GDK_SCALE");
		if (!string.IsNullOrEmpty(environmentVariable) && float.TryParse(environmentVariable, out var result))
		{
			scale = result;
			return true;
		}
		string environmentVariable2 = Environment.GetEnvironmentVariable("GDK_DPI_SCALE");
		if (!string.IsNullOrEmpty(environmentVariable2) && float.TryParse(environmentVariable2, out var result2))
		{
			scale = result2;
			return true;
		}
		string environmentVariable3 = Environment.GetEnvironmentVariable("QT_SCALE_FACTOR");
		if (!string.IsNullOrEmpty(environmentVariable3) && float.TryParse(environmentVariable3, out var result3))
		{
			scale = result3;
			return true;
		}
		string environmentVariable4 = Environment.GetEnvironmentVariable("QT_SCREEN_SCALE_FACTORS");
		if (!string.IsNullOrEmpty(environmentVariable4))
		{
			string text = environmentVariable4.Split(';')[0];
			if (text.Contains('='))
			{
				text = text.Split('=')[1];
			}
			if (float.TryParse(text, out var result4))
			{
				scale = result4;
				return true;
			}
		}
		return false;
	}

	private static bool TryGetGnomeScale(out float scale, out float dpi)
	{
		scale = 1f;
		dpi = 96f;
		try
		{
			string text = RunCommand("gsettings", "get org.gnome.desktop.interface scaling-factor");
			if (!string.IsNullOrEmpty(text))
			{
				Match match = Regex.Match(text, "uint32\\s+(\\d+)");
				if (match.Success && int.TryParse(match.Groups[1].Value, out var result) && result > 0)
				{
					scale = result;
				}
			}
			text = RunCommand("gsettings", "get org.gnome.desktop.interface text-scaling-factor");
			if (!string.IsNullOrEmpty(text) && float.TryParse(text.Trim(), out var result2) && result2 > 0.5f)
			{
				scale = Math.Max(scale, result2);
			}
			text = RunCommand("gsettings", "get org.gnome.mutter experimental-features");
			if (text != null && text.Contains("scale-monitor-framebuffer"))
			{
				text = RunCommand("gdbus", "call --session --dest org.gnome.Mutter.DisplayConfig --object-path /org/gnome/Mutter/DisplayConfig --method org.gnome.Mutter.DisplayConfig.GetCurrentState");
				if (text != null)
				{
					Match match2 = Regex.Match(text, "'scale':\\s*<(\\d+\\.?\\d*)>");
					if (match2.Success && float.TryParse(match2.Groups[1].Value, out var result3))
					{
						scale = result3;
					}
				}
			}
			return scale > 1f || Math.Abs(scale - 1f) < 0.01f;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryGetKdeScale(out float scale)
	{
		scale = 1f;
		try
		{
			string text = RunCommand("kreadconfig5", "--file kdeglobals --group KScreen --key ScaleFactor");
			if (!string.IsNullOrEmpty(text) && float.TryParse(text.Trim(), out var result) && result > 0f)
			{
				scale = result;
				return true;
			}
			text = RunCommand("kreadconfig6", "--file kdeglobals --group KScreen --key ScaleFactor");
			if (!string.IsNullOrEmpty(text) && float.TryParse(text.Trim(), out var result2) && result2 > 0f)
			{
				scale = result2;
				return true;
			}
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "kdeglobals");
			if (File.Exists(path))
			{
				string[] array = File.ReadAllLines(path);
				bool flag = false;
				string[] array2 = array;
				foreach (string text2 in array2)
				{
					if (text2.Trim() == "[KScreen]")
					{
						flag = true;
						continue;
					}
					if (flag && text2.StartsWith("["))
					{
						break;
					}
					if (flag && text2.StartsWith("ScaleFactor=") && float.TryParse(text2.Substring("ScaleFactor=".Length), out var result3))
					{
						scale = result3;
						return true;
					}
				}
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private bool TryGetX11Scale(out float scale, out float dpi)
	{
		scale = 1f;
		dpi = 96f;
		try
		{
			string text = RunCommand("xrdb", "-query");
			if (!string.IsNullOrEmpty(text))
			{
				Match match = Regex.Match(text, "Xft\\.dpi:\\s*(\\d+)");
				if (match.Success && float.TryParse(match.Groups[1].Value, out var result))
				{
					dpi = result;
					scale = result / 96f;
					return true;
				}
			}
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Xresources");
			if (File.Exists(path))
			{
				Match match2 = Regex.Match(File.ReadAllText(path), "Xft\\.dpi:\\s*(\\d+)");
				if (match2.Success && float.TryParse(match2.Groups[1].Value, out var result2))
				{
					dpi = result2;
					scale = result2 / 96f;
					return true;
				}
			}
			return TryGetX11DpiDirect(out scale, out dpi);
		}
		catch
		{
			return false;
		}
	}

	private bool TryGetX11DpiDirect(out float scale, out float dpi)
	{
		scale = 1f;
		dpi = 96f;
		try
		{
			IntPtr intPtr = XOpenDisplay(IntPtr.Zero);
			if (intPtr == IntPtr.Zero)
			{
				return false;
			}
			try
			{
				int screen = XDefaultScreen(intPtr);
				int num = XDisplayWidthMM(intPtr, screen);
				int num2 = XDisplayHeightMM(intPtr, screen);
				int num3 = XDisplayWidth(intPtr, screen);
				int num4 = XDisplayHeight(intPtr, screen);
				if (num > 0 && num2 > 0)
				{
					float num5 = (float)num3 * 25.4f / (float)num;
					float num6 = (float)num4 * 25.4f / (float)num2;
					dpi = (num5 + num6) / 2f;
					scale = dpi / 96f;
					return true;
				}
				return false;
			}
			finally
			{
				XCloseDisplay(intPtr);
			}
		}
		catch
		{
			return false;
		}
	}

	private static bool TryGetXrandrScale(out float scale)
	{
		scale = 1f;
		try
		{
			string text = RunCommand("xrandr", "--query");
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			string[] array = text.Split('\n');
			foreach (string text2 in array)
			{
				if (text2.Contains("connected") && !text2.Contains("disconnected"))
				{
					Match match = Regex.Match(text2, "(\\d+)x(\\d+)\\+\\d+\\+\\d+");
					Match match2 = Regex.Match(text2, "(\\d+)mm x (\\d+)mm");
					if (match.Success && match2.Success && int.TryParse(match.Groups[1].Value, out var result) && int.TryParse(match2.Groups[1].Value, out var result2) && result2 > 0)
					{
						float num = (float)result * 25.4f / (float)result2;
						scale = num / 96f;
						return true;
					}
				}
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private static string? RunCommand(string command, string arguments)
	{
		try
		{
			using Process process = new Process();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = command,
				Arguments = arguments,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			process.Start();
			string result = process.StandardOutput.ReadToEnd();
			process.WaitForExit(1000);
			return result;
		}
		catch
		{
			return null;
		}
	}

	public float ToPhysicalPixels(float logicalPixels)
	{
		return logicalPixels * _scaleFactor;
	}

	public float ToLogicalPixels(float physicalPixels)
	{
		return physicalPixels / _scaleFactor;
	}

	public float GetFontScaleFactor()
	{
		try
		{
			string text = RunCommand("gsettings", "get org.gnome.desktop.interface text-scaling-factor");
			if (!string.IsNullOrEmpty(text) && float.TryParse(text.Trim(), out var result))
			{
				return result;
			}
		}
		catch
		{
		}
		return _scaleFactor;
	}

	[DllImport("libX11.so.6")]
	private static extern IntPtr XOpenDisplay(IntPtr display);

	[DllImport("libX11.so.6")]
	private static extern void XCloseDisplay(IntPtr display);

	[DllImport("libX11.so.6")]
	private static extern int XDefaultScreen(IntPtr display);

	[DllImport("libX11.so.6")]
	private static extern int XDisplayWidth(IntPtr display, int screen);

	[DllImport("libX11.so.6")]
	private static extern int XDisplayHeight(IntPtr display, int screen);

	[DllImport("libX11.so.6")]
	private static extern int XDisplayWidthMM(IntPtr display, int screen);

	[DllImport("libX11.so.6")]
	private static extern int XDisplayHeightMM(IntPtr display, int screen);
}
