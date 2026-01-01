namespace Microsoft.Maui.Platform.Linux;

public class LinuxApplicationOptions
{
	public string? Title { get; set; } = "MAUI Application";

	public int Width { get; set; } = 800;

	public int Height { get; set; } = 600;

	public bool UseHardwareAcceleration { get; set; } = true;

	public DisplayServerType DisplayServer { get; set; }

	public bool ForceDemo { get; set; }

	public string? IconPath { get; set; }

	public bool UseGtk { get; set; }
}
