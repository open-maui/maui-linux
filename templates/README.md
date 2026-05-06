# OpenMaui Linux Project Templates

Project templates for building .NET MAUI applications on Linux desktop using OpenMaui.

## Installation

```bash
dotnet new install OpenMaui.Linux.Templates
```

## Templates

### openmaui-linux-app

Basic Linux app with code-based UI.

```bash
dotnet new openmaui-linux-app -n MyApp
cd MyApp
dotnet run
```

### openmaui-linux-xaml-app

Full XAML support with standard MAUI syntax.

```bash
dotnet new openmaui-linux-xaml-app -n MyXamlApp
cd MyXamlApp
dotnet run
```

## Requirements

- .NET 10.0 SDK or later
- Linux with X11 or Wayland
- SkiaSharp native dependencies (`libSkiaSharp.so`)

## Related Packages

- [OpenMaui.Controls.Linux](https://www.nuget.org/packages/OpenMaui.Controls.Linux) - Control library
- [OpenMaui.Hosting](https://www.nuget.org/packages/OpenMaui.Hosting) - Hosting integration

## License

MIT - See [LICENSE](https://github.com/open-maui/maui-linux/blob/main/LICENSE)
