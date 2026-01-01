using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

public class SystemThemeService
{
	private static SystemThemeService? _instance;

	private static readonly object _lock = new object();

	private FileSystemWatcher? _settingsWatcher;

	public static SystemThemeService Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_lock)
				{
					if (_instance == null)
					{
						_instance = new SystemThemeService();
					}
				}
			}
			return _instance;
		}
	}

	public SystemTheme CurrentTheme { get; private set; }

	public SKColor AccentColor { get; private set; } = new SKColor((byte)33, (byte)150, (byte)243);

	public DesktopEnvironment Desktop { get; private set; }

	public SystemColors Colors { get; private set; }

	public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

	private SystemThemeService()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		DetectDesktopEnvironment();
		DetectTheme();
		UpdateColors();
		SetupWatcher();
	}

	private void DetectDesktopEnvironment()
	{
		string text = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")?.ToLowerInvariant() ?? "";
		string text2 = Environment.GetEnvironmentVariable("DESKTOP_SESSION")?.ToLowerInvariant() ?? "";
		if (text.Contains("gnome") || text2.Contains("gnome"))
		{
			Desktop = DesktopEnvironment.GNOME;
		}
		else if (text.Contains("kde") || text.Contains("plasma") || text2.Contains("plasma"))
		{
			Desktop = DesktopEnvironment.KDE;
		}
		else if (text.Contains("xfce") || text2.Contains("xfce"))
		{
			Desktop = DesktopEnvironment.XFCE;
		}
		else if (text.Contains("mate") || text2.Contains("mate"))
		{
			Desktop = DesktopEnvironment.MATE;
		}
		else if (text.Contains("cinnamon") || text2.Contains("cinnamon"))
		{
			Desktop = DesktopEnvironment.Cinnamon;
		}
		else if (text.Contains("lxqt"))
		{
			Desktop = DesktopEnvironment.LXQt;
		}
		else if (text.Contains("lxde"))
		{
			Desktop = DesktopEnvironment.LXDE;
		}
		else
		{
			Desktop = DesktopEnvironment.Unknown;
		}
	}

	private void DetectTheme()
	{
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		CurrentTheme = (Desktop switch
		{
			DesktopEnvironment.GNOME => DetectGnomeTheme(), 
			DesktopEnvironment.KDE => DetectKdeTheme(), 
			DesktopEnvironment.XFCE => DetectXfceTheme(), 
			DesktopEnvironment.Cinnamon => DetectCinnamonTheme(), 
			_ => DetectGtkTheme(), 
		}).GetValueOrDefault();
		AccentColor = (SKColor)(Desktop switch
		{
			DesktopEnvironment.GNOME => GetGnomeAccentColor(), 
			DesktopEnvironment.KDE => GetKdeAccentColor(), 
			_ => new SKColor((byte)33, (byte)150, (byte)243), 
		});
	}

	private SystemTheme? DetectGnomeTheme()
	{
		try
		{
			string text = RunCommand("gsettings", "get org.gnome.desktop.interface color-scheme");
			if (text.Contains("prefer-dark"))
			{
				return SystemTheme.Dark;
			}
			if (text.Contains("prefer-light") || text.Contains("default"))
			{
				return SystemTheme.Light;
			}
			text = RunCommand("gsettings", "get org.gnome.desktop.interface gtk-theme");
			if (text.ToLowerInvariant().Contains("dark"))
			{
				return SystemTheme.Dark;
			}
		}
		catch
		{
		}
		return null;
	}

	private SystemTheme? DetectKdeTheme()
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "kdeglobals");
			if (File.Exists(path))
			{
				string text = File.ReadAllText(path);
				if (text.Contains("BreezeDark", StringComparison.OrdinalIgnoreCase) || text.Contains("Dark", StringComparison.OrdinalIgnoreCase))
				{
					return SystemTheme.Dark;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private SystemTheme? DetectXfceTheme()
	{
		try
		{
			if (RunCommand("xfconf-query", "-c xsettings -p /Net/ThemeName").ToLowerInvariant().Contains("dark"))
			{
				return SystemTheme.Dark;
			}
		}
		catch
		{
		}
		return DetectGtkTheme();
	}

	private SystemTheme? DetectCinnamonTheme()
	{
		try
		{
			if (RunCommand("gsettings", "get org.cinnamon.desktop.interface gtk-theme").ToLowerInvariant().Contains("dark"))
			{
				return SystemTheme.Dark;
			}
		}
		catch
		{
		}
		return null;
	}

	private SystemTheme? DetectGtkTheme()
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "gtk-3.0", "settings.ini");
			if (File.Exists(path))
			{
				string[] array = File.ReadAllText(path).Split('\n');
				foreach (string text in array)
				{
					if (text.StartsWith("gtk-theme-name=", StringComparison.OrdinalIgnoreCase) && text.Substring("gtk-theme-name=".Length).Trim().Contains("dark", StringComparison.OrdinalIgnoreCase))
					{
						return SystemTheme.Dark;
					}
					if (text.StartsWith("gtk-application-prefer-dark-theme=", StringComparison.OrdinalIgnoreCase))
					{
						string text2 = text.Substring("gtk-application-prefer-dark-theme=".Length).Trim();
						if (text2 == "1" || text2.Equals("true", StringComparison.OrdinalIgnoreCase))
						{
							return SystemTheme.Dark;
						}
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private SKColor GetGnomeAccentColor()
	{
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return (SKColor)(RunCommand("gsettings", "get org.gnome.desktop.interface accent-color").Trim().Trim('\'') switch
			{
				"blue" => new SKColor((byte)53, (byte)132, (byte)228), 
				"teal" => new SKColor((byte)42, (byte)195, (byte)222), 
				"green" => new SKColor((byte)58, (byte)148, (byte)74), 
				"yellow" => new SKColor((byte)246, (byte)211, (byte)45), 
				"orange" => new SKColor(byte.MaxValue, (byte)120, (byte)0), 
				"red" => new SKColor((byte)224, (byte)27, (byte)36), 
				"pink" => new SKColor((byte)214, (byte)86, (byte)140), 
				"purple" => new SKColor((byte)145, (byte)65, (byte)172), 
				"slate" => new SKColor((byte)94, (byte)92, (byte)100), 
				_ => new SKColor((byte)33, (byte)150, (byte)243), 
			});
		}
		catch
		{
			return new SKColor((byte)33, (byte)150, (byte)243);
		}
	}

	private SKColor GetKdeAccentColor()
	{
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "kdeglobals");
			if (File.Exists(path))
			{
				string[] array = File.ReadAllText(path).Split('\n');
				bool flag = false;
				string[] array2 = array;
				foreach (string text in array2)
				{
					if (text.StartsWith("[Colors:Header]"))
					{
						flag = true;
						continue;
					}
					if (text.StartsWith("[") && flag)
					{
						break;
					}
					if (flag && text.StartsWith("BackgroundNormal="))
					{
						string[] array3 = text.Substring("BackgroundNormal=".Length).Split(',');
						if (array3.Length >= 3 && byte.TryParse(array3[0], out var result) && byte.TryParse(array3[1], out var result2) && byte.TryParse(array3[2], out var result3))
						{
							return new SKColor(result, result2, result3);
						}
					}
				}
			}
		}
		catch
		{
		}
		return new SKColor((byte)33, (byte)150, (byte)243);
	}

	private void UpdateColors()
	{
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		Colors = ((CurrentTheme == SystemTheme.Dark) ? new SystemColors
		{
			Background = new SKColor((byte)30, (byte)30, (byte)30),
			Surface = new SKColor((byte)45, (byte)45, (byte)45),
			Primary = AccentColor,
			OnPrimary = SKColors.White,
			Text = new SKColor((byte)240, (byte)240, (byte)240),
			TextSecondary = new SKColor((byte)160, (byte)160, (byte)160),
			Border = new SKColor((byte)64, (byte)64, (byte)64),
			Divider = new SKColor((byte)58, (byte)58, (byte)58),
			Error = new SKColor((byte)207, (byte)102, (byte)121),
			Success = new SKColor((byte)129, (byte)201, (byte)149)
		} : new SystemColors
		{
			Background = new SKColor((byte)250, (byte)250, (byte)250),
			Surface = SKColors.White,
			Primary = AccentColor,
			OnPrimary = SKColors.White,
			Text = new SKColor((byte)33, (byte)33, (byte)33),
			TextSecondary = new SKColor((byte)117, (byte)117, (byte)117),
			Border = new SKColor((byte)224, (byte)224, (byte)224),
			Divider = new SKColor((byte)238, (byte)238, (byte)238),
			Error = new SKColor((byte)176, (byte)0, (byte)32),
			Success = new SKColor((byte)46, (byte)125, (byte)50)
		});
	}

	private void SetupWatcher()
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
			if (Directory.Exists(path))
			{
				_settingsWatcher = new FileSystemWatcher(path)
				{
					NotifyFilter = NotifyFilters.LastWrite,
					IncludeSubdirectories = true,
					EnableRaisingEvents = true
				};
				_settingsWatcher.Changed += OnSettingsChanged;
			}
		}
		catch
		{
		}
	}

	private void OnSettingsChanged(object sender, FileSystemEventArgs e)
	{
		string? name = e.Name;
		if (name == null || !name.Contains("kdeglobals"))
		{
			string? name2 = e.Name;
			if (name2 == null || !name2.Contains("gtk"))
			{
				string? name3 = e.Name;
				if (name3 == null || !name3.Contains("settings"))
				{
					return;
				}
			}
		}
		Task.Delay(500).ContinueWith(delegate
		{
			SystemTheme currentTheme = CurrentTheme;
			DetectTheme();
			UpdateColors();
			if (currentTheme != CurrentTheme)
			{
				this.ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(CurrentTheme));
			}
		});
	}

	private string RunCommand(string command, string arguments)
	{
		try
		{
			using Process process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = command,
					Arguments = arguments,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			string result = process.StandardOutput.ReadToEnd();
			process.WaitForExit(1000);
			return result;
		}
		catch
		{
			return "";
		}
	}

	public void RefreshTheme()
	{
		SystemTheme currentTheme = CurrentTheme;
		DetectTheme();
		UpdateColors();
		if (currentTheme != CurrentTheme)
		{
			this.ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(CurrentTheme));
		}
	}
}
