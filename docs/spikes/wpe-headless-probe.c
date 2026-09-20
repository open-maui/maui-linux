// WPE headless probe: proves the embedder path OpenMaui will use.
//   headless WPEDisplay -> WebKitWebView("display") -> "buffer-rendered" signal
//   -> wpe_buffer_import_to_pixels -> write PPM.
#include <wpe/webkit.h>
#include <wpe/wpe-platform.h>
#include <wpe/headless/wpe-headless.h>
#include <stdio.h>
#include <stdlib.h>

static GMainLoop *loop;
static int frames = 0;

static void on_buffer_rendered(WPEView *view, WPEBuffer *buffer, gpointer user_data)
{
    frames++;
    int w = wpe_buffer_get_width(buffer), h = wpe_buffer_get_height(buffer);
    const char *kind = WPE_IS_BUFFER_DMA_BUF(buffer) ? "DMA-BUF" : WPE_IS_BUFFER_SHM(buffer) ? "SHM" : "other";
    GError *err = NULL;
    GBytes *bytes = wpe_buffer_import_to_pixels(buffer, &err);
    if (!bytes) {
        fprintf(stderr, "frame %d: %dx%d %s import_to_pixels failed: %s\n", frames, w, h, kind, err ? err->message : "?");
        wpe_view_buffer_released(view, buffer);
        return;
    }
    gsize len = 0;
    const unsigned char *px = g_bytes_get_data(bytes, &len);
    printf("frame %d: %dx%d %s, %zu bytes (%zu bytes/px)\n", frames, w, h, kind, len, len / (gsize)(w * h));

    if (frames == 1) { // let the page settle, then dump
        FILE *f = fopen(user_data, "wb");
        fprintf(f, "P6\n%d %d\n255\n", w, h);
        for (int i = 0; i < w * h; i++) { // assume BGRA/ARGB32 little-endian
            fputc(px[i * 4 + 2], f); fputc(px[i * 4 + 1], f); fputc(px[i * 4 + 0], f);
        }
        fclose(f);
        printf("wrote %s\n", (char *)user_data);
        g_main_loop_quit(loop);
    }
    g_bytes_unref(bytes);
    wpe_view_buffer_released(view, buffer);
}

static void on_load_changed(WebKitWebView *wv, WebKitLoadEvent ev, gpointer d)
{
    if (ev == WEBKIT_LOAD_FINISHED) printf("load finished: %s\n", webkit_web_view_get_title(wv));
}

static gboolean timeout(gpointer d) { fprintf(stderr, "timeout after %d frames\n", frames); g_main_loop_quit(loop); return G_SOURCE_REMOVE; }

int main(int argc, char **argv)
{
    const char *out = argc > 1 ? argv[1] : "frame.ppm";
    GError *err = NULL;
    WPEDisplay *display = wpe_display_headless_new();
    if (!wpe_display_connect(display, &err)) { fprintf(stderr, "connect: %s\n", err->message); return 1; }
    printf("display: %s\n", G_OBJECT_TYPE_NAME(display));

    WebKitWebView *wv = WEBKIT_WEB_VIEW(g_object_new(WEBKIT_TYPE_WEB_VIEW, "display", display, NULL));
    WPEView *view = webkit_web_view_get_wpe_view(wv);
    printf("view: %s\n", G_OBJECT_TYPE_NAME(view));
    wpe_view_resized(view, 640, 400);
    g_signal_connect(view, "buffer-rendered", G_CALLBACK(on_buffer_rendered), (gpointer)out);
    g_signal_connect(wv, "load-changed", G_CALLBACK(on_load_changed), NULL);

    webkit_web_view_load_html(wv,
        "<html><body style='margin:0;background:#385aaf;color:white;font:32px sans-serif'>"
        "<div style='padding:40px'>OpenMaui + WPE 2.54<br><small style='font-size:18px'>headless buffer-rendered probe</small>"
        "<div style='margin-top:30px;width:200px;height:60px;background:#10b981;border-radius:12px'></div></div>"
        "</body></html>", NULL);

    loop = g_main_loop_new(NULL, FALSE);
    g_timeout_add(8000, timeout, NULL);
    g_main_loop_run(loop);
    return frames > 0 ? 0 : 2;
}
