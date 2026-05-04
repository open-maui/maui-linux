# OpenMaui Visual Studio Extension

This Visual Studio extension adds Linux platform support for .NET MAUI applications.

## Features

### Project Templates
When installed, you'll see **"OpenMaui Linux App"** in Visual Studio's New Project dialog:

```
File → New → Project → Search "OpenMaui"
```

### Launch Profiles
The template includes pre-configured launch profiles:

| Profile | Description |
|---------|-------------|
| **Linux (Local)** | Run directly (requires Linux or WSL with GUI) |
| **Linux (WSL)** | Run via Windows Subsystem for Linux |
| **Linux (x64 Release)** | Build and run release for x64 |
| **Linux (ARM64 Release)** | Build and run release for ARM64 |
| **Publish Linux x64** | Create self-contained x64 package |
| **Publish Linux ARM64** | Create self-contained ARM64 package |

### How It Works

```
┌─────────────────────────────────────────────────────────┐
│                   Visual Studio                          │
├─────────────────────────────────────────────────────────┤
│  File → New → Project                                    │
│    └── OpenMaui Linux App  ←  This extension adds this  │
├─────────────────────────────────────────────────────────┤
│  Debug Dropdown                                          │
│    ├── Linux (Local)                                     │
│    ├── Linux (WSL)        ←  Launch profiles             │
│    ├── Linux (x64 Release)                               │
│    └── Publish Linux...                                  │
└─────────────────────────────────────────────────────────┘
```

## Installation

### From Visual Studio Marketplace
1. Open Visual Studio 2022
2. Extensions → Manage Extensions
3. Search for "OpenMaui"
4. Click Download and restart VS

### From VSIX File
1. Download `OpenMaui.VisualStudio.vsix`
2. Double-click to install
3. Restart Visual Studio

## Building the Extension

### Prerequisites
- Visual Studio 2022 with "Visual Studio extension development" workload
- .NET Framework 4.8 Developer Pack

### Build Steps
```bash
cd vsix/OpenMaui.VisualStudio
dotnet restore
msbuild /p:Configuration=Release
```

The VSIX will be in `bin/Release/OpenMaui.VisualStudio.vsix`

## Project Structure

```
vsix/
└── OpenMaui.VisualStudio/
    ├── OpenMaui.VisualStudio.csproj    # Extension project
    ├── source.extension.vsixmanifest   # VSIX metadata
    ├── ProjectTemplates/               # VS project templates
    │   └── OpenMauiLinuxApp/
    │       ├── OpenMauiLinuxApp.vstemplate
    │       ├── OpenMauiLinuxApp.csproj
    │       ├── Program.cs
    │       ├── App.cs
    │       ├── MainPage.cs
    │       ├── MainPage.xaml
    │       └── Properties/
    │           └── launchSettings.json
    └── Resources/
        ├── Icon.png
        └── Preview.png
```

## Adding Linux to Existing MAUI Projects

If you have an existing MAUI project and want to add Linux support:

### Option 1: Add Platform Folder
1. Add `Platforms/Linux/Program.cs` to your project
2. Add `OpenMaui.Controls.Linux` NuGet package
3. Copy `launchSettings.json` from template to `Properties/`

### Option 2: Create Companion Project
1. Create new "OpenMaui Linux App" project
2. Reference your shared MAUI library
3. Build Linux version separately

## Debugging on Linux

### Via WSL (Recommended)
1. Install WSL 2 with Ubuntu
2. Install .NET SDK in WSL: `sudo apt install dotnet-sdk-9.0`
3. Install X11 libs: `sudo apt install libx11-6`
4. Select "Linux (WSL)" profile and press F5

### Via Remote Machine
1. Set up SSH access to Linux machine
2. Install `vsdbg` on remote machine
3. Configure remote debugging in VS

### Via Virtual Machine
1. Set up Linux VM (VMware, VirtualBox, Hyper-V)
2. Share project folder with VM
3. Build and run inside VM

## Troubleshooting

### "Cannot find 'wsl.exe'"
Install WSL: `wsl --install` in PowerShell (Admin)

### "Display not found"
Ensure WSLg is enabled (Windows 11) or configure X server (Windows 10)

### Template not appearing
1. Clear template cache: `devenv /updateconfiguration`
2. Restart Visual Studio

## License

MIT License - Copyright (c) 2025 MarketAlly LLC

---

*Developed by MarketAlly LLC • Lead Architect: David H. Friedel Jr.*
