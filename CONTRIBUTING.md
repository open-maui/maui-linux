# Contributing to .NET MAUI Linux Platform

Thank you for your interest in contributing to the .NET MAUI Linux Platform! This project is developed and maintained by [MarketAlly LLC](https://marketally.com) under the leadership of David H. Friedel Jr.

This document provides guidelines and information for contributors.

## Code of Conduct

This project follows the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct). By participating, you are expected to uphold this code.

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- Linux development environment (Ubuntu 22.04+ recommended)
- Git

### Setting Up the Development Environment

1. Fork and clone the repository:
```bash
git clone https://github.com/your-username/maui-linux.git
cd maui-linux
```

2. Install dependencies:
```bash
# Ubuntu/Debian
sudo apt-get install libx11-dev libxrandr-dev libxcursor-dev libxi-dev libgl1-mesa-dev

# Fedora
sudo dnf install libX11-devel libXrandr-devel libXcursor-devel libXi-devel mesa-libGL-devel
```

3. Build the project:
```bash
dotnet build
```

4. Run tests:
```bash
dotnet test
```

## How to Contribute

### Reporting Bugs

- Check if the bug has already been reported in [Issues](https://github.com/open-maui/maui-linux/issues)
- Use the bug report template
- Include reproduction steps, expected behavior, and actual behavior
- Include system information (distro, .NET version, desktop environment)

### Suggesting Features

- Check existing feature requests first
- Use the feature request template
- Explain the use case and benefits

### Pull Requests

1. Create a branch from `main`:
```bash
git checkout -b feature/your-feature-name
```

2. Make your changes following the coding guidelines

3. Add or update tests as needed

4. Ensure all tests pass:
```bash
dotnet test
```

5. Commit your changes:
```bash
git commit -m "Add feature: description"
```

6. Push and create a pull request

## Coding Guidelines

### Code Style

- Use C# 12 features where appropriate
- Follow [.NET naming conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use `var` for obvious types
- Prefer expression-bodied members for simple methods
- Use nullable reference types

### File Organization

```
Views/          # Skia-rendered view implementations
  Skia*.cs      # View classes (SkiaButton, SkiaLabel, etc.)

Handlers/       # MAUI handler implementations
  *Handler.cs   # Platform handlers

Services/       # Platform services
  *Service.cs   # Service implementations

Rendering/      # Rendering infrastructure
  *.cs          # Rendering helpers and caches

tests/          # Unit tests
  Views/        # View tests
  Services/     # Service tests
```

### Naming Conventions

- Views: `Skia{ControlName}` (e.g., `SkiaButton`, `SkiaCarouselView`)
- Handlers: `{ControlName}Handler` (e.g., `ButtonHandler`)
- Services: `{Feature}Service` (e.g., `ClipboardService`)
- Tests: `{ClassName}Tests` (e.g., `SkiaButtonTests`)

### Documentation

- Add XML documentation to public APIs
- Update README and docs for new features
- Include code examples where helpful

Example:
```csharp
/// <summary>
/// A horizontally scrolling carousel view with snap-to-item behavior.
/// </summary>
public class SkiaCarouselView : SkiaLayoutView
{
    /// <summary>
    /// Gets or sets the current position (0-based index).
    /// </summary>
    public int Position { get; set; }
}
```

### Testing

- Write unit tests for new functionality
- Maintain test coverage above 80%
- Use descriptive test names: `MethodName_Condition_ExpectedResult`

Example:
```csharp
[Fact]
public void Position_WhenSetToValidValue_UpdatesPosition()
{
    var carousel = new SkiaCarouselView();
    carousel.AddItem(new SkiaLabel());

    carousel.Position = 0;

    Assert.Equal(0, carousel.Position);
}
```

## Architecture Overview

### Rendering Pipeline

1. `LinuxApplication` creates the main window
2. `SkiaRenderingEngine` manages the render loop
3. Views implement `Draw(SKCanvas canvas)` for rendering
4. `DirtyRectManager` optimizes partial redraws

### View Hierarchy

```
SkiaView (base class)
├── SkiaLayoutView (containers)
│   ├── SkiaStackLayout
│   ├── SkiaScrollView
│   └── SkiaCarouselView
└── Control views
    ├── SkiaButton
    ├── SkiaLabel
    └── SkiaEntry
```

### Handler Pattern

Handlers connect MAUI virtual views to platform-specific implementations:

```csharp
public class ButtonHandler : ViewHandler<IButton, SkiaButton>
{
    public static void MapText(ButtonHandler handler, IButton button)
    {
        handler.PlatformView.Text = button.Text;
    }
}
```

## Development Workflow

### Branch Naming

- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation updates
- `refactor/` - Code refactoring
- `test/` - Test additions/fixes

### Commit Messages

Follow conventional commits:
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation
- `test:` - Tests
- `refactor:` - Code refactoring
- `chore:` - Maintenance

### Pull Request Checklist

- [ ] Code follows style guidelines
- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] All tests pass
- [ ] No breaking changes (or documented if unavoidable)

## Areas for Contribution

### High Priority

- Additional control implementations
- Accessibility improvements (AT-SPI2)
- Performance optimizations
- Wayland support improvements

### Good First Issues

Look for issues labeled `good-first-issue` for beginner-friendly tasks.

### Documentation

- API documentation improvements
- Tutorials and guides
- Sample applications

## Getting Help

- Open a [Discussion](https://github.com/open-maui/maui-linux/discussions) for questions
- Join the .NET community on Discord
- Check existing issues and discussions

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to .NET MAUI on Linux!
