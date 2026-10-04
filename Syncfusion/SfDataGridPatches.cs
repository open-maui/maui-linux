// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using Syncfusion.Maui.Core.Internals;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;
using SfKeyEventArgs = Syncfusion.Maui.Core.Internals.KeyEventArgs;
using SfPointerEventArgs = Syncfusion.Maui.Core.Internals.PointerEventArgs;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// SfDataGrid's desktop input, which its platform-neutral build strips and the
/// Windows build implements. Each patch ports the Windows method body:
/// <list type="bullet">
/// <item><b>Mouse drags.</b> Column resizing, column and row drag-and-drop,
/// row resizing, swiping and drag selection run on pan updates, which Windows
/// raises from the grid panel's manipulation events and iOS and Mac Catalyst
/// from a pan recognizer on the grid (<c>WirePanGestureRecognizer</c>).
/// <c>OnDataGridHandlerChanged</c> is empty in the neutral build, so nothing
/// fed them: the grid gets the recognizer, and a pointer release ends any
/// gesture still open, as <c>Panel_PointerReleased(false)</c> does.</item>
/// <item><b>Keyboard.</b> <c>VisualContainer.OnKeyDown</c> runs the
/// selection controller's key processing (arrows, Page Up/Down, Home/End,
/// Tab, Enter, F2, Escape, Ctrl+A), undo/redo and the multi-column combo box
/// editor's keys; the neutral body only records the modifier keys. Focus goes
/// back to the grid after an edit is committed from the keyboard.</item>
/// <item><b>Right-click.</b> <c>DataGridCell.OnRightTap</c> selects the cell
/// (<c>AllowSelectionOnSecondaryTap</c>), raises <c>CellRightTapped</c> and
/// opens the context menu; the neutral body does nothing.</item>
/// <item><b>Hover and cursors.</b> Header-cell hover highlighting
/// (<c>AllowHeaderCellHoverHighlighting</c>), and the resize and move
/// cursors of column and row resizing and of column, row and group-drop-area
/// dragging (<c>ChangeCursor</c>), shown on the grid as WinUI's
/// <c>ProtectedCursor</c> does.</item>
/// <item><b>Row resizing.</b> The row header's pointer handling
/// (<c>DataGridRowResizingController.OnTouch</c>) and its long press, which
/// start a row resize.</item>
/// <item><b>Tool tips.</b> <c>TooltipDelay</c> is honoured, and the pointer
/// position is tracked so a tool tip opens where the pointer is.</item>
/// <item><b>Current row border.</b> With multiple selection and no current
/// cell, the current row gets its dashed border (<c>DataGridRow.OnDraw</c>,
/// which the neutral build does not declare).</item>
/// <item><b>Column chooser.</b> <c>ShowColumnChooser</c> set before the grid
/// loads opens the chooser once the grid is loaded.</item>
/// </list>
/// The DataGrid is optional, so every type is looked up by name. A patch is
/// applied only over the neutral stub; a build that implements a method keeps
/// it.
/// </summary>
internal static class SfDataGridPatches
{
    private const string Asm = "Syncfusion.Maui.DataGrid";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static int s_installed;
    private static Type? s_rowColumnIndex;
    private static Type? s_helpers;
    private static Type? s_indexResolver;
    private static readonly ConditionalWeakTable<object, object> s_loadedHooked = new();
    private static readonly ConditionalWeakTable<object, StrongBox<int>> s_tooltipGeneration = new();

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        var grid = Type("Syncfusion.Maui.DataGrid.SfDataGrid", Asm);
        if (grid == null)
            return; // SfDataGrid is not part of the app
        s_rowColumnIndex = Type("Syncfusion.Maui.GridCommon.ScrollAxis.RowColumnIndex", "Syncfusion.Maui.GridCommon");
        s_helpers = Type("Syncfusion.Maui.DataGrid.Helper.SfDataGridHelpers", Asm);
        s_indexResolver = Type("Syncfusion.Maui.DataGrid.Helper.GridIndexResolver", Asm);
        var harmony = new Harmony("com.openmaui.syncfusion.datagrid");

        var interaction = Type("Syncfusion.Maui.DataGrid.DataGridInteractionController", Asm);
        Patch(harmony, interaction, "OnDataGridHandlerChanged", nameof(OnDataGridHandlerChanged_Postfix), postfix: true,
            when: m => Il(m).Length <= 2);
        Patch(harmony, interaction, "HandleTouchGesture", nameof(HandleTouchGesture_Postfix), postfix: true,
            when: m => interaction != null && !HasMethod(interaction, "Panel_PointerReleased"));

        var container = Type("Syncfusion.Maui.DataGrid.VisualContainer", Asm);
        Patch(harmony, container, "OnKeyDown", nameof(OnKeyDown_Prefix),
            when: m => !Calls(m, "ProcessKeyDown"));
        var editing = Type("Syncfusion.Maui.DataGrid.DataGridEditingController", Asm);
        Patch(harmony, editing, "CommitCellValueInKeyDown", nameof(CommitCellValueInKeyDown_Postfix), postfix: true,
            when: m => !Calls(m, "Focus"));

        var cell = Type("Syncfusion.Maui.DataGrid.DataGridCell", Asm);
        Patch(harmony, cell, "OnRightTap", nameof(OnRightTap_Prefix),
            when: m => !Calls(m, "RaiseCellRightTapped"));
        Patch(harmony, cell, "ApplyHeaderCellHoverHighlighting", nameof(ApplyHeaderCellHoverHighlighting_Prefix),
            when: m => Il(m).Length <= 2);
        Patch(harmony, Type("Syncfusion.Maui.DataGrid.DataGridRowHeaderCell", Asm), "OnCellLongPress", nameof(RowHeaderLongPress_Prefix),
            when: m => !Calls(m, "HandleLongPressGesture"));

        foreach (var (controller, cursor) in new[]
        {
            ("DataGridColumnResizingController", CursorType.SizeWestEast),
            ("DataGridRowResizingController", CursorType.SizeNorthSouth),
            ("DataGridColumnDragDropController", CursorType.SizeAll),
            ("DataGridRowDragDropController", CursorType.SizeAll),
            ("DataGridGroupDropAreaController", CursorType.SizeAll),
        })
        {
            var type = Type("Syncfusion.Maui.DataGrid." + controller, Asm);
            if (type != null)
                s_cursors[type] = cursor;
            Patch(harmony, type, "ChangeCursor", nameof(ChangeCursor_Prefix), when: m => Il(m).Length <= 2);
        }
        var columnResizing = Type("Syncfusion.Maui.DataGrid.DataGridColumnResizingController", Asm);
        Patch(harmony, columnResizing, "CursorUpdate", nameof(CursorUpdate_Prefix), when: m => !Calls(m, "ChangeCursor"));
        Patch(harmony, Type("Syncfusion.Maui.DataGrid.DataGridRowResizingController", Asm), "OnTouch", nameof(RowResizingOnTouch_Prefix),
            when: m => Il(m).Length <= 2);

        var tooltip = Type("Syncfusion.Maui.DataGrid.DataGridToolTipView", Asm);
        Patch(harmony, tooltip, "OpenToolTip", nameof(OpenToolTip_Prefix), when: m => !Calls(m, "ShowTooltipAfterDelayAsync"));
        Patch(harmony, tooltip, "UpdatePointerPosition", nameof(UpdatePointerPosition_Prefix), when: m => !Calls(m, "set_ToolTipX"));
        Patch(harmony, tooltip, "DisplayToolTip", nameof(DisplayToolTip_Postfix), postfix: true, when: m => !Calls(m, "set_ToolTipX"));

        Patch(harmony, grid, "Setup", nameof(Setup_Postfix), postfix: true, when: _ => !HasMethod(grid, "OnDataGridLoaded"));
        Patch(harmony, grid, "SetClippedToBounds", nameof(SetClippedToBounds_Prefix), when: m => Il(m).Length <= 2);
        Patch(harmony, Type("Syncfusion.Maui.DataGrid.DataGridExportHelper", Asm), "HelperStream", nameof(HelperStream_Prefix),
            when: m => Il(m).Length <= 2);

        var row = Type("Syncfusion.Maui.DataGrid.DataGridRow", Asm);
        if (row != null && row.GetMethod("OnDraw", Any) == null)
        {
            var onDraw = typeof(global::Syncfusion.Maui.Core.SfView).GetMethod("OnDraw", Any, null, new[] { typeof(ICanvas), typeof(RectF) }, null);
            s_dataGridRow = row;
            Patch(harmony, onDraw, nameof(SfViewOnDraw_Postfix), postfix: true);
            Patch(harmony, row.GetConstructor(System.Type.EmptyTypes), nameof(DataGridRowCtor_Postfix), postfix: true);
        }
    }

    private static readonly Dictionary<Type, CursorType> s_cursors = new();
    private static Type? s_dataGridRow;

    private static void Patch(Harmony harmony, Type? type, string method, string patch, bool postfix = false, Func<MethodInfo, bool>? when = null)
    {
        if (type == null)
            return;
        foreach (var target in type.GetMethods(Any).Where(m => m.Name == method && !m.IsAbstract))
        {
            if (when != null && !when(target))
                continue;
            Patch(harmony, target, patch, postfix);
        }
    }

    private static void Patch(Harmony harmony, MethodBase? target, string patch, bool postfix)
    {
        if (target == null)
            return;
        try
        {
            var hm = new HarmonyMethod(typeof(SfDataGridPatches).GetMethod(patch, BindingFlags.Static | BindingFlags.NonPublic));
            if (postfix)
                harmony.Patch(target, postfix: hm);
            else
                harmony.Patch(target, prefix: hm);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Patching SfDataGrid {target.DeclaringType?.Name}.{target.Name} failed", ex);
        }
    }

    private static void Guard(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"SfDataGrid {what} failed", ex);
        }
    }

    // ---- Mouse drags -------------------------------------------------------------------------

    // DataGridInteractionController.OnDataGridHandlerChanged(View view): Windows wires the grid
    // panel's manipulation (pan) events here; the desktop equivalent of iOS's pan recognizer.
    private static void OnDataGridHandlerChanged_Postfix(object __instance, View view) => Guard("pan wiring", () =>
    {
        if (view?.Handler != null)
            Call(__instance, "WirePanGestureRecognizer", view);
    });

    // DataGridInteractionController.HandleTouchGesture: Windows ends any pan still open on release
    // (Panel_PointerReleased(isSuccess: false)), so a long-press resize or drag that never moved ends.
    private static void HandleTouchGesture_Postfix(object __instance, SfPointerEventArgs e) => Guard("pointer release", () =>
    {
        if (e.Action != PointerActions.Released || Is(Get(__instance, "DataGrid"), "IsBusy"))
            return;
        Call(__instance, "HandlePanGesture", __instance, new PanUpdatedEventArgs(GestureStatus.Canceled, 1));
    });

    // ---- Keyboard ------------------------------------------------------------------------------

    // VisualContainer.OnKeyDown(KeyEventArgs args), as the Windows build has it.
    private static bool OnKeyDown_Prefix(object __instance, SfKeyEventArgs args)
    {
        try
        {
            var dataGrid = Get(__instance, "Datagrid");
            if (dataGrid != null)
            {
                var selection = Get(dataGrid, "SelectionController");
                Set(selection, "IsShiftKeyPressed", args.IsShiftKeyPressed);
                Set(selection, "IsCtrlKeyPressed", args.IsCtrlKeyPressed);
                Set(selection, "IsCommandKeyPressed", args.IsCommandKeyPressed);
            }
            var current = Get(Get(dataGrid, "CurrentCellManager"), "DataColumn");
            if (current != null && Get(current, "DataGridColumn") != null && Is(current, "IsEditing")
                && Get(Get(current, "ColumnElement"), "Content") is { } editor && editor.GetType().Name == "SfMultiColumnComboBox")
            {
                Call(editor, "ProcessKeyDown", args, args.IsCtrlKeyPressed, args.IsShiftKeyPressed);
                return false;
            }
            if (dataGrid == null)
                return false;
            var top = s_helpers != null ? CallStatic(s_helpers, "GetTopLevelParentDataGrid", dataGrid) : dataGrid;
            if (top != null && Get(top, "SelectedDetailsViewDataGrid") != null && s_helpers != null)
                top = CallStatic(s_helpers, "GetSelectedDetailsViewDataGrid", top);
            if (Get(top, "UndoRedoController") is { } undoRedo && Is(top, "AllowUndoRedo"))
                Call(undoRedo, "HandleKeyDown", args, args.IsCtrlKeyPressed, args.IsShiftKeyPressed);
            if (Is(dataGrid, "AllowKeyboardNavigation"))
                Call(Get(dataGrid, "SelectionController"), "ProcessKeyDown", args, args.IsCtrlKeyPressed, args.IsShiftKeyPressed);
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfDataGrid key handling failed", ex);
            return true;
        }
    }

    // DataGridEditingController.CommitCellValueInKeyDown: Windows focuses the grid again once the
    // edit is committed, so the arrow keys keep navigating.
    private static void CommitCellValueInKeyDown_Postfix(object __instance) => Guard("focus after edit", () =>
    {
        // The edit ended when validation passed (RaiseValidationAndEndEdit).
        if (Is(Get(Get(__instance, "CurrentCellManager"), "DataColumn"), "IsEditing"))
            return;
        if (Get(Get(__instance, "DataGrid"), "VisualContainer") is VisualElement container)
            container.Focus();
    });

    // ---- Right-click ---------------------------------------------------------------------------

    // DataGridCell.OnRightTap(RightTapEventArgs e), as the Windows build has it.
    private static bool OnRightTap_Prefix(object __instance, RightTapEventArgs e)
    {
        try
        {
            var dataColumn = Get(__instance, "DataColumn");
            var dataGrid = Get(Get(dataColumn, "DataRow"), "DataGrid");
            if (dataGrid == null)
                return false;
            if (Get(dataGrid, "SwipingController") is { } swiping && Call(swiping, "IsSwipeIsInView") is false)
                Call(dataGrid, "ResetSwipeOffset");
            int rowIndex = (int)Get(dataColumn, "RowIndex")!;
            int columnIndex = (int)Get(dataColumn, "ColumnIndex")!;
            var rowColumnIndex = RowColumnIndex(rowIndex, columnIndex);
            var cellType = Get(dataColumn, "CellType")?.ToString();
            if (cellType is not ("RowHeaderCell" or "ExpanderCell" or "IndentCell"))
            {
                if (Get(dataColumn, "DataGridColumn") is { } column && !Is(column, "IsTemplate")
                    && Get(dataGrid, "VisualContainer") is VisualElement { IsFocused: false } container)
                    container.Focus();
                if (Get(dataGrid, "SelectionController") is { } selection && Is(dataGrid, "AllowSelectionOnSecondaryTap")
                    && selection.GetType().GetMethods(Any | BindingFlags.FlattenHierarchy).Any(m => m.Name == "HandlePointerOperation" && m.GetParameters().Length == 1))
                    Call(selection, "HandlePointerOperation", rowColumnIndex);
            }
            Set(dataGrid, "ContextMenuRowIndex", rowIndex);
            Set(dataGrid, "ContextMenuColumnIndex", columnIndex);
            var trigger = Call(dataGrid, "ExtractTriggerPosition", e);
            Set(dataGrid, "lastTriggerPoint", trigger);
            Set(dataGrid, "contextMenuType", Call(dataGrid, "GetCellType", rowIndex, columnIndex, trigger));
            if (Call(dataGrid, "IsCellRightTappedEventWired") is true
                && Type("Syncfusion.Maui.DataGrid.DataGridCellRightTappedEventArgs", Asm) is { } argsType)
            {
                var args = New(argsType, rowColumnIndex, Get(Get(dataColumn, "DataRow"), "RowData"), Get(dataColumn, "DataGridColumn"), e.PointerDeviceType);
                Call(dataGrid, "RaiseCellRightTapped", args);
            }
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfDataGrid right-tap failed", ex);
            return true;
        }
    }

    private static object? RowColumnIndex(int row, int column)
    {
        if (s_rowColumnIndex == null)
            return null;
        var value = Activator.CreateInstance(s_rowColumnIndex)!;
        Set(value, "RowIndex", row);
        Set(value, "ColumnIndex", column);
        return value;
    }

    // ---- Hover and cursors ---------------------------------------------------------------------

    // DataGridCell.ApplyHeaderCellHoverHighlighting(PointerEventArgs e, SfDataGrid dataGrid).
    private static bool ApplyHeaderCellHoverHighlighting_Prefix(object __instance, SfPointerEventArgs e, object dataGrid)
    {
        Guard("header hover", () =>
        {
            var dataColumn = Get(__instance, "DataColumn");
            if (dataColumn == null || Get(dataColumn, "DataRow") == null || __instance.GetType().Name != "DataGridHeaderCell")
                return;
            if (!Is(dataGrid, "AllowHeaderCellHoverHighlighting") || e.PointerDeviceType != PointerDeviceType.Mouse)
                return;
            bool? hovered = e.Action switch
            {
                PointerActions.Entered => true,
                PointerActions.Exited => false,
                _ => null,
            };
            if (hovered == null)
                return;
            Set(dataColumn, "IsHeaderCellHovered", hovered.Value);
            if (Get(dataColumn, "Renderer") is { } renderer)
                Call(renderer, "SetCellBackground", dataColumn);
        });
        return false;
    }

    // ChangeCursor(bool) of the resizing and drag controllers: the cursor shows over the grid.
    private static bool ChangeCursor_Prefix(object __instance, bool __0)
    {
        Guard("cursor", () =>
        {
            var dataGrid = Get(__instance, "DataGrid") ?? Get(__instance, "_dataGrid") ?? Get(__instance, "dataGrid");
            if ((dataGrid as VisualElement)?.Handler?.PlatformView is SkiaView view)
            {
                view.CursorType = __0 && s_cursors.TryGetValue(__instance.GetType(), out var cursor) ? cursor : CursorType.Arrow;
            }
        });
        return false;
    }

    // DataGridColumnResizingController.CursorUpdate(PointerEventArgs e, DataGridCell gridCell).
    private static bool CursorUpdate_Prefix(object __instance, SfPointerEventArgs e, Element gridCell)
    {
        try
        {
            var dataGrid = Get(__instance, "DataGrid");
            if (dataGrid == null || !Is(dataGrid, "AllowResizingColumns"))
                return false;
            if (e.GetPosition(gridCell.Parent) is not { } position)
                return false;
            double x = (double)Call(__instance, "GetActualPosition", position.X)!;
            var hit = Call(__instance, "GetHitTest", x, gridCell, false);
            if (Get(__instance, "ResizingLine") is { } line)
            {
                int lineIndex = (int)Get(line, "LineIndex")!;
                int index = (int)CallStatic(s_indexResolver!, "ResolveToGridVisibleColumnIndex", dataGrid, lineIndex)!;
                if (Get(dataGrid, "Columns") is System.Collections.IList columns && index >= 0 && index < columns.Count)
                    Set(columns[index], "ExtendedWidth", double.NaN);
            }
            bool resize = hit != null && !Is(dataGrid, "CanDrawResizingIndicator")
                && Get(__instance, "DataColumn") is { } dataColumn && Call(__instance, "CanAllowResizing", x, dataColumn) is true;
            Call(__instance, "ChangeCursor", resize);
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfDataGrid column-resize cursor failed", ex);
            return true;
        }
    }

    // ---- Row resizing --------------------------------------------------------------------------

    // DataGridRowResizingController.OnTouch(PointerEventArgs e, DataGridCell dataGridCell).
    private static bool RowResizingOnTouch_Prefix(object __instance, SfPointerEventArgs e, object dataGridCell)
    {
        Guard("row resizing", () =>
        {
            var dataGrid = Get(__instance, "_dataGrid");
            var dataColumn = Get(dataGridCell, "DataColumn");
            if (dataGrid == null || !Is(dataGrid, "ShowRowHeader") || dataGridCell == null || !IsEnum(Get(dataColumn, "CellType"), "RowHeaderCell"))
                return;
            var rowType = Get(Get(dataColumn, "DataRow"), "RowType");
            if (IsEnum(rowType, "HeaderRow") || IsEnum(rowType, "StackedHeaderRow"))
                return;
            switch (e.Action)
            {
                case PointerActions.Moved:
                case PointerActions.Entered:
                    Call(__instance, "HandlePointerMoved", e, dataGridCell);
                    break;
                case PointerActions.Released:
                    Call(__instance, "OnPointerReleased");
                    break;
                case PointerActions.Exited:
                    Call(__instance, "ChangeCursor", false);
                    break;
            }
        });
        return false;
    }

    // DataGridRowHeaderCell.OnCellLongPress(LongPressEventArgs e): a long press on the row header
    // starts a row resize (the base cell's long press only reaches cells with a column).
    private static bool RowHeaderLongPress_Prefix(object __instance, LongPressEventArgs e)
    {
        Guard("row header long press", () =>
        {
            var dataGrid = Get(Get(Get(__instance, "DataColumn"), "DataRow"), "DataGrid");
            if (dataGrid != null && Is(dataGrid, "AllowResizingRows"))
                Call(Get(dataGrid, "InteractionController"), "HandleLongPressGesture", GestureStatus.Started,
                    e.GetPosition(((Element)__instance).Parent), __instance);
        });
        return false;
    }

    // ---- Tool tips -----------------------------------------------------------------------------

    // DataGridToolTipView.OpenToolTip(DataGridCell cell, Point? position): with a TooltipDelay the
    // tool tip opens after it, unless the pointer moved on (CancelPendingTooltip) first.
    private static bool OpenToolTip_Prefix(object __instance, object cell, Point? position)
    {
        try
        {
            var dataGrid = Get(__instance, "_dataGrid");
            var dataColumn = Get(cell, "DataColumn");
            if (dataColumn == null || position == null || !Is(dataGrid, "ShowToolTip") || Get(dataColumn, "CellValue") == null)
                return false;
            if (Get(Get(dataColumn, "DataGridColumn"), "ShowToolTip") is not true)
                return false;
            Call(__instance, "CancelPendingTooltip");
            int delay = Get<int>(dataGrid, "TooltipDelay");
            if (delay <= 0)
            {
                Call(__instance, "DisplayToolTip", cell, position);
                return false;
            }
            var generation = s_tooltipGeneration.GetValue(__instance, _ => new StrongBox<int>());
            int mine = ++generation.Value;
            var token = new CancellationTokenSource();
            Set(__instance, "_pendingTooltipToken", token);
            ((BindableObject)cell).Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(delay), () => Guard("tool tip", () =>
            {
                if (token.IsCancellationRequested || generation.Value != mine)
                    return;
                if (Get(cell, "DataColumn") is { } column && Get(column, "CellValue") != null && Is(dataGrid, "ShowToolTip")
                    && Get(Get(column, "DataGridColumn"), "ShowToolTip") is true)
                    Call(__instance, "DisplayToolTip", cell, position);
            }));
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfDataGrid tool tip failed", ex);
            return true;
        }
    }

    // DataGridToolTipView.UpdatePointerPosition(Point? point).
    private static bool UpdatePointerPosition_Prefix(object __instance, Point? point)
    {
        if (point is { } p)
        {
            Set(__instance, "ToolTipX", p.X);
            Set(__instance, "ToolTipY", p.Y);
        }
        return false;
    }

    // DataGridToolTipView.DisplayToolTip: without a delay the tool tip is placed at the pointer.
    private static void DisplayToolTip_Postfix(object __instance, Point? position)
    {
        if (position is { } p && Get<int>(Get(__instance, "_dataGrid"), "TooltipDelay") <= 0)
        {
            Set(__instance, "ToolTipX", p.X);
            Set(__instance, "ToolTipY", p.Y);
        }
    }

    // ---- Column chooser ------------------------------------------------------------------------

    // SfDataGrid.Setup: Windows opens the column chooser once the grid is loaded (OnDataGridLoaded).
    private static void Setup_Postfix(VisualElement __instance)
    {
        if (s_loadedHooked.TryGetValue(__instance, out _))
            return;
        s_loadedHooked.Add(__instance, new object());
        __instance.Loaded += (sender, _) => Guard("column chooser", () =>
        {
            if (sender is VisualElement grid && Is(grid, "ShowColumnChooser") && Get(grid, "ColumnChooser") is { } chooser
                && grid.Height > 0 && grid.Width > 0)
                Call(chooser, "ShowColumnChooser");
        });
    }

    // SfDataGrid.SetClippedToBounds(Rect bounds): the grid's view is clipped to its bounds.
    private static bool SetClippedToBounds_Prefix(VisualElement __instance)
    {
        if (__instance.Handler?.PlatformView is SkiaLayoutView view && !view.ClipToBounds)
            view.ClipToBounds = true;
        return false;
    }

    // ---- Export --------------------------------------------------------------------------------

    // DataGridExportHelper.HelperStream(ImageSource imageSource): the picture of an image column,
    // for the Excel and PDF exports (the neutral build exports no pictures). The caller blocks
    // on the result, so nothing here resumes on the UI thread.
    private static bool HelperStream_Prefix(ImageSource imageSource, ref Task<Stream>? __result)
    {
        __result = ExportImage(imageSource);
        return false;
    }

    private static async Task<Stream> ExportImage(ImageSource source)
    {
        try
        {
            switch (source)
            {
                case IStreamImageSource streamSource:
                {
                    using var stream = await streamSource.GetStreamAsync(CancellationToken.None).ConfigureAwait(false);
                    if (stream == null)
                        return null!;
                    var copy = new MemoryStream();
                    await stream.CopyToAsync(copy).ConfigureAwait(false);
                    copy.Position = 0;
                    return copy;
                }
                case FileImageSource { File: { Length: > 0 } file }:
                {
                    foreach (var path in new[] { file, Path.Combine(AppContext.BaseDirectory, file), Path.Combine(AppContext.BaseDirectory, "Resources", "Images", file) })
                    {
                        if (File.Exists(path))
                            return new MemoryStream(await File.ReadAllBytesAsync(path).ConfigureAwait(false));
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfDataGrid export image failed", ex);
        }
        return null!;
    }

    // ---- Current row border --------------------------------------------------------------------

    private static void DataGridRowCtor_Postfix(object __instance)
    {
        if (__instance is global::Syncfusion.Maui.Core.IDrawableLayout layout)
            layout.DrawingOrder = global::Syncfusion.Maui.Core.DrawingOrder.AboveContent;
    }

    // DataGridRow.OnDraw(ICanvas canvas, RectF dirtyRect), declared only by the Windows build.
    private static void SfViewOnDraw_Postfix(object __instance, ICanvas canvas, RectF dirtyRect)
    {
        if (s_dataGridRow == null || !s_dataGridRow.IsInstanceOfType(__instance))
            return;
        Guard("current row border", () =>
        {
            var dataRow = Get(__instance, "DataRow");
            if (dataRow == null || !Is(dataRow, "IsCurrentRow"))
                return;
            var dataGrid = Get(dataRow, "DataGrid");
            if (dataGrid == null || !IsEnum(Get(dataGrid, "SelectionMode"), "Multiple") || Is(Get(dataGrid, "CurrentCellManager"), "HasCurrentCell"))
                return;
            DrawCurrentRowBorder(__instance, canvas, dirtyRect, dataRow, dataGrid);
        });
    }

    // DataGridRow.OnDrawCurrentRowBorder, as the Windows build has it.
    private static void DrawCurrentRowBorder(object row, ICanvas canvas, RectF dirtyRect, object dataRow, object dataGrid)
    {
        Call(row, "SetCurrentRowHighlightColor");
        canvas.StrokeColor = Get<Color>(row, "CurrentRowHighlightColor");
        canvas.StrokeSize = 1f;
        float half = Convert.ToSingle(Get(Get(dataGrid, "DefaultStyle"), "GridLineStrokeThickness") ?? 1.0) / 2f;
        const float inset = 1f;
        float indent = Convert.ToSingle(Get(dataGrid, "IndentColumnWidth") ?? 0.0);
        int groups = (Get(Get(dataGrid, "View"), "GroupDescriptions") as System.Collections.ICollection)?.Count ?? 0;
        int level = Get<int>(dataRow, "RowLevel") - 1;
        canvas.StrokeDashPattern = new float[] { 4f, 4f };
        var container = Get(dataGrid, "VisualContainer");
        float right = (float)Math.Min(Get<double>(container, "ExtendedWidth"), Get<double>(Get(container, "ScrollColumns"), "ViewSize"));
        float offset = (float)Get<double>(container, "HorizontalOffset");
        float left = dirtyRect.X + inset;
        if (Is(dataGrid, "ShowRowHeader"))
            left += (float)Get<double>(dataGrid, "RowHeaderWidth");
        var rowType = Get(dataRow, "RowType")?.ToString();
        int count;
        if (rowType is "DefaultRow" or "GroupSummaryCoveredRow" or "GroupSummaryRow")
            count = groups;
        else if (rowType is "CaptionCoveredRow" or "CaptionRow" && groups > 0)
            count = level;
        else
            return;
        if (offset < left + indent * count)
            left += indent * count - offset;
        float top = dirtyRect.Y + inset, bottom = dirtyRect.Height - inset - half;
        canvas.DrawLine(left, top, right - inset + indent, top);
        canvas.DrawLine(left, bottom, right - inset + indent, bottom);
        canvas.DrawLine(left, top, left, bottom);
        canvas.DrawLine(right - inset, top, right - inset, bottom);
    }
}
