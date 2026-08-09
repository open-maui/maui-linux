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

## Limitations

- **Non-Shell roots** (a direct `ContentPage`/`NavigationPage` as the window
  page) get C# hot reload and a redraw, but **structural XAML reload is not
  wired** — the platform doesn't retain the type + DI context needed to
  re-instantiate the root page. Use a `Shell` root for full XAML hot reload.
- Adding/removing types, changing method signatures, and other
  [rude edits](https://learn.microsoft.com/dotnet/core/tools/dotnet-watch#hot-reload)
  require a restart, as with any .NET Hot Reload target.
- Hot reload is inert in Release / when no agent is attached; there is no runtime
  cost.
