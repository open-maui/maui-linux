// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// GPU render target over Vulkan WSI: a VkSurfaceKHR on the window's
/// wl_surface (VK_KHR_wayland_surface) or X11 window (VK_KHR_xlib_surface), a
/// swapchain, and a Skia <see cref="GRContext"/> from Skia's Vulkan backend
/// that draws straight into the acquired swapchain image. Opt-in with
/// <c>OPENMAUI_RENDERER=vulkan</c> or <see cref="RendererPreference.Vulkan"/>.
/// </summary>
/// <remarks>
/// <para>
/// Frame protocol. SkiaSharp does not expose Skia's flush-with-semaphores or
/// mutable-state (layout) API, so the target wraps Skia's own queue
/// submissions with two tiny pre-recorded command buffers per swapchain image:
/// </para>
/// <list type="number">
/// <item><see cref="BeginFrame"/>: wait for the frame slot's fence, acquire an
/// image (signalling the slot's acquire semaphore), then submit the image's
/// "pre" buffer, which waits on that semaphore and transitions the image
/// UNDEFINED → COLOR_ATTACHMENT_OPTIMAL (contents are discarded: the target
/// does not preserve contents, the engine repaints fully).</item>
/// <item>The engine draws on the image's <see cref="SKSurface"/>, which Skia
/// wrapped with ImageLayout = COLOR_ATTACHMENT_OPTIMAL.</item>
/// <item><see cref="EndFrame"/>: Skia flushes and submits; then the "post"
/// buffer transitions COLOR_ATTACHMENT_OPTIMAL → PRESENT_SRC_KHR, signals the
/// image's render-done semaphore and the slot's fence; vkQueuePresentKHR
/// waits on render-done.</item>
/// </list>
/// <para>
/// Pipeline barriers and semaphore signals order against everything earlier
/// in queue submission order, so Skia's command buffers (submitted between
/// "pre" and "post" on the same queue) are covered. Skia ends every frame
/// with the image in COLOR_ATTACHMENT_OPTIMAL because the swapchain images are
/// created without INPUT_ATTACHMENT usage (no GENERAL-layout dst reads) and a
/// frame always ends with a draw, never a copy or readback. That keeps Skia's
/// tracked layout and the real layout in agreement across frames.
/// </para>
/// </remarks>
public sealed unsafe class VulkanRenderTarget : IRenderTarget
{
    /// <summary>Optional present-mode override: <c>mailbox</c>, <c>immediate</c>, <c>fifo</c>, <c>fifo-relaxed</c>.</summary>
    public const string PresentModeEnvironmentVariable = "OPENMAUI_VULKAN_PRESENT_MODE";

    private const int FramesInFlight = 2;

    // Acquire timeout: never block the UI thread indefinitely (a hidden
    // Wayland surface in FIFO mode can withhold images forever).
    private const ulong AcquireTimeoutNs = 100_000_000UL;
    private const ulong FenceTimeoutNs = 1_000_000_000UL;

    private readonly VulkanContext _vk;

    private ulong _swapchain;
    private uint _format;
    private SKColorType _colorType;
    private uint _imageUsage;
    private int _extentWidth;
    private int _extentHeight;
    private ulong[] _images = Array.Empty<ulong>();
    private ulong[] _renderDone = Array.Empty<ulong>();
    private IntPtr[] _preBuffers = Array.Empty<IntPtr>();
    private IntPtr[] _postBuffers = Array.Empty<IntPtr>();
    private SKSurface?[] _surfaces = Array.Empty<SKSurface?>();
    private GRBackendRenderTarget?[] _backendTargets = Array.Empty<GRBackendRenderTarget?>();

    private ulong _commandPool;
    private readonly ulong[] _acquireSemaphores = new ulong[FramesInFlight];
    private readonly ulong[] _fences = new ulong[FramesInFlight];
    private int _slot;

    private int _width;
    private int _height;
    private bool _needsRecreate = true;
    private bool _frameActive;
    private uint _imageIndex;
    private bool _disposed;
    private bool _presentErrorLogged;
    private bool _acquireErrorLogged;

    public string Name { get; }
    public bool IsGpuAccelerated => true;

    /// <summary>Swapchain images come back with undefined contents; the engine repaints fully.</summary>
    public bool PreservesContents => false;

    public int Width => _width;
    public int Height => _height;

    /// <summary>Selected device name, e.g. "Intel(R) UHD Graphics (CML GT2)".</summary>
    public string DeviceName => _vk.DeviceName;

    /// <summary>Device, driver and Vulkan version, e.g. "Intel(R) UHD Graphics (CML GT2); Mesa 26.1.5; Vulkan 1.4.354".</summary>
    public string DeviceDescription => _vk.Description;

    /// <summary>Why this device was picked (override, compositor device, integrated, ...).</summary>
    public string SelectionReason => _vk.SelectionReason;

    /// <summary>Negotiated present mode: mailbox, immediate, fifo or fifo-relaxed.</summary>
    public string PresentMode { get; private set; } = "unknown";

    /// <summary>Swapchain image count.</summary>
    public int ImageCount => _images.Length;

    private uint _presentMode;

    private VulkanRenderTarget(string name, VulkanContext vk, int width, int height)
    {
        Name = name;
        _vk = vk;
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
    }

    /// <summary>
    /// Vulkan over a native Wayland surface. The caller must already have set
    /// the window to external presentation (its wl_shm buffer released); the
    /// swapchain owns attach/damage/commit from then on, and the
    /// wp_viewporter destination the window maintains keeps mapping the
    /// physical-size buffer to the logical size.
    /// </summary>
    public static VulkanRenderTarget CreateWayland(IWaylandSurface surface, int width, int height)
    {
        var vk = VulkanContext.Create(VulkanSurfaceKind.Wayland, surface.Display, surface.Surface);
        return Finish(new VulkanRenderTarget("vulkan-wayland", vk, width, height));
    }

    /// <summary>Vulkan over an X11 window (VK_KHR_xlib_surface on the window's own Display*).</summary>
    public static VulkanRenderTarget CreateX11(IX11Surface surface, int width, int height)
    {
        var vk = VulkanContext.Create(VulkanSurfaceKind.Xlib, surface.Display, surface.Handle);
        return Finish(new VulkanRenderTarget("vulkan-x11", vk, width, height));
    }

    /// <summary>
    /// A real swapchain on a VK_EXT_headless_surface (no window): exercises
    /// acquire / barriers / present / resize in tests without a display.
    /// </summary>
    internal static VulkanRenderTarget CreateHeadlessSurface(int width, int height)
    {
        var vk = VulkanContext.Create(VulkanSurfaceKind.HeadlessSurface);
        return Finish(new VulkanRenderTarget("vulkan-headless", vk, width, height));
    }

    private static VulkanRenderTarget Finish(VulkanRenderTarget target)
    {
        try
        {
            target.CreateSyncObjects();
            // Build the swapchain now so an unusable surface fails here (and
            // the factory falls back) rather than on the first frame.
            if (!target.RecreateSwapchain())
                throw new InvalidOperationException("Surface reports a zero extent; cannot create a swapchain");
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    #region Pure policy helpers (unit-tested)

    /// <summary>
    /// Present mode: the override if the surface supports it, else MAILBOX
    /// (no tearing, never blocks), IMMEDIATE, FIFO. This mirrors the EGL
    /// targets' eglSwapInterval(0): the run loop renders only dirty frames and
    /// must not stall in present while a Wayland surface is hidden.
    /// </summary>
    internal static uint ChoosePresentMode(IReadOnlyCollection<uint> available, string? overrideValue)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            uint? wanted = overrideValue.Trim().ToLowerInvariant() switch
            {
                "mailbox" => Vk.VK_PRESENT_MODE_MAILBOX_KHR,
                "immediate" => Vk.VK_PRESENT_MODE_IMMEDIATE_KHR,
                "fifo" or "vsync" => Vk.VK_PRESENT_MODE_FIFO_KHR,
                "fifo-relaxed" or "fifo_relaxed" => Vk.VK_PRESENT_MODE_FIFO_RELAXED_KHR,
                _ => null,
            };
            if (wanted is uint w && available.Contains(w))
                return w;
        }
        if (available.Contains(Vk.VK_PRESENT_MODE_MAILBOX_KHR)) return Vk.VK_PRESENT_MODE_MAILBOX_KHR;
        if (available.Contains(Vk.VK_PRESENT_MODE_IMMEDIATE_KHR)) return Vk.VK_PRESENT_MODE_IMMEDIATE_KHR;
        return Vk.VK_PRESENT_MODE_FIFO_KHR; // always supported
    }

    /// <summary>BGRA8 then RGBA8 UNORM in sRGB non-linear (what the raster and EGL paths produce); else the first 8-bit one.</summary>
    internal static (uint Format, uint ColorSpace, SKColorType ColorType)? ChooseSurfaceFormat(IReadOnlyList<(uint Format, uint ColorSpace)> formats)
    {
        foreach (var preferred in new[] { Vk.VK_FORMAT_B8G8R8A8_UNORM, Vk.VK_FORMAT_R8G8B8A8_UNORM })
        {
            foreach (var f in formats)
            {
                if (f.Format == preferred && f.ColorSpace == Vk.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR)
                    return (f.Format, f.ColorSpace, preferred == Vk.VK_FORMAT_B8G8R8A8_UNORM ? SKColorType.Bgra8888 : SKColorType.Rgba8888);
            }
        }
        return null;
    }

    /// <summary>Premultiplied alpha (what wl_shm ARGB8888 and the EGL ARGB config give), else opaque, inherit, post-multiplied.</summary>
    internal static uint ChooseCompositeAlpha(uint supported)
    {
        foreach (var flag in new[] { Vk.VK_COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR, Vk.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR,
                     Vk.VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR, Vk.VK_COMPOSITE_ALPHA_POST_MULTIPLIED_BIT_KHR })
        {
            if ((supported & flag) != 0) return flag;
        }
        return Vk.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;
    }

    /// <summary>
    /// Swapchain extent: the surface's current extent when it dictates one
    /// (X11), else the requested size clamped to the allowed range (Wayland
    /// reports 0xFFFFFFFF: the client picks). (0,0) means "cannot present now".
    /// </summary>
    internal static (int Width, int Height) ChooseExtent(in Vk.VkSurfaceCapabilitiesKHR caps, int width, int height)
    {
        if (caps.currentExtentWidth != uint.MaxValue)
            return ((int)caps.currentExtentWidth, (int)caps.currentExtentHeight);

        int w = (int)Math.Clamp((uint)Math.Max(1, width), caps.minImageExtentWidth, Math.Max(caps.minImageExtentWidth, caps.maxImageExtentWidth));
        int h = (int)Math.Clamp((uint)Math.Max(1, height), caps.minImageExtentHeight, Math.Max(caps.minImageExtentHeight, caps.maxImageExtentHeight));
        return (w, h);
    }

    /// <summary>One more than the minimum (so mailbox has a spare image), capped by the maximum (0 = unbounded).</summary>
    internal static uint ChooseImageCount(in Vk.VkSurfaceCapabilitiesKHR caps)
    {
        uint count = Math.Max(caps.minImageCount + 1, 3);
        if (caps.maxImageCount > 0)
            count = Math.Min(count, caps.maxImageCount);
        return Math.Max(count, caps.minImageCount);
    }

    #endregion

    #region Swapchain

    private void CreateSyncObjects()
    {
        var device = _vk.Device;
        var poolInfo = new Vk.VkCommandPoolCreateInfo
        {
            sType = Vk.VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            flags = Vk.VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT,
            queueFamilyIndex = _vk.QueueFamily,
        };
        ulong pool;
        VulkanContext.Check(Vk.vkCreateCommandPool(device, &poolInfo, null, &pool), "vkCreateCommandPool");
        _commandPool = pool;

        var semInfo = new Vk.VkSemaphoreCreateInfo { sType = Vk.VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO };
        var fenceInfo = new Vk.VkFenceCreateInfo { sType = Vk.VK_STRUCTURE_TYPE_FENCE_CREATE_INFO, flags = Vk.VK_FENCE_CREATE_SIGNALED_BIT };
        for (int i = 0; i < FramesInFlight; i++)
        {
            ulong sem, fence;
            VulkanContext.Check(Vk.vkCreateSemaphore(device, &semInfo, null, &sem), "vkCreateSemaphore");
            _acquireSemaphores[i] = sem;
            VulkanContext.Check(Vk.vkCreateFence(device, &fenceInfo, null, &fence), "vkCreateFence");
            _fences[i] = fence;
        }
    }

    /// <summary>
    /// (Re)creates the swapchain for the current size. Returns false when the
    /// surface currently has a zero extent (minimised); the caller skips the
    /// frame and retries later.
    /// </summary>
    private bool RecreateSwapchain()
    {
        var device = _vk.Device;
        var phys = _vk.PhysicalDevice;
        var surface = _vk.Surface;

        Vk.VkSurfaceCapabilitiesKHR caps;
        VulkanContext.Check(Vk.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(phys, surface, &caps), "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");
        var (extentW, extentH) = ChooseExtent(caps, _width, _height);
        if (extentW <= 0 || extentH <= 0)
            return false;

        if (_swapchain == 0)
        {
            // First creation: negotiate format, usage, alpha and present mode once.
            uint formatCount = 0;
            VulkanContext.Check(Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(phys, surface, &formatCount, null), "vkGetPhysicalDeviceSurfaceFormatsKHR");
            var formats = new Vk.VkSurfaceFormatKHR[formatCount];
            fixed (Vk.VkSurfaceFormatKHR* pf = formats)
                Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(phys, surface, &formatCount, pf);
            var chosen = ChooseSurfaceFormat(formats.Take((int)formatCount).Select(f => (f.format, f.colorSpace)).ToList())
                ?? throw new InvalidOperationException($"No 8-bit BGRA/RGBA sRGB swapchain format (surface offers {string.Join(",", formats.Select(f => f.format))})");
            _format = chosen.Format;
            _colorType = chosen.ColorType;

            uint modeCount = 0;
            VulkanContext.Check(Vk.vkGetPhysicalDeviceSurfacePresentModesKHR(phys, surface, &modeCount, null), "vkGetPhysicalDeviceSurfacePresentModesKHR");
            var modes = new uint[modeCount];
            fixed (uint* pm = modes)
                Vk.vkGetPhysicalDeviceSurfacePresentModesKHR(phys, surface, &modeCount, pm);
            _presentMode = ChoosePresentMode(modes.Take((int)modeCount).ToList(), Environment.GetEnvironmentVariable(PresentModeEnvironmentVariable));
            PresentMode = Vk.PresentModeName(_presentMode);

            // Color attachment is mandatory for a swapchain; transfer lets
            // Skia copy for dst reads. No INPUT_ATTACHMENT on purpose (see remarks).
            _imageUsage = Vk.VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT
                | (caps.supportedUsageFlags & (Vk.VK_IMAGE_USAGE_TRANSFER_SRC_BIT | Vk.VK_IMAGE_USAGE_TRANSFER_DST_BIT));
        }

        // Everything touching the old images must be finished before they go.
        Vk.vkDeviceWaitIdle(device);
        DestroyPerImageResources();

        var info = new Vk.VkSwapchainCreateInfoKHR
        {
            sType = Vk.VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
            surface = surface,
            minImageCount = ChooseImageCount(caps),
            imageFormat = _format,
            imageColorSpace = Vk.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR,
            imageExtentWidth = (uint)extentW,
            imageExtentHeight = (uint)extentH,
            imageArrayLayers = 1,
            imageUsage = _imageUsage,
            imageSharingMode = Vk.VK_SHARING_MODE_EXCLUSIVE,
            preTransform = (caps.supportedTransforms & Vk.VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR) != 0
                ? Vk.VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR
                : caps.currentTransform,
            compositeAlpha = ChooseCompositeAlpha(caps.supportedCompositeAlpha),
            presentMode = _presentMode,
            clipped = Vk.VK_TRUE,
            oldSwapchain = _swapchain,
        };
        ulong swapchain;
        int result = Vk.vkCreateSwapchainKHR(device, &info, null, &swapchain);
        if (_swapchain != 0)
        {
            Vk.vkDestroySwapchainKHR(device, _swapchain, null);
            _swapchain = 0;
        }
        VulkanContext.Check(result, "vkCreateSwapchainKHR");
        _swapchain = swapchain;
        _extentWidth = extentW;
        _extentHeight = extentH;

        uint imageCount = 0;
        VulkanContext.Check(Vk.vkGetSwapchainImagesKHR(device, _swapchain, &imageCount, null), "vkGetSwapchainImagesKHR");
        _images = new ulong[imageCount];
        fixed (ulong* pi = _images)
            VulkanContext.Check(Vk.vkGetSwapchainImagesKHR(device, _swapchain, &imageCount, pi), "vkGetSwapchainImagesKHR");

        CreatePerImageResources();
        _needsRecreate = false;
        DiagnosticLog.Debug("VulkanRenderTarget", $"Swapchain {extentW}x{extentH}, {imageCount} images, {PresentMode}, format {_format}");
        return true;
    }

    private void CreatePerImageResources()
    {
        var device = _vk.Device;
        int n = _images.Length;
        _renderDone = new ulong[n];
        _preBuffers = new IntPtr[n];
        _postBuffers = new IntPtr[n];
        _surfaces = new SKSurface?[n];
        _backendTargets = new GRBackendRenderTarget?[n];

        var semInfo = new Vk.VkSemaphoreCreateInfo { sType = Vk.VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO };
        for (int i = 0; i < n; i++)
        {
            ulong sem;
            VulkanContext.Check(Vk.vkCreateSemaphore(device, &semInfo, null, &sem), "vkCreateSemaphore");
            _renderDone[i] = sem;
        }

        var buffers = new IntPtr[n * 2];
        var alloc = new Vk.VkCommandBufferAllocateInfo
        {
            sType = Vk.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
            commandPool = _commandPool,
            level = Vk.VK_COMMAND_BUFFER_LEVEL_PRIMARY,
            commandBufferCount = (uint)buffers.Length,
        };
        fixed (IntPtr* pb = buffers)
            VulkanContext.Check(Vk.vkAllocateCommandBuffers(device, &alloc, pb), "vkAllocateCommandBuffers");

        for (int i = 0; i < n; i++)
        {
            _preBuffers[i] = buffers[i * 2];
            _postBuffers[i] = buffers[i * 2 + 1];

            // Before Skia: discard + make it a colour attachment. Everything
            // later on the queue (Skia's render passes, copies) waits for it.
            RecordBarrier(_preBuffers[i], _images[i],
                Vk.VK_IMAGE_LAYOUT_UNDEFINED, Vk.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                srcAccess: 0,
                dstAccess: Vk.VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | Vk.VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT
                    | Vk.VK_ACCESS_TRANSFER_READ_BIT | Vk.VK_ACCESS_TRANSFER_WRITE_BIT | Vk.VK_ACCESS_SHADER_READ_BIT,
                srcStage: Vk.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT,
                dstStage: Vk.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT);

            // After Skia: make Skia's writes available and hand the image to the presentation engine.
            RecordBarrier(_postBuffers[i], _images[i],
                Vk.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, Vk.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
                srcAccess: Vk.VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT | Vk.VK_ACCESS_TRANSFER_WRITE_BIT,
                dstAccess: 0,
                srcStage: Vk.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT,
                dstStage: Vk.VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT);
        }
    }

    private void RecordBarrier(IntPtr cb, ulong image, uint oldLayout, uint newLayout,
        uint srcAccess, uint dstAccess, uint srcStage, uint dstStage)
    {
        var begin = new Vk.VkCommandBufferBeginInfo
        {
            sType = Vk.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
            // The same buffer may be re-submitted while an earlier submission
            // of it is still pending (fences are per frame slot, not per image).
            flags = Vk.VK_COMMAND_BUFFER_USAGE_SIMULTANEOUS_USE_BIT,
        };
        VulkanContext.Check(Vk.vkBeginCommandBuffer(cb, &begin), "vkBeginCommandBuffer");
        var barrier = new Vk.VkImageMemoryBarrier
        {
            sType = Vk.VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
            srcAccessMask = srcAccess,
            dstAccessMask = dstAccess,
            oldLayout = oldLayout,
            newLayout = newLayout,
            srcQueueFamilyIndex = Vk.VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vk.VK_QUEUE_FAMILY_IGNORED,
            image = image,
            aspectMask = Vk.VK_IMAGE_ASPECT_COLOR_BIT,
            levelCount = 1,
            layerCount = 1,
        };
        Vk.vkCmdPipelineBarrier(cb, srcStage, dstStage, 0, 0, null, 0, null, 1, &barrier);
        VulkanContext.Check(Vk.vkEndCommandBuffer(cb), "vkEndCommandBuffer");
    }

    private void DestroyPerImageResources()
    {
        var device = _vk.Device;
        bool hadSurfaces = false;
        for (int i = 0; i < _surfaces.Length; i++)
        {
            hadSurfaces |= _surfaces[i] != null;
            _surfaces[i]?.Dispose();
            _backendTargets[i]?.Dispose();
        }
        _surfaces = Array.Empty<SKSurface?>();
        _backendTargets = Array.Empty<GRBackendRenderTarget?>();

        // Let Skia retire its finished command buffers now, so the image
        // views and framebuffers it made for the wrapped images are released
        // before the swapchain images themselves go away.
        if (hadSurfaces)
            _vk.GrContext?.Flush(submit: true, synchronous: true);

        foreach (var sem in _renderDone)
            if (sem != 0) Vk.vkDestroySemaphore(device, sem, null);
        _renderDone = Array.Empty<ulong>();

        if (_preBuffers.Length > 0 && _commandPool != 0)
        {
            var all = _preBuffers.Concat(_postBuffers).Where(b => b != IntPtr.Zero).ToArray();
            fixed (IntPtr* pb = all)
                Vk.vkFreeCommandBuffers(device, _commandPool, (uint)all.Length, pb);
        }
        _preBuffers = Array.Empty<IntPtr>();
        _postBuffers = Array.Empty<IntPtr>();
        _images = Array.Empty<ulong>();
    }

    private SKSurface? SurfaceFor(uint index)
    {
        var surface = _surfaces[index];
        if (surface != null) return surface;

        var imageInfo = new GRVkImageInfo
        {
            Image = _images[index],
            ImageTiling = Vk.VK_IMAGE_TILING_OPTIMAL,
            ImageLayout = Vk.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            Format = _format,
            ImageUsageFlags = _imageUsage,
            SampleCount = 1,
            LevelCount = 1,
            CurrentQueueFamily = _vk.QueueFamily,
            SharingMode = Vk.VK_SHARING_MODE_EXCLUSIVE,
        };
        var backend = new GRBackendRenderTarget(_extentWidth, _extentHeight, imageInfo);
        surface = SKSurface.Create(_vk.GrContext, backend, GRSurfaceOrigin.TopLeft, _colorType);
        if (surface == null)
        {
            backend.Dispose();
            DiagnosticLog.Error("VulkanRenderTarget", "SKSurface.Create over a swapchain image failed");
            return null;
        }
        _backendTargets[index] = backend;
        _surfaces[index] = surface;
        return surface;
    }

    #endregion

    #region IRenderTarget

    public void Resize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (width == _width && height == _height) return;
        _width = width;
        _height = height;
        // Recreated lazily at the next BeginFrame, outside any active frame.
        _needsRecreate = true;
    }

    public SKCanvas? BeginFrame()
    {
        if (_disposed) return null;
        if (_frameActive)
            return _surfaces[_imageIndex]?.Canvas;

        if (_needsRecreate || _swapchain == 0)
        {
            if (!TryRecreate())
                return null;
        }

        var device = _vk.Device;
        ulong fence = _fences[_slot];
        int wait = Vk.vkWaitForFences(device, 1, &fence, Vk.VK_TRUE, FenceTimeoutNs);
        if (wait != Vk.VK_SUCCESS)
        {
            LogAcquireOnce($"vkWaitForFences: {Vk.ResultName(wait)}");
            return null;
        }

        uint index = 0;
        int acquire = Vk.vkAcquireNextImageKHR(device, _swapchain, AcquireTimeoutNs, _acquireSemaphores[_slot], 0, &index);
        if (acquire == Vk.VK_ERROR_OUT_OF_DATE_KHR)
        {
            // Resize raced the compositor/X server: rebuild and try once more.
            if (!TryRecreate())
                return null;
            acquire = Vk.vkAcquireNextImageKHR(device, _swapchain, AcquireTimeoutNs, _acquireSemaphores[_slot], 0, &index);
        }
        if (acquire == Vk.VK_SUBOPTIMAL_KHR)
        {
            _needsRecreate = true; // usable this frame; rebuild before the next one
        }
        else if (acquire != Vk.VK_SUCCESS)
        {
            if (acquire != Vk.VK_TIMEOUT && acquire != Vk.VK_NOT_READY)
                LogAcquireOnce($"vkAcquireNextImageKHR: {Vk.ResultName(acquire)}");
            if (acquire == Vk.VK_ERROR_OUT_OF_DATE_KHR)
                _needsRecreate = true;
            return null;
        }

        // The image is ours and the acquire semaphore will be signalled:
        // from here on the frame must be submitted.
        Vk.vkResetFences(device, 1, &fence);
        _imageIndex = index;

        ulong acquireSem = _acquireSemaphores[_slot];
        uint waitStage = Vk.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT;
        IntPtr pre = _preBuffers[index];
        var submit = new Vk.VkSubmitInfo
        {
            sType = Vk.VK_STRUCTURE_TYPE_SUBMIT_INFO,
            waitSemaphoreCount = 1,
            pWaitSemaphores = &acquireSem,
            pWaitDstStageMask = &waitStage,
            commandBufferCount = 1,
            pCommandBuffers = &pre,
        };
        int r = Vk.vkQueueSubmit(_vk.Queue, 1, &submit, 0);
        if (r != Vk.VK_SUCCESS)
        {
            LogPresentOnce($"vkQueueSubmit (acquire barrier): {Vk.ResultName(r)}");
            // Nothing was submitted, so the fence would never signal: restore it.
            RecoverSlotFence();
            return null;
        }

        _frameActive = true;
        var surface = SurfaceFor(index);
        if (surface == null)
        {
            // Still present the (undefined) image so the swapchain and the
            // slot's fence stay consistent.
            SubmitAndPresent();
            return null;
        }
        return surface.Canvas;
    }

    public void EndFrame()
    {
        if (_disposed || !_frameActive) return;

        var surface = _surfaces[_imageIndex];
        if (surface != null)
        {
            surface.Canvas.Flush();
            // Record and submit Skia's work now, so it precedes the "post" barrier in queue order.
            _vk.GrContext.Flush(submit: true, synchronous: false);
        }
        SubmitAndPresent();
    }

    private void SubmitAndPresent()
    {
        _frameActive = false;
        uint index = _imageIndex;
        ulong renderDone = _renderDone[index];
        IntPtr post = _postBuffers[index];
        var submit = new Vk.VkSubmitInfo
        {
            sType = Vk.VK_STRUCTURE_TYPE_SUBMIT_INFO,
            commandBufferCount = 1,
            pCommandBuffers = &post,
            signalSemaphoreCount = 1,
            pSignalSemaphores = &renderDone,
        };
        int r = Vk.vkQueueSubmit(_vk.Queue, 1, &submit, _fences[_slot]);
        if (r != Vk.VK_SUCCESS)
        {
            LogPresentOnce($"vkQueueSubmit (present barrier): {Vk.ResultName(r)}");
            RecoverSlotFence();
            _needsRecreate = true;
            _slot = (_slot + 1) % FramesInFlight;
            return;
        }

        ulong swapchain = _swapchain;
        int presentResult = 0;
        var present = new Vk.VkPresentInfoKHR
        {
            sType = Vk.VK_STRUCTURE_TYPE_PRESENT_INFO_KHR,
            waitSemaphoreCount = 1,
            pWaitSemaphores = &renderDone,
            swapchainCount = 1,
            pSwapchains = &swapchain,
            pImageIndices = &index,
            pResults = &presentResult,
        };
        int pr = Vk.vkQueuePresentKHR(_vk.Queue, &present);
        if (pr == Vk.VK_SUBOPTIMAL_KHR || pr == Vk.VK_ERROR_OUT_OF_DATE_KHR)
            _needsRecreate = true;
        else if (pr != Vk.VK_SUCCESS)
            LogPresentOnce($"vkQueuePresentKHR: {Vk.ResultName(pr)}");

        _slot = (_slot + 1) % FramesInFlight;
    }

    private bool TryRecreate()
    {
        try
        {
            return RecreateSwapchain();
        }
        catch (Exception ex)
        {
            LogPresentOnce($"swapchain recreation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// After a failed submit the slot's fence was reset but will never be
    /// signalled; wait for the device and recreate the fence signalled.
    /// </summary>
    private void RecoverSlotFence()
    {
        var device = _vk.Device;
        Vk.vkDeviceWaitIdle(device);
        Vk.vkDestroyFence(device, _fences[_slot], null);
        var fenceInfo = new Vk.VkFenceCreateInfo { sType = Vk.VK_STRUCTURE_TYPE_FENCE_CREATE_INFO, flags = Vk.VK_FENCE_CREATE_SIGNALED_BIT };
        ulong fence;
        Vk.vkCreateFence(device, &fenceInfo, null, &fence);
        _fences[_slot] = fence;
        _frameActive = false;
    }

    private void LogPresentOnce(string message)
    {
        if (_presentErrorLogged) return;
        _presentErrorLogged = true;
        DiagnosticLog.Error("VulkanRenderTarget", message);
    }

    private void LogAcquireOnce(string message)
    {
        if (_acquireErrorLogged) return;
        _acquireErrorLogged = true;
        DiagnosticLog.Warn("VulkanRenderTarget", message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var device = _vk.Device;
        if (device != IntPtr.Zero)
        {
            Vk.vkDeviceWaitIdle(device);
            DestroyPerImageResources();
            if (_swapchain != 0)
            {
                Vk.vkDestroySwapchainKHR(device, _swapchain, null);
                _swapchain = 0;
            }
            for (int i = 0; i < FramesInFlight; i++)
            {
                if (_acquireSemaphores[i] != 0) Vk.vkDestroySemaphore(device, _acquireSemaphores[i], null);
                if (_fences[i] != 0) Vk.vkDestroyFence(device, _fences[i], null);
                _acquireSemaphores[i] = 0;
                _fences[i] = 0;
            }
            if (_commandPool != 0)
            {
                Vk.vkDestroyCommandPool(device, _commandPool, null);
                _commandPool = 0;
            }
        }

        // Device, surface and instance go before the window closes its
        // display connection (the engine is disposed before the window).
        _vk.Dispose();
    }

    #endregion
}
