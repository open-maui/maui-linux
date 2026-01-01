using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public static class GestureManager
{
	private class GestureTrackingState
	{
		public double StartX { get; set; }

		public double StartY { get; set; }

		public double CurrentX { get; set; }

		public double CurrentY { get; set; }

		public DateTime StartTime { get; set; }

		public bool IsPanning { get; set; }

		public bool IsPressed { get; set; }
	}

	private enum PointerEventType
	{
		Entered,
		Exited,
		Pressed,
		Moved,
		Released
	}

	private static MethodInfo? _sendTappedMethod;

	private static readonly Dictionary<View, (DateTime lastTap, int tapCount)> _tapTracking = new Dictionary<View, (DateTime, int)>();

	private static readonly Dictionary<View, GestureTrackingState> _gestureState = new Dictionary<View, GestureTrackingState>();

	private const double SwipeMinDistance = 50.0;

	private const double SwipeMaxTime = 500.0;

	private const double SwipeDirectionThreshold = 0.5;

	private const double PanMinDistance = 10.0;

	public static bool ProcessTap(View? view, double x, double y)
	{
		if (view == null)
		{
			return false;
		}
		View val = view;
		while (val != null)
		{
			IList<IGestureRecognizer> gestureRecognizers = val.GestureRecognizers;
			if (gestureRecognizers != null && gestureRecognizers.Count > 0 && ProcessTapOnView(val, x, y))
			{
				return true;
			}
			Element parent = ((Element)val).Parent;
			val = (View)(object)((parent is View) ? parent : null);
		}
		return false;
	}

	private static bool ProcessTapOnView(View view, double x, double y)
	{
		//IL_031f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0326: Expected O, but got Unknown
		//IL_03cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d2: Expected O, but got Unknown
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Expected O, but got Unknown
		IList<IGestureRecognizer> gestureRecognizers = view.GestureRecognizers;
		if (gestureRecognizers == null || gestureRecognizers.Count == 0)
		{
			return false;
		}
		bool result = false;
		foreach (IGestureRecognizer item in gestureRecognizers)
		{
			TapGestureRecognizer val = (TapGestureRecognizer)(object)((item is TapGestureRecognizer) ? item : null);
			if (val == null)
			{
				continue;
			}
			Console.WriteLine($"[GestureManager] Processing TapGestureRecognizer on {((object)view).GetType().Name}, CommandParameter={val.CommandParameter}, NumberOfTapsRequired={val.NumberOfTapsRequired}");
			int numberOfTapsRequired = val.NumberOfTapsRequired;
			if (numberOfTapsRequired > 1)
			{
				DateTime utcNow = DateTime.UtcNow;
				if (!_tapTracking.TryGetValue(view, out (DateTime, int) value))
				{
					_tapTracking[view] = (utcNow, 1);
					Console.WriteLine($"[GestureManager] First tap 1/{numberOfTapsRequired}");
					continue;
				}
				if (!((utcNow - value.Item1).TotalMilliseconds < 300.0))
				{
					_tapTracking[view] = (utcNow, 1);
					Console.WriteLine($"[GestureManager] Tap timeout, reset to 1/{numberOfTapsRequired}");
					continue;
				}
				int num = value.Item2 + 1;
				if (num < numberOfTapsRequired)
				{
					_tapTracking[view] = (utcNow, num);
					Console.WriteLine($"[GestureManager] Tap {num}/{numberOfTapsRequired}, waiting for more taps");
					continue;
				}
				_tapTracking.Remove(view);
			}
			bool flag = false;
			try
			{
				if ((object)_sendTappedMethod == null)
				{
					_sendTappedMethod = typeof(TapGestureRecognizer).GetMethod("SendTapped", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
				if (_sendTappedMethod != null)
				{
					Console.WriteLine($"[GestureManager] Found SendTapped method with {_sendTappedMethod.GetParameters().Length} params");
					TappedEventArgs e = new TappedEventArgs(val.CommandParameter);
					_sendTappedMethod.Invoke(val, new object[2] { view, e });
					Console.WriteLine("[GestureManager] SendTapped invoked successfully");
					flag = true;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[GestureManager] SendTapped failed: " + ex.Message);
			}
			if (!flag)
			{
				try
				{
					FieldInfo fieldInfo = typeof(TapGestureRecognizer).GetField("Tapped", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? typeof(TapGestureRecognizer).GetField("_tapped", BindingFlags.Instance | BindingFlags.NonPublic);
					if (fieldInfo != null && fieldInfo.GetValue(val) is EventHandler<TappedEventArgs> eventHandler)
					{
						Console.WriteLine("[GestureManager] Invoking Tapped event directly");
						TappedEventArgs e2 = new TappedEventArgs(val.CommandParameter);
						eventHandler(val, e2);
						flag = true;
					}
				}
				catch (Exception ex2)
				{
					Console.WriteLine("[GestureManager] Direct event invoke failed: " + ex2.Message);
				}
			}
			if (!flag)
			{
				try
				{
					string[] array = new string[3] { "TappedEvent", "_TappedHandler", "<Tapped>k__BackingField" };
					foreach (string text in array)
					{
						FieldInfo field = typeof(TapGestureRecognizer).GetField(text, BindingFlags.Instance | BindingFlags.NonPublic);
						if (field != null)
						{
							Console.WriteLine("[GestureManager] Found field: " + text);
							if (field.GetValue(val) is EventHandler<TappedEventArgs> eventHandler2)
							{
								TappedEventArgs e3 = new TappedEventArgs(val.CommandParameter);
								eventHandler2(val, e3);
								Console.WriteLine("[GestureManager] Event fired via " + text);
								flag = true;
								break;
							}
						}
					}
				}
				catch (Exception ex3)
				{
					Console.WriteLine("[GestureManager] Backing field approach failed: " + ex3.Message);
				}
			}
			if (!flag)
			{
				Console.WriteLine("[GestureManager] Could not fire event, dumping type info...");
				MethodInfo[] methods = typeof(TapGestureRecognizer).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (MethodInfo methodInfo in methods)
				{
					if (methodInfo.Name.Contains("Tap", StringComparison.OrdinalIgnoreCase) || methodInfo.Name.Contains("Send", StringComparison.OrdinalIgnoreCase))
					{
						Console.WriteLine($"[GestureManager]   Method: {methodInfo.Name}({string.Join(", ", from p in methodInfo.GetParameters()
							select p.ParameterType.Name)})");
					}
				}
			}
			ICommand command = val.Command;
			if (command != null && command.CanExecute(val.CommandParameter))
			{
				Console.WriteLine("[GestureManager] Executing Command");
				val.Command.Execute(val.CommandParameter);
			}
			result = true;
		}
		return result;
	}

	public static bool HasGestureRecognizers(View? view)
	{
		if (view == null)
		{
			return false;
		}
		return view.GestureRecognizers?.Count > 0;
	}

	public static bool HasTapGestureRecognizer(View? view)
	{
		if (((view != null) ? view.GestureRecognizers : null) == null)
		{
			return false;
		}
		foreach (IGestureRecognizer gestureRecognizer in view.GestureRecognizers)
		{
			if (gestureRecognizer is TapGestureRecognizer)
			{
				return true;
			}
		}
		return false;
	}

	public static void ProcessPointerDown(View? view, double x, double y)
	{
		if (view != null)
		{
			_gestureState[view] = new GestureTrackingState
			{
				StartX = x,
				StartY = y,
				CurrentX = x,
				CurrentY = y,
				StartTime = DateTime.UtcNow,
				IsPanning = false,
				IsPressed = true
			};
			ProcessPointerEvent(view, x, y, PointerEventType.Pressed);
		}
	}

	public static void ProcessPointerMove(View? view, double x, double y)
	{
		if (view == null)
		{
			return;
		}
		if (!_gestureState.TryGetValue(view, out GestureTrackingState value))
		{
			ProcessPointerEvent(view, x, y, PointerEventType.Moved);
			return;
		}
		value.CurrentX = x;
		value.CurrentY = y;
		if (!value.IsPressed)
		{
			ProcessPointerEvent(view, x, y, PointerEventType.Moved);
			return;
		}
		double num = x - value.StartX;
		double num2 = y - value.StartY;
		if (Math.Sqrt(num * num + num2 * num2) >= 10.0)
		{
			ProcessPanGesture(view, num, num2, (GestureStatus)(value.IsPanning ? 1 : 0));
			value.IsPanning = true;
		}
		ProcessPointerEvent(view, x, y, PointerEventType.Moved);
	}

	public static void ProcessPointerUp(View? view, double x, double y)
	{
		if (view == null)
		{
			return;
		}
		if (_gestureState.TryGetValue(view, out GestureTrackingState value))
		{
			value.CurrentX = x;
			value.CurrentY = y;
			double num = x - value.StartX;
			double num2 = y - value.StartY;
			double num3 = Math.Sqrt(num * num + num2 * num2);
			double totalMilliseconds = (DateTime.UtcNow - value.StartTime).TotalMilliseconds;
			if (num3 >= 50.0 && totalMilliseconds <= 500.0)
			{
				SwipeDirection swipeDirection = DetermineSwipeDirection(num, num2);
				if (swipeDirection != SwipeDirection.Right)
				{
					ProcessSwipeGesture(view, swipeDirection);
				}
				else if (Math.Abs(num) > Math.Abs(num2) * 0.5)
				{
					ProcessSwipeGesture(view, (!(num > 0.0)) ? SwipeDirection.Left : SwipeDirection.Right);
				}
			}
			if (value.IsPanning)
			{
				ProcessPanGesture(view, num, num2, (GestureStatus)2);
			}
			else if (num3 < 15.0 && totalMilliseconds < 500.0)
			{
				Console.WriteLine($"[GestureManager] Detected tap on {((object)view).GetType().Name} (distance={num3:F1}, elapsed={totalMilliseconds:F0}ms)");
				ProcessTap(view, x, y);
			}
			_gestureState.Remove(view);
		}
		ProcessPointerEvent(view, x, y, PointerEventType.Released);
	}

	public static void ProcessPointerEntered(View? view, double x, double y)
	{
		if (view != null)
		{
			ProcessPointerEvent(view, x, y, PointerEventType.Entered);
		}
	}

	public static void ProcessPointerExited(View? view, double x, double y)
	{
		if (view != null)
		{
			ProcessPointerEvent(view, x, y, PointerEventType.Exited);
		}
	}

	private static SwipeDirection DetermineSwipeDirection(double deltaX, double deltaY)
	{
		double num = Math.Abs(deltaX);
		double num2 = Math.Abs(deltaY);
		if (num > num2 * 0.5)
		{
			if (!(deltaX > 0.0))
			{
				return SwipeDirection.Left;
			}
			return SwipeDirection.Right;
		}
		if (num2 > num * 0.5)
		{
			if (!(deltaY > 0.0))
			{
				return SwipeDirection.Up;
			}
			return SwipeDirection.Down;
		}
		if (!(deltaX > 0.0))
		{
			return SwipeDirection.Left;
		}
		return SwipeDirection.Right;
	}

	private static void ProcessSwipeGesture(View view, SwipeDirection direction)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		IList<IGestureRecognizer> gestureRecognizers = view.GestureRecognizers;
		if (gestureRecognizers == null)
		{
			return;
		}
		foreach (IGestureRecognizer item in gestureRecognizers)
		{
			SwipeGestureRecognizer val = (SwipeGestureRecognizer)(object)((item is SwipeGestureRecognizer) ? item : null);
			if (val == null || !((Enum)val.Direction).HasFlag((Enum)direction))
			{
				continue;
			}
			Console.WriteLine($"[GestureManager] Swipe detected: {direction}");
			try
			{
				MethodInfo method = typeof(SwipeGestureRecognizer).GetMethod("SendSwiped", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					method.Invoke(val, new object[2] { view, direction });
					Console.WriteLine("[GestureManager] SendSwiped invoked successfully");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[GestureManager] SendSwiped failed: " + ex.Message);
			}
			ICommand command = val.Command;
			if (command != null && command.CanExecute(val.CommandParameter))
			{
				val.Command.Execute(val.CommandParameter);
			}
		}
	}

	private static void ProcessPanGesture(View view, double totalX, double totalY, GestureStatus status)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Expected I4, but got Unknown
		IList<IGestureRecognizer> gestureRecognizers = view.GestureRecognizers;
		if (gestureRecognizers == null)
		{
			return;
		}
		foreach (IGestureRecognizer item in gestureRecognizers)
		{
			PanGestureRecognizer val = (PanGestureRecognizer)(object)((item is PanGestureRecognizer) ? item : null);
			if (val == null)
			{
				continue;
			}
			Console.WriteLine($"[GestureManager] Pan gesture: status={status}, totalX={totalX:F1}, totalY={totalY:F1}");
			try
			{
				MethodInfo method = typeof(PanGestureRecognizer).GetMethod("SendPan", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					method.Invoke(val, new object[4]
					{
						view,
						totalX,
						totalY,
						(int)status
					});
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[GestureManager] SendPan failed: " + ex.Message);
			}
		}
	}

	private static void ProcessPointerEvent(View view, double x, double y, PointerEventType eventType)
	{
		IList<IGestureRecognizer> gestureRecognizers = view.GestureRecognizers;
		if (gestureRecognizers == null)
		{
			return;
		}
		foreach (IGestureRecognizer item in gestureRecognizers)
		{
			PointerGestureRecognizer val = (PointerGestureRecognizer)(object)((item is PointerGestureRecognizer) ? item : null);
			if (val == null)
			{
				continue;
			}
			try
			{
				string text = eventType switch
				{
					PointerEventType.Entered => "SendPointerEntered", 
					PointerEventType.Exited => "SendPointerExited", 
					PointerEventType.Pressed => "SendPointerPressed", 
					PointerEventType.Moved => "SendPointerMoved", 
					PointerEventType.Released => "SendPointerReleased", 
					_ => null, 
				};
				if (text != null)
				{
					MethodInfo method = typeof(PointerGestureRecognizer).GetMethod(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method != null)
					{
						object obj = CreatePointerEventArgs(view, x, y);
						method.Invoke(val, new object[2] { view, obj });
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[GestureManager] Pointer event failed: " + ex.Message);
			}
		}
	}

	private static object CreatePointerEventArgs(View view, double x, double y)
	{
		try
		{
			Type type = typeof(PointerGestureRecognizer).Assembly.GetType("Microsoft.Maui.Controls.PointerEventArgs");
			if (type != null)
			{
				ConstructorInfo constructorInfo = type.GetConstructors().FirstOrDefault();
				if (constructorInfo != null)
				{
					return constructorInfo.Invoke(new object[0]);
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static bool HasSwipeGestureRecognizer(View? view)
	{
		if (((view != null) ? view.GestureRecognizers : null) == null)
		{
			return false;
		}
		foreach (IGestureRecognizer gestureRecognizer in view.GestureRecognizers)
		{
			if (gestureRecognizer is SwipeGestureRecognizer)
			{
				return true;
			}
		}
		return false;
	}

	public static bool HasPanGestureRecognizer(View? view)
	{
		if (((view != null) ? view.GestureRecognizers : null) == null)
		{
			return false;
		}
		foreach (IGestureRecognizer gestureRecognizer in view.GestureRecognizers)
		{
			if (gestureRecognizer is PanGestureRecognizer)
			{
				return true;
			}
		}
		return false;
	}

	public static bool HasPointerGestureRecognizer(View? view)
	{
		if (((view != null) ? view.GestureRecognizers : null) == null)
		{
			return false;
		}
		foreach (IGestureRecognizer gestureRecognizer in view.GestureRecognizers)
		{
			if (gestureRecognizer is PointerGestureRecognizer)
			{
				return true;
			}
		}
		return false;
	}
}
