using System.Globalization;

namespace ParityHarness;

/// <summary>
/// Command line of the harness (the same on both platforms):
///   --dump &lt;outdir&gt;      dump every gallery page to outdir and exit
///   --size WxH            content size in device-independent units (default 800x600)
///   --pages a,b,c         only these pages (dump or interactive start page)
///   --page name           interactive: open this page directly
///   --settle-timeout ms   per page settle timeout (default 10000)
///   --timeout s           whole-run watchdog in dump mode (default 300)
/// Unknown arguments are ignored (OpenMaui parses its own, e.g. --title).
/// </summary>
public static class HarnessOptions
{
    public static string? DumpDir { get; private set; }
    public static double Width { get; private set; } = 800;
    public static double Height { get; private set; } = 600;
    public static HashSet<string>? Pages { get; private set; }
    public static string? StartPage { get; private set; }
    public static int SettleTimeoutMs { get; private set; } = 10000;
    public static int WatchdogSeconds { get; private set; } = 300;
    public static bool DumpMode => DumpDir != null;

    private static bool _parsed;

    public static void Parse(string[] args)
    {
        if (_parsed) return;
        _parsed = true;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string? next = i + 1 < args.Length ? args[i + 1] : null;
            switch (a.ToLowerInvariant())
            {
                case "--dump" when next != null:
                    DumpDir = Path.GetFullPath(next); i++;
                    break;
                case "--size" when next != null:
                    var parts = next.ToLowerInvariant().Split('x');
                    if (parts.Length == 2
                        && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                        && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                    {
                        Width = w; Height = h;
                    }
                    i++;
                    break;
                case "--pages" when next != null:
                    Pages = new HashSet<string>(next.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
                    i++;
                    break;
                case "--page" when next != null:
                    StartPage = next; i++;
                    break;
                case "--settle-timeout" when next != null && int.TryParse(next, out var st):
                    SettleTimeoutMs = st; i++;
                    break;
                case "--timeout" when next != null && int.TryParse(next, out var wd):
                    WatchdogSeconds = wd; i++;
                    break;
            }
        }
    }
}
