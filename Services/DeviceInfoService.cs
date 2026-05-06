using System;
using System.IO;
using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Platform.Linux.Services;

public class DeviceInfoService : IDeviceInfo
{
    private static readonly Lazy<DeviceInfoService> _instance = new Lazy<DeviceInfoService>(() => new DeviceInfoService());

    private string? _model;

    private string? _manufacturer;

    private string? _name;

    private string? _versionString;

    public static DeviceInfoService Instance => _instance.Value;

    public string Model => _model ?? "Linux Desktop";

    public string Manufacturer => _manufacturer ?? "Unknown";

    public string Name => _name ?? Environment.MachineName;

    public string VersionString => _versionString ?? Environment.OSVersion.VersionString;

    public Version Version
    {
        get
        {
            try
            {
                if (System.Version.TryParse(Environment.OSVersion.Version.ToString(), out Version? result))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("DeviceInfoService", "OS version parsing failed", ex);
            }
            return new Version(1, 0);
        }
    }

    public DevicePlatform Platform => DevicePlatform.Create("Linux");

    public DeviceIdiom Idiom => DeviceIdiom.Desktop;

    public DeviceType DeviceType => DeviceType.Physical;

    public DeviceInfoService()
    {
        LoadDeviceInfo();
    }

    private void LoadDeviceInfo()
    {
        try
        {
            if (File.Exists("/sys/class/dmi/id/product_name"))
            {
                _model = File.ReadAllText("/sys/class/dmi/id/product_name").Trim();
            }
            if (File.Exists("/sys/class/dmi/id/sys_vendor"))
            {
                _manufacturer = File.ReadAllText("/sys/class/dmi/id/sys_vendor").Trim();
            }
            _name = Environment.MachineName;
            _versionString = Environment.OSVersion.VersionString;
        }
        catch
        {
            if (_model == null)
            {
                _model = "Linux Desktop";
            }
            if (_manufacturer == null)
            {
                _manufacturer = "Unknown";
            }
            if (_name == null)
            {
                _name = "localhost";
            }
            if (_versionString == null)
            {
                _versionString = "Linux";
            }
        }
    }
}
