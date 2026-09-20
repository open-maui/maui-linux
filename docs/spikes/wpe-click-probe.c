#include <wpe/webkit.h>
#include <wpe/wpe-platform.h>
#include <wpe/headless/wpe-headless.h>
#include <stdio.h>
static GMainLoop *loop; static WPEView *view; static int frames, loads;
static gboolean send_click(gpointer d){
    int variant = GPOINTER_TO_INT(d);
    double x = 100, y = 60; guint32 t = 1000;
    printf("sending click variant %d at %g,%g\n", variant, x, y);
    WPEEvent *e;
    if (variant >= 1) { e = wpe_event_pointer_move_new(WPE_EVENT_POINTER_ENTER, view, WPE_INPUT_SOURCE_MOUSE, t, 0, x, y, 0, 0); wpe_view_event(view, e); wpe_event_unref(e); }
    if (variant >= 1) { e = wpe_event_pointer_move_new(WPE_EVENT_POINTER_MOVE, view, WPE_INPUT_SOURCE_MOUSE, t+1, 0, x, y, 0, 0); wpe_view_event(view, e); wpe_event_unref(e); }
    guint mods_down = variant == 2 ? WPE_MODIFIER_POINTER_BUTTON1 : 0;
    e = wpe_event_pointer_button_new(WPE_EVENT_POINTER_DOWN, view, WPE_INPUT_SOURCE_MOUSE, t+2, mods_down, WPE_BUTTON_PRIMARY, x, y, 1); wpe_view_event(view, e); wpe_event_unref(e);
    e = wpe_event_pointer_button_new(WPE_EVENT_POINTER_UP, view, WPE_INPUT_SOURCE_MOUSE, t+50, 0, WPE_BUTTON_PRIMARY, x, y, 0); wpe_view_event(view, e); wpe_event_unref(e);
    return G_SOURCE_REMOVE;
}
static void on_buffer(WPEView *v, WPEBuffer *b, gpointer d){ frames++; wpe_view_buffer_released(v, b); }
static void on_load(WebKitWebView *wv, WebKitLoadEvent ev, gpointer d){
    if (ev == WEBKIT_LOAD_STARTED) printf("load started: %s\n", webkit_web_view_get_uri(wv));
    if (ev == WEBKIT_LOAD_FINISHED) { loads++; printf("load finished: %s (frames so far %d)\n", webkit_web_view_get_uri(wv), frames);
        if (loads == 1) g_timeout_add(500, send_click, GINT_TO_POINTER(0));
        if (loads == 2) { printf("CLICK NAVIGATED with variant 0\n"); g_main_loop_quit(loop);} }
}
static gboolean retry(gpointer d){ if (loads < 2) { send_click(GINT_TO_POINTER(1)); g_timeout_add(800, (GSourceFunc)send_click, GINT_TO_POINTER(2)); } return G_SOURCE_REMOVE; }
static gboolean timeout(gpointer d){ printf("timeout: loads=%d frames=%d\n", loads, frames); g_main_loop_quit(loop); return G_SOURCE_REMOVE; }
int main(){
    GError *err=NULL; WPEDisplay *display = wpe_display_headless_new_for_device("/dev/dri/renderD128", &err);
    wpe_display_connect(display, &err);
    WebKitWebView *wv = WEBKIT_WEB_VIEW(g_object_new(WEBKIT_TYPE_WEB_VIEW, "display", display, NULL));
    view = webkit_web_view_get_wpe_view(wv);
    wpe_view_resized(view, 640, 400);
    g_signal_connect(view, "buffer-rendered", G_CALLBACK(on_buffer), NULL);
    g_signal_connect(wv, "load-changed", G_CALLBACK(on_load), NULL);
    webkit_web_view_load_html(wv, "<html><body style='margin:0'><a href='about:blank' style='display:block;width:400px;height:120px;background:#ccc;font:30px sans-serif'>CLICK ME</a></body></html>", "http://example.test/");
    loop = g_main_loop_new(NULL, FALSE);
    g_timeout_add(2500, retry, NULL);
    g_timeout_add(6000, timeout, NULL);
    g_main_loop_run(loop);
    return 0;
}
