# ParityHarness

A differential layout harness: the same gallery of MAUI pages is rendered by WinUI (the
reference) and by OpenMaui on Linux, at the same page size. Each run dumps every element's
layout frame to JSON and a screenshot of each page to PNG. A diff tool reports the elements
whose frames differ beyond a tolerance, so layout differences from real MAUI are caught
automatically, and puts the two screenshots of each page side by side with the differing
pixels marked, for what a layout dump cannot show (colours, borders, clipping, drawing).

```
tools/ParityHarness/
  ParityHarness.App/   MAUI app: gallery pages + dump mode (net10.0 on Linux, net10.0-windows on Windows)
  Diff/                console app: compares two dump folders, writes a markdown report
```

Neither project is part of the OpenMaui packages (the root project excludes `tools/**`).

## Gallery

| Page | What it exercises |
|---|---|
| GridStars | absolute/star rows and columns, spans, row/column spacing, padding, child margin |
| GridAutos | Auto rows/columns, margins, Start/Center/End, Fill with an explicit size |
| Stacks | vertical/horizontal stacks, spacing, alignment, legacy StackLayout, centred stack |
| Flex | FlexLayout wrap + SpaceBetween + AlignItems, column direction with Grow/Basis/AlignSelf, SpaceEvenly |
| Absolute | AbsoluteLayout absolute, proportional and AutoSize bounds |
| ScrollShort | ScrollView content shorter than the viewport; ScrollView centred in a frame |
| ScrollTall | content taller than the viewport; horizontal ScrollView |
| BorderFrame | Border/Frame padding and stroke, auto-size around fixed content |
| Labels | wrapping, truncation, MaxLines, CharacterWrap, padding, Auto cell |
| Images | Aspect modes in fixed boxes, unsized images, image in a Border |
| CollectionView | header, footer, item spacing, DataTemplateSelector with two heights |
| CollectionEmpty | EmptyView |
| NavigationPage | NavigationPage title area + content |
| Shell | Shell title area + content |
| Nested | ScrollView > stack > Border > Grid > FlexLayout with margins and padding |

Content is mostly BoxViews with explicit sizes, so it is measure-independent. Text uses the
bundled Open Sans (`Resources/Fonts`, Apache-2.0) on both platforms; text widths still differ
across platforms (different shapers and rasterisers), which the diff absorbs with a separate,
larger tolerance.

Every element has a stable `AutomationId`; templated (recyclable) views use the re-settable
`DumpKey.Key` attached property instead. Elements without either are keyed
`<parent key>/<Type>` (`[2]`, `[3]`, ... for siblings of the same type).

## Running locally

Linux (needs a display: a desktop session, or Xvfb, see below):

```bash
dotnet build tools/ParityHarness/ParityHarness.App -c Release
dotnet <bin>/ParityHarness.App.dll --dump /tmp/dump-linux --size 800x600
```

`<bin>` is `tools/ParityHarness/ParityHarness.App/bin/Release/net10.0` (or
`<artifacts>/bin/ParityHarness.App/release_net10.0` with `--artifacts-path`). Under Wayland it
renders natively; `MAUI_PREFER_X11=1` uses X11/XWayland. Without a display:

```bash
xvfb-run -a -s "-screen 0 1600x1200x24" dotnet <bin>/ParityHarness.App.dll --dump /tmp/dump-linux
```

Windows (.NET 10 SDK + `maui` workload):

```powershell
dotnet publish tools/ParityHarness/ParityHarness.App -f net10.0-windows10.0.19041.0 -c Release `
  -r win-x64 --self-contained -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -o out/win
out/win/ParityHarness.App.exe --dump C:\temp\dump-windows --size 800x600
```

The exe is a GUI app: it returns immediately in a console, so wait for it
(`Start-Process ... -Wait`) and read `harness.log` in the dump folder.

Compare:

```bash
dotnet run --project tools/ParityHarness/Diff -c Release -- /tmp/dump-windows /tmp/dump-linux --out report.md
```

The first folder is the reference, the second the candidate.

### App options

| Option | Meaning |
|---|---|
| `--dump <dir>` | dump mode: visit every page, write `<page>.json`, `index.json`, `harness.log`, exit |
| `--size WxH` | page size in device-independent units (default 800x600) |
| `--pages a,b` | only these pages |
| `--page name` | interactive: open this page directly (without `--dump` the app shows an index of pages) |
| `--settle-timeout ms` | per-page settle timeout (default 10000) |
| `--timeout s` | whole-run watchdog in dump mode (default 300, exit code 3) |

Exit codes: 0 all pages dumped, 1 a page reported an error (e.g. wrong page size), 2 the run
failed, 3 watchdog.

### How the dump works

1. The window starts with a plain ContentPage. The runner sets `Window.Width/Height`, reads the
   page's size back and corrects the window by the difference until the page itself is
   exactly `--size`. This absorbs whatever chrome the platform adds (WinUI title bar and
   borders, client-side decorations) with no platform code.
2. For each gallery page it sets `Window.Page`, re-checks the size, then polls the tree every
   150 ms until the geometry is identical three polls in a row (at least 600 ms).
3. It walks `IVisualTreeElement.GetVisualChildren()` from the page and records, per
   VisualElement: `key`, `automationId`, `type`, `parentKey`, `depth`, `isVisible`, `text`
   (the element shows text: `IText`/`ITextInput`), `containsText`, and two rects relative
   to the page:
   - `frame`: the MAUI layout frame (`VisualElement.Frame`) accumulated up the parent chain.
     This is what MAUI's cross-platform layout decided.
   - `native`: where the platform view actually is, relative to the page's platform view
     (WinUI `TransformToVisual`; OpenMaui `SkiaView.ScreenBounds`). This catches a platform
     that draws a view somewhere other than its frame, and positions a frame cannot express
     (CollectionView cells, pages under a navigation bar).

A root NavigationPage/Shell that reports no size of its own is dumped with a `warning`, not
an error (the window was already sized by the plain page).

## Running in CI

`.gitea/workflows/parity.yml` is manual only (Actions > Layout parity > Run workflow) and
separate from the CI, tests and release workflows: none of them waits for it, and it publishes
nothing. Inputs: `size`, `tolerance`, `text_tolerance`, `pages`.

- `dump-windows` (runner label `windows`): publishes the unpackaged, self-contained WinUI app
  and runs the dump. WinUI needs an interactive desktop: the act_runner must run as a
  logged-in user, not as a Windows service (session 0 has no usable desktop); the job checks
  this first and fails with that message. It installs the `maui-windows` workload when it is
  missing (which may need an elevated runner; otherwise install it once by hand).
- `dump-linux` (`linux-latest`): installs Xvfb and the runtime libraries (GTK 3, libX11,
  fontconfig) with apt or dnf if missing, publishes the app and runs it under `xvfb-run` with
  X11 and the raster renderer. It assumes the image is Debian/Ubuntu or Fedora and the job
  runs as root or has sudo.
- `diff`: downloads both dumps, runs the diff, uploads `parity-report` (report.md,
  report.json, `images/` with each page's screenshots side by side, both index.json files
  and harness logs) and fails if the layout diff failed (the visual comparison only
  reports). It runs even when a dump
  job failed, so a partial dump is still compared.

Artifacts: `parity-dump-windows`, `parity-dump-linux`, `parity-report`.

## Reading the report

The summary table has one row per page: elements compared, elements over tolerance, elements
within tolerance (in brackets: of those, how many were not exactly equal), elements present on
one side only, status.

Each failing page then lists:

- notes: dump errors/warnings, unsettled layout, different page sizes, a channel one side
  could not report (noted, never failed);
- **Over tolerance**: element, type, channel (`frame`, `native`, or `visible` for an
  IsVisible mismatch), both rects as `x, y WxH`, the delta (candidate minus reference) and the
  tolerance applied, marked `(text)` when the text tolerance was used;
- **Only in reference / candidate**: elements one platform has in its visual tree and the
  other does not (a missing AutomationId, or a control that does not expose its children).

Look at the topmost differing element of a subtree first: a container off by N moves all its
descendants by N.

### Diff options

| Option | Default | Meaning |
|---|---|---|
| `--tol px` | 1 | tolerance for non-text elements (max of the x, y, w, h deltas) |
| `--text-tol px` | 16 | tolerance for text-bearing elements |
| `--text-scope self\|ancestors` | ancestors | text tolerance for text elements only, or also for every element containing text |
| `--channel frame\|native\|both` | both | which rects to compare |
| `--pages a,b` | all | only these pages |
| `--allow-missing` | off | one-side-only elements/pages do not fail the run |
| `--out file.md` / `--json file.json` | | write the report (markdown is always printed too) |
| `--max-rows n` | 200 | rows per table |
| `--images dir` | | also compare the screenshots: write reference / candidate / difference images to `dir` and a visual section to the report (never fails the run; text rasterises differently per platform) |

Exit codes: 0 within tolerance, 1 differences (or one-side-only elements without
`--allow-missing`, or a dump error), 2 usage or input error.

Text tolerance only covers the text element and its containers; an element laid out after a
text element (e.g. below a wrapping Label in a stack) moves with the text and is compared with
the normal tolerance, unless the page marks it with `DumpKey.TextDependent` (recorded as
`textDependent`; the Labels page does this for the box after its Auto-width label). Gallery
pages keep such dependencies to the Labels page.

## Adding a page

Add a factory to one of the `Gallery/*Pages.cs` files and an entry to
`GalleryCatalog.All`. Give every element an AutomationId (`Ui.Box("id", w, h)`,
`Ui.Text("id", ...)`, `.Id("id")`), unique within the page.
