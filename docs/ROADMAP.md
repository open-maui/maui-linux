# OpenMaui Linux Platform Roadmap

This document outlines the development roadmap for the OpenMaui Linux platform.

## Version 1.0 (Current - Preview)

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

| Milestone | Target | Status |
|-----------|--------|--------|
| v1.0.0-preview.1 | Q1 2025 | ✅ Released |
| v1.0.0-preview.2 | Q1 2025 | ✅ Released |
| v1.0.0 | Q2 2025 | 🚧 In Progress |
| v1.1.0 | Q3 2025 | 📋 Planned |
| v1.2.0 | Q4 2025 | 📋 Planned |

## Feedback

- GitHub Issues: https://github.com/open-maui/maui-linux/issues
- Discussions: https://github.com/open-maui/maui-linux/discussions

---

*Last updated: January 2025*
*Copyright 2025 MarketAlly LLC*
