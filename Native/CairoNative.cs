using System;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Native;

internal static partial class CairoNative
{
    public enum cairo_format_t
    {
        CAIRO_FORMAT_INVALID = -1,
        CAIRO_FORMAT_ARGB32,
        CAIRO_FORMAT_RGB24,
        CAIRO_FORMAT_A8,
        CAIRO_FORMAT_A1,
        CAIRO_FORMAT_RGB16_565,
        CAIRO_FORMAT_RGB30
    }

    private const string Lib = "libcairo.so.2";

    [LibraryImport("libcairo.so.2")]
    public static partial IntPtr cairo_image_surface_create_for_data(IntPtr data, cairo_format_t format, int width, int height, int stride);

    [LibraryImport("libcairo.so.2")]
    public static partial IntPtr cairo_image_surface_create(cairo_format_t format, int width, int height);

    [LibraryImport("libcairo.so.2")]
    public static partial IntPtr cairo_image_surface_get_data(IntPtr surface);

    [LibraryImport("libcairo.so.2")]
    public static partial int cairo_image_surface_get_width(IntPtr surface);

    [LibraryImport("libcairo.so.2")]
    public static partial int cairo_image_surface_get_height(IntPtr surface);

    [LibraryImport("libcairo.so.2")]
    public static partial int cairo_image_surface_get_stride(IntPtr surface);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_surface_destroy(IntPtr surface);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_surface_flush(IntPtr surface);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_surface_mark_dirty(IntPtr surface);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_surface_mark_dirty_rectangle(IntPtr surface, int x, int y, int width, int height);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_set_source_surface(IntPtr cr, IntPtr surface, double x, double y);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_set_source_rgb(IntPtr cr, double red, double green, double blue);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_set_source_rgba(IntPtr cr, double red, double green, double blue, double alpha);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_paint(IntPtr cr);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_paint_with_alpha(IntPtr cr, double alpha);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_fill(IntPtr cr);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_rectangle(IntPtr cr, double x, double y, double width, double height);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_clip(IntPtr cr);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_save(IntPtr cr);

    [LibraryImport("libcairo.so.2")]
    public static partial void cairo_restore(IntPtr cr);
}
