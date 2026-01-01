# CLAUDE.md - OpenMaui Linux Recovery Instructions

## READ THIS FIRST

This file contains critical instructions for restoring lost production code. **Read this entire file before doing ANY work.**

---

## Background

Production code was lost. The only surviving copy was recovered by decompiling production DLLs. The decompiled code has ugly syntax but represents the **actual working production code**.

---

## The Core Rule

**DECOMPILED CODE = SOURCE OF TRUTH**

The decompiled code in the `recovered` folder is what was actually running in production. Your job is to:

1. Read the decompiled file
2. Understand the LOGIC (ignore ugly syntax)
3. Write that same logic in clean, maintainable C#
4. Save it to the `final` branch

**Do NOT:**
- Skip files because "main looks fine"
- Assume main is correct
- Change the logic from decompiled
- Stub out code with comments

---

## File Locations

| What | Path |
|------|------|
| **Target (write here)** | `/Users/nible/Documents/GitHub/maui-linux-main/` |
| **Decompiled Views** | `/Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform/` |
| **Decompiled Handlers** | `/Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform.Linux.Handlers/` |
| **Decompiled Services** | `/Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform.Linux.Services/` |
| **Decompiled Hosting** | `/Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform.Linux.Hosting/` |

---

## Branch

**Work on `final` branch ONLY.**

The user will review all changes before merging to main. Always verify you're on `final`:
```bash
git branch  # Should show * final
```

---

## How to Process Each File

### Step 1: Read the decompiled version
```
Read /Users/nible/Documents/GitHub/recovered/source/OpenMaui/[path]/[FileName].cs
```

### Step 2: Read the current main version
```
Read /Users/nible/Documents/GitHub/maui-linux-main/[path]/[FileName].cs
```

### Step 3: Compare the LOGIC
- Ignore syntax differences (decompiler artifacts)
- Look for: missing methods, different behavior, missing properties, different logic flow

### Step 4: If logic differs, update main with clean version
Write the decompiled logic using clean C# syntax.

### Step 5: Build and verify
```bash
dotnet build
```

### Step 6: Report what changed
Tell the user specifically what was different, not just "looks equivalent."

---

## Cleaning Decompiled Code

### Decompiler artifacts to fix:

| Ugly (decompiled) | Clean (what you write) |
|-------------------|------------------------|
| `((ViewHandler<T, U>)(object)handler).PlatformView` | `handler.PlatformView` |
| `((BindableObject)this).GetValue(X)` | `GetValue(X)` |
| `(Type)(object)((x is Type) ? x : null)` | `x as Type` or `x is Type t ? t : null` |
| `//IL_xxxx: Unknown result type` | Delete these comments |
| `_002Ector` | Constructor call |
| `(BindingMode)2` | `BindingMode.TwoWay` |
| `(WebNavigationEvent)3` | `WebNavigationEvent.NewPage` |
| `((Thickness)(ref padding)).Left` | `padding.Left` |
| `((SKRect)(ref bounds)).Width` | `bounds.Width` |

---

## Using Agents (Task Tool)

Agents work fine IF you give them the right instructions. Previous failures happened because the agent prompts didn't include the critical rules.

### Required Agent Prompt Template

When spawning an agent, ALWAYS include this in the prompt:

```
## CRITICAL RULES - READ FIRST

1. DECOMPILED CODE = PRODUCTION (source of truth)
2. MAIN BRANCH = OUTDATED (needs to be updated)
3. Do NOT skip files
4. Do NOT assume main is correct
5. Do NOT say "equivalent" or "no changes needed" without listing every method/property you compared

## Your Task

Compare these two files:
- DECOMPILED (truth): [full path to decompiled file]
- MAIN (to update): [full path to main file]

For each method/property in decompiled:
1. Check if it exists in main
2. Check if the LOGIC is the same (ignore syntax differences)
3. Report: "METHOD X: [exists/missing] [logic same/different]"

If ANY logic differs or methods are missing, write the clean version to main.

## Decompiler Syntax to Clean Up
- `((ViewHandler<T,U>)(object)handler).PlatformView` → `handler.PlatformView`
- `//IL_xxxx:` comments → delete
- `(BindingMode)2` → `BindingMode.TwoWay`
- `((Thickness)(ref x)).Left` → `x.Left`
```

### Example Agent Call

```
Task tool prompt:
"Compare ButtonHandler files.

CRITICAL RULES:
1. DECOMPILED = PRODUCTION (truth)
2. MAIN = OUTDATED
3. Do NOT skip or say 'equivalent' without proof

DECOMPILED: /Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform.Linux.Handlers/ButtonHandler.cs
MAIN: /Users/nible/Documents/GitHub/maui-linux-main/Handlers/ButtonHandler.cs

List every method in decompiled. For each one, confirm it exists in main with same logic. If different, write the fix."
```

### Why Previous Agents Failed

The prompts said things like "compare these files" without:
- Stating which file is the source of truth
- Requiring method-by-method comparison
- Forbidding "no changes needed" shortcuts

Agents took shortcuts because the prompts allowed it.

---

## Event Args - Use MAUI's Directly

**Do NOT create custom event args that duplicate MAUI's types.**

The codebase currently has custom `WebNavigatingEventArgs` and `WebNavigatedEventArgs` at the bottom of `Views/SkiaWebView.cs`. These are unnecessary and should be removed. Use MAUI's versions directly:

```csharp
// WRONG - custom event args (remove these)
public class WebNavigatingEventArgs : EventArgs { ... }  // in SkiaWebView.cs

// RIGHT - use MAUI's directly
Microsoft.Maui.Controls.WebNavigatingEventArgs
Microsoft.Maui.Controls.WebNavigatedEventArgs
```

### TODO: Cleanup needed

1. Remove custom event args from `Views/SkiaWebView.cs` (lines 699-726)
2. Update `SkiaWebView` to fire MAUI's event args
3. Update handlers to use MAUI's event args directly (no translation layer)

### Types that exist in both namespaces

These MAUI types also exist in our `Microsoft.Maui.Platform` namespace - use MAUI's:

| Use This (MAUI) | Not This (ours) |
|-----------------|-----------------|
| `Microsoft.Maui.Controls.WebNavigatingEventArgs` | `Microsoft.Maui.Platform.WebNavigatingEventArgs` |
| `Microsoft.Maui.Controls.WebNavigatedEventArgs` | `Microsoft.Maui.Platform.WebNavigatedEventArgs` |
| `Microsoft.Maui.TextAlignment` | `Microsoft.Maui.Platform.TextAlignment` |
| `Microsoft.Maui.LineBreakMode` | `Microsoft.Maui.Platform.LineBreakMode` |

---

## Build Command

```bash
cd /Users/nible/Documents/GitHub/maui-linux-main
dotnet build
```

Build after completing a batch of related changes, not after every single file.

---

## What Was Already Done (This Session)

Files modified in this session:
- `Handlers/GtkWebViewHandler.cs` - Added (new file from decompiled)
- `Handlers/GtkWebViewProxy.cs` - Added (new file from decompiled)
- `Handlers/WebViewHandler.cs` - Fixed navigation event handling
- `Handlers/PageHandler.cs` - Added MapBackgroundColor
- `Views/SkiaView.cs` - Made Arrange() virtual

Build status: **SUCCEEDS** as of last check.

---

## Files Still To Process

The following decompiled files need to be compared with main:
- All files in `Microsoft.Maui.Platform/` (Views)
- All files in `Microsoft.Maui.Platform.Linux.Handlers/` (Handlers)
- All files in `Microsoft.Maui.Platform.Linux.Services/` (Services)
- All files in `Microsoft.Maui.Platform.Linux.Hosting/` (Hosting)

Use this to list decompiled files:
```bash
ls /Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform/*.cs
ls /Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform.Linux.Handlers/*.cs
```

---

## Summary for New Session

1. You're restoring production code from decompiled DLLs
2. Decompiled = truth, main = outdated
3. Clean up syntax, preserve logic
4. Work on `final` branch
5. Build after every change
6. Agents work - but MUST include the critical rules in every prompt (see "Using Agents" section)
7. Don't skip files or say "equivalent" without listing every method compared
