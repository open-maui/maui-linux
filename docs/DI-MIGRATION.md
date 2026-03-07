<!-- Licensed to the .NET Foundation under one or more agreements.
     The .NET Foundation licenses this file to you under the MIT license. -->

# Singleton-to-DI Migration Plan

This document catalogs all singleton service classes in the maui-linux project that use a
static `Instance` property pattern instead of proper dependency injection, and provides a
migration plan for each.

---

## Identified Singleton Services

### 1. GtkHostService

| Property | Value |
|---|---|
| **File** | `Services/GtkHostService.cs` |
| **Pattern** | `_instance ??= new GtkHostService()` (null-coalescing assignment) |
| **Interface** | None (concrete class) |
| **Static references** | 8 across 6 files |
| **Init/Cleanup** | `Initialize(title, width, height)` must be called before use; `Shutdown()` must be called on exit |
| **DI Registration** | `AddSingleton<GtkHostService>` |
| **Difficulty** | **Hard** |

**Files referencing `GtkHostService.Instance`:**
- `LinuxApplication.cs` (2) -- creates/configures host window
- `LinuxApplication.Lifecycle.cs` (1) -- calls `Shutdown()`
- `Hosting/LinuxProgramHost.cs` (1) -- calls `Initialize()`
- `Hosting/LinuxMauiAppBuilderExtensions.cs` (1) -- DI registration (already wrapping Instance)
- `Handlers/GtkWebViewPlatformView.cs` (1) -- reads `HostWindow`
- `Handlers/GtkWebViewHandler.cs` (2) -- reads host service

**Notes:** This service is initialized very early in the startup path (`LinuxProgramHost`) before the
DI container is fully built, and is also accessed by `LinuxApplication` which itself is constructed
before DI resolution. Migration requires refactoring the startup pipeline so that `GtkHostService`
is constructed by the container and then explicitly initialized via a hosted-service or startup hook.

---

### 2. SystemThemeService

| Property | Value |
|---|---|
| **File** | `Services/SystemThemeService.cs` |
| **Pattern** | Double-checked locking with `lock (_lock)` |
| **Interface** | None (concrete class) |
| **Static references** | 3 across 2 files (excluding its own definition) |
| **Init/Cleanup** | Constructor auto-detects theme and starts a `FileSystemWatcher` + polling `Timer` |
| **DI Registration** | `AddSingleton<SystemThemeService>` |
| **Difficulty** | **Medium** |

**Files referencing `SystemThemeService.Instance`:**
- `LinuxApplication.Lifecycle.cs` (2) -- reads `CurrentTheme`, subscribes to `ThemeChanged`
- `Services/AppInfoService.cs` (1) -- reads `CurrentTheme` for `RequestedTheme`

**Notes:** Already registered in DI (`TryAddSingleton<SystemThemeService>()`), but the registration
does not use the static Instance -- it lets the container construct it. However, call sites still
use `SystemThemeService.Instance` directly instead of resolving from the container. `AppInfoService`
itself is a singleton and would need the `SystemThemeService` injected via constructor.

---

### 3. FontFallbackManager

| Property | Value |
|---|---|
| **File** | `Services/FontFallbackManager.cs` |
| **Pattern** | Double-checked locking with `lock (_lock)` |
| **Interface** | None (concrete class) |
| **Static references** | 4 across 3 files (excluding its own definition) |
| **Init/Cleanup** | Constructor pre-caches top 10 fallback font typefaces |
| **DI Registration** | `AddSingleton<FontFallbackManager>` |
| **Difficulty** | **Medium** |

**Files referencing `FontFallbackManager.Instance`:**
- `Hosting/LinuxMauiAppBuilderExtensions.cs` (1) -- DI registration (wrapping Instance)
- `Views/SkiaLabel.cs` (2) -- calls `ShapeTextWithFallback`
- `Rendering/TextRenderingHelper.cs` (1) -- calls `ShapeTextWithFallback`

**Notes:** The view and rendering classes currently access the static Instance directly rather than
accepting it via constructor injection. These classes would need to receive `FontFallbackManager`
through the handler or view constructor.

---

### 4. InputMethodServiceFactory

| Property | Value |
|---|---|
| **File** | `Services/InputMethodServiceFactory.cs` |
| **Pattern** | Static factory class with double-checked locking |
| **Interface** | Returns `IInputMethodService` |
| **Static references** | 3 across 3 files (excluding its own definition) |
| **Init/Cleanup** | Factory auto-detects IBus/Fcitx5/XIM; `Reset()` calls `Shutdown()` on existing instance |
| **DI Registration** | `AddSingleton<IInputMethodService>` (via factory delegate) |
| **Difficulty** | **Medium** |

**Files referencing `InputMethodServiceFactory.Instance`:**
- `Hosting/LinuxMauiAppBuilderExtensions.cs` (1) -- DI registration (wrapping Instance)
- `Views/SkiaEditor.cs` (1) -- assigns to `_inputMethodService` field
- `Views/SkiaEntry.cs` (1) -- assigns to `_inputMethodService` field

**Notes:** The factory pattern can be replaced with a DI factory registration:
`AddSingleton<IInputMethodService>(sp => InputMethodServiceFactory.CreateService())`.
View classes should receive `IInputMethodService` via constructor injection.

---

### 5. AccessibilityServiceFactory

| Property | Value |
|---|---|
| **File** | `Services/AccessibilityServiceFactory.cs` |
| **Pattern** | Static factory class with double-checked locking |
| **Interface** | Returns `IAccessibilityService` |
| **Static references** | 2 across 2 files (excluding its own definition) |
| **Init/Cleanup** | Factory creates `AtSpi2AccessibilityService` and calls `Initialize()`; `Reset()` calls `Shutdown()` |
| **DI Registration** | `AddSingleton<IAccessibilityService>` (via factory delegate) |
| **Difficulty** | **Easy** |

**Files referencing `AccessibilityServiceFactory.Instance`:**
- `Hosting/LinuxMauiAppBuilderExtensions.cs` (1) -- DI registration (wrapping Instance)
- `Views/SkiaView.Accessibility.cs` (1) -- accesses the service

**Notes:** Similar to `InputMethodServiceFactory`. Replace with a factory delegate in DI
registration. Only one non-registration call site to update.

---

### 6. ConnectivityService

| Property | Value |
|---|---|
| **File** | `Services/ConnectivityService.cs` |
| **Pattern** | `Lazy<ConnectivityService>` |
| **Interface** | `IConnectivity`, `IDisposable` |
| **Static references** | 1 across 1 file (excluding its own definition) |
| **Init/Cleanup** | Constructor subscribes to `NetworkChange` events; `Dispose()` unsubscribes |
| **DI Registration** | `AddSingleton<IConnectivity>` |
| **Difficulty** | **Easy** |

**Files referencing `ConnectivityService.Instance`:**
- `Hosting/LinuxMauiAppBuilderExtensions.cs` (1) -- DI registration

**Notes:** Already only referenced via the DI registration line. Migration is trivial: change to
`AddSingleton<IConnectivity, ConnectivityService>()`. The constructor is already `public`.

---

### 7. DeviceDisplayService

| Property | Value |
|---|---|
| **File** | `Services/DeviceDisplayService.cs` |
| **Pattern** | `Lazy<DeviceDisplayService>` |
| **Interface** | `IDeviceDisplay` |
| **Static references** | 1 across 1 file (excluding its own definition) |
| **Init/Cleanup** | Constructor calls `RefreshDisplayInfo()`; internally references `MonitorService.Instance` |
| **DI Registration** | `AddSingleton<IDeviceDisplay>` |
| **Difficulty** | **Easy** |

**Files referencing `DeviceDisplayService.Instance`:**
- `Hosting/LinuxMauiAppBuilderExtensions.cs` (1) -- DI registration

**Notes:** Only referenced at the DI registration point. The dependency on `MonitorService.Instance`
inside `RefreshDisplayInfo()` means `MonitorService` should be migrated first (or simultaneously)
and injected via constructor.

---

### 8. AppInfoService

| Property | Value |
|---|---|
| **File** | `Services/AppInfoService.cs` |
| **Pattern** | `Lazy<AppInfoService>` |
| **Interface** | `IAppInfo` |
| **Static references** | 1 across 1 file (excluding its own definition) |
| **Init/Cleanup** | Constructor reads assembly metadata; `RequestedTheme` accesses `SystemThemeService.Instance` |
| **DI Registration** | `AddSingleton<IAppInfo>` |
| **Difficulty** | **Easy** |

**Files referencing `AppInfoService.Instance`:**
- `Hosting/LinuxMauiAppBuilderExtensions.cs` (1) -- DI registration

**Notes:** Only referenced at the DI registration point. Has an internal dependency on
`SystemThemeService.Instance` that should be replaced with constructor injection of
`SystemThemeService`.

---

### 9. DeviceInfoService

| Property | Value |
|---|---|
| **File** | `Services/DeviceInfoService.cs` |
| **Pattern** | `Lazy<DeviceInfoService>` |
| **Interface** | `IDeviceInfo` |
| **Static references** | 1 across 1 file (excluding its own definition) |
| **Init/Cleanup** | Constructor reads `/sys/class/dmi/id/` files |
| **DI Registration** | `AddSingleton<IDeviceInfo>` |
| **Difficulty** | **Easy** |

**Files referencing `DeviceInfoService.Instance`:**
- `Hosting/LinuxMauiAppBuilderExtensions.cs` (1) -- DI registration

**Notes:** No dependencies on other singletons. Simplest migration candidate.

---

### 10. MonitorService

| Property | Value |
|---|---|
| **File** | `Services/MonitorService.cs` |
| **Pattern** | Double-checked locking with `lock (_lock)` |
| **Interface** | `IDisposable` |
| **Static references** | 2 across 2 files (excluding its own definition) |
| **Init/Cleanup** | Lazy initialization via `EnsureInitialized()`; opens X11 display; `Dispose()` closes display |
| **DI Registration** | `AddSingleton<MonitorService>` |
| **Difficulty** | **Easy** |

**Files referencing `MonitorService.Instance`:**
- `Hosting/LinuxMauiAppBuilderExtensions.cs` (1) -- DI registration (wrapping Instance)
- `Services/DeviceDisplayService.cs` (1) -- accesses `PrimaryMonitor`

**Notes:** Referenced by `DeviceDisplayService` internally. Both should be migrated together, with
`MonitorService` injected into `DeviceDisplayService` via constructor.

---

### 11. LinuxDispatcherProvider

| Property | Value |
|---|---|
| **File** | `Dispatching/LinuxDispatcherProvider.cs` |
| **Pattern** | `_instance ?? (_instance = new ...)` (not thread-safe) |
| **Interface** | `IDispatcherProvider` |
| **Static references** | 2 across 2 files (excluding its own definition) |
| **Init/Cleanup** | None |
| **DI Registration** | `AddSingleton<IDispatcherProvider>` |
| **Difficulty** | **Easy** |

**Files referencing `LinuxDispatcherProvider.Instance`:**
- `Hosting/LinuxMauiAppBuilderExtensions.cs` (1) -- DI registration
- `LinuxApplication.Lifecycle.cs` (1) -- used during lifecycle setup

**Notes:** Minimal state, no initialization. Trivial to migrate.

---

## Services That SHOULD Remain Static

### DiagnosticLog

| Property | Value |
|---|---|
| **File** | `Services/DiagnosticLog.cs` |
| **Pattern** | Pure static class (not a singleton Instance pattern) |
| **References** | 518 occurrences across 74 files |

**Rationale for keeping static:** `DiagnosticLog` is used pervasively throughout the entire codebase,
including inside constructors of the singleton services themselves, during startup before the DI
container is built, in native interop code, in static factory methods, and in exception handlers.
Converting it to an injected dependency would require threading a logger parameter through every
class in the project and would create circular dependency issues (services log during their own
construction). It is a cross-cutting concern that is appropriately static.

---

## Recommended DI Registrations

Replace the current registration block in `Hosting/LinuxMauiAppBuilderExtensions.cs` with:

```csharp
// Dispatcher
builder.Services.TryAddSingleton<IDispatcherProvider, LinuxDispatcherProvider>();

// Device services (no inter-dependencies)
builder.Services.TryAddSingleton<IDeviceInfo, DeviceInfoService>();
builder.Services.TryAddSingleton<IConnectivity, ConnectivityService>();

// Theme and monitor services
builder.Services.TryAddSingleton<SystemThemeService>();
builder.Services.TryAddSingleton<MonitorService>();

// Services with dependencies on theme/monitor
builder.Services.TryAddSingleton<IAppInfo, AppInfoService>();          // depends on SystemThemeService
builder.Services.TryAddSingleton<IDeviceDisplay, DeviceDisplayService>(); // depends on MonitorService

// Factory-created services
builder.Services.TryAddSingleton<IAccessibilityService>(sp =>
{
    try
    {
        var service = new AtSpi2AccessibilityService();
        service.Initialize();
        return service;
    }
    catch
    {
        return new NullAccessibilityService();
    }
});

builder.Services.TryAddSingleton<IInputMethodService>(sp =>
    InputMethodServiceFactory.CreateService());

// Infrastructure services
builder.Services.TryAddSingleton<FontFallbackManager>();
builder.Services.TryAddSingleton<GtkHostService>();
```

---

## Migration Priority by Difficulty

### Easy (change registration + remove static Instance; few or no call-site changes)

1. **DeviceInfoService** -- zero non-registration references; no dependencies
2. **ConnectivityService** -- zero non-registration references; implements `IDisposable`
3. **LinuxDispatcherProvider** -- one non-registration reference in `LinuxApplication.Lifecycle.cs`
4. **DeviceDisplayService** -- zero non-registration references; depends on `MonitorService`
5. **MonitorService** -- one non-registration reference (in `DeviceDisplayService`); migrate together with #4
6. **AppInfoService** -- zero non-registration references; depends on `SystemThemeService`
7. **AccessibilityServiceFactory** -- one non-registration reference; factory pattern

### Medium (requires updating view/rendering classes to accept injected dependencies)

8. **SystemThemeService** -- two non-registration references in `LinuxApplication.Lifecycle.cs`; also depended on by `AppInfoService`
9. **FontFallbackManager** -- three non-registration references in view/rendering code
10. **InputMethodServiceFactory** -- two non-registration references in view code (`SkiaEditor`, `SkiaEntry`)

### Hard (requires refactoring the application startup pipeline)

11. **GtkHostService** -- seven non-registration references across application core, startup host, and handler code; initialized before DI container is built

---

## Migration Example: DeviceInfoService (Before/After)

### Before

**Services/DeviceInfoService.cs:**
```csharp
public class DeviceInfoService : IDeviceInfo
{
    private static readonly Lazy<DeviceInfoService> _instance =
        new Lazy<DeviceInfoService>(() => new DeviceInfoService());

    public static DeviceInfoService Instance => _instance.Value;

    public DeviceInfoService()
    {
        LoadDeviceInfo();
    }
    // ...
}
```

**Hosting/LinuxMauiAppBuilderExtensions.cs:**
```csharp
builder.Services.TryAddSingleton<IDeviceInfo>(DeviceInfoService.Instance);
```

### After

**Services/DeviceInfoService.cs:**
```csharp
public class DeviceInfoService : IDeviceInfo
{
    // Remove: private static readonly Lazy<DeviceInfoService> _instance = ...
    // Remove: public static DeviceInfoService Instance => ...

    public DeviceInfoService()
    {
        LoadDeviceInfo();
    }
    // ... (rest unchanged)
}
```

**Hosting/LinuxMauiAppBuilderExtensions.cs:**
```csharp
builder.Services.TryAddSingleton<IDeviceInfo, DeviceInfoService>();
```

No other files reference `DeviceInfoService.Instance`, so no further changes are needed. The DI
container will construct the instance lazily on first resolution and manage its lifetime.

---

## Dependency Graph for Migration Order

```
DeviceInfoService          (no deps)         -- migrate first
ConnectivityService        (no deps)
LinuxDispatcherProvider    (no deps)
MonitorService             (no deps)
  \--> DeviceDisplayService  (depends on MonitorService)
SystemThemeService         (no deps)
  \--> AppInfoService        (depends on SystemThemeService)
FontFallbackManager        (no deps, but used by views)
InputMethodServiceFactory  (no deps, but used by views)
AccessibilityServiceFactory (no deps)
GtkHostService             (no deps, but used before DI is built) -- migrate last
```
