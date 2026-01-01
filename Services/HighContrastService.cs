using System;
using System.Diagnostics;
using System.IO;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

public class HighContrastService
{
	private bool _isHighContrastEnabled;

	private HighContrastTheme _currentTheme;

	private bool _initialized;

	public bool IsHighContrastEnabled => _isHighContrastEnabled;

	public HighContrastTheme CurrentTheme => _currentTheme;

	public event EventHandler<HighContrastChangedEventArgs>? HighContrastChanged;

	public void Initialize()
	{
		if (!_initialized)
		{
			_initialized = true;
			DetectHighContrast();
		}
	}

	public void DetectHighContrast()
	{
		bool isEnabled = false;
		HighContrastTheme theme = HighContrastTheme.None;
		bool isEnabled3;
		string themeName2;
		bool isEnabled4;
		string themeName3;
		bool isEnabled5;
		if (TryGetGnomeHighContrast(out bool isEnabled2, out string themeName))
		{
			isEnabled = isEnabled2;
			if (isEnabled2)
			{
				theme = ParseThemeName(themeName);
			}
		}
		else if (TryGetKdeHighContrast(out isEnabled3, out themeName2))
		{
			isEnabled = isEnabled3;
			if (isEnabled3)
			{
				theme = ParseThemeName(themeName2);
			}
		}
		else if (TryGetGtkHighContrast(out isEnabled4, out themeName3))
		{
			isEnabled = isEnabled4;
			if (isEnabled4)
			{
				theme = ParseThemeName(themeName3);
			}
		}
		else if (TryGetEnvironmentHighContrast(out isEnabled5))
		{
			isEnabled = isEnabled5;
			theme = HighContrastTheme.WhiteOnBlack;
		}
		UpdateHighContrast(isEnabled, theme);
	}

	private void UpdateHighContrast(bool isEnabled, HighContrastTheme theme)
	{
		if (_isHighContrastEnabled != isEnabled || _currentTheme != theme)
		{
			_isHighContrastEnabled = isEnabled;
			_currentTheme = theme;
			this.HighContrastChanged?.Invoke(this, new HighContrastChangedEventArgs(isEnabled, theme));
		}
	}

	private static bool TryGetGnomeHighContrast(out bool isEnabled, out string? themeName)
	{
		isEnabled = false;
		themeName = null;
		try
		{
			string text = RunCommand("gsettings", "get org.gnome.desktop.a11y.interface high-contrast");
			if (!string.IsNullOrEmpty(text))
			{
				isEnabled = text.Trim().ToLower() == "true";
			}
			text = RunCommand("gsettings", "get org.gnome.desktop.interface gtk-theme");
			if (!string.IsNullOrEmpty(text))
			{
				themeName = text.Trim().Trim('\'');
				if (!isEnabled && themeName != null)
				{
					string text2 = themeName.ToLower();
					isEnabled = text2.Contains("highcontrast") || text2.Contains("high-contrast") || text2.Contains("hc");
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryGetKdeHighContrast(out bool isEnabled, out string? themeName)
	{
		isEnabled = false;
		themeName = null;
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "kdeglobals");
			if (!File.Exists(path))
			{
				return false;
			}
			string[] array = File.ReadAllLines(path);
			foreach (string text in array)
			{
				if (text.StartsWith("ColorScheme="))
				{
					themeName = text.Substring("ColorScheme=".Length);
					string text2 = themeName.ToLower();
					isEnabled = text2.Contains("highcontrast") || text2.Contains("high-contrast") || text2.Contains("breeze-high-contrast");
					return true;
				}
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryGetGtkHighContrast(out bool isEnabled, out string? themeName)
	{
		isEnabled = false;
		themeName = null;
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "gtk-3.0", "settings.ini");
			if (!File.Exists(path))
			{
				path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "gtk-4.0", "settings.ini");
			}
			if (!File.Exists(path))
			{
				return false;
			}
			string[] array = File.ReadAllLines(path);
			foreach (string text in array)
			{
				if (text.StartsWith("gtk-theme-name="))
				{
					themeName = text.Substring("gtk-theme-name=".Length);
					string text2 = themeName.ToLower();
					isEnabled = text2.Contains("highcontrast") || text2.Contains("high-contrast");
					return true;
				}
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryGetEnvironmentHighContrast(out bool isEnabled)
	{
		isEnabled = false;
		string environmentVariable = Environment.GetEnvironmentVariable("GTK_THEME");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			string text = environmentVariable.ToLower();
			isEnabled = text.Contains("highcontrast") || text.Contains("high-contrast");
			if (isEnabled)
			{
				return true;
			}
		}
		string environmentVariable2 = Environment.GetEnvironmentVariable("GTK_A11Y");
		if (!(environmentVariable2?.ToLower() == "atspi"))
		{
			_ = environmentVariable2 == "1";
		}
		return isEnabled;
	}

	private static HighContrastTheme ParseThemeName(string? themeName)
	{
		if (string.IsNullOrEmpty(themeName))
		{
			return HighContrastTheme.WhiteOnBlack;
		}
		string text = themeName.ToLower();
		if (text.Contains("inverse") || text.Contains("dark") || text.Contains("white-on-black"))
		{
			return HighContrastTheme.WhiteOnBlack;
		}
		if (text.Contains("light") || text.Contains("black-on-white"))
		{
			return HighContrastTheme.BlackOnWhite;
		}
		return HighContrastTheme.WhiteOnBlack;
	}

	public HighContrastColors GetColors()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		return _currentTheme switch
		{
			HighContrastTheme.WhiteOnBlack => new HighContrastColors
			{
				Background = SKColors.Black,
				Foreground = SKColors.White,
				Accent = new SKColor((byte)0, byte.MaxValue, byte.MaxValue),
				Border = SKColors.White,
				Error = new SKColor(byte.MaxValue, (byte)100, (byte)100),
				Success = new SKColor((byte)100, byte.MaxValue, (byte)100),
				Warning = SKColors.Yellow,
				Link = new SKColor((byte)100, (byte)200, byte.MaxValue),
				LinkVisited = new SKColor((byte)200, (byte)150, byte.MaxValue),
				Selection = new SKColor((byte)0, (byte)120, (byte)215),
				SelectionText = SKColors.White,
				DisabledText = new SKColor((byte)160, (byte)160, (byte)160),
				DisabledBackground = new SKColor((byte)40, (byte)40, (byte)40)
			}, 
			HighContrastTheme.BlackOnWhite => new HighContrastColors
			{
				Background = SKColors.White,
				Foreground = SKColors.Black,
				Accent = new SKColor((byte)0, (byte)0, (byte)200),
				Border = SKColors.Black,
				Error = new SKColor((byte)180, (byte)0, (byte)0),
				Success = new SKColor((byte)0, (byte)130, (byte)0),
				Warning = new SKColor((byte)180, (byte)120, (byte)0),
				Link = new SKColor((byte)0, (byte)0, (byte)180),
				LinkVisited = new SKColor((byte)80, (byte)0, (byte)120),
				Selection = new SKColor((byte)0, (byte)120, (byte)215),
				SelectionText = SKColors.White,
				DisabledText = new SKColor((byte)100, (byte)100, (byte)100),
				DisabledBackground = new SKColor((byte)220, (byte)220, (byte)220)
			}, 
			_ => GetDefaultColors(), 
		};
	}

	private static HighContrastColors GetDefaultColors()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		return new HighContrastColors
		{
			Background = SKColors.White,
			Foreground = new SKColor((byte)33, (byte)33, (byte)33),
			Accent = new SKColor((byte)33, (byte)150, (byte)243),
			Border = new SKColor((byte)200, (byte)200, (byte)200),
			Error = new SKColor((byte)244, (byte)67, (byte)54),
			Success = new SKColor((byte)76, (byte)175, (byte)80),
			Warning = new SKColor(byte.MaxValue, (byte)152, (byte)0),
			Link = new SKColor((byte)33, (byte)150, (byte)243),
			LinkVisited = new SKColor((byte)156, (byte)39, (byte)176),
			Selection = new SKColor((byte)33, (byte)150, (byte)243),
			SelectionText = SKColors.White,
			DisabledText = new SKColor((byte)158, (byte)158, (byte)158),
			DisabledBackground = new SKColor((byte)238, (byte)238, (byte)238)
		};
	}

	public void ForceHighContrast(bool enabled, HighContrastTheme theme = HighContrastTheme.WhiteOnBlack)
	{
		UpdateHighContrast(enabled, theme);
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
}
