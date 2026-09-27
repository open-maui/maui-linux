// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Moq;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Maui.Controls.Linux.Tests.Rendering;

/// <summary>
/// Vulkan render target: selection/fallback policy (pure, no GPU) plus real
/// Vulkan tests that create a headless device and GRContext, draw and read
/// back, and drive a real swapchain on VK_EXT_headless_surface. The real
/// tests return early with a message when no Vulkan ICD is usable.
/// </summary>
public class VulkanRenderTargetTests
{
    private readonly ITestOutputHelper _output;

    public VulkanRenderTargetTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static T WithEnv<T>(string name, string? value, Func<T> action)
    {
        var previous = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }

    #region Preference / factory fallback

    [Theory]
    [InlineData("vulkan", RendererPreference.Auto, RendererPreference.Vulkan)]
    [InlineData("VK", RendererPreference.Raster, RendererPreference.Vulkan)]
    [InlineData(" Vulkan ", RendererPreference.Gpu, RendererPreference.Vulkan)]
    [InlineData("gpu", RendererPreference.Vulkan, RendererPreference.Gpu)]
    [InlineData("", RendererPreference.Vulkan, RendererPreference.Vulkan)]
    public void Environment_variable_selects_vulkan(string env, RendererPreference configured, RendererPreference expected)
    {
        WithEnv(RenderTargetFactory.EnvironmentVariable, env, () => RenderTargetFactory.ResolvePreference(configured))
            .Should().Be(expected);
    }

    [Fact]
    public void Auto_never_resolves_to_vulkan()
    {
        WithEnv(RenderTargetFactory.EnvironmentVariable, null, () => RenderTargetFactory.ResolvePreference(RendererPreference.Auto))
            .Should().Be(RendererPreference.Auto);
    }

    [Fact]
    public void Vulkan_request_on_a_window_without_native_surface_falls_back_to_raster()
    {
        var window = new Mock<IDisplayWindow>();
        window.SetupGet(w => w.Width).Returns(64);
        window.SetupGet(w => w.Height).Returns(48);

        using var target = WithEnv(RenderTargetFactory.EnvironmentVariable, null,
            () => RenderTargetFactory.Create(window.Object, RendererPreference.Vulkan));

        target.Should().BeOfType<RasterRenderTarget>();
        target.Width.Should().Be(64);
    }

    #endregion

    #region Device selection

    private static readonly DrmDeviceId IntelCard = new(226, 1);
    private static readonly DrmDeviceId IntelRender = new(226, 128);
    private static readonly DrmDeviceId NvidiaCard = new(226, 0);
    private static readonly DrmDeviceId NvidiaRender = new(226, 129);

    /// <summary>This box's enumeration order: Intel iGPU, NVIDIA dGPU, llvmpipe.</summary>
    private static List<VulkanDeviceCandidate> Hybrid(bool nvidiaFirst = false)
    {
        var intel = new VulkanDeviceCandidate(nvidiaFirst ? 1 : 0, "Intel(R) UHD Graphics (CML GT2)", Vk.VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU, true, IntelCard, IntelRender);
        var nvidia = new VulkanDeviceCandidate(nvidiaFirst ? 0 : 1, "NVIDIA GeForce RTX 2060 with Max-Q Design", Vk.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU, true, NvidiaCard, NvidiaRender);
        var cpu = new VulkanDeviceCandidate(2, "llvmpipe (LLVM 22.1.8, 256 bits)", Vk.VK_PHYSICAL_DEVICE_TYPE_CPU, true);
        return nvidiaFirst ? new() { nvidia, intel, cpu } : new() { intel, nvidia, cpu };
    }

    [Fact]
    public void Compositor_device_wins_over_type_preference()
    {
        int index = VulkanDeviceSelector.Select(Hybrid(), new[] { NvidiaCard }, null, out var reason);
        index.Should().Be(1);
        reason.Should().Contain("compositor");
    }

    [Fact]
    public void Compositor_device_matches_by_render_node_too()
    {
        VulkanDeviceSelector.Select(Hybrid(), new[] { IntelRender }, null, out _).Should().Be(0);
    }

    [Fact]
    public void Without_a_compositor_hint_integrated_is_preferred_even_when_enumerated_second()
    {
        int index = VulkanDeviceSelector.Select(Hybrid(nvidiaFirst: true), Array.Empty<DrmDeviceId>(), null, out var reason);
        index.Should().Be(1);
        reason.Should().Contain("integrated");
    }

    [Fact]
    public void Cpu_rasteriser_is_last_resort()
    {
        var list = new List<VulkanDeviceCandidate>
        {
            new(0, "llvmpipe", Vk.VK_PHYSICAL_DEVICE_TYPE_CPU, true),
            new(1, "Some GPU", Vk.VK_PHYSICAL_DEVICE_TYPE_OTHER, true),
        };
        VulkanDeviceSelector.Select(list, Array.Empty<DrmDeviceId>(), null, out _).Should().Be(1);

        VulkanDeviceSelector.Select(new List<VulkanDeviceCandidate> { list[0] }, Array.Empty<DrmDeviceId>(), null, out _)
            .Should().Be(0);
    }

    [Fact]
    public void Devices_that_cannot_present_are_never_selected()
    {
        var list = new List<VulkanDeviceCandidate>
        {
            new(0, "Intel", Vk.VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU, CanPresent: false, IntelCard, IntelRender),
            new(1, "NVIDIA", Vk.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU, CanPresent: true, NvidiaCard, NvidiaRender),
        };
        // Even the compositor hint and an explicit override cannot pick device 0.
        VulkanDeviceSelector.Select(list, new[] { IntelCard }, "0", out _).Should().Be(1);

        VulkanDeviceSelector.Select(new List<VulkanDeviceCandidate> { list[0] }, Array.Empty<DrmDeviceId>(), null, out var reason)
            .Should().Be(-1);
        reason.Should().Contain("present");
        VulkanDeviceSelector.Select(new List<VulkanDeviceCandidate>(), Array.Empty<DrmDeviceId>(), null, out _).Should().Be(-1);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("discrete", 1)]
    [InlineData("integrated", 0)]
    [InlineData("cpu", 2)]
    [InlineData("nvidia", 1)]
    [InlineData("UHD", 0)]
    [InlineData("renderD129", 1)]
    [InlineData("/dev/dri/renderD128", 0)]
    [InlineData("/dev/dri/card0", 1)]
    [InlineData("card1", 0)]
    public void Override_selects_by_index_type_node_or_name(string value, int expected)
    {
        // The compositor hint points at Intel; the override must still win.
        int index = VulkanDeviceSelector.Select(Hybrid(), new[] { IntelCard }, value, out var reason);
        index.Should().Be(expected);
        reason.Should().Contain(VulkanDeviceSelector.EnvironmentVariable);
    }

    [Fact]
    public void Unmatched_override_falls_through_and_says_so()
    {
        int index = VulkanDeviceSelector.Select(Hybrid(), Array.Empty<DrmDeviceId>(), "radeon", out var reason);
        index.Should().Be(0);
        reason.Should().Contain("matched no presentable device").And.Contain("integrated");
    }

    [Theory]
    [InlineData("226:1\n", 226L, 1L)]
    [InlineData(" 226:128 ", 226L, 128L)]
    public void DrmDeviceId_parses_sysfs_dev(string text, long major, long minor)
    {
        DrmDeviceId.Parse(text).Should().Be(new DrmDeviceId(major, minor));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("226")]
    [InlineData("a:b")]
    public void DrmDeviceId_rejects_garbage(string? text)
    {
        DrmDeviceId.Parse(text).Should().BeNull();
    }

    [Fact]
    public void Compositor_probe_reads_boot_vga_cards_only()
    {
        var root = Path.Combine(Path.GetTempPath(), "openmaui-drm-" + Guid.NewGuid().ToString("N"));
        try
        {
            void Card(string name, string dev, string? bootVga)
            {
                var dir = Path.Combine(root, name);
                Directory.CreateDirectory(Path.Combine(dir, "device"));
                File.WriteAllText(Path.Combine(dir, "dev"), dev + "\n");
                if (bootVga != null)
                    File.WriteAllText(Path.Combine(dir, "device", "boot_vga"), bootVga + "\n");
            }

            Card("card0", "226:0", "0");        // dGPU
            Card("card1", "226:1", "1");        // iGPU, drives the panel
            Card("card1-eDP-1", "226:9", "1");  // connector entry, not a card
            Card("renderD128", "226:128", null);

            VulkanDeviceSelector.ProbeCompositorDevices(root).Should().Equal(new DrmDeviceId(226, 1));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Compositor_probe_tolerates_missing_sysfs()
    {
        VulkanDeviceSelector.ProbeCompositorDevices("/nonexistent/openmaui/drm").Should().BeEmpty();
    }

    [Theory]
    [InlineData("Intel open-source Mesa driver", "Mesa 26.1.5", "Mesa 26.1.5")]
    [InlineData("NVIDIA", "610.43.03", "NVIDIA 610.43.03")]
    [InlineData("llvmpipe", "Mesa 26.1.5 (LLVM 22.1.8)", "Mesa 26.1.5 (LLVM 22.1.8)")]
    [InlineData("radv", "", "radv")]
    public void Driver_string_is_compact(string name, string info, string expected)
    {
        VulkanContext.FormatDriver(name, info).Should().Be(expected);
    }

    #endregion

    #region Swapchain policy

    [Fact]
    public void Present_mode_prefers_mailbox_then_immediate_then_fifo()
    {
        var all = new[] { Vk.VK_PRESENT_MODE_FIFO_KHR, Vk.VK_PRESENT_MODE_IMMEDIATE_KHR, Vk.VK_PRESENT_MODE_MAILBOX_KHR };
        VulkanRenderTarget.ChoosePresentMode(all, null).Should().Be(Vk.VK_PRESENT_MODE_MAILBOX_KHR);
        VulkanRenderTarget.ChoosePresentMode(new[] { Vk.VK_PRESENT_MODE_FIFO_KHR, Vk.VK_PRESENT_MODE_IMMEDIATE_KHR }, null)
            .Should().Be(Vk.VK_PRESENT_MODE_IMMEDIATE_KHR);
        VulkanRenderTarget.ChoosePresentMode(new[] { Vk.VK_PRESENT_MODE_FIFO_KHR }, null).Should().Be(Vk.VK_PRESENT_MODE_FIFO_KHR);
    }

    [Fact]
    public void Present_mode_override_applies_only_when_supported()
    {
        var all = new[] { Vk.VK_PRESENT_MODE_FIFO_KHR, Vk.VK_PRESENT_MODE_MAILBOX_KHR };
        VulkanRenderTarget.ChoosePresentMode(all, "fifo").Should().Be(Vk.VK_PRESENT_MODE_FIFO_KHR);
        VulkanRenderTarget.ChoosePresentMode(all, "vsync").Should().Be(Vk.VK_PRESENT_MODE_FIFO_KHR);
        VulkanRenderTarget.ChoosePresentMode(all, "immediate").Should().Be(Vk.VK_PRESENT_MODE_MAILBOX_KHR);
        VulkanRenderTarget.ChoosePresentMode(all, "bogus").Should().Be(Vk.VK_PRESENT_MODE_MAILBOX_KHR);
    }

    [Fact]
    public void Surface_format_prefers_bgra_unorm_srgb()
    {
        var formats = new List<(uint, uint)>
        {
            (50u /* B8G8R8A8_SRGB */, Vk.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR),
            (Vk.VK_FORMAT_R8G8B8A8_UNORM, Vk.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR),
            (Vk.VK_FORMAT_B8G8R8A8_UNORM, Vk.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR),
        };
        VulkanRenderTarget.ChooseSurfaceFormat(formats)!.Value.ColorType.Should().Be(SKColorType.Bgra8888);

        formats.RemoveAt(2);
        var rgba = VulkanRenderTarget.ChooseSurfaceFormat(formats)!.Value;
        rgba.Format.Should().Be(Vk.VK_FORMAT_R8G8B8A8_UNORM);
        rgba.ColorType.Should().Be(SKColorType.Rgba8888);

        VulkanRenderTarget.ChooseSurfaceFormat(new List<(uint, uint)> { (64u /* A2B10G10R10 */, 0u) }).Should().BeNull();
    }

    [Fact]
    public void Composite_alpha_prefers_premultiplied()
    {
        VulkanRenderTarget.ChooseCompositeAlpha(0xF).Should().Be(Vk.VK_COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR);
        VulkanRenderTarget.ChooseCompositeAlpha(Vk.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR | Vk.VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR)
            .Should().Be(Vk.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR);
        VulkanRenderTarget.ChooseCompositeAlpha(Vk.VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR).Should().Be(Vk.VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR);
    }

    [Fact]
    public void Extent_follows_the_window_on_wayland_and_the_surface_on_x11()
    {
        var wayland = new Vk.VkSurfaceCapabilitiesKHR
        {
            currentExtentWidth = uint.MaxValue, currentExtentHeight = uint.MaxValue,
            minImageExtentWidth = 1, minImageExtentHeight = 1,
            maxImageExtentWidth = 4096, maxImageExtentHeight = 4096,
        };
        VulkanRenderTarget.ChooseExtent(wayland, 1400, 1050).Should().Be((1400, 1050));
        VulkanRenderTarget.ChooseExtent(wayland, 9000, 0).Should().Be((4096, 1));

        var x11 = wayland with { currentExtentWidth = 800, currentExtentHeight = 600 };
        VulkanRenderTarget.ChooseExtent(x11, 1400, 1050).Should().Be((800, 600));

        var minimised = wayland with { currentExtentWidth = 0, currentExtentHeight = 0 };
        VulkanRenderTarget.ChooseExtent(minimised, 1400, 1050).Should().Be((0, 0));
    }

    [Fact]
    public void Image_count_leaves_a_spare_for_mailbox_within_limits()
    {
        VulkanRenderTarget.ChooseImageCount(new Vk.VkSurfaceCapabilitiesKHR { minImageCount = 2, maxImageCount = 0 }).Should().Be(3);
        VulkanRenderTarget.ChooseImageCount(new Vk.VkSurfaceCapabilitiesKHR { minImageCount = 3, maxImageCount = 8 }).Should().Be(4);
        VulkanRenderTarget.ChooseImageCount(new Vk.VkSurfaceCapabilitiesKHR { minImageCount = 2, maxImageCount = 2 }).Should().Be(2);
    }

    #endregion

    #region Real Vulkan (skips without an ICD)

    private VulkanContext? TryCreateHeadless()
    {
        try
        {
            return VulkanContext.Create(VulkanSurfaceKind.Headless);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            _output.WriteLine($"SKIPPED: no usable Vulkan ICD ({ex.Message})");
            return null;
        }
    }

    [Fact]
    public void Headless_device_and_GRContext_draw_and_read_back()
    {
        using var vk = TryCreateHeadless();
        if (vk == null) return;

        _output.WriteLine($"Device: {vk.Description} ({vk.SelectionReason})");
        vk.GrContext.Backend.Should().Be(GRBackend.Vulkan);

        var info = new SKImageInfo(64, 32, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var surface = SKSurface.Create(vk.GrContext, false, info))
        {
            surface.Should().NotBeNull("Skia must allocate a Vulkan render target");
            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(0, 0, 255));
            using (var paint = new SKPaint { Color = new SKColor(255, 0, 0), IsAntialias = false })
                canvas.DrawRect(new SKRect(0, 0, 32, 32), paint);
            vk.GrContext.Flush(submit: true, synchronous: true);

            using var bitmap = new SKBitmap(info);
            surface.ReadPixels(info, bitmap.GetPixels(), info.RowBytes, 0, 0).Should().BeTrue();

            bitmap.GetPixel(8, 8).Should().Be(new SKColor(255, 0, 0));
            bitmap.GetPixel(48, 16).Should().Be(new SKColor(0, 0, 255));
        }
    }

    [Fact]
    public void Headless_swapchain_presents_frames_across_resizes()
    {
        VulkanRenderTarget target;
        try
        {
            target = VulkanRenderTarget.CreateHeadlessSurface(200, 120);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            _output.WriteLine($"SKIPPED: no Vulkan ICD with VK_EXT_headless_surface ({ex.Message})");
            return;
        }

        using (target)
        {
            _output.WriteLine($"Device: {target.DeviceDescription}; {target.PresentMode}, {target.ImageCount} images");
            target.IsGpuAccelerated.Should().BeTrue();
            target.PreservesContents.Should().BeFalse();

            // More frames than images and frame slots, with a resize (and
            // therefore swapchain recreation) in the middle.
            int drawn = 0;
            for (int i = 0; i < 12; i++)
            {
                if (i == 5) target.Resize(320, 240);
                if (i == 9) target.Resize(64, 64);

                var canvas = target.BeginFrame();
                if (canvas == null) continue;
                canvas.Clear(i % 2 == 0 ? SKColors.Red : SKColors.Green);
                using (var paint = new SKPaint { Color = SKColors.White })
                    canvas.DrawCircle(20, 20, 10, paint);
                target.EndFrame();
                drawn++;
            }

            drawn.Should().BeGreaterThan(8, "acquire must not time out on a headless swapchain");
            target.Width.Should().Be(64);
        }
    }

    #endregion
}
