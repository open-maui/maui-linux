// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>Which window-system surface a <see cref="VulkanContext"/> is created for.</summary>
internal enum VulkanSurfaceKind
{
    /// <summary>No surface: offscreen rendering only (tests, diagnostics).</summary>
    Headless,
    /// <summary>VK_KHR_wayland_surface over a wl_display / wl_surface.</summary>
    Wayland,
    /// <summary>VK_KHR_xlib_surface over an Xlib Display* / Window.</summary>
    Xlib,
    /// <summary>VK_EXT_headless_surface: a real swapchain with no window (tests).</summary>
    HeadlessSurface,
}

/// <summary>A DRM device number (major:minor), as reported by VK_EXT_physical_device_drm and sysfs.</summary>
internal readonly record struct DrmDeviceId(long Major, long Minor)
{
    public override string ToString() => $"{Major}:{Minor}";

    /// <summary>Parses a sysfs <c>dev</c> file body ("226:1").</summary>
    public static DrmDeviceId? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Trim().Split(':');
        return parts.Length == 2 && long.TryParse(parts[0], out var major) && long.TryParse(parts[1], out var minor)
            ? new DrmDeviceId(major, minor)
            : null;
    }
}

/// <summary>What device selection needs to know about one physical device.</summary>
internal sealed record VulkanDeviceCandidate(
    int Index,
    string Name,
    uint DeviceType,
    bool CanPresent,
    DrmDeviceId? Primary = null,
    DrmDeviceId? Render = null)
{
    public string TypeName => DeviceType switch
    {
        Vk.VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU => "integrated",
        Vk.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU => "discrete",
        Vk.VK_PHYSICAL_DEVICE_TYPE_VIRTUAL_GPU => "virtual",
        Vk.VK_PHYSICAL_DEVICE_TYPE_CPU => "cpu",
        _ => "other",
    };
}

/// <summary>
/// Physical-device choice for the Vulkan render target. Pure logic, so the
/// policy is unit-testable without a GPU. Order:
/// <list type="number">
/// <item><c>OPENMAUI_VULKAN_DEVICE</c>: an enumeration index, a type
/// (<c>integrated</c>, <c>discrete</c>, <c>virtual</c>, <c>cpu</c>), a DRM node
/// (<c>renderD129</c>, <c>/dev/dri/card0</c>) or a case-insensitive substring
/// of the device name.</item>
/// <item>The device the compositor scans out from (matched by DRM major:minor
/// through VK_EXT_physical_device_drm), so buffers never cross GPUs.</item>
/// <item>An integrated GPU (hybrid laptops: the iGPU drives the panel and the
/// dGPU stays asleep).</item>
/// <item>Discrete, then virtual, then other; a CPU rasteriser (llvmpipe/lavapipe) last.</item>
/// </list>
/// Only devices that can present to the window (graphics queue with present
/// support) are candidates.
/// </summary>
internal static partial class VulkanDeviceSelector
{
    public const string EnvironmentVariable = "OPENMAUI_VULKAN_DEVICE";

    public static int Select(IReadOnlyList<VulkanDeviceCandidate> candidates, IReadOnlyCollection<DrmDeviceId> compositorDevices,
        string? overrideValue, out string reason)
    {
        var usable = candidates.Where(c => c.CanPresent).ToList();
        if (usable.Count == 0)
        {
            reason = candidates.Count == 0 ? "no Vulkan devices" : "no device can present to this window";
            return -1;
        }

        string? overrideNote = null;
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            var match = MatchOverride(usable, overrideValue.Trim());
            if (match != null)
            {
                reason = $"{EnvironmentVariable}={overrideValue.Trim()}";
                return match.Index;
            }
            overrideNote = $"{EnvironmentVariable}='{overrideValue.Trim()}' matched no presentable device; ";
        }

        if (compositorDevices.Count > 0)
        {
            var onCompositor = usable.FirstOrDefault(c =>
                (c.Primary is { } p && compositorDevices.Contains(p)) ||
                (c.Render is { } r && compositorDevices.Contains(r)));
            if (onCompositor != null)
            {
                reason = overrideNote + "compositor's DRM device";
                return onCompositor.Index;
            }
        }

        var ranked = usable
            .OrderBy(c => Rank(c.DeviceType))
            .ThenBy(c => c.Index)
            .First();
        reason = overrideNote + (ranked.DeviceType == Vk.VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU && usable.Count > 1
            ? "integrated GPU preferred"
            : $"{ranked.TypeName} GPU");
        return ranked.Index;
    }

    private static int Rank(uint type) => type switch
    {
        Vk.VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU => 0,
        Vk.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU => 1,
        Vk.VK_PHYSICAL_DEVICE_TYPE_VIRTUAL_GPU => 2,
        Vk.VK_PHYSICAL_DEVICE_TYPE_CPU => 4,
        _ => 3,
    };

    private static VulkanDeviceCandidate? MatchOverride(List<VulkanDeviceCandidate> usable, string value)
    {
        if (int.TryParse(value, out int index))
            return usable.FirstOrDefault(c => c.Index == index);

        switch (value.ToLowerInvariant())
        {
            case "integrated": return usable.FirstOrDefault(c => c.DeviceType == Vk.VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU);
            case "discrete": return usable.FirstOrDefault(c => c.DeviceType == Vk.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU);
            case "virtual": return usable.FirstOrDefault(c => c.DeviceType == Vk.VK_PHYSICAL_DEVICE_TYPE_VIRTUAL_GPU);
            case "cpu": return usable.FirstOrDefault(c => c.DeviceType == Vk.VK_PHYSICAL_DEVICE_TYPE_CPU);
        }

        // DRM node: renderD129 / card0, optionally with a /dev/dri/ prefix.
        var node = DrmNodeRegex().Match(value);
        if (node.Success)
        {
            long minor = long.Parse(node.Groups[2].Value);
            bool render = node.Groups[1].Value == "renderD";
            return usable.FirstOrDefault(c => render ? c.Render?.Minor == minor : c.Primary?.Minor == minor);
        }

        return usable.FirstOrDefault(c => c.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"^(?:/dev/dri/)?(renderD|card)(\d+)$")]
    private static partial Regex DrmNodeRegex();

    /// <summary>
    /// DRM devices the compositor most likely renders and scans out on: the
    /// primary node(s) of GPUs the firmware marked <c>boot_vga</c> (the GPU
    /// driving the display at boot; on hybrid laptops the iGPU). Wayland
    /// compositors and the X server pick that device by default. Empty when
    /// sysfs does not say (then selection falls through to device type).
    /// </summary>
    public static IReadOnlyCollection<DrmDeviceId> ProbeCompositorDevices(string sysfsDrm = "/sys/class/drm")
    {
        var result = new List<DrmDeviceId>();
        try
        {
            if (!Directory.Exists(sysfsDrm)) return result;
            foreach (var dir in Directory.GetDirectories(sysfsDrm))
            {
                var name = Path.GetFileName(dir);
                if (!CardRegex().IsMatch(name)) continue;
                var bootVga = ReadText(Path.Combine(dir, "device", "boot_vga"));
                if (bootVga?.Trim() != "1") continue;
                if (DrmDeviceId.Parse(ReadText(Path.Combine(dir, "dev"))) is { } id)
                    result.Add(id);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("VulkanDeviceSelector", $"boot_vga probe failed: {ex.Message}");
        }
        return result;
    }

    [GeneratedRegex(@"^card\d+$")]
    private static partial Regex CardRegex();

    private static string? ReadText(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch { return null; }
    }
}

/// <summary>
/// One Vulkan instance + physical device + logical device + graphics/present
/// queue, and the Skia <see cref="GRContext"/> over them. Owned by exactly one
/// <see cref="VulkanRenderTarget"/> (one device per window, like the EGL
/// targets' one context per window), or used headless by tests.
/// </summary>
internal sealed unsafe class VulkanContext : IDisposable
{
    private IntPtr _instance;
    private IntPtr _device;
    private ulong _surface;
    private void* _enabledFeatures;
    private GRVkExtensions? _grExtensions;
    private GRVkBackendContext? _backendContext;
    private bool _disposed;

    // Kept alive for the GRContext's lifetime: Skia resolves its function
    // table through this during creation and holds the proxy afterwards.
    private static readonly GRVkGetProcedureAddressDelegate s_getProc = GetProc;

    public IntPtr Instance => _instance;
    public IntPtr PhysicalDevice { get; private set; }
    public IntPtr Device => _device;
    public IntPtr Queue { get; private set; }
    public uint QueueFamily { get; private set; }
    public ulong Surface => _surface;
    public GRContext GrContext { get; private set; } = null!;

    public string DeviceName { get; private set; } = "unknown";
    public string DeviceTypeName { get; private set; } = "other";
    public string? DriverInfo { get; private set; }
    public uint DeviceApiVersion { get; private set; }
    public string SelectionReason { get; private set; } = string.Empty;

    /// <summary>"Intel(R) UHD Graphics (CML GT2); Mesa 26.1.5; Vulkan 1.4.354"</summary>
    public string Description =>
        $"{DeviceName}; {(string.IsNullOrEmpty(DriverInfo) ? "" : DriverInfo + "; ")}Vulkan {Vk.ApiMajor(DeviceApiVersion)}.{Vk.ApiMinor(DeviceApiVersion)}.{Vk.ApiPatch(DeviceApiVersion)}";

    private VulkanContext() { }

    /// <summary>
    /// Brings up instance, surface (unless headless), device and GRContext.
    /// Throws <see cref="InvalidOperationException"/> (or
    /// <see cref="DllNotFoundException"/> without a loader) on any failure,
    /// after releasing whatever was created.
    /// </summary>
    public static VulkanContext Create(VulkanSurfaceKind kind, IntPtr nativeDisplay = default, IntPtr nativeWindow = default)
    {
        var ctx = new VulkanContext();
        try
        {
            ctx.Initialize(kind, nativeDisplay, nativeWindow);
            return ctx;
        }
        catch
        {
            ctx.Dispose();
            throw;
        }
    }

    private void Initialize(VulkanSurfaceKind kind, IntPtr nativeDisplay, IntPtr nativeWindow)
    {
        HashSet<string> available;
        try
        {
            available = Vk.EnumerateInstanceExtensions();
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException($"{Vk.LibVulkan} (Vulkan loader) not found");
        }

        var instanceExtensions = new List<string>();
        if (kind != VulkanSurfaceKind.Headless)
        {
            instanceExtensions.Add(Vk.VK_KHR_SURFACE);
            instanceExtensions.Add(kind switch
            {
                VulkanSurfaceKind.Wayland => Vk.VK_KHR_WAYLAND_SURFACE,
                VulkanSurfaceKind.Xlib => Vk.VK_KHR_XLIB_SURFACE,
                _ => Vk.VK_EXT_HEADLESS_SURFACE,
            });
            var missing = instanceExtensions.Where(e => !available.Contains(e)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"Vulkan loader lacks {string.Join(", ", missing)}");
        }

        CreateInstance(instanceExtensions);

        if (kind == VulkanSurfaceKind.Wayland)
        {
            var info = new Vk.VkWaylandSurfaceCreateInfoKHR
            {
                sType = Vk.VK_STRUCTURE_TYPE_WAYLAND_SURFACE_CREATE_INFO_KHR,
                display = nativeDisplay,
                surface = nativeWindow,
            };
            ulong surface;
            Check(Vk.vkCreateWaylandSurfaceKHR(_instance, &info, null, &surface), "vkCreateWaylandSurfaceKHR");
            _surface = surface;
        }
        else if (kind == VulkanSurfaceKind.Xlib)
        {
            var info = new Vk.VkXlibSurfaceCreateInfoKHR
            {
                sType = Vk.VK_STRUCTURE_TYPE_XLIB_SURFACE_CREATE_INFO_KHR,
                dpy = nativeDisplay,
                window = (ulong)nativeWindow,
            };
            ulong surface;
            Check(Vk.vkCreateXlibSurfaceKHR(_instance, &info, null, &surface), "vkCreateXlibSurfaceKHR");
            _surface = surface;
        }

        else if (kind == VulkanSurfaceKind.HeadlessSurface)
        {
            var info = new Vk.VkHeadlessSurfaceCreateInfoEXT { sType = Vk.VK_STRUCTURE_TYPE_HEADLESS_SURFACE_CREATE_INFO_EXT };
            ulong surface;
            Check(Vk.vkCreateHeadlessSurfaceEXT(_instance, &info, &surface), "vkCreateHeadlessSurfaceEXT");
            _surface = surface;
        }

        var deviceExtensions = new List<string>();
        if (kind != VulkanSurfaceKind.Headless)
            deviceExtensions.Add(Vk.VK_KHR_SWAPCHAIN);

        var devices = EnumerateDevices(deviceExtensions, out var candidates, out var queueFamilies, out var infos);
        int chosen = VulkanDeviceSelector.Select(candidates,
            kind is VulkanSurfaceKind.Headless or VulkanSurfaceKind.HeadlessSurface ? Array.Empty<DrmDeviceId>() : VulkanDeviceSelector.ProbeCompositorDevices(),
            Environment.GetEnvironmentVariable(VulkanDeviceSelector.EnvironmentVariable),
            out var reason);
        if (chosen < 0)
            throw new InvalidOperationException($"No usable Vulkan device ({reason}; found: {string.Join(", ", candidates.Select(c => c.Name))})");

        PhysicalDevice = devices[chosen];
        QueueFamily = queueFamilies[chosen];
        DeviceName = candidates[chosen].Name;
        DeviceTypeName = candidates[chosen].TypeName;
        DeviceApiVersion = infos[chosen].ApiVersion;
        DriverInfo = infos[chosen].Driver;
        SelectionReason = reason;
        DiagnosticLog.Debug("VulkanContext",
            $"Selected [{chosen}] {DeviceName} ({DeviceTypeName}; {reason}) from {string.Join(", ", candidates.Select(c => $"[{c.Index}] {c.Name}{(c.CanPresent ? "" : " (cannot present)")}"))}");

        CreateDevice(deviceExtensions);
        CreateGrContext(instanceExtensions, deviceExtensions);
    }

    private void CreateInstance(List<string> extensions)
    {
        var appName = Marshal.StringToCoTaskMemUTF8("OpenMaui");
        var extArray = Vk.AllocStringArray(extensions);
        try
        {
            var app = new Vk.VkApplicationInfo
            {
                sType = Vk.VK_STRUCTURE_TYPE_APPLICATION_INFO,
                pApplicationName = (byte*)appName,
                pEngineName = (byte*)appName,
                apiVersion = Vk.VK_API_VERSION_1_1,
            };
            var info = new Vk.VkInstanceCreateInfo
            {
                sType = Vk.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                pApplicationInfo = &app,
                enabledExtensionCount = (uint)extensions.Count,
                ppEnabledExtensionNames = extArray,
            };
            IntPtr instance;
            Check(Vk.vkCreateInstance(&info, null, &instance), "vkCreateInstance");
            _instance = instance;
        }
        finally
        {
            Vk.FreeStringArray(extArray, extensions.Count);
            Marshal.FreeCoTaskMem(appName);
        }
    }

    private readonly record struct DeviceInfo(uint ApiVersion, string? Driver);

    private IntPtr[] EnumerateDevices(List<string> requiredExtensions,
        out List<VulkanDeviceCandidate> candidates, out uint[] queueFamilies, out DeviceInfo[] infos)
    {
        uint count = 0;
        Check(Vk.vkEnumeratePhysicalDevices(_instance, &count, null), "vkEnumeratePhysicalDevices");
        var devices = new IntPtr[count];
        if (count > 0)
        {
            fixed (IntPtr* p = devices)
            {
                int r = Vk.vkEnumeratePhysicalDevices(_instance, &count, p);
                if (r < 0) Check(r, "vkEnumeratePhysicalDevices");
            }
        }

        candidates = new List<VulkanDeviceCandidate>((int)count);
        queueFamilies = new uint[count];
        infos = new DeviceInfo[count];
        var props = stackalloc byte[Vk.PhysicalDevicePropertiesSize + 16];

        for (int i = 0; i < count; i++)
        {
            var dev = devices[i];
            var exts = Vk.EnumerateDeviceExtensions(dev);

            new Span<byte>(props, Vk.PhysicalDevicePropertiesSize + 16).Clear();
            Vk.vkGetPhysicalDeviceProperties(dev, props);
            var header = (Vk.VkPhysicalDevicePropertiesHeader*)props;
            string name = Marshal.PtrToStringUTF8((IntPtr)header->deviceName) ?? $"device {i}";
            uint apiVersion = header->apiVersion;
            uint type = header->deviceType;

            DrmDeviceId? primary = null, render = null;
            string? driver = null;
            if (apiVersion >= Vk.VK_API_VERSION_1_1)
                QueryExtendedProperties(dev, apiVersion, exts.Contains(Vk.VK_EXT_PHYSICAL_DEVICE_DRM), out primary, out render, out driver);

            // Skia needs Vulkan 1.1 (we create a 1.1 instance and hand Skia 1.1).
            bool usable = apiVersion >= Vk.VK_API_VERSION_1_1 && requiredExtensions.All(exts.Contains);
            uint family = usable ? FindQueueFamily(dev) : uint.MaxValue;
            queueFamilies[i] = family;
            infos[i] = new DeviceInfo(apiVersion, driver);
            candidates.Add(new VulkanDeviceCandidate(i, name, type, family != uint.MaxValue, primary, render));
        }

        return devices;
    }

    private static void QueryExtendedProperties(IntPtr dev, uint apiVersion, bool hasDrm,
        out DrmDeviceId? primary, out DrmDeviceId? render, out string? driver)
    {
        primary = render = null;
        driver = null;

        // VkPhysicalDeviceProperties2 { sType, pNext, VkPhysicalDeviceProperties }
        var props2 = stackalloc byte[16 + Vk.PhysicalDevicePropertiesSize];
        new Span<byte>(props2, 16 + Vk.PhysicalDevicePropertiesSize).Clear();
        *(uint*)props2 = Vk.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2;

        var drm = new Vk.VkPhysicalDeviceDrmPropertiesEXT { sType = Vk.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DRM_PROPERTIES_EXT };

        // VkPhysicalDeviceDriverProperties (core 1.2): sType, pNext, driverID,
        // driverName[256], driverInfo[256], conformanceVersion.
        const int DriverPropsSize = 4 + 4 + 8 + 4 + 256 + 256 + 4 + 4;
        var driverProps = stackalloc byte[DriverPropsSize];
        new Span<byte>(driverProps, DriverPropsSize).Clear();
        *(uint*)driverProps = 1000196000; // VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DRIVER_PROPERTIES

        void** tail = (void**)(props2 + 8);
        if (hasDrm)
        {
            *tail = &drm;
            tail = &drm.pNext;
        }
        if (apiVersion >= Vk.MakeApiVersion(1, 2, 0))
            *tail = driverProps;

        Vk.vkGetPhysicalDeviceProperties2(dev, props2);

        if (hasDrm)
        {
            if (drm.hasPrimary != 0) primary = new DrmDeviceId(drm.primaryMajor, drm.primaryMinor);
            if (drm.hasRender != 0) render = new DrmDeviceId(drm.renderMajor, drm.renderMinor);
        }
        if (apiVersion >= Vk.MakeApiVersion(1, 2, 0))
        {
            // driverName at 20, driverInfo at 276.
            var driverName = Marshal.PtrToStringUTF8((IntPtr)(driverProps + 20));
            var driverInfo = Marshal.PtrToStringUTF8((IntPtr)(driverProps + 276));
            driver = FormatDriver(driverName, driverInfo);
        }
    }

    /// <summary>"Intel open-source Mesa driver" + "Mesa 26.1.5" → "Mesa 26.1.5"; "NVIDIA" + "610.43.03" → "NVIDIA 610.43.03".</summary>
    internal static string? FormatDriver(string? name, string? info)
    {
        name = name?.Trim();
        info = info?.Trim();
        if (string.IsNullOrEmpty(info)) return string.IsNullOrEmpty(name) ? null : name;
        if (string.IsNullOrEmpty(name) || info.Contains(name, StringComparison.OrdinalIgnoreCase)) return info;
        if (info.StartsWith("Mesa", StringComparison.Ordinal)) return info;
        return $"{name} {info}";
    }

    /// <summary>Graphics queue family that can also present to the surface (headless: any graphics family).</summary>
    private uint FindQueueFamily(IntPtr dev)
    {
        uint count = 0;
        Vk.vkGetPhysicalDeviceQueueFamilyProperties(dev, &count, null);
        if (count == 0) return uint.MaxValue;
        var families = stackalloc Vk.VkQueueFamilyProperties[(int)count];
        Vk.vkGetPhysicalDeviceQueueFamilyProperties(dev, &count, families);
        for (uint f = 0; f < count; f++)
        {
            if ((families[f].queueFlags & Vk.VK_QUEUE_GRAPHICS_BIT) == 0 || families[f].queueCount == 0)
                continue;
            if (_surface == 0)
                return f;
            uint supported = 0;
            if (Vk.vkGetPhysicalDeviceSurfaceSupportKHR(dev, f, _surface, &supported) == Vk.VK_SUCCESS && supported != 0)
                return f;
        }
        return uint.MaxValue;
    }

    private void CreateDevice(List<string> extensions)
    {
        // Enable only the optional features Skia benefits from; zero the rest
        // (robustBufferAccess and friends cost performance for nothing here).
        var supported = stackalloc uint[Vk.PhysicalDeviceFeaturesSize / 4];
        Vk.vkGetPhysicalDeviceFeatures(PhysicalDevice, supported);
        _enabledFeatures = NativeMemory.AllocZeroed(Vk.PhysicalDeviceFeaturesSize);
        var enabled = (uint*)_enabledFeatures;
        enabled[Vk.FeatureDualSrcBlend] = supported[Vk.FeatureDualSrcBlend];
        enabled[Vk.FeatureSampleRateShading] = supported[Vk.FeatureSampleRateShading];

        float priority = 1f;
        var queueInfo = new Vk.VkDeviceQueueCreateInfo
        {
            sType = Vk.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
            queueFamilyIndex = QueueFamily,
            queueCount = 1,
            pQueuePriorities = &priority,
        };
        var extArray = Vk.AllocStringArray(extensions);
        try
        {
            var info = new Vk.VkDeviceCreateInfo
            {
                sType = Vk.VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
                queueCreateInfoCount = 1,
                pQueueCreateInfos = &queueInfo,
                enabledExtensionCount = (uint)extensions.Count,
                ppEnabledExtensionNames = extArray,
                pEnabledFeatures = _enabledFeatures,
            };
            IntPtr device;
            Check(Vk.vkCreateDevice(PhysicalDevice, &info, null, &device), "vkCreateDevice");
            _device = device;
        }
        finally
        {
            Vk.FreeStringArray(extArray, extensions.Count);
        }

        IntPtr queue;
        Vk.vkGetDeviceQueue(_device, QueueFamily, 0, &queue);
        Queue = queue;
    }

    private void CreateGrContext(List<string> instanceExtensions, List<string> deviceExtensions)
    {
        _grExtensions = GRVkExtensions.Create(s_getProc, _instance, PhysicalDevice,
            instanceExtensions.ToArray(), deviceExtensions.ToArray());

        _backendContext = new GRVkBackendContext
        {
            VkInstance = _instance,
            VkPhysicalDevice = PhysicalDevice,
            VkDevice = _device,
            VkQueue = Queue,
            GraphicsQueueIndex = QueueFamily,
            MaxAPIVersion = Vk.VK_API_VERSION_1_1,
            Extensions = _grExtensions,
            VkPhysicalDeviceFeatures = (IntPtr)_enabledFeatures,
            GetProcedureAddress = s_getProc,
        };

        GrContext = GRContext.CreateVulkan(_backendContext)
            ?? throw new InvalidOperationException("GRContext.CreateVulkan returned null (Skia rejected the device)");
    }

    private static IntPtr GetProc(string name, IntPtr instance, IntPtr device)
        => device != IntPtr.Zero ? Vk.vkGetDeviceProcAddr(device, name) : Vk.vkGetInstanceProcAddr(instance, name);

    internal static void Check(int result, string call)
    {
        if (result != Vk.VK_SUCCESS)
            throw new InvalidOperationException($"{call} failed: {Vk.ResultName(result)}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_device != IntPtr.Zero)
            Vk.vkDeviceWaitIdle(_device);

        // Skia first: its GrVkGpu owns command pools, memory and pipelines on the device.
        GrContext?.Dispose();
        GrContext = null!;
        _backendContext?.Dispose();
        _backendContext = null;
        _grExtensions?.Dispose();
        _grExtensions = null;

        if (_device != IntPtr.Zero)
        {
            Vk.vkDestroyDevice(_device, null);
            _device = IntPtr.Zero;
        }
        if (_surface != 0)
        {
            Vk.vkDestroySurfaceKHR(_instance, _surface, null);
            _surface = 0;
        }
        if (_instance != IntPtr.Zero)
        {
            Vk.vkDestroyInstance(_instance, null);
            _instance = IntPtr.Zero;
        }
        if (_enabledFeatures != null)
        {
            NativeMemory.Free(_enabledFeatures);
            _enabledFeatures = null;
        }
    }
}
