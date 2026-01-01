using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public class HotkeyEventArgs : EventArgs
{
	public int Id { get; }

	public HotkeyKey Key { get; }

	public HotkeyModifiers Modifiers { get; }

	public HotkeyEventArgs(int id, HotkeyKey key, HotkeyModifiers modifiers)
	{
		Id = id;
		Key = key;
		Modifiers = modifiers;
	}
}
