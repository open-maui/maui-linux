using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Native;

namespace Microsoft.Maui.Platform.Linux.Services;

public static class GtkContextMenuService
{
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void ActivateCallback(IntPtr menuItem, IntPtr userData);

	private static readonly List<ActivateCallback> _callbacks = new List<ActivateCallback>();

	private static readonly List<Action> _actions = new List<Action>();

	public static void ShowContextMenu(List<GtkMenuItem> items)
	{
		if (items == null || items.Count == 0)
		{
			return;
		}
		_callbacks.Clear();
		_actions.Clear();
		IntPtr intPtr = GtkNative.gtk_menu_new();
		if (intPtr == IntPtr.Zero)
		{
			Console.WriteLine("[GtkContextMenuService] Failed to create GTK menu");
			return;
		}
		foreach (GtkMenuItem item in items)
		{
			IntPtr intPtr2;
			if (item.IsSeparator)
			{
				intPtr2 = GtkNative.gtk_separator_menu_item_new();
			}
			else
			{
				intPtr2 = GtkNative.gtk_menu_item_new_with_label(item.Text);
				GtkNative.gtk_widget_set_sensitive(intPtr2, item.IsEnabled);
				if (item.IsEnabled && item.Action != null)
				{
					Action action = item.Action;
					_actions.Add(action);
					int actionIndex = _actions.Count - 1;
					ActivateCallback activateCallback = delegate
					{
						Console.WriteLine("[GtkContextMenuService] Menu item activated: " + item.Text);
						_actions[actionIndex]?.Invoke();
					};
					_callbacks.Add(activateCallback);
					GtkNative.g_signal_connect_data(intPtr2, "activate", Marshal.GetFunctionPointerForDelegate(activateCallback), IntPtr.Zero, IntPtr.Zero, 0);
				}
			}
			GtkNative.gtk_menu_shell_append(intPtr, intPtr2);
			GtkNative.gtk_widget_show(intPtr2);
		}
		GtkNative.gtk_widget_show(intPtr);
		IntPtr intPtr3 = GtkNative.gtk_get_current_event();
		GtkNative.gtk_menu_popup_at_pointer(intPtr, intPtr3);
		if (intPtr3 != IntPtr.Zero)
		{
			GtkNative.gdk_event_free(intPtr3);
		}
		Console.WriteLine($"[GtkContextMenuService] Showed GTK menu with {items.Count} items");
	}
}
