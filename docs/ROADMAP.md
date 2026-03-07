# OpenMaui Linux Platform Roadmap

This document outlines the development roadmap for the OpenMaui Linux platform.

## Version 1.0 (Released)

### Completed Features ✅

| Feature | Status | Description |
|---------|--------|-------------|
| Core Control Library | ✅ Complete | 35+ controls including Button, Label, Entry, etc. |
| SkiaSharp Rendering | ✅ Complete | Hardware-accelerated 2D graphics |
| X11 Support | ✅ Complete | Full X11 display server integration |
| Platform Services | ✅ Complete | Clipboard, file picker, notifications, etc. |
| Accessibility (AT-SPI2) | ✅ Complete | Screen reader support |
| Input Methods | ✅ Complete | IBus and XIM support |
| High DPI Support | ✅ Complete | Automatic scale factor detection |
| Drag and Drop | ✅ Complete | XDND protocol implementation |
| Global Hotkeys | ✅ Complete | System-wide keyboard shortcuts |
| XAML Support | ✅ Complete | Standard .NET MAUI XAML syntax |
| Project Templates | ✅ Complete | Code and XAML-based templates |
| Visual Studio Extension | ✅ Complete | Project templates and launch profiles |

## Version 1.1 (Next Release)

### In Progress 🚧

| Feature | Priority | Description |
|---------|----------|-------------|
| Complete Wayland Support | High | Full Wayland compositor support |
| XAML Hot Reload | High | Live XAML editing during debugging |
| Performance Optimizations | Medium | Rendering and memory improvements |

### Planned 📋

| Feature | Priority | Description |
|---------|----------|-------------|
| Hardware Video Acceleration | Medium | VA-API/VDPAU integration |
| Live Visual Tree | Medium | Debug tool for inspecting UI hierarchy |
| Theming Improvements | Medium | Better system theme integration |

## Version 1.2 (Future)

### Planned 📋

| Feature | Priority | Description |
|---------|----------|-------------|
| GTK4 Interop Layer | Low | Native GTK dialog support |
| WebView Control | Medium | Embedded web browser support |
| Maps Integration | Low | OpenStreetMap-based mapping |
| Printing Support | Medium | CUPS printing integration |

## Version 2.0 (Long-term)

### Vision 🔮

| Feature | Description |
|---------|-------------|
| Vulkan Rendering | Next-gen graphics API support |
| Flatpak Packaging | Easy distribution via Flatpak |
| Snap Packaging | Ubuntu Snap store support |
| AppImage Support | Portable Linux app format |
| Multi-window Support | Multiple top-level windows |
| System Tray Menus | Rich tray icon interactions |

## Contributing

We welcome contributions! Priority areas:

1. **Wayland Support** - Help complete the Wayland backend
2. **Testing** - Integration tests on various distributions
3. **Documentation** - API docs and tutorials
4. **Controls** - Additional control implementations
5. **Samples** - Real-world demo applications

See [CONTRIBUTING.md](../CONTRIBUTING.md) for details.

## Milestones

| Milestone | .NET / MAUI | Target | Status |
|-----------|-------------|--------|--------|
| v1.0.0 (now v9.0.0) | .NET 9 / MAUI 9.0.x | Q1 2026 | ✅ Released |
| v9.0.x | .NET 9 / MAUI 9.0.x | Q1-Q2 2026 | 🔧 Maintenance |
| v10.0.0 | .NET 10 / MAUI 10.0.x | Q4 2026 | 📋 Planned |

## Feedback

- Issues: https://git.marketally.com/open-maui/maui-linux/issues

---

*Last updated: March 2026*
*Copyright 2025-2026 MarketAlly Pte Ltd*
