# CLAUDE.md - CRITICAL INSTRUCTIONS

## CRITICAL: SOURCE OF TRUTH

**DECOMPILED CODE = PRODUCTION VERSION (with all fixes/changes)**
**MAIN BRANCH = OUTDATED VERSION**

The decompiled code in `/Users/nible/Documents/GitHub/recovered/source/OpenMaui/` represents the PRODUCTION version that was actually running. It contains all fixes and changes that were made during development. We don't know exactly what was fixed or changed - that work was LOST.

The main branch is OUTDATED and missing those production fixes.

DO NOT assume main is correct. DO NOT skip files because "they already exist in main". Compare EVERYTHING and apply the differences from decompiled to bring main up to the production state.

## Merge Process

1. **For EVERY file in decompiled**: Compare with main and APPLY THE FIXES
2. **Embedded classes**: Files like `LayoutHandler.cs` contain multiple classes (GridHandler, StackLayoutHandler, etc.) - these ALL need to be compared and updated with decompiled fixes
3. **Do not skip**: Even if a class "exists" in main, it's likely BROKEN and needs the decompiled version

## Branch

- Work on `final` branch only
- User will review and merge to main

## Decompiled Code Location

- `/Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform/` - Views, Types
- `/Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform.Linux.Handlers/` - Handlers
- `/Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform.Linux.Hosting/` - Hosting
- `/Users/nible/Documents/GitHub/recovered/source/OpenMaui/Microsoft.Maui.Platform.Linux.Services/` - Services

## Tracking

Update `/Users/nible/Documents/GitHub/maui-linux-main/MERGE_TRACKING.md` after EVERY change.

## What Was INCORRECTLY Skipped (Must Be Fixed)

These were skipped because I wrongly assumed main was correct:

### Type files deleted (need to compare and update inline versions):
- All event args in SkiaView.cs, SkiaCheckBox.cs, etc.
- GridLength, GridPosition, AbsoluteLayoutBounds in SkiaLayoutView.cs
- MenuItem, MenuBarItem in SkiaMenuBar.cs
- ShellSection, ShellContent in SkiaShell.cs
- Many more...

### Handler files not compared:
- GridHandler (in LayoutHandler.cs) - NEEDS FIXES FROM DECOMPILED
- StackLayoutHandler (in LayoutHandler.cs) - NEEDS FIXES FROM DECOMPILED
- ContentPageHandler (in PageHandler.cs) - NEEDS FIXES FROM DECOMPILED

### View files not compared:
- SkiaGrid (in SkiaLayoutView.cs) - NEEDS FIXES FROM DECOMPILED
- SkiaStackLayout (in SkiaLayoutView.cs) - NEEDS FIXES FROM DECOMPILED
- SkiaAbsoluteLayout (in SkiaLayoutView.cs) - NEEDS FIXES FROM DECOMPILED
- SkiaContentPage (in SkiaPage.cs) - NEEDS FIXES FROM DECOMPILED
- SkiaFrame (in SkiaBorder.cs) - NEEDS FIXES FROM DECOMPILED

## Clean Code Rule

When copying from decompiled, clean up decompiler artifacts:
- `_002Ector` → constructor
- `((Type)(ref var))` patterns → clean casting
- IL comments → remove
