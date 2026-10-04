// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	public partial class LabelHandlerTests
	{
		SkiaLabel GetPlatformLabel(LabelHandler handler) => handler.PlatformView;

		string GetNativeText(LabelHandler handler) => GetPlatformLabel(handler).Text;

		Color GetNativeTextColor(LabelHandler handler) => GetPlatformLabel(handler).TextColor;

		double GetNativeCharacterSpacing(LabelHandler handler) => GetPlatformLabel(handler).CharacterSpacing;

		TextAlignment GetNativeHorizontalTextAlignment(LabelHandler handler) => GetPlatformLabel(handler).HorizontalTextAlignment;

		double GetNativeLineHeight(LabelHandler handler) => GetPlatformLabel(handler).LineHeight;

		TextDecorations GetNativeTextDecorations(LabelHandler handler) => GetPlatformLabel(handler).TextDecorations;
	}
}
