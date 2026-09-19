# Hot Reload

This platform supports .NET Hot Reload so you can edit your app and see the
change without a restart. Because pages render through Skia handlers (not native
controls), the platform hooks the underlying .NET hot-reload signal directly and
re-renders the affected page's Skia tree — see
`Diagnostics/HotReloadService.cs` and `LinuxApplication.HotReload.cs`.

## Launch under `dotnet watch`

Run the app through the watch tool instead of `dotnet run`:

```bash
dotnet watch --project path/to/YourApp.csproj
```

`dotnet watch` starts the app with the hot-reload agent attached
(`MetadataUpdater.IsSupported` becomes true) and enables XAML hot reload
(`MauiXamlHotReload`). Edit a `.cs` or `.xaml` file and save — the watcher
applies the delta and this platform re-renders the current page.

You can confirm the agent was detected by the startup log line:

```
[HotReload] Hot-reload agent detected; XAML/C# edits will re-render the current page.
```

## What works

- **C# method-body edits** — applied by CoreCLR; the new code runs on the next
  call. Edits that change what a page draws are reflected on the next re-render.
- **XAML edits (Shell-based apps)** — each re-render rebuilds every Shell section
  from its template, so a fresh `InitializeComponent` picks up the new XAML.
  Navigation state (selected section/item) is preserved.
- **XAML edits (non-Shell roots)** — a direct `ContentPage`, `NavigationPage`,
  or other page as the window page. The platform retains the root page and its
  DI context at startup; on a delta it builds a fresh instance (DI-first, then
  `Activator`), re-parents it into the MAUI `Window` (so bindings/resources and
  `Appearing`/`Disappearing` lifecycle work), and swaps the root Skia tree.

## Limitations

- **`NavigationPage` roots reset to their root page** on a structural reload:
  the stack is rebuilt from a fresh root instance, so pushed pages are popped.
  This matches MAUI's own hot-reload semantics — `MauiHotReloadHelper`
  replaces an edited page with a fresh `Activator.CreateInstance` and never
  reconstructs runtime navigation stacks. Bar styling set in code
  (`BarBackgroundColor`, `BarTextColor`, `Title`) is carried over for plain
  `new NavigationPage(page)` roots; other code-set state on the old root
  instance is not.
- **Pages built inline** (e.g. a root constructed with runtime arguments the DI
  container can't supply) fall back to the parameterless constructor; if none
  exists, the reload is skipped and only a redraw happens.
- Adding/removing types, changing method signatures, and other
  [rude edits](https://learn.microsoft.com/dotnet/core/tools/dotnet-watch#hot-reload)
  require a restart, as with any .NET Hot Reload target.
- Hot reload is inert in Release / when no agent is attached; there is no runtime
  cost.
