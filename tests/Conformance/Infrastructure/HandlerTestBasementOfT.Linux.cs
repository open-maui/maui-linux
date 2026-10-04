// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Linux counterpart of HandlerTestBasementOfT.Windows.cs / .Android.cs:
	/// the generic per-view properties read back from the Skia platform view.
	/// </summary>
	public partial class HandlerTestBasement<THandler, TStub>
	{
		protected static SkiaView Skia(IViewHandler viewHandler) => viewHandler.PlatformView.AsSkia();

		protected Task<Graphics.Rect> GetPlatformViewBounds(IViewHandler viewHandler)
		{
			var view = Skia(viewHandler);
			return view.AttachAndRun(() => view.Bounds);
		}

		protected System.Numerics.Matrix4x4 GetViewTransform(IViewHandler viewHandler)
		{
			var v = Skia(viewHandler);
			var b = v.Bounds;
			// Same composition MAUI's platforms report: scale, rotation, translation (+ frame origin).
			var m = System.Numerics.Matrix4x4.CreateScale((float)(v.Scale * v.ScaleX), (float)(v.Scale * v.ScaleY), 1);
			m *= System.Numerics.Matrix4x4.CreateRotationZ((float)(v.Rotation * System.Math.PI / 180));
			m *= System.Numerics.Matrix4x4.CreateTranslation((float)(b.X + v.TranslationX), (float)(b.Y + v.TranslationY), 0);
			return m;
		}

		protected Task<Graphics.Rect> GetBoundingBox(IViewHandler viewHandler)
		{
			var view = Skia(viewHandler);
			return view.AttachAndRun(() => view.Bounds);
		}

		protected string GetAutomationId(IViewHandler viewHandler) =>
			Skia(viewHandler).AutomationId;

		// MAUI's Semantics.Description is the accessible name (AutomationProperties.Name on
		// Windows); OpenMaui exposes it as SemanticName to AT-SPI.
		protected string GetSemanticDescription(IViewHandler viewHandler) =>
			Skia(viewHandler).SemanticName;

		protected string GetSemanticHint(IViewHandler viewHandler) =>
			Skia(viewHandler).SemanticHint;

		protected SemanticHeadingLevel GetSemanticHeading(IViewHandler viewHandler) =>
			Skia(viewHandler).SemanticHeadingLevel;

		protected double GetOpacity(IViewHandler viewHandler) =>
			Skia(viewHandler).Opacity;

		protected double GetTranslationX(IViewHandler viewHandler) => Skia(viewHandler).TranslationX;

		protected double GetTranslationY(IViewHandler viewHandler) => Skia(viewHandler).TranslationY;

		protected double GetScaleX(IViewHandler viewHandler) => Skia(viewHandler).ScaleX;

		protected double GetScaleY(IViewHandler viewHandler) => Skia(viewHandler).ScaleY;

		protected double GetRotation(IViewHandler viewHandler) => Skia(viewHandler).Rotation;

		protected double GetRotationX(IViewHandler viewHandler) => Skia(viewHandler).RotationX;

		protected double GetRotationY(IViewHandler viewHandler) => Skia(viewHandler).RotationY;

		protected Visibility GetVisibility(IViewHandler viewHandler) =>
			Skia(viewHandler).Visibility;

		protected FlowDirection GetFlowDirection(IViewHandler viewHandler) =>
			Skia(viewHandler).FlowDirection;

		protected bool GetHitTestVisible(IViewHandler viewHandler) =>
			!Skia(viewHandler).InputTransparent;
	}
}
