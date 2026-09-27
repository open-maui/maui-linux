// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// Minimal Vulkan 1.1 surface (libvulkan.so.1, the Khronos loader) for the
/// Vulkan render target: instance/device bring-up, WSI (surface + swapchain),
/// and the few command-buffer/sync calls needed around Skia's own submissions.
/// Everything else Skia resolves itself through the proc-address callback.
/// Structs mirror the C layouts on LP64 (x86_64, aarch64): dispatchable
/// handles are pointers (<see cref="IntPtr"/>), non-dispatchable handles are
/// 64-bit integers (<see cref="ulong"/>).
/// </summary>
internal static unsafe partial class Vk
{
    public const string LibVulkan = "libvulkan.so.1";

    // Results
    public const int VK_SUCCESS = 0;
    public const int VK_NOT_READY = 1;
    public const int VK_TIMEOUT = 2;
    public const int VK_INCOMPLETE = 5;
    public const int VK_SUBOPTIMAL_KHR = 1000001003;
    public const int VK_ERROR_OUT_OF_DATE_KHR = -1000001004;
    public const int VK_ERROR_SURFACE_LOST_KHR = -1000000000;
    public const int VK_ERROR_DEVICE_LOST = -4;

    // Structure types
    public const uint VK_STRUCTURE_TYPE_APPLICATION_INFO = 0;
    public const uint VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO = 1;
    public const uint VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO = 2;
    public const uint VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO = 3;
    public const uint VK_STRUCTURE_TYPE_SUBMIT_INFO = 4;
    public const uint VK_STRUCTURE_TYPE_FENCE_CREATE_INFO = 8;
    public const uint VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO = 9;
    public const uint VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO = 39;
    public const uint VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO = 40;
    public const uint VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO = 42;
    public const uint VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER = 45;
    public const uint VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2 = 1000059001;
    public const uint VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR = 1000001000;
    public const uint VK_STRUCTURE_TYPE_PRESENT_INFO_KHR = 1000001001;
    public const uint VK_STRUCTURE_TYPE_XLIB_SURFACE_CREATE_INFO_KHR = 1000004000;
    public const uint VK_STRUCTURE_TYPE_WAYLAND_SURFACE_CREATE_INFO_KHR = 1000006000;
    public const uint VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DRM_PROPERTIES_EXT = 1000353000;
    public const uint VK_STRUCTURE_TYPE_HEADLESS_SURFACE_CREATE_INFO_EXT = 1000256000;

    // Versions
    public static uint MakeApiVersion(uint major, uint minor, uint patch) => (major << 22) | (minor << 12) | patch;
    public static uint ApiMajor(uint v) => (v >> 22) & 0x7F;
    public static uint ApiMinor(uint v) => (v >> 12) & 0x3FF;
    public static uint ApiPatch(uint v) => v & 0xFFF;
    public static readonly uint VK_API_VERSION_1_1 = MakeApiVersion(1, 1, 0);

    // Physical device types
    public const uint VK_PHYSICAL_DEVICE_TYPE_OTHER = 0;
    public const uint VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU = 1;
    public const uint VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU = 2;
    public const uint VK_PHYSICAL_DEVICE_TYPE_VIRTUAL_GPU = 3;
    public const uint VK_PHYSICAL_DEVICE_TYPE_CPU = 4;

    // Queue flags
    public const uint VK_QUEUE_GRAPHICS_BIT = 0x1;

    // Formats / colour spaces
    public const uint VK_FORMAT_R8G8B8A8_UNORM = 37;
    public const uint VK_FORMAT_B8G8R8A8_UNORM = 44;
    public const uint VK_COLOR_SPACE_SRGB_NONLINEAR_KHR = 0;

    // Image usage
    public const uint VK_IMAGE_USAGE_TRANSFER_SRC_BIT = 0x1;
    public const uint VK_IMAGE_USAGE_TRANSFER_DST_BIT = 0x2;
    public const uint VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT = 0x10;

    // Image layouts
    public const uint VK_IMAGE_LAYOUT_UNDEFINED = 0;
    public const uint VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL = 2;
    public const uint VK_IMAGE_LAYOUT_PRESENT_SRC_KHR = 1000001002;

    public const uint VK_IMAGE_TILING_OPTIMAL = 0;
    public const uint VK_SHARING_MODE_EXCLUSIVE = 0;
    public const uint VK_QUEUE_FAMILY_IGNORED = ~0u;
    public const uint VK_IMAGE_ASPECT_COLOR_BIT = 0x1;

    // Present modes
    public const uint VK_PRESENT_MODE_IMMEDIATE_KHR = 0;
    public const uint VK_PRESENT_MODE_MAILBOX_KHR = 1;
    public const uint VK_PRESENT_MODE_FIFO_KHR = 2;
    public const uint VK_PRESENT_MODE_FIFO_RELAXED_KHR = 3;

    // Composite alpha
    public const uint VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR = 0x1;
    public const uint VK_COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR = 0x2;
    public const uint VK_COMPOSITE_ALPHA_POST_MULTIPLIED_BIT_KHR = 0x4;
    public const uint VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR = 0x8;

    public const uint VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR = 0x1;

    // Pipeline stages / access
    public const uint VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT = 0x2000;
    public const uint VK_PIPELINE_STAGE_ALL_COMMANDS_BIT = 0x10000;
    public const uint VK_ACCESS_SHADER_READ_BIT = 0x20;
    public const uint VK_ACCESS_COLOR_ATTACHMENT_READ_BIT = 0x80;
    public const uint VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT = 0x100;
    public const uint VK_ACCESS_TRANSFER_READ_BIT = 0x800;
    public const uint VK_ACCESS_TRANSFER_WRITE_BIT = 0x1000;

    // Command buffers / fences
    public const uint VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT = 0x2;
    public const uint VK_COMMAND_BUFFER_LEVEL_PRIMARY = 0;
    public const uint VK_COMMAND_BUFFER_USAGE_SIMULTANEOUS_USE_BIT = 0x4;
    public const uint VK_FENCE_CREATE_SIGNALED_BIT = 0x1;

    public const uint VK_TRUE = 1;
    public const uint VK_FALSE = 0;

    public const string VK_KHR_SURFACE = "VK_KHR_surface";
    public const string VK_KHR_WAYLAND_SURFACE = "VK_KHR_wayland_surface";
    public const string VK_KHR_XLIB_SURFACE = "VK_KHR_xlib_surface";
    public const string VK_KHR_SWAPCHAIN = "VK_KHR_swapchain";
    public const string VK_EXT_HEADLESS_SURFACE = "VK_EXT_headless_surface";
    public const string VK_EXT_PHYSICAL_DEVICE_DRM = "VK_EXT_physical_device_drm";

    #region Structs

    [StructLayout(LayoutKind.Sequential)]
    public struct VkApplicationInfo
    {
        public uint sType;
        public void* pNext;
        public byte* pApplicationName;
        public uint applicationVersion;
        public byte* pEngineName;
        public uint engineVersion;
        public uint apiVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkInstanceCreateInfo
    {
        public uint sType;
        public void* pNext;
        public uint flags;
        public VkApplicationInfo* pApplicationInfo;
        public uint enabledLayerCount;
        public byte** ppEnabledLayerNames;
        public uint enabledExtensionCount;
        public byte** ppEnabledExtensionNames;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkExtensionProperties
    {
        public fixed byte extensionName[256];
        public uint specVersion;
    }

    /// <summary>
    /// Leading fields of VkPhysicalDeviceProperties. The full struct (limits,
    /// sparse properties) is 824 bytes; callers allocate
    /// <see cref="PhysicalDevicePropertiesSize"/> and read this prefix.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VkPhysicalDevicePropertiesHeader
    {
        public uint apiVersion;
        public uint driverVersion;
        public uint vendorID;
        public uint deviceID;
        public uint deviceType;
        public fixed byte deviceName[256];
        public fixed byte pipelineCacheUUID[16];
    }

    /// <summary>Generous upper bound of sizeof(VkPhysicalDeviceProperties) (824 on LP64).</summary>
    public const int PhysicalDevicePropertiesSize = 1024;

    [StructLayout(LayoutKind.Sequential)]
    public struct VkPhysicalDeviceDrmPropertiesEXT
    {
        public uint sType;
        public void* pNext;
        public uint hasPrimary;
        public uint hasRender;
        public long primaryMajor;
        public long primaryMinor;
        public long renderMajor;
        public long renderMinor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkQueueFamilyProperties
    {
        public uint queueFlags;
        public uint queueCount;
        public uint timestampValidBits;
        public uint granularityWidth;
        public uint granularityHeight;
        public uint granularityDepth;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkDeviceQueueCreateInfo
    {
        public uint sType;
        public void* pNext;
        public uint flags;
        public uint queueFamilyIndex;
        public uint queueCount;
        public float* pQueuePriorities;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkDeviceCreateInfo
    {
        public uint sType;
        public void* pNext;
        public uint flags;
        public uint queueCreateInfoCount;
        public VkDeviceQueueCreateInfo* pQueueCreateInfos;
        public uint enabledLayerCount;
        public byte** ppEnabledLayerNames;
        public uint enabledExtensionCount;
        public byte** ppEnabledExtensionNames;
        public void* pEnabledFeatures;
    }

    /// <summary>VkPhysicalDeviceFeatures: 55 VkBool32 fields.</summary>
    public const int PhysicalDeviceFeaturesSize = 55 * 4;
    public const int FeatureSampleRateShading = 6;
    public const int FeatureDualSrcBlend = 7;

    [StructLayout(LayoutKind.Sequential)]
    public struct VkWaylandSurfaceCreateInfoKHR
    {
        public uint sType;
        public void* pNext;
        public uint flags;
        public IntPtr display;
        public IntPtr surface;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkXlibSurfaceCreateInfoKHR
    {
        public uint sType;
        public void* pNext;
        public uint flags;
        public IntPtr dpy;
        public ulong window;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkHeadlessSurfaceCreateInfoEXT
    {
        public uint sType;
        public void* pNext;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkSurfaceCapabilitiesKHR
    {
        public uint minImageCount;
        public uint maxImageCount;
        public uint currentExtentWidth;
        public uint currentExtentHeight;
        public uint minImageExtentWidth;
        public uint minImageExtentHeight;
        public uint maxImageExtentWidth;
        public uint maxImageExtentHeight;
        public uint maxImageArrayLayers;
        public uint supportedTransforms;
        public uint currentTransform;
        public uint supportedCompositeAlpha;
        public uint supportedUsageFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkSurfaceFormatKHR
    {
        public uint format;
        public uint colorSpace;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkSwapchainCreateInfoKHR
    {
        public uint sType;
        public void* pNext;
        public uint flags;
        public ulong surface;
        public uint minImageCount;
        public uint imageFormat;
        public uint imageColorSpace;
        public uint imageExtentWidth;
        public uint imageExtentHeight;
        public uint imageArrayLayers;
        public uint imageUsage;
        public uint imageSharingMode;
        public uint queueFamilyIndexCount;
        public uint* pQueueFamilyIndices;
        public uint preTransform;
        public uint compositeAlpha;
        public uint presentMode;
        public uint clipped;
        public ulong oldSwapchain;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkSemaphoreCreateInfo
    {
        public uint sType;
        public void* pNext;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkFenceCreateInfo
    {
        public uint sType;
        public void* pNext;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkCommandPoolCreateInfo
    {
        public uint sType;
        public void* pNext;
        public uint flags;
        public uint queueFamilyIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkCommandBufferAllocateInfo
    {
        public uint sType;
        public void* pNext;
        public ulong commandPool;
        public uint level;
        public uint commandBufferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkCommandBufferBeginInfo
    {
        public uint sType;
        public void* pNext;
        public uint flags;
        public void* pInheritanceInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkImageMemoryBarrier
    {
        public uint sType;
        public void* pNext;
        public uint srcAccessMask;
        public uint dstAccessMask;
        public uint oldLayout;
        public uint newLayout;
        public uint srcQueueFamilyIndex;
        public uint dstQueueFamilyIndex;
        public ulong image;
        public uint aspectMask;
        public uint baseMipLevel;
        public uint levelCount;
        public uint baseArrayLayer;
        public uint layerCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkSubmitInfo
    {
        public uint sType;
        public void* pNext;
        public uint waitSemaphoreCount;
        public ulong* pWaitSemaphores;
        public uint* pWaitDstStageMask;
        public uint commandBufferCount;
        public IntPtr* pCommandBuffers;
        public uint signalSemaphoreCount;
        public ulong* pSignalSemaphores;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkPresentInfoKHR
    {
        public uint sType;
        public void* pNext;
        public uint waitSemaphoreCount;
        public ulong* pWaitSemaphores;
        public uint swapchainCount;
        public ulong* pSwapchains;
        public uint* pImageIndices;
        public int* pResults;
    }

    #endregion

    #region Loader / instance

    [LibraryImport(LibVulkan, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr vkGetInstanceProcAddr(IntPtr instance, string name);

    [LibraryImport(LibVulkan, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr vkGetDeviceProcAddr(IntPtr device, string name);

    [LibraryImport(LibVulkan)]
    public static partial int vkEnumerateInstanceExtensionProperties(byte* layerName, uint* count, VkExtensionProperties* properties);

    [LibraryImport(LibVulkan)]
    public static partial int vkCreateInstance(VkInstanceCreateInfo* createInfo, void* allocator, IntPtr* instance);

    [LibraryImport(LibVulkan)]
    public static partial void vkDestroyInstance(IntPtr instance, void* allocator);

    [LibraryImport(LibVulkan)]
    public static partial int vkEnumeratePhysicalDevices(IntPtr instance, uint* count, IntPtr* devices);

    [LibraryImport(LibVulkan)]
    public static partial void vkGetPhysicalDeviceProperties(IntPtr physicalDevice, void* properties);

    [LibraryImport(LibVulkan)]
    public static partial void vkGetPhysicalDeviceProperties2(IntPtr physicalDevice, void* properties2);

    [LibraryImport(LibVulkan)]
    public static partial void vkGetPhysicalDeviceFeatures(IntPtr physicalDevice, void* features);

    [LibraryImport(LibVulkan)]
    public static partial void vkGetPhysicalDeviceQueueFamilyProperties(IntPtr physicalDevice, uint* count, VkQueueFamilyProperties* properties);

    [LibraryImport(LibVulkan)]
    public static partial int vkEnumerateDeviceExtensionProperties(IntPtr physicalDevice, byte* layerName, uint* count, VkExtensionProperties* properties);

    #endregion

    #region Device

    [LibraryImport(LibVulkan)]
    public static partial int vkCreateDevice(IntPtr physicalDevice, VkDeviceCreateInfo* createInfo, void* allocator, IntPtr* device);

    [LibraryImport(LibVulkan)]
    public static partial void vkDestroyDevice(IntPtr device, void* allocator);

    [LibraryImport(LibVulkan)]
    public static partial void vkGetDeviceQueue(IntPtr device, uint queueFamilyIndex, uint queueIndex, IntPtr* queue);

    [LibraryImport(LibVulkan)]
    public static partial int vkDeviceWaitIdle(IntPtr device);

    [LibraryImport(LibVulkan)]
    public static partial int vkQueueSubmit(IntPtr queue, uint submitCount, VkSubmitInfo* submits, ulong fence);

    [LibraryImport(LibVulkan)]
    public static partial int vkCreateSemaphore(IntPtr device, VkSemaphoreCreateInfo* createInfo, void* allocator, ulong* semaphore);

    [LibraryImport(LibVulkan)]
    public static partial void vkDestroySemaphore(IntPtr device, ulong semaphore, void* allocator);

    [LibraryImport(LibVulkan)]
    public static partial int vkCreateFence(IntPtr device, VkFenceCreateInfo* createInfo, void* allocator, ulong* fence);

    [LibraryImport(LibVulkan)]
    public static partial void vkDestroyFence(IntPtr device, ulong fence, void* allocator);

    [LibraryImport(LibVulkan)]
    public static partial int vkWaitForFences(IntPtr device, uint fenceCount, ulong* fences, uint waitAll, ulong timeout);

    [LibraryImport(LibVulkan)]
    public static partial int vkResetFences(IntPtr device, uint fenceCount, ulong* fences);

    [LibraryImport(LibVulkan)]
    public static partial int vkCreateCommandPool(IntPtr device, VkCommandPoolCreateInfo* createInfo, void* allocator, ulong* pool);

    [LibraryImport(LibVulkan)]
    public static partial void vkDestroyCommandPool(IntPtr device, ulong pool, void* allocator);

    [LibraryImport(LibVulkan)]
    public static partial int vkAllocateCommandBuffers(IntPtr device, VkCommandBufferAllocateInfo* allocateInfo, IntPtr* commandBuffers);

    [LibraryImport(LibVulkan)]
    public static partial void vkFreeCommandBuffers(IntPtr device, ulong pool, uint count, IntPtr* commandBuffers);

    [LibraryImport(LibVulkan)]
    public static partial int vkBeginCommandBuffer(IntPtr commandBuffer, VkCommandBufferBeginInfo* beginInfo);

    [LibraryImport(LibVulkan)]
    public static partial int vkEndCommandBuffer(IntPtr commandBuffer);

    [LibraryImport(LibVulkan)]
    public static partial void vkCmdPipelineBarrier(IntPtr commandBuffer, uint srcStageMask, uint dstStageMask, uint dependencyFlags,
        uint memoryBarrierCount, void* memoryBarriers, uint bufferMemoryBarrierCount, void* bufferMemoryBarriers,
        uint imageMemoryBarrierCount, VkImageMemoryBarrier* imageMemoryBarriers);

    #endregion

    #region WSI

    [LibraryImport(LibVulkan)]
    public static partial int vkCreateWaylandSurfaceKHR(IntPtr instance, VkWaylandSurfaceCreateInfoKHR* createInfo, void* allocator, ulong* surface);

    [LibraryImport(LibVulkan)]
    public static partial int vkCreateXlibSurfaceKHR(IntPtr instance, VkXlibSurfaceCreateInfoKHR* createInfo, void* allocator, ulong* surface);

    /// <summary>VK_EXT_headless_surface is not exported by the loader; resolved per instance.</summary>
    public static int vkCreateHeadlessSurfaceEXT(IntPtr instance, VkHeadlessSurfaceCreateInfoEXT* createInfo, ulong* surface)
    {
        var fn = (delegate* unmanaged[Cdecl]<IntPtr, VkHeadlessSurfaceCreateInfoEXT*, void*, ulong*, int>)vkGetInstanceProcAddr(instance, "vkCreateHeadlessSurfaceEXT");
        return fn == null ? -7 /* VK_ERROR_EXTENSION_NOT_PRESENT */ : fn(instance, createInfo, null, surface);
    }

    [LibraryImport(LibVulkan)]
    public static partial void vkDestroySurfaceKHR(IntPtr instance, ulong surface, void* allocator);

    [LibraryImport(LibVulkan)]
    public static partial int vkGetPhysicalDeviceSurfaceSupportKHR(IntPtr physicalDevice, uint queueFamilyIndex, ulong surface, uint* supported);

    [LibraryImport(LibVulkan)]
    public static partial int vkGetPhysicalDeviceSurfaceCapabilitiesKHR(IntPtr physicalDevice, ulong surface, VkSurfaceCapabilitiesKHR* capabilities);

    [LibraryImport(LibVulkan)]
    public static partial int vkGetPhysicalDeviceSurfaceFormatsKHR(IntPtr physicalDevice, ulong surface, uint* count, VkSurfaceFormatKHR* formats);

    [LibraryImport(LibVulkan)]
    public static partial int vkGetPhysicalDeviceSurfacePresentModesKHR(IntPtr physicalDevice, ulong surface, uint* count, uint* modes);

    [LibraryImport(LibVulkan)]
    public static partial int vkCreateSwapchainKHR(IntPtr device, VkSwapchainCreateInfoKHR* createInfo, void* allocator, ulong* swapchain);

    [LibraryImport(LibVulkan)]
    public static partial void vkDestroySwapchainKHR(IntPtr device, ulong swapchain, void* allocator);

    [LibraryImport(LibVulkan)]
    public static partial int vkGetSwapchainImagesKHR(IntPtr device, ulong swapchain, uint* count, ulong* images);

    [LibraryImport(LibVulkan)]
    public static partial int vkAcquireNextImageKHR(IntPtr device, ulong swapchain, ulong timeout, ulong semaphore, ulong fence, uint* imageIndex);

    [LibraryImport(LibVulkan)]
    public static partial int vkQueuePresentKHR(IntPtr queue, VkPresentInfoKHR* presentInfo);

    #endregion

    #region Helpers

    /// <summary>Instance extensions the loader offers (empty when the loader is missing or broken).</summary>
    public static HashSet<string> EnumerateInstanceExtensions()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        uint count = 0;
        if (vkEnumerateInstanceExtensionProperties(null, &count, null) != VK_SUCCESS || count == 0)
            return result;
        var props = new VkExtensionProperties[count];
        fixed (VkExtensionProperties* p = props)
        {
            if (vkEnumerateInstanceExtensionProperties(null, &count, p) < 0)
                return result;
            for (int i = 0; i < count; i++)
                result.Add(ExtensionName(&p[i]));
        }
        return result;
    }

    public static HashSet<string> EnumerateDeviceExtensions(IntPtr physicalDevice)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        uint count = 0;
        if (vkEnumerateDeviceExtensionProperties(physicalDevice, null, &count, null) != VK_SUCCESS || count == 0)
            return result;
        var props = new VkExtensionProperties[count];
        fixed (VkExtensionProperties* p = props)
        {
            if (vkEnumerateDeviceExtensionProperties(physicalDevice, null, &count, p) < 0)
                return result;
            for (int i = 0; i < count; i++)
                result.Add(ExtensionName(&p[i]));
        }
        return result;
    }

    private static string ExtensionName(VkExtensionProperties* p)
        => Marshal.PtrToStringUTF8((IntPtr)p->extensionName) ?? string.Empty;

    /// <summary>
    /// Marshals a list of strings into a native array of NUL-terminated UTF-8
    /// strings. Free with <see cref="FreeStringArray"/>.
    /// </summary>
    public static byte** AllocStringArray(IReadOnlyList<string> values)
    {
        var array = (byte**)NativeMemory.AllocZeroed((nuint)(Math.Max(1, values.Count) * sizeof(byte*)));
        for (int i = 0; i < values.Count; i++)
            array[i] = (byte*)Marshal.StringToCoTaskMemUTF8(values[i]);
        return array;
    }

    public static void FreeStringArray(byte** array, int count)
    {
        if (array == null) return;
        for (int i = 0; i < count; i++)
            Marshal.FreeCoTaskMem((IntPtr)array[i]);
        NativeMemory.Free(array);
    }

    public static string ResultName(int result) => result switch
    {
        0 => "VK_SUCCESS",
        1 => "VK_NOT_READY",
        2 => "VK_TIMEOUT",
        5 => "VK_INCOMPLETE",
        -1 => "VK_ERROR_OUT_OF_HOST_MEMORY",
        -2 => "VK_ERROR_OUT_OF_DEVICE_MEMORY",
        -3 => "VK_ERROR_INITIALIZATION_FAILED",
        -4 => "VK_ERROR_DEVICE_LOST",
        -6 => "VK_ERROR_LAYER_NOT_PRESENT",
        -7 => "VK_ERROR_EXTENSION_NOT_PRESENT",
        -8 => "VK_ERROR_FEATURE_NOT_PRESENT",
        -9 => "VK_ERROR_INCOMPATIBLE_DRIVER",
        -1000000000 => "VK_ERROR_SURFACE_LOST_KHR",
        -1000000001 => "VK_ERROR_NATIVE_WINDOW_IN_USE_KHR",
        1000001003 => "VK_SUBOPTIMAL_KHR",
        -1000001004 => "VK_ERROR_OUT_OF_DATE_KHR",
        _ => $"VkResult({result})",
    };

    public static string PresentModeName(uint mode) => mode switch
    {
        VK_PRESENT_MODE_IMMEDIATE_KHR => "immediate",
        VK_PRESENT_MODE_MAILBOX_KHR => "mailbox",
        VK_PRESENT_MODE_FIFO_KHR => "fifo",
        VK_PRESENT_MODE_FIFO_RELAXED_KHR => "fifo-relaxed",
        _ => $"mode{mode}",
    };

    #endregion
}
