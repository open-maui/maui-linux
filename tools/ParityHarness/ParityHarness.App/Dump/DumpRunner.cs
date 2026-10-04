using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Maui.Devices;
using ParityHarness.Gallery;

namespace ParityHarness.Dump;

/// <summary>
/// Dump mode: sizes the window so the root page is exactly the requested size, shows each
/// gallery page, waits until its geometry stops changing, writes &lt;page&gt;.json, and exits.
/// Everything runs on the UI thread, driven by the MAUI dispatcher, with the same code on
/// every platform.
/// </summary>
public sealed class DumpRunner
{
    private const int PollMs = 150;
    private const int StablePolls = 3;
    private const int MinSettleMs = 600;
    private const double SizeEpsilon = 0.5;

    private readonly Window _window;
    private readonly IDispatcher _dispatcher;
    private readonly string _outDir;
    private readonly DumpIndex _index;
    private Timer? _watchdog;

    public DumpRunner(Window window)
    {
        _window = window;
        _dispatcher = window.Dispatcher;
        _outDir = HarnessOptions.DumpDir!;
        _index = new DumpIndex
        {
            Platform = PlatformName,
            Os = RuntimeInformation.OSDescription,
            Framework = RuntimeInformation.FrameworkDescription,
            Requested = new SizeDto(HarnessOptions.Width, HarnessOptions.Height),
            StartedUtc = DateTime.UtcNow.ToString("O"),
        };
    }

    public static string PlatformName =>
#if OPENMAUI_LINUX
        "linux";
#elif WINDOWS
        "windows";
#else
        "other";
#endif

    public async Task RunAsync()
    {
        int exitCode = 0;
        try
        {
            Directory.CreateDirectory(_outDir);
            _watchdog = new Timer(_ => Watchdog(), null, TimeSpan.FromSeconds(HarnessOptions.WatchdogSeconds), Timeout.InfiniteTimeSpan);
            try { _index.DisplayDensity = DeviceDisplay.Current.MainDisplayInfo.Density; } catch { }

            Log($"dump to {_outDir} at {HarnessOptions.Width}x{HarnessOptions.Height} on {PlatformName}");

            // Size the window once with the plain sizing page (a ContentPage reports its
            // size on every platform); gallery pages re-check and correct below.
            if (_window.Page is { } sizing)
                await EnsureSizeAsync(sizing);

            foreach (var entry in GalleryCatalog.All)
            {
                if (HarnessOptions.Pages != null && !HarnessOptions.Pages.Contains(entry.Name))
                    continue;
                var dump = await DumpPageAsync(entry);
                string file = entry.Name + ".json";
                await File.WriteAllTextAsync(Path.Combine(_outDir, file), JsonSerializer.Serialize(dump, DumpJson.Options));
                _index.Pages.Add(new IndexEntry(entry.Name, file, dump.Settled, dump.Elements.Count, dump.Error, dump.Warning));
                if (dump.Error != null) exitCode = 1;
                Log($"{entry.Name}: {dump.Elements.Count} elements, page {dump.PageSize.W}x{dump.PageSize.H}, settled={dump.Settled} in {dump.SettleMs} ms{(dump.Error != null ? ", error: " + dump.Error : "")}{(dump.Warning != null ? ", warning: " + dump.Warning : "")}");
            }
        }
        catch (Exception ex)
        {
            _index.Error = ex.ToString();
            Log("dump failed: " + ex);
            exitCode = 2;
        }

        WriteIndex();
        Log($"done, exit {exitCode}");
        Environment.Exit(exitCode);
    }

    private async Task<PageDump> DumpPageAsync(GalleryEntry entry)
    {
        var dump = new PageDump
        {
            Page = entry.Name,
            Platform = PlatformName,
            Os = _index.Os,
            Requested = new SizeDto(HarnessOptions.Width, HarnessOptions.Height),
        };
        var sw = Stopwatch.StartNew();
        try
        {
            var page = entry.Create();
            _window.Page = page;
            await EnsureSizeAsync(page);

            string? last = null;
            int stable = 0;
            List<ElementRecord> records = new();
            while (true)
            {
                await Delay(PollMs);
                records = TreeDumper.Walk(page);
                string fp = TreeDumper.Fingerprint(records) + $"{page.Width}x{page.Height}";
                stable = fp == last ? stable + 1 : 0;
                last = fp;
                if (stable >= StablePolls && sw.ElapsedMilliseconds >= MinSettleMs)
                {
                    dump.Settled = true;
                    break;
                }
                if (sw.ElapsedMilliseconds > HarnessOptions.SettleTimeoutMs)
                    break;
            }

            dump.Elements = records;
            dump.PageSize = new SizeDto(Math.Round(page.Width, 2), Math.Round(page.Height, 2));
            if (page.Width <= 0 || page.Height <= 0)
                dump.Warning = $"root page reports no size ({page.Width}x{page.Height}); the window was sized with a plain ContentPage";
            else if (Math.Abs(page.Width - HarnessOptions.Width) > SizeEpsilon || Math.Abs(page.Height - HarnessOptions.Height) > SizeEpsilon)
                dump.Error = $"page is {page.Width}x{page.Height}, requested {HarnessOptions.Width}x{HarnessOptions.Height}";
            if (!dump.Settled)
                dump.Warning = (dump.Warning != null ? dump.Warning + "; " : "") + $"geometry still changing after {HarnessOptions.SettleTimeoutMs} ms";
        }
        catch (Exception ex)
        {
            dump.Error = ex.GetType().Name + ": " + ex.Message;
        }
        dump.SettleMs = (int)sw.ElapsedMilliseconds;
        return dump;
    }

    /// <summary>
    /// Window.Width/Height include whatever chrome the platform puts around the page
    /// (WinUI's title bar and borders, a client-side decoration on Linux). Size the window,
    /// read back the page size, and correct by the difference until the page itself is the
    /// requested size. The same loop works for every platform without knowing its chrome.
    /// </summary>
    private async Task EnsureSizeAsync(Page page)
    {
        double tw = HarnessOptions.Width, th = HarnessOptions.Height;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            // Wait for the page to have been laid out at all.
            for (int i = 0; i < 40 && (page.Width <= 0 || page.Height <= 0); i++)
                await Delay(50);
            await Delay(100);

            // A page that never reports a size (a platform that does not set the
            // container page's Frame) cannot drive the loop; keep the window as sized
            // by the plain sizing page and let the dump record the page size it has.
            if (page.Width <= 0 || page.Height <= 0)
            {
                Log($"  size: {page.GetType().Name} reports no size ({page.Width}x{page.Height}); window left at {_window.Width}x{_window.Height}");
                return;
            }

            double dw = tw - page.Width, dh = th - page.Height;
            if (Math.Abs(dw) <= SizeEpsilon && Math.Abs(dh) <= SizeEpsilon)
                return;

            double cw = _window.Width > 0 ? _window.Width : tw;
            double ch = _window.Height > 0 ? _window.Height : th;
            if (cw + dw > tw * 2 || ch + dh > th * 2 || cw + dw <= 0 || ch + dh <= 0)
            {
                Log($"  size: refusing to resize window to {cw + dw}x{ch + dh} (page {page.Width}x{page.Height})");
                return;
            }
            Log($"  size: page {page.Width}x{page.Height}, window {cw}x{ch}; resizing window to {cw + dw}x{ch + dh}");
            _window.Width = cw + dw;
            _window.Height = ch + dh;
            await Delay(300);
        }
    }

    private Task Delay(int ms)
    {
        var tcs = new TaskCompletionSource();
        _dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(ms), () => tcs.TrySetResult());
        return tcs.Task;
    }

    private void WriteIndex()
    {
        try
        {
            File.WriteAllText(Path.Combine(_outDir, "index.json"), JsonSerializer.Serialize(_index, DumpJson.Options));
        }
        catch (Exception ex)
        {
            Log("writing index.json failed: " + ex.Message);
        }
    }

    private void Watchdog()
    {
        _index.Error = $"watchdog: dump did not finish within {HarnessOptions.WatchdogSeconds} s";
        Log(_index.Error);
        WriteIndex();
        Environment.Exit(3);
    }

    /// <summary>To stderr and to &lt;outdir&gt;/harness.log (a WinExe has no console).</summary>
    private static void Log(string message)
    {
        string line = $"[parity {DateTime.UtcNow:HH:mm:ss.fff}] {message}";
        Console.Error.WriteLine(line);
        try
        {
            if (HarnessOptions.DumpDir is { } dir)
            {
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "harness.log"), line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }
}
