// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Media;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Storage;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// SfImageEditor's image work, which its platform-neutral build leaves out (the image editor's
/// view shows the picture, but nothing edits it). Two layers are bridged:
/// <list type="bullet">
/// <item><b>Syncfusion.Maui.Core's <c>ImageViewExt</c></b>, whose calls into its native
/// handler (flip, rotate, reset, crop, effects) find no handler in the neutral build: they go
/// to <see cref="SfImageEditorBridgeHandler"/>, the Linux image-editor handler.</item>
/// <item><b>Syncfusion.Maui.ImageEditor's <c>ImageLayout</c></b>, whose neutral build stubs
/// <c>GetImageStream</c> (an empty stream), <c>Save</c> (nothing written), the effect calls,
/// the crop and the undo and redo of crops and effects: each is the Windows body, with Skia
/// bitmaps where it has WinRT streams. The bitmap an undo restores, which the Windows build
/// keeps on the undo action (<c>EditorAction.CachedImage</c>, absent from the neutral build),
/// is kept beside the action. A reset also resets the edited bitmap (the neutral
/// <c>ResetView</c> drops that call), and a flip of a quarter-turned picture flips the other
/// axis, as on Windows.</item>
/// <item><b>Browse</b> (<c>ShowImagePicker</c>) opens the photo picker, as on Windows; the
/// neutral build does nothing.</item>
/// <item><b><c>View.GetStreamAsync</c> and <c>View.SaveAsImage</c></b> (Syncfusion.Maui.Core's
/// view snapshot, used to export charts and other controls) render the view; the neutral build
/// returns an empty stream and writes nothing.</item>
/// </list>
/// The image editor is optional, so its types are looked up by name.
/// </summary>
internal static class SfImageEditorPatches
{
    private const string Asm = "Syncfusion.Maui.ImageEditor";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConditionalWeakTable<object, byte[]> s_cachedImages = new();
    private static Type? s_imageEditorInterface;
    private static int s_installed;

    internal static Type? ImageViewExtType { get; } =
        Type("Syncfusion.Maui.Core.Internals.ImageViewExt", "Syncfusion.Maui.Core");

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        var harmony = new Harmony("com.openmaui.syncfusion.imageeditor");

        // Syncfusion.Maui.Core: the image view's calls into its handler, and the view snapshot.
        var view = ImageViewExtType;
        Patch(harmony, view, "FlipImage", nameof(ViewFlip_Prefix));
        Patch(harmony, view, "RotateImage", nameof(ViewRotate_Prefix));
        Patch(harmony, view, "ResetImage", nameof(ViewReset_Prefix));
        Patch(harmony, view, "GetCroppedImageSource", nameof(ViewCrop_Prefix));
        Patch(harmony, view, "StartEffect", nameof(ViewStartEffect_Prefix));
        Patch(harmony, view, "ApplyEffect", nameof(ViewApplyEffect_Prefix));
        var viewExtensions = Type("Syncfusion.Maui.Core.Internals.ViewExtensions", "Syncfusion.Maui.Core");
        Patch(harmony, viewExtensions, "GetStreamAsync", nameof(GetStreamAsync_Prefix), when: m => !Calls(m, "ConvertToBitmapEncoder") && !HasMethod(viewExtensions!, "ConvertToBitmapEncoder"));
        Patch(harmony, viewExtensions, "SaveAsImage", nameof(SaveAsImage_Prefix), when: m => !HasMethod(viewExtensions!, "ConvertToBitmapEncoder"));

        // Syncfusion.Maui.ImageEditor (optional).
        var layout = Type("Syncfusion.Maui.ImageEditor.ImageLayout", Asm);
        if (layout == null)
            return;
        s_imageEditorInterface = Type("Syncfusion.Maui.ImageEditor.IImageEditor", Asm);
        // Only over the neutral build: the Windows one reads its native stream here.
        if (Calls(layout.GetMethod("Save", Any), "GetMergedStream"))
            return;
        Patch(harmony, layout, "GetImageStream", nameof(GetImageStream_Prefix));
        Patch(harmony, layout, "Save", nameof(Save_Prefix));
        Patch(harmony, layout, "ResetView", nameof(ResetView_Postfix), postfix: true);
        Patch(harmony, layout, "ApplyEffects", nameof(ApplyEffects_Prefix));
        Patch(harmony, layout, "StartEffect", nameof(LayoutStartEffect_Prefix));
        Patch(harmony, layout, "EndEffect", nameof(EndEffect_Prefix));
        Patch(harmony, layout, "CropImageView", nameof(CropImageView_Prefix));
        Patch(harmony, layout, "SetImageSourceFromCache", nameof(SetImageSourceFromCache_Prefix));
        Patch(harmony, layout, "FlipAnimation", nameof(FlipAnimation_Prefix));
        Patch(harmony, Type("Syncfusion.Maui.ImageEditor.TextView", Asm), "OnEditorHandlerChanged", nameof(TextEditorHandlerChanged_Prefix),
            when: m => Il(m).Length <= 2);
        Patch(harmony, Type("Syncfusion.Maui.ImageEditor.SfImageEditor", Asm), "Save", nameof(EditorSave_Prefix),
            when: m => Calls(StateMachineOf(m), "RequestAsync"));
        var editLayout = Type("Syncfusion.Maui.ImageEditor.ImageEditLayout", Asm);
        Patch(harmony, editLayout, "Syncfusion.Maui.ImageEditor.IToolbarActions.ShowImagePicker", nameof(ShowImagePicker_Prefix),
            when: m => !HasMethod(editLayout!, "PickPhotosAsync") && !Calls(StateMachineOf(m), "PickPhotosAsync"));
    }

    private static MethodInfo? StateMachineOf(MethodInfo method) =>
        method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType.GetMethod("MoveNext", Any);

    private static void Patch(Harmony harmony, Type? type, string method, string patch, bool postfix = false, Func<MethodInfo, bool>? when = null)
    {
        if (type == null)
            return;
        foreach (var target in type.GetMethods(Any).Where(m => m.Name == method && !m.IsAbstract))
        {
            if (when != null && !when(target))
                continue;
            try
            {
                var hm = new HarmonyMethod(typeof(SfImageEditorPatches).GetMethod(patch, BindingFlags.Static | BindingFlags.NonPublic));
                if (postfix)
                    harmony.Patch(target, postfix: hm);
                else
                    harmony.Patch(target, prefix: hm);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("Syncfusion", $"Patching SfImageEditor {type.Name}.{method} failed", ex);
            }
        }
    }

    private static SfImageEditorBridgeHandler? HandlerOf(object? imageView) =>
        (imageView as VisualElement)?.Handler as SfImageEditorBridgeHandler;

    private static void Guard(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"SfImageEditor {what} failed", ex);
        }
    }

    private static object? Info(object layout) => Get(layout, "imageEditorInfo");

    private static object? CallInfo(object layout, string method, params object?[] args)
    {
        var info = Info(layout);
        var m = s_imageEditorInterface?.GetMethod(method);
        return info == null || m == null ? null : m.Invoke(info, args);
    }

    private static string FileTypeOf(object layout) => Get(Get(layout, "toolbarActions"), "ImageFileType")?.ToString() ?? "Png";

    // ---- ImageViewExt (Syncfusion.Maui.Core) --------------------------------------------------------

    private static bool ViewFlip_Prefix(object __instance, bool horizontalFlip, ref Task __result)
    {
        if (HandlerOf(__instance) is not { } handler)
            return true;
        Guard("flip", () => handler.Flip(horizontalFlip));
        __result = Task.CompletedTask;
        return false;
    }

    private static bool ViewRotate_Prefix(object __instance, double angle, ref Task __result)
    {
        if (HandlerOf(__instance) is not { } handler)
            return true;
        Guard("rotate", () => handler.Rotate(angle));
        __result = Task.CompletedTask;
        return false;
    }

    private static bool ViewReset_Prefix(object __instance, bool isSourceChange)
    {
        if (HandlerOf(__instance) is not { } handler)
            return true;
        Guard("reset", () => handler.Reset(isSourceChange));
        return false;
    }

    // ImageViewExt.GetCroppedImageSource(View? annotationLayout, Rect cropRect, string cropType, string imageFormat).
    private static bool ViewCrop_Prefix(object __instance, View? annotationLayout, Rect cropRect, string cropType, string imageFormat, ref ImageSource? __result)
    {
        if (HandlerOf(__instance) is not { } handler || __instance is not VisualElement image)
            return true;
        ImageSource? source = null;
        Guard("crop", () =>
        {
            var desired = image.DesiredSize;
            var rendered = Math.Abs(image.Rotation / 90.0 % 2.0) != 1.0 ? desired : new Size(desired.Height, desired.Width);
            if (handler.Crop(annotationLayout, cropRect, rendered, cropType, imageFormat) is { } bytes)
                source = ImageSource.FromStream(() => new MemoryStream(bytes));
        });
        __result = source;
        return false;
    }

    private static bool ViewStartEffect_Prefix(object __instance)
    {
        if (HandlerOf(__instance) is not { } handler)
            return true;
        Guard("effects", handler.StartEffect);
        return false;
    }

    private static bool ViewApplyEffect_Prefix(object __instance, string editorEffects, double value)
    {
        if (HandlerOf(__instance) is not { } handler)
            return true;
        Guard("effect", () => handler.ApplyEffect(editorEffects, value, null));
        return false;
    }

    // ---- View snapshot (Syncfusion.Maui.Core ViewExtensions) ---------------------------------------

    // GetStreamAsync(this View view, ImageFileFormat format): the view as drawn, PNG or JPEG.
    private static bool GetStreamAsync_Prefix(View view, global::Syncfusion.Maui.Core.ImageFileFormat format, ref Task<Stream> __result)
    {
        __result = Task.FromResult(Snapshot(view, format.ToString()));
        return false;
    }

    // SaveAsImage(this View view, string fileName): into the Pictures folder, PNG unless .jpg/.jpeg.
    private static bool SaveAsImage_Prefix(View view, string fileName)
    {
        Guard("save as image", () =>
        {
            var extension = Path.GetExtension(fileName).Trim('.').ToLowerInvariant();
            var format = extension is "jpg" or "jpeg" ? "Jpeg" : "Png";
            var name = Path.GetFileNameWithoutExtension(fileName) + "." + format.ToLowerInvariant();
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures, Environment.SpecialFolderOption.Create);
            if (string.IsNullOrEmpty(folder))
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
            Directory.CreateDirectory(folder);
            using var stream = Snapshot(view, format);
            using var file = File.Create(Path.Combine(folder, name));
            stream.CopyTo(file);
        });
        return false;
    }

    internal static Stream Snapshot(View view, string? format)
    {
        if (view.Handler?.PlatformView is not SkiaView skia || skia.Bounds.Width <= 0 || skia.Bounds.Height <= 0)
            return Stream.Null;
        using var bitmap = new SkiaSharp.SKBitmap((int)Math.Ceiling(skia.Bounds.Width), (int)Math.Ceiling(skia.Bounds.Height));
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            canvas.Clear(SkiaSharp.SKColors.Transparent);
            SfImageProcessing.DrawView(canvas, skia, 1, 1);
        }
        return new MemoryStream(SfImageProcessing.Encode(bitmap, format == "Jpeg" ? "Jpeg" : "Png"));
    }

    // ---- ImageLayout (Syncfusion.Maui.ImageEditor) --------------------------------------------------

    // ImageLayout.GetImageStream(): the edited image with its annotations, in the toolbar's file type.
    private static bool GetImageStream_Prefix(object __instance, ref Task<Stream> __result)
    {
        Stream stream = Stream.Null;
        Guard("image stream", () =>
        {
            var format = FileTypeOf(__instance);
            Call(__instance, "ClearSelection", false);
            if (MergedImage(__instance, format, null) is { } bytes)
                stream = new MemoryStream(bytes);
        });
        __result = Task.FromResult(stream);
        return false;
    }

    private static byte[]? MergedImage(object layout, string format, Size? size)
    {
        var imageView = Get(layout, "imageView");
        var annotations = Get(layout, "annotationsLayout") as View;
        if (HandlerOf(imageView) is { } handler)
            return handler.Merged(annotations, format, size);
        return SfImageEditorBridgeHandler.MergedOf(null, annotations, format, size);
    }

    // ImageLayout.Save(ImageFileType fileType, string? filePath, string? fileName, Size? imageSize).
    private static bool Save_Prefix(object __instance, object fileType, string? filePath, string? fileName, Size? imageSize, ref Task __result)
    {
        __result = SaveAsync(__instance, fileType, filePath, fileName, imageSize);
        return false;
    }

    private static async Task SaveAsync(object layout, object fileType, string? filePath, string? fileName, Size? imageSize)
    {
        try
        {
            Call(layout, "ClearSelection", false);
            var bytes = MergedImage(layout, fileType.ToString()!, imageSize);
            await SaveImage(layout, bytes, fileType, filePath, fileName).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfImageEditor save failed", ex);
        }
    }

    /// <summary>NativeHelper.SaveImage, with the desktop's Save dialog in place of WinUI's FileSavePicker.</summary>
    private static async Task SaveImage(object layout, byte[]? bytes, object fileType, string? filePath, string? fileName)
    {
        if (bytes == null || bytes.Length == 0)
            return;
        var helper = Type("Syncfusion.Maui.ImageEditor.ImageHelper", Asm)!;
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        fileName = string.IsNullOrEmpty(fileName) ? "EditedImage_" + stamp : fileName;
        if (filePath == null && CallInfo(layout, "RaiseSavePickerOpening") is false)
        {
            var extension = (string)CallStatic(helper, "GetImageExtension", fileType)!;
            var picked = await ToolkitFileSaver.PickSavePathAsync(null, fileName + "." + extension, CancellationToken.None).ConfigureAwait(true);
            if (string.IsNullOrEmpty(picked))
                return;
            filePath = Path.GetDirectoryName(picked);
            fileName = Path.GetFileNameWithoutExtension(picked);
            var pickedExtension = Path.GetExtension(picked);
            if (!string.IsNullOrEmpty(pickedExtension))
                fileType = CallStatic(helper, "GetFileType", pickedExtension)!;
        }
        filePath ??= Environment.GetFolderPath(Environment.SpecialFolder.MyPictures, Environment.SpecialFolderOption.Create);
        var argsType = Type("Syncfusion.Maui.ImageEditor.ImageSavingEventArgs", Asm)!;
        var args = New(argsType)!;
        Set(args, "ImageStream", new MemoryStream(bytes));
        Set(args, "FileType", fileType);
        Set(args, "FileName", fileName);
        Set(args, "FilePath", filePath);
        args = CallInfo(layout, "RaiseImageSaving", args) ?? args;
        if (args is CancelEventArgs { Cancel: true })
            return;
        var finalType = Get(args, "FileType")!;
        var name = Get(args, "FileName") + "." + (string)CallStatic(helper, "GetImageExtension", finalType)!;
        var folder = Get(args, "FilePath") as string ?? filePath;
        Directory.CreateDirectory(folder);
        var path = Path.GetFullPath(Path.Combine(folder, name));
        await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(true);
        CallStatic(helper, "RaiseImageSavedEvent", path, Info(layout));
    }

    // ImageLayout.ResetView(bool isSourceChange): the Windows build resets the edited image too.
    private static void ResetView_Postfix(object __instance, bool isSourceChange) => Guard("reset view", () =>
    {
        if (Get(__instance, "imageView") is { } imageView && Get(Info(__instance), "Source") != null)
            Call(imageView, "ResetImage", isSourceChange);
    });

    // ImageLayout.ApplyEffects(ImageEffect editorEffects, double value, ImageFileType format).
    private static bool ApplyEffects_Prefix(object __instance, object editorEffects, double value, object format)
    {
        Guard("effect", () =>
        {
            if (HandlerOf(Get(__instance, "imageView")) is { } handler)
                handler.ApplyEffect(editorEffects.ToString() ?? string.Empty, value, format.ToString());
        });
        return false;
    }

    // ImageLayout.StartEffect().
    private static bool LayoutStartEffect_Prefix(object __instance)
    {
        Guard("effects", () => HandlerOf(Get(__instance, "imageView"))?.StartEffect());
        return false;
    }

    // ImageLayout.EndEffect(bool isSave): a kept effect becomes an undo step holding the image
    // before it; a dropped one shows the image as it was.
    private static bool EndEffect_Prefix(object __instance, bool isSave)
    {
        Guard("end effect", () =>
        {
            if (HandlerOf(Get(__instance, "imageView")) is not { } handler)
                return;
            var bytes = handler.EndEffect(isSave);
            if (isSave)
            {
                var action = NewAction("Effect");
                if (action == null)
                    return;
                if (bytes != null)
                    s_cachedImages.AddOrUpdate(action, bytes);
                Call(Get(__instance, "toolbarActions"), "PushUndoStack", action, false);
            }
            else if (bytes != null)
            {
                Call(__instance, "UpdateImageSoure", ImageSource.FromStream(() => new MemoryStream(bytes)));
            }
        });
        return false;
    }

    private static object? NewAction(string toolbarAction)
    {
        var actionType = Type("Syncfusion.Maui.ImageEditor.EditorAction", Asm);
        var toolbarActionType = Type("Syncfusion.Maui.ImageEditor.ToolbarAction", Asm);
        if (actionType == null || toolbarActionType == null)
            return null;
        var action = New(actionType)!;
        Set(action, "Action", EnumValue(toolbarActionType, toolbarAction));
        return action;
    }

    // ImageLayout.CropImageView(Rect rect, ImageCropType cropType, double width, double height).
    private static bool CropImageView_Prefix(object __instance, Rect rect, object cropType, double width, double height, ref Task __result)
    {
        try
        {
            var imageView = Get(__instance, "imageView");
            var annotations = Get(__instance, "annotationsLayout") as View;
            if (imageView == null && annotations == null)
            {
                __result = Task.CompletedTask;
                return false;
            }
            var action = NewAction("Crop");
            if (action == null)
                return true;
            ImageSource? imageSource = null;
            var format = FileTypeOf(__instance);
            if (imageView != null)
            {
                if (HandlerOf(imageView)?.EncodeProcessed() is { } before)
                    s_cachedImages.AddOrUpdate(action, before);
                imageSource = Call(imageView, "GetCroppedImageSource", annotations, rect, cropType.ToString(), format) as ImageSource;
            }
            else
            {
                var self = (VisualElement)__instance;
                var desired = annotations!.DesiredSize;
                var size = Math.Abs(self.Rotation / 90.0 % 2.0) != 1.0 ? desired : new Size(self.DesiredSize.Height, self.DesiredSize.Width);
                var mergedBytes = SfImageEditorBridgeHandler.MergedOf(null, annotations, format, size);
                using var merged = SfImageProcessing.Decode(mergedBytes);
                if (merged != null)
                {
                    using var cropped = SfImageProcessing.Crop(merged, rect, size, cropType.ToString()!);
                    var bytes = SfImageProcessing.Encode(cropped, cropType.ToString() is "Circle" or "Ellipse" ? "Png" : format);
                    imageSource = ImageSource.FromStream(() => new MemoryStream(bytes));
                }
            }
            if (annotations != null)
            {
                Set(action, "Annotations", Call(annotations, "GetAnnotationsDetails"));
                Call(__instance, "RemoveAnnotationLayout");
            }
            Call(Get(__instance, "toolbarActions"), "PushUndoStack", action, false);
            Call(__instance, "UpdateImageSoure", imageSource);
            Call(__instance, "LayoutMeasure", width, height);
            __result = Task.CompletedTask;
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfImageEditor crop failed", ex);
            return true;
        }
    }

    // ImageLayout.SetImageSourceFromCache(EditorAction editorAction, bool isUndo): swaps the
    // image an undo step holds with the current one.
    private static bool SetImageSourceFromCache_Prefix(object __instance, object editorAction, ref Task __result)
    {
        __result = Task.CompletedTask;
        Guard("undo image", () =>
        {
            var imageView = Get(__instance, "imageView");
            if (HandlerOf(imageView) is not { } handler || imageView is not Image image)
                return;
            var current = handler.EncodeProcessed();
            if (s_cachedImages.TryGetValue(editorAction, out var cached))
            {
                handler.SetProcessed(SfImageProcessing.Decode(cached));
                image.Source = ImageSource.FromStream(() => new MemoryStream(cached));
            }
            else
            {
                handler.Reset(isSourceChange: false);
                Call(__instance, "UpdateImageView");
            }
            if (current != null)
                s_cachedImages.AddOrUpdate(editorAction, current);
            else
                s_cachedImages.Remove(editorAction);
        });
        return false;
    }

    // ImageLayout.FlipAnimation(ImageFlipDirection flipDirection), as the Windows build has it: a
    // quarter-turned picture flips about its other axis, and each flip adds a half turn.
    private static bool FlipAnimation_Prefix(object __instance, object flipDirection)
    {
        if (__instance is not Layout layout)
            return true;
        bool vertical = flipDirection.ToString() == "Vertical";
        var imageView = Get(layout, "imageView") as VisualElement;
        bool flipVertical = vertical;
        if (imageView != null && imageView.Rotation != 0.0 && Math.Abs(imageView.Rotation / 90.0 % 2.0) == 1.0)
            flipVertical = !vertical;
        bool updated = false;
        double originalX = layout.ScaleX, originalY = layout.ScaleY;
        layout.Animate("FlipAnimation", value =>
        {
            double scale;
            if (value < 1.0)
            {
                scale = 1.0 - value;
            }
            else
            {
                if (!updated)
                {
                    if (imageView != null)
                    {
                        if (flipVertical)
                            imageView.RotationX += 180.0;
                        else
                            imageView.RotationY += 180.0;
                    }
                    Guard("flip annotations", () => Call(Get(layout, "annotationsLayout"), "TransformView", 0.0, flipDirection));
                    updated = true;
                }
                scale = value - 1.0;
            }
            if (vertical)
                layout.ScaleY = scale;
            else
                layout.ScaleX = scale;
        }, 0.0, 2.0, 16, 250, null, (_, _) =>
        {
            layout.ScaleX = originalX;
            layout.ScaleY = originalY;
        });
        return false;
    }

    // TextView.OnEditorHandlerChanged(object? sender, EventArgs e): the text annotation's editor
    // gets the Windows text box's 5-pixel padding (Windows also strips the text box's border,
    // which OpenMaui's editor does not draw).
    private static bool TextEditorHandlerChanged_Prefix(object? sender)
    {
        if ((sender as VisualElement)?.Handler?.PlatformView is SkiaEditor editor)
            editor.Padding = new Thickness(5);
        return false;
    }

    // SfImageEditor.Save(ImageFileType? fileType, string? filePath, string? fileName, Size? imageSize):
    // saving asks for the storage-write permission first, which a Linux desktop app holds (as a
    // Windows desktop app does), so the save goes straight to the editor.
    private static bool EditorSave_Prefix(object __instance, object? fileType, string? filePath, string? fileName, Size? imageSize)
    {
        if (Get(__instance, "toolbarActions") is not { } toolbarActions)
            return false;
        _ = SaveThrough(toolbarActions, fileType, filePath, fileName, imageSize);
        return false;
    }

    private static async Task SaveThrough(object toolbarActions, object? fileType, string? filePath, string? fileName, Size? imageSize)
    {
        try
        {
            if (Call(toolbarActions, "Save", fileType, filePath, fileName, imageSize) is Task task)
                await task.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfImageEditor save failed", ex);
        }
    }

    // ImageEditLayout.IToolbarActions.ShowImagePicker(): the photo picker, as on Windows.
    private static bool ShowImagePicker_Prefix(object __instance)
    {
        _ = BrowseAsync(__instance);
        return false;
    }

    private static async Task BrowseAsync(object editLayout)
    {
        try
        {
            if (CallInfo(editLayout, "RaiseImageBrowse", new CancelEventArgs()) is CancelEventArgs { Cancel: true })
                return;
            var resources = Type("Syncfusion.Maui.ImageEditor.SfImageEditorResources", Asm);
            var title = resources != null ? CallStatic(resources, "GetLocalizedString", "Please select an image") as string : null;
            var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = title ?? "Please select an image" }).ConfigureAwait(true);
            if (results?.FirstOrDefault() is not { } result)
                return;
            var path = result.FullPath;
            var source = !string.IsNullOrEmpty(path) && File.Exists(path)
                ? ImageSource.FromStream(() => File.OpenRead(path))
                : ImageSource.FromStream(() => result.OpenReadAsync().GetAwaiter().GetResult());
            CallInfo(editLayout, "UpdateImageSource", source);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfImageEditor browse failed", ex);
        }
    }
}
