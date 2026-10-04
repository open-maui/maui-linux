// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for CommunityToolkit.Maui's <c>DrawingView</c>. The toolkit's
/// generic net10.0 handler has no platform view (it throws when MAUI asks for
/// one), so without this the control rendered as OpenMaui's unsupported-view
/// placeholder. Registered automatically when the toolkit is part of the app;
/// OpenMaui takes no dependency on it (see <see cref="SkiaDrawingView"/>).
/// </summary>
public class DrawingViewHandler : LinuxViewHandler<IView, SkiaDrawingView>
{
    public static IPropertyMapper<IView, DrawingViewHandler> Mapper = new PropertyMapper<IView, DrawingViewHandler>(ViewHandler.ViewMapper)
    {
        // The platform view observes the virtual view's PropertyChanged and its
        // Lines/Points collections; these keep the mapper-driven paths
        // (initial sync, handler re-attach) invalidating too.
        ["Lines"] = MapRefresh,
        ["LineColor"] = MapInvalidate,
        ["LineWidth"] = MapInvalidate,
        ["DrawAction"] = MapInvalidate,
        [nameof(IView.Background)] = MapInvalidate,
    };

    public DrawingViewHandler() : base(Mapper, ViewHandler.ViewCommandMapper)
    {
    }

    /// <summary>
    /// The toolkit's DrawingView type when CommunityToolkit.Maui is loadable in
    /// this app, otherwise null (the handler is then never registered).
    /// </summary>
    internal static Type? ToolkitDrawingViewType { get; } = ResolveToolkitType();

    private static Type? ResolveToolkitType()
    {
        try
        {
            return Type.GetType(DrawingViewContract.DrawingViewTypeName, throwOnError: false);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or TypeLoadException)
        {
            return null;
        }
    }

    private object? _lineAdapter;

    /// <summary>
    /// Sets the <c>CommunityToolkit.Maui.Core.Handlers.IDrawingLineAdapter</c> that turns each
    /// finished stroke into the line added to the DrawingView (null restores the toolkit's own
    /// DrawingLineAdapter), as the toolkit handler's <c>SetDrawingLineAdapter</c> does. Apps reach
    /// it through the toolkit's <c>IDrawingViewHandler</c>, which the handler OpenMaui creates for a
    /// DrawingView implements (<see cref="HandlerType"/>).
    /// </summary>
    public void SetDrawingLineAdapter(object? drawingLineAdapter)
    {
        if (drawingLineAdapter != null && SkiaDrawingView.AdapterInterfaceFor(drawingLineAdapter.GetType()) == null)
            throw new ArgumentException("Not a CommunityToolkit.Maui IDrawingLineAdapter", nameof(drawingLineAdapter));
        _lineAdapter = drawingLineAdapter;
        if (PlatformView is { } platformView)
            platformView.LineAdapter = drawingLineAdapter;
    }

    protected override SkiaDrawingView CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaDrawingView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.LineAdapter = _lineAdapter;
        platformView.DrawingView = VirtualView;
    }

    protected override void DisconnectHandler(SkiaDrawingView platformView)
    {
        platformView.CancelStroke();
        platformView.DrawingView = null;
        base.DisconnectHandler(platformView);
    }

    /// <summary>
    /// The handler type registered for the toolkit's DrawingView: this handler, extended at run
    /// time to implement the toolkit's <c>IDrawingViewHandler</c> so
    /// <c>((IDrawingViewHandler)drawingView.Handler).SetDrawingLineAdapter(...)</c> works as on the
    /// toolkit's platforms (OpenMaui does not reference the toolkit, so it cannot implement the
    /// interface at compile time). This handler itself when that is not possible.
    /// </summary>
    internal static Type HandlerType => s_handlerType.Value;

    private static readonly Lazy<Type> s_handlerType = new(BuildHandlerType);

    /// <summary>A new handler of <see cref="HandlerType"/>.</summary>
    internal static DrawingViewHandler Create() => (DrawingViewHandler)Activator.CreateInstance(HandlerType)!;

    private static Type BuildHandlerType()
    {
        try
        {
            var contract = ToolkitDrawingViewType?.Assembly.GetType("CommunityToolkit.Maui.Core.Handlers.IDrawingViewHandler")
                ?? Type.GetType("CommunityToolkit.Maui.Core.Handlers.IDrawingViewHandler, CommunityToolkit.Maui.Core", throwOnError: false);
            var setAdapter = contract?.GetMethod("SetDrawingLineAdapter");
            if (contract == null || setAdapter == null || setAdapter.GetParameters().Length != 1)
                return typeof(DrawingViewHandler);

            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("OpenMaui.Toolkit.DrawingViewHandler"), AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("OpenMaui.Toolkit.DrawingViewHandler");
            var type = module.DefineType("OpenMaui.Toolkit.LinuxDrawingViewHandler",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
                typeof(DrawingViewHandler), new[] { contract });
            type.DefineDefaultConstructor(MethodAttributes.Public);

            var method = type.DefineMethod(contract.FullName + ".SetDrawingLineAdapter",
                MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                typeof(void), new[] { setAdapter.GetParameters()[0].ParameterType });
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(DrawingViewHandler).GetMethod(nameof(SetDrawingLineAdapter), new[] { typeof(object) })!);
            il.Emit(OpCodes.Ret);
            type.DefineMethodOverride(method, setAdapter);
            return type.CreateType();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("DrawingViewHandler", "Implementing IDrawingViewHandler failed; a custom IDrawingLineAdapter cannot be set", ex);
            return typeof(DrawingViewHandler);
        }
    }

    public static void MapRefresh(DrawingViewHandler handler, IView view) => handler.PlatformView?.Refresh();

    public static void MapInvalidate(DrawingViewHandler handler, IView view) => handler.PlatformView?.Invalidate();
}
