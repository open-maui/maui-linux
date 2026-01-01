using System;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Native;

internal static class CairoNative
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

    [DllImport("libcairo.so.2")]
    public static extern IntPtr cairo_image_surface_create_for_data(IntPtr data, cairo_format_t format, int width, int height, int stride);

    [DllImport("libcairo.so.2")]
    public static extern IntPtr cairo_image_surface_create(cairo_format_t format, int width, int height);

    [DllImport("libcairo.so.2")]
    public static extern IntPtr cairo_image_surface_get_data(IntPtr surface);

    [DllImport("libcairo.so.2")]
    public static extern int cairo_image_surface_get_width(IntPtr surface);

    [DllImport("libcairo.so.2")]
    public static extern int cairo_image_surface_get_height(IntPtr surface);

    [DllImport("libcairo.so.2")]
    public static extern int cairo_image_surface_get_stride(IntPtr surface);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_surface_destroy(IntPtr surface);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_surface_flush(IntPtr surface);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_surface_mark_dirty(IntPtr surface);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_surface_mark_dirty_rectangle(IntPtr surface, int x, int y, int width, int height);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_set_source_surface(IntPtr cr, IntPtr surface, double x, double y);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_set_source_rgb(IntPtr cr, double red, double green, double blue);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_set_source_rgba(IntPtr cr, double red, double green, double blue, double alpha);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_paint(IntPtr cr);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_paint_with_alpha(IntPtr cr, double alpha);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_fill(IntPtr cr);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_rectangle(IntPtr cr, double x, double y, double width, double height);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_clip(IntPtr cr);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_save(IntPtr cr);

    [DllImport("libcairo.so.2")]
    public static extern void cairo_restore(IntPtr cr);
}
