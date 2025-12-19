# Frequently Asked Questions

## Visual Studio Integration

### How do I add Linux support to my existing MAUI project?

Unlike Android, iOS, and Windows which appear in Visual Studio's platform dropdown, Linux requires manual configuration since it's a community platform.

**Step 1: Add the NuGet Package**

```bash
dotnet add package OpenMaui.Controls.Linux --prerelease
```

Or in Visual Studio: Right-click project → Manage NuGet Packages → Search "OpenMaui.Controls.Linux"

**Step 2: Create a Linux Startup Project**

Create a new folder called `Platforms/Linux` in your project and add a `Program.cs`:

```csharp
using OpenMaui.Platform.Linux;

namespace MyApp.Platforms.Linux;

public class Program
{
    public static void Main(string[] args)
    {
        var app = new LinuxApplication();
        app.MainPage = new MainPage(); // Your existing MainPage
        app.Run();
    }
}
```

**Step 3: Add a Linux Build Configuration**

Add to your `.csproj`:

```xml
<PropertyGroup Condition="'$(Configuration)|$(TargetFramework)'=='Debug|net9.0'">
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
</PropertyGroup>
```

Or create a separate `MyApp.Linux.csproj` that references your shared code.

---

### Why doesn't Linux appear in Visual Studio's platform dropdown?

Visual Studio's MAUI tooling only shows platforms officially supported by Microsoft (.NET MAUI team). Linux is a community-supported platform through OpenMaui.

**Workarounds:**

1. **Use a separate Linux project** - Create a dedicated `MyApp.Linux` console project
2. **Use VS Code** - Better cross-platform support with command-line builds
3. **Use JetBrains Rider** - Excellent Linux and cross-platform support
4. **Command line** - `dotnet build -r linux-x64` works from any IDE

---

### How do I build for Linux from Visual Studio on Windows?

**Option A: Command Line (Recommended)**

Open Terminal in VS and run:
```bash
dotnet build -c Release -r linux-x64
dotnet publish -c Release -r linux-x64 --self-contained
```

**Option B: Custom Build Profile**

1. Right-click solution → Properties → Configuration Manager
2. Create a new configuration called "Linux"
3. Edit project properties to set RuntimeIdentifier for this configuration

**Option C: WSL Integration**

If you have WSL (Windows Subsystem for Linux) installed:
```bash
wsl dotnet build
wsl dotnet run
```

---

### How do I debug Linux apps from Windows?

**Option 1: Remote Debugging**

1. Install `vsdbg` on your Linux machine
2. Configure remote debugging in VS/VS Code
3. Attach to the running process

**Option 2: WSL (Recommended for development)**

1. Install WSL 2 with Ubuntu
2. Install .NET SDK in WSL
3. Run your app in WSL with X11 forwarding:
   ```bash
   export DISPLAY=:0
   dotnet run
   ```
4. Use WSLg (Windows 11) for native GUI support

**Option 3: Virtual Machine**

1. Set up a Linux VM (VMware, VirtualBox, Hyper-V)
2. Share your project folder
3. Build and run inside the VM

---

## Project Structure

### What's the recommended project structure for cross-platform MAUI with Linux?

```
MyApp/
├── MyApp.sln
├── MyApp/                      # Shared MAUI project
│   ├── MyApp.csproj
│   ├── App.xaml
│   ├── MainPage.xaml
│   ├── Platforms/
│   │   ├── Android/
│   │   ├── iOS/
│   │   ├── Windows/
│   │   └── Linux/              # Add this folder
│   │       └── Program.cs
│   └── ...
└── MyApp.Linux/                # Optional: Separate Linux project
    ├── MyApp.Linux.csproj
    └── Program.cs
```

### Should I use a separate project or add Linux to my existing project?

**Add to existing project** if:
- You want a single codebase
- Your MAUI code is mostly XAML-based
- You're comfortable with conditional compilation

**Use a separate project** if:
- You want cleaner separation
- You need Linux-specific features
- You want independent build/deploy cycles
- Your team includes Linux specialists

---

## Build & Deploy

### How do I create a Linux executable?

```bash
# Self-contained (includes .NET runtime)
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish

# Framework-dependent (smaller, requires .NET on target)
dotnet publish -c Release -r linux-x64 --no-self-contained -o ./publish
```

For ARM64 (Raspberry Pi, etc.):
```bash
dotnet publish -c Release -r linux-arm64 --self-contained -o ./publish
```

### How do I create a .deb or .rpm package?

We recommend using packaging tools:

**For .deb (Debian/Ubuntu):**
```bash
dotnet tool install -g dotnet-deb
dotnet deb -c Release -r linux-x64
```

**For .rpm (Fedora/RHEL):**
```bash
dotnet tool install -g dotnet-rpm
dotnet rpm -c Release -r linux-x64
```

**For AppImage (Universal):**
Use [AppImageKit](https://appimage.org/) with your published output.

**For Flatpak:**
Create a flatpak manifest and build with `flatpak-builder`.

---

## Common Issues

### "SkiaSharp native library not found"

Install the required native libraries:

**Ubuntu/Debian:**
```bash
sudo apt-get install libfontconfig1 libfreetype6
```

**Fedora:**
```bash
sudo dnf install fontconfig freetype
```

### "Cannot open display" or "No display server"

Ensure X11 or Wayland is running:
```bash
echo $DISPLAY        # Should show :0 or similar
echo $WAYLAND_DISPLAY # For Wayland
```

For headless/SSH sessions:
```bash
export DISPLAY=:0    # If X11 is running
```

### "libX11.so not found"

Install X11 development libraries:

**Ubuntu/Debian:**
```bash
sudo apt-get install libx11-6 libx11-dev
```

### App runs but window doesn't appear

Check if you're running under Wayland without XWayland:
```bash
# Force X11 mode
export GDK_BACKEND=x11
./MyApp
```

---

## IDE Recommendations

### What's the best IDE for developing MAUI apps with Linux support?

| IDE | Linux Dev | Windows Dev | Pros | Cons |
|-----|-----------|-------------|------|------|
| **VS Code** | ⭐⭐⭐ | ⭐⭐⭐ | Cross-platform, lightweight, great C# extension | No visual XAML designer |
| **JetBrains Rider** | ⭐⭐⭐ | ⭐⭐⭐ | Excellent cross-platform, powerful refactoring | Paid license |
| **Visual Studio** | ⭐ | ⭐⭐⭐ | Best MAUI tooling on Windows | No native Linux support |
| **Visual Studio Mac** | ⭐⭐ | N/A | Good MAUI support | macOS only |

**Our recommendation:**
- **Windows developers:** Visual Studio for Android/iOS/Windows, VS Code or command line for Linux builds
- **Linux developers:** JetBrains Rider or VS Code
- **Cross-platform teams:** VS Code with standardized build scripts

---

## Continuous Integration

### How do I set up CI/CD for Linux builds?

**GitHub Actions example:**

```yaml
jobs:
  build-linux:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'

    - name: Install dependencies
      run: |
        sudo apt-get update
        sudo apt-get install -y libx11-dev libfontconfig1-dev

    - name: Build
      run: dotnet build -c Release

    - name: Publish
      run: dotnet publish -c Release -r linux-x64 --self-contained -o ./publish

    - name: Upload artifact
      uses: actions/upload-artifact@v4
      with:
        name: linux-app
        path: ./publish
```

---

## Getting Help

- **GitHub Issues:** https://github.com/open-maui/maui-linux/issues
- **Discussions:** https://github.com/open-maui/maui-linux/discussions
- **Documentation:** https://github.com/open-maui/maui-linux/tree/main/docs

---

*Developed by [MarketAlly LLC](https://marketally.com) • Lead Architect: David H. Friedel Jr.*
