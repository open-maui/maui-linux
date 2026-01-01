using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Maui.Platform.Linux.Services;

public class Gtk4InteropService : IDisposable
{
	private struct GdkRGBA
	{
		public float Red;

		public float Green;

		public float Blue;

		public float Alpha;
	}

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void GAsyncReadyCallback(IntPtr sourceObject, IntPtr result, IntPtr userData);

	private const string LibGtk4 = "libgtk-4.so.1";

	private const string LibGio = "libgio-2.0.so.0";

	private const string LibGlib = "libglib-2.0.so.0";

	private const string LibGObject = "libgobject-2.0.so.0";

	private const string LibGtk3 = "libgtk-3.so.0";

	private bool _initialized;

	private bool _useGtk4;

	private bool _disposed;

	private readonly object _lock = new object();

	private GAsyncReadyCallback? _currentCallback;

	private TaskCompletionSource<FileDialogResult>? _fileDialogTcs;

	private TaskCompletionSource<ColorDialogResult>? _colorDialogTcs;

	private IntPtr _currentDialog;

	public bool IsInitialized => _initialized;

	public bool IsGtk4 => _useGtk4;

	[DllImport("libgtk-4.so.1")]
	private static extern bool gtk_init_check();

	[DllImport("libgtk-4.so.1")]
	private static extern bool gtk_is_initialized();

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr g_main_context_default();

	[DllImport("libgtk-4.so.1")]
	private static extern bool g_main_context_iteration(IntPtr context, bool mayBlock);

	[DllImport("libglib-2.0.so.0")]
	private static extern void g_free(IntPtr mem);

	[DllImport("libgobject-2.0.so.0")]
	private static extern void g_object_unref(IntPtr obj);

	[DllImport("libgobject-2.0.so.0")]
	private static extern void g_object_ref(IntPtr obj);

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr gtk_window_new();

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_window_set_title(IntPtr window, [MarshalAs(UnmanagedType.LPStr)] string title);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_window_set_modal(IntPtr window, bool modal);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_window_set_transient_for(IntPtr window, IntPtr parent);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_window_destroy(IntPtr window);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_window_present(IntPtr window);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_window_close(IntPtr window);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_widget_show(IntPtr widget);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_widget_hide(IntPtr widget);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_widget_set_visible(IntPtr widget, bool visible);

	[DllImport("libgtk-4.so.1")]
	private static extern bool gtk_widget_get_visible(IntPtr widget);

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr gtk_alert_dialog_new([MarshalAs(UnmanagedType.LPStr)] string format);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_alert_dialog_set_message(IntPtr dialog, [MarshalAs(UnmanagedType.LPStr)] string message);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_alert_dialog_set_detail(IntPtr dialog, [MarshalAs(UnmanagedType.LPStr)] string detail);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_alert_dialog_set_buttons(IntPtr dialog, string[] labels);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_alert_dialog_set_cancel_button(IntPtr dialog, int button);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_alert_dialog_set_default_button(IntPtr dialog, int button);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_alert_dialog_show(IntPtr dialog, IntPtr parent);

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr gtk_file_dialog_new();

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_dialog_set_title(IntPtr dialog, [MarshalAs(UnmanagedType.LPStr)] string title);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_dialog_set_modal(IntPtr dialog, bool modal);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_dialog_set_accept_label(IntPtr dialog, [MarshalAs(UnmanagedType.LPStr)] string label);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_dialog_open(IntPtr dialog, IntPtr parent, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr gtk_file_dialog_open_finish(IntPtr dialog, IntPtr result, out IntPtr error);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_dialog_save(IntPtr dialog, IntPtr parent, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr gtk_file_dialog_save_finish(IntPtr dialog, IntPtr result, out IntPtr error);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_dialog_select_folder(IntPtr dialog, IntPtr parent, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr gtk_file_dialog_select_folder_finish(IntPtr dialog, IntPtr result, out IntPtr error);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_dialog_open_multiple(IntPtr dialog, IntPtr parent, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr gtk_file_dialog_open_multiple_finish(IntPtr dialog, IntPtr result, out IntPtr error);

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr gtk_file_filter_new();

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_filter_set_name(IntPtr filter, [MarshalAs(UnmanagedType.LPStr)] string name);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_filter_add_pattern(IntPtr filter, [MarshalAs(UnmanagedType.LPStr)] string pattern);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_filter_add_mime_type(IntPtr filter, [MarshalAs(UnmanagedType.LPStr)] string mimeType);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_file_dialog_set_default_filter(IntPtr dialog, IntPtr filter);

	[DllImport("libgio-2.0.so.0")]
	private static extern IntPtr g_file_get_path(IntPtr file);

	[DllImport("libgio-2.0.so.0")]
	private static extern uint g_list_model_get_n_items(IntPtr list);

	[DllImport("libgio-2.0.so.0")]
	private static extern IntPtr g_list_model_get_item(IntPtr list, uint position);

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr gtk_color_dialog_new();

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_color_dialog_set_title(IntPtr dialog, [MarshalAs(UnmanagedType.LPStr)] string title);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_color_dialog_set_modal(IntPtr dialog, bool modal);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_color_dialog_set_with_alpha(IntPtr dialog, bool withAlpha);

	[DllImport("libgtk-4.so.1")]
	private static extern void gtk_color_dialog_choose_rgba(IntPtr dialog, IntPtr parent, IntPtr initialColor, IntPtr cancellable, GAsyncReadyCallback callback, IntPtr userData);

	[DllImport("libgtk-4.so.1")]
	private static extern IntPtr gtk_color_dialog_choose_rgba_finish(IntPtr dialog, IntPtr result, out IntPtr error);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_init_check")]
	private static extern bool gtk3_init_check(ref int argc, ref IntPtr argv);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_file_chooser_dialog_new")]
	private static extern IntPtr gtk3_file_chooser_dialog_new([MarshalAs(UnmanagedType.LPStr)] string title, IntPtr parent, int action, [MarshalAs(UnmanagedType.LPStr)] string firstButtonText, int firstButtonResponse, [MarshalAs(UnmanagedType.LPStr)] string secondButtonText, int secondButtonResponse, IntPtr terminator);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_dialog_run")]
	private static extern int gtk3_dialog_run(IntPtr dialog);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_destroy")]
	private static extern void gtk3_widget_destroy(IntPtr widget);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_file_chooser_get_filename")]
	private static extern IntPtr gtk3_file_chooser_get_filename(IntPtr chooser);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_file_chooser_get_filenames")]
	private static extern IntPtr gtk3_file_chooser_get_filenames(IntPtr chooser);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_file_chooser_set_select_multiple")]
	private static extern void gtk3_file_chooser_set_select_multiple(IntPtr chooser, bool selectMultiple);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_message_dialog_new")]
	private static extern IntPtr gtk3_message_dialog_new(IntPtr parent, int flags, int type, int buttons, [MarshalAs(UnmanagedType.LPStr)] string message);

	[DllImport("libglib-2.0.so.0")]
	private static extern uint g_slist_length(IntPtr list);

	[DllImport("libglib-2.0.so.0")]
	private static extern IntPtr g_slist_nth_data(IntPtr list, uint n);

	[DllImport("libglib-2.0.so.0")]
	private static extern void g_slist_free(IntPtr list);

	public bool Initialize()
	{
		if (_initialized)
		{
			return true;
		}
		lock (_lock)
		{
			if (_initialized)
			{
				return true;
			}
			try
			{
				if (gtk_init_check())
				{
					_useGtk4 = true;
					_initialized = true;
					Console.WriteLine("[GTK4] Initialized GTK4");
					return true;
				}
			}
			catch (DllNotFoundException)
			{
				Console.WriteLine("[GTK4] GTK4 not found, trying GTK3");
			}
			catch (Exception ex2)
			{
				Console.WriteLine("[GTK4] GTK4 init failed: " + ex2.Message);
			}
			try
			{
				int argc = 0;
				IntPtr argv = IntPtr.Zero;
				if (gtk3_init_check(ref argc, ref argv))
				{
					_useGtk4 = false;
					_initialized = true;
					Console.WriteLine("[GTK4] Initialized GTK3 (fallback)");
					return true;
				}
			}
			catch (DllNotFoundException)
			{
				Console.WriteLine("[GTK4] GTK3 not found");
			}
			catch (Exception ex4)
			{
				Console.WriteLine("[GTK4] GTK3 init failed: " + ex4.Message);
			}
			return false;
		}
	}

	public void ShowAlert(string title, string message, GtkMessageType type = GtkMessageType.Info)
	{
		if (EnsureInitialized())
		{
			if (_useGtk4)
			{
				IntPtr intPtr = gtk_alert_dialog_new(title);
				gtk_alert_dialog_set_detail(intPtr, message);
				string[] labels = new string[1] { "OK" };
				gtk_alert_dialog_set_buttons(intPtr, labels);
				gtk_alert_dialog_show(intPtr, IntPtr.Zero);
				g_object_unref(intPtr);
			}
			else
			{
				IntPtr intPtr2 = gtk3_message_dialog_new(IntPtr.Zero, 1, (int)type, 1, message);
				gtk3_dialog_run(intPtr2);
				gtk3_widget_destroy(intPtr2);
			}
			ProcessPendingEvents();
		}
	}

	public bool ShowConfirmation(string title, string message)
	{
		if (!EnsureInitialized())
		{
			return false;
		}
		if (_useGtk4)
		{
			IntPtr intPtr = gtk_alert_dialog_new(title);
			gtk_alert_dialog_set_detail(intPtr, message);
			string[] labels = new string[2] { "No", "Yes" };
			gtk_alert_dialog_set_buttons(intPtr, labels);
			gtk_alert_dialog_set_default_button(intPtr, 1);
			gtk_alert_dialog_set_cancel_button(intPtr, 0);
			gtk_alert_dialog_show(intPtr, IntPtr.Zero);
			g_object_unref(intPtr);
			return true;
		}
		IntPtr intPtr2 = gtk3_message_dialog_new(IntPtr.Zero, 1, 2, 4, message);
		int num = gtk3_dialog_run(intPtr2);
		gtk3_widget_destroy(intPtr2);
		ProcessPendingEvents();
		return num == -8;
	}

	public FileDialogResult ShowOpenFileDialog(string title = "Open File", string? initialFolder = null, bool allowMultiple = false, params (string Name, string Pattern)[] filters)
	{
		if (!EnsureInitialized())
		{
			return new FileDialogResult
			{
				Accepted = false
			};
		}
		if (_useGtk4)
		{
			return ShowGtk4FileDialog(title, GtkFileChooserAction.Open, allowMultiple, filters);
		}
		return ShowGtk3FileDialog(title, 0, allowMultiple, filters);
	}

	public FileDialogResult ShowSaveFileDialog(string title = "Save File", string? suggestedName = null, params (string Name, string Pattern)[] filters)
	{
		if (!EnsureInitialized())
		{
			return new FileDialogResult
			{
				Accepted = false
			};
		}
		if (_useGtk4)
		{
			return ShowGtk4FileDialog(title, GtkFileChooserAction.Save, allowMultiple: false, filters);
		}
		return ShowGtk3FileDialog(title, 1, allowMultiple: false, filters);
	}

	public FileDialogResult ShowFolderDialog(string title = "Select Folder")
	{
		if (!EnsureInitialized())
		{
			return new FileDialogResult
			{
				Accepted = false
			};
		}
		if (_useGtk4)
		{
			return ShowGtk4FileDialog(title, GtkFileChooserAction.SelectFolder, allowMultiple: false, Array.Empty<(string, string)>());
		}
		return ShowGtk3FileDialog(title, 2, allowMultiple: false, Array.Empty<(string, string)>());
	}

	private FileDialogResult ShowGtk4FileDialog(string title, GtkFileChooserAction action, bool allowMultiple, (string Name, string Pattern)[] filters)
	{
		IntPtr dialog = gtk_file_dialog_new();
		gtk_file_dialog_set_title(dialog, title);
		gtk_file_dialog_set_modal(dialog, modal: true);
		if (filters.Length != 0)
		{
			IntPtr filter = gtk_file_filter_new();
			gtk_file_filter_set_name(filter, filters[0].Name);
			gtk_file_filter_add_pattern(filter, filters[0].Pattern);
			gtk_file_dialog_set_default_filter(dialog, filter);
		}
		_fileDialogTcs = new TaskCompletionSource<FileDialogResult>();
		_currentDialog = dialog;
		_currentCallback = delegate(IntPtr source, IntPtr result, IntPtr userData)
		{
			IntPtr error = IntPtr.Zero;
			IntPtr intPtr = IntPtr.Zero;
			try
			{
				if (action == GtkFileChooserAction.Open && !allowMultiple)
				{
					intPtr = gtk_file_dialog_open_finish(dialog, result, out error);
				}
				else if (action == GtkFileChooserAction.Save)
				{
					intPtr = gtk_file_dialog_save_finish(dialog, result, out error);
				}
				else if (action == GtkFileChooserAction.SelectFolder)
				{
					intPtr = gtk_file_dialog_select_folder_finish(dialog, result, out error);
				}
				if (intPtr != IntPtr.Zero && error == IntPtr.Zero)
				{
					IntPtr intPtr2 = g_file_get_path(intPtr);
					string text = Marshal.PtrToStringUTF8(intPtr2) ?? "";
					g_free(intPtr2);
					g_object_unref(intPtr);
					_fileDialogTcs?.TrySetResult(new FileDialogResult
					{
						Accepted = true,
						SelectedFiles = new string[1] { text }
					});
				}
				else
				{
					_fileDialogTcs?.TrySetResult(new FileDialogResult
					{
						Accepted = false
					});
				}
			}
			catch
			{
				_fileDialogTcs?.TrySetResult(new FileDialogResult
				{
					Accepted = false
				});
			}
		};
		if (action == GtkFileChooserAction.Open && !allowMultiple)
		{
			gtk_file_dialog_open(dialog, IntPtr.Zero, IntPtr.Zero, _currentCallback, IntPtr.Zero);
		}
		else if (action == GtkFileChooserAction.Open && allowMultiple)
		{
			gtk_file_dialog_open_multiple(dialog, IntPtr.Zero, IntPtr.Zero, _currentCallback, IntPtr.Zero);
		}
		else if (action == GtkFileChooserAction.Save)
		{
			gtk_file_dialog_save(dialog, IntPtr.Zero, IntPtr.Zero, _currentCallback, IntPtr.Zero);
		}
		else if (action == GtkFileChooserAction.SelectFolder)
		{
			gtk_file_dialog_select_folder(dialog, IntPtr.Zero, IntPtr.Zero, _currentCallback, IntPtr.Zero);
		}
		while (!_fileDialogTcs.Task.IsCompleted)
		{
			ProcessPendingEvents();
			Thread.Sleep(10);
		}
		g_object_unref(dialog);
		return _fileDialogTcs.Task.Result;
	}

	private FileDialogResult ShowGtk3FileDialog(string title, int action, bool allowMultiple, (string Name, string Pattern)[] filters)
	{
		IntPtr intPtr = gtk3_file_chooser_dialog_new(title, IntPtr.Zero, action, "_Cancel", -6, (action == 1) ? "_Save" : "_Open", -3, IntPtr.Zero);
		if (allowMultiple)
		{
			gtk3_file_chooser_set_select_multiple(intPtr, selectMultiple: true);
		}
		int num = gtk3_dialog_run(intPtr);
		FileDialogResult result = new FileDialogResult
		{
			Accepted = false
		};
		if (num == -3)
		{
			if (allowMultiple)
			{
				IntPtr list = gtk3_file_chooser_get_filenames(intPtr);
				uint num2 = g_slist_length(list);
				List<string> list2 = new List<string>();
				for (uint num3 = 0u; num3 < num2; num3++)
				{
					IntPtr intPtr2 = g_slist_nth_data(list, num3);
					string text = Marshal.PtrToStringUTF8(intPtr2);
					if (!string.IsNullOrEmpty(text))
					{
						list2.Add(text);
						g_free(intPtr2);
					}
				}
				g_slist_free(list);
				result = new FileDialogResult
				{
					Accepted = true,
					SelectedFiles = list2.ToArray()
				};
			}
			else
			{
				IntPtr intPtr3 = gtk3_file_chooser_get_filename(intPtr);
				string text2 = Marshal.PtrToStringUTF8(intPtr3);
				g_free(intPtr3);
				if (!string.IsNullOrEmpty(text2))
				{
					FileDialogResult fileDialogResult = new FileDialogResult();
					fileDialogResult.Accepted = true;
					fileDialogResult.SelectedFiles = new string[1] { text2 };
					result = fileDialogResult;
				}
			}
		}
		gtk3_widget_destroy(intPtr);
		ProcessPendingEvents();
		return result;
	}

	public ColorDialogResult ShowColorDialog(string title = "Choose Color", float initialRed = 1f, float initialGreen = 1f, float initialBlue = 1f, float initialAlpha = 1f, bool withAlpha = true)
	{
		if (!EnsureInitialized())
		{
			return new ColorDialogResult
			{
				Accepted = false
			};
		}
		if (_useGtk4)
		{
			return ShowGtk4ColorDialog(title, initialRed, initialGreen, initialBlue, initialAlpha, withAlpha);
		}
		return new ColorDialogResult
		{
			Accepted = false
		};
	}

	private ColorDialogResult ShowGtk4ColorDialog(string title, float r, float g, float b, float a, bool withAlpha)
	{
		IntPtr dialog = gtk_color_dialog_new();
		gtk_color_dialog_set_title(dialog, title);
		gtk_color_dialog_set_modal(dialog, modal: true);
		gtk_color_dialog_set_with_alpha(dialog, withAlpha);
		_colorDialogTcs = new TaskCompletionSource<ColorDialogResult>();
		_currentCallback = delegate(IntPtr source, IntPtr result, IntPtr userData)
		{
			IntPtr error = IntPtr.Zero;
			try
			{
				IntPtr intPtr = gtk_color_dialog_choose_rgba_finish(dialog, result, out error);
				if (intPtr != IntPtr.Zero && error == IntPtr.Zero)
				{
					GdkRGBA gdkRGBA = Marshal.PtrToStructure<GdkRGBA>(intPtr);
					_colorDialogTcs?.TrySetResult(new ColorDialogResult
					{
						Accepted = true,
						Red = gdkRGBA.Red,
						Green = gdkRGBA.Green,
						Blue = gdkRGBA.Blue,
						Alpha = gdkRGBA.Alpha
					});
				}
				else
				{
					_colorDialogTcs?.TrySetResult(new ColorDialogResult
					{
						Accepted = false
					});
				}
			}
			catch
			{
				_colorDialogTcs?.TrySetResult(new ColorDialogResult
				{
					Accepted = false
				});
			}
		};
		gtk_color_dialog_choose_rgba(dialog, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, _currentCallback, IntPtr.Zero);
		while (!_colorDialogTcs.Task.IsCompleted)
		{
			ProcessPendingEvents();
			Thread.Sleep(10);
		}
		g_object_unref(dialog);
		return _colorDialogTcs.Task.Result;
	}

	private bool EnsureInitialized()
	{
		if (!_initialized)
		{
			Initialize();
		}
		return _initialized;
	}

	private void ProcessPendingEvents()
	{
		IntPtr context = g_main_context_default();
		while (g_main_context_iteration(context, mayBlock: false))
		{
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_initialized = false;
			GC.SuppressFinalize(this);
		}
	}

	~Gtk4InteropService()
	{
		Dispose();
	}
}
