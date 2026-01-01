// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for Skia controls that support ControlTemplates.
/// Provides infrastructure for completely redefining control appearance via XAML.
/// </summary>
public abstract class SkiaTemplatedView : SkiaView
{
    private SkiaView? _templateRoot;
    private bool _templateApplied;

    #region BindableProperties

    public static readonly BindableProperty ControlTemplateProperty =
        BindableProperty.Create(nameof(ControlTemplate), typeof(ControlTemplate), typeof(SkiaTemplatedView), null,
            propertyChanged: OnControlTemplateChanged);

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the control template that defines the visual appearance.
    /// </summary>
    public ControlTemplate? ControlTemplate
    {
        get => (ControlTemplate?)GetValue(ControlTemplateProperty);
        set => SetValue(ControlTemplateProperty, value);
    }

    /// <summary>
    /// Gets the root element created from the ControlTemplate.
    /// </summary>
    protected SkiaView? TemplateRoot => _templateRoot;

    /// <summary>
    /// Gets a value indicating whether a template has been applied.
    /// </summary>
    protected bool IsTemplateApplied => _templateApplied;

    #endregion

    private static void OnControlTemplateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaTemplatedView view)
        {
            view.OnControlTemplateChanged((ControlTemplate?)oldValue, (ControlTemplate?)newValue);
        }
    }

    /// <summary>
    /// Called when the ControlTemplate changes.
    /// </summary>
    protected virtual void OnControlTemplateChanged(ControlTemplate? oldTemplate, ControlTemplate? newTemplate)
    {
        _templateApplied = false;
        _templateRoot = null;

        if (newTemplate != null)
        {
            ApplyTemplate();
        }

        InvalidateMeasure();
    }

    /// <summary>
    /// Applies the current ControlTemplate if one is set.
    /// </summary>
    protected virtual void ApplyTemplate()
    {
        if (ControlTemplate == null || _templateApplied)
            return;

        try
        {
            // Create content from template
            var content = ControlTemplate.CreateContent();

            // If the content is a MAUI Element, try to convert it to a SkiaView
            if (content is Element element)
            {
                _templateRoot = ConvertElementToSkiaView(element);
            }
            else if (content is SkiaView skiaView)
            {
                _templateRoot = skiaView;
            }

            if (_templateRoot != null)
            {
                _templateRoot.Parent = this;
                OnTemplateApplied();
            }

            _templateApplied = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error applying template: {ex.Message}");
        }
    }

    /// <summary>
    /// Called after a template has been successfully applied.
    /// Override to perform template-specific initialization.
    /// </summary>
    protected virtual void OnTemplateApplied()
    {
        // Find and bind ContentPresenter if present
        var presenter = FindTemplateChild<SkiaContentPresenter>("PART_ContentPresenter");
        if (presenter != null)
        {
            OnContentPresenterFound(presenter);
        }
    }

    /// <summary>
    /// Called when a ContentPresenter is found in the template.
    /// Override to set up the content binding.
    /// </summary>
    protected virtual void OnContentPresenterFound(SkiaContentPresenter presenter)
    {
        // Derived classes should override to bind their content
    }

    /// <summary>
    /// Finds a named element in the template tree.
    /// </summary>
    protected T? FindTemplateChild<T>(string name) where T : SkiaView
    {
        if (_templateRoot == null)
            return null;

        return FindChild<T>(_templateRoot, name);
    }

    private static T? FindChild<T>(SkiaView root, string name) where T : SkiaView
    {
        if (root is T typed && root.Name == name)
            return typed;

        if (root is SkiaLayoutView layout)
        {
            foreach (var child in layout.Children)
            {
                var found = FindChild<T>(child, name);
                if (found != null)
                    return found;
            }
        }
        else if (root is SkiaContentPresenter presenter && presenter.Content != null)
        {
            return FindChild<T>(presenter.Content, name);
        }

        return null;
    }

    /// <summary>
    /// Converts a MAUI Element to a SkiaView.
    /// Override to provide custom conversion logic.
    /// </summary>
    protected virtual SkiaView? ConvertElementToSkiaView(Element element)
    {
        // This is a simplified conversion - in a full implementation,
        // you would use the handler system to create proper platform views

        return element switch
        {
            // Handle common layout types
            Microsoft.Maui.Controls.StackLayout sl => CreateSkiaStackLayout(sl),
            Microsoft.Maui.Controls.Grid grid => CreateSkiaGrid(grid),
            Microsoft.Maui.Controls.Border border => CreateSkiaBorder(border),
            Microsoft.Maui.Controls.Label label => CreateSkiaLabel(label),
            Microsoft.Maui.Controls.ContentPresenter cp => new SkiaContentPresenter(),
            _ => new SkiaLabel { Text = $"[{element.GetType().Name}]", TextColor = SKColors.Gray }
        };
    }

    private SkiaStackLayout CreateSkiaStackLayout(Microsoft.Maui.Controls.StackLayout sl)
    {
        var layout = new SkiaStackLayout
        {
            Orientation = sl.Orientation == Microsoft.Maui.Controls.StackOrientation.Vertical
                ? StackOrientation.Vertical
                : StackOrientation.Horizontal,
            Spacing = (float)sl.Spacing
        };

        foreach (var child in sl.Children)
        {
            if (child is Element element)
            {
                var skiaChild = ConvertElementToSkiaView(element);
                if (skiaChild != null)
                    layout.AddChild(skiaChild);
            }
        }

        return layout;
    }

    private SkiaGrid CreateSkiaGrid(Microsoft.Maui.Controls.Grid grid)
    {
        var layout = new SkiaGrid();

        // Set row definitions
        foreach (var rowDef in grid.RowDefinitions)
        {
            var gridLength = rowDef.Height.IsAuto ? GridLength.Auto :
                            rowDef.Height.IsStar ? new GridLength((float)rowDef.Height.Value, GridUnitType.Star) :
                            new GridLength((float)rowDef.Height.Value, GridUnitType.Absolute);
            layout.RowDefinitions.Add(gridLength);
        }

        // Set column definitions
        foreach (var colDef in grid.ColumnDefinitions)
        {
            var gridLength = colDef.Width.IsAuto ? GridLength.Auto :
                            colDef.Width.IsStar ? new GridLength((float)colDef.Width.Value, GridUnitType.Star) :
                            new GridLength((float)colDef.Width.Value, GridUnitType.Absolute);
            layout.ColumnDefinitions.Add(gridLength);
        }

        // Add children
        foreach (var child in grid.Children)
        {
            if (child is Element element)
            {
                var skiaChild = ConvertElementToSkiaView(element);
                if (skiaChild != null)
                {
                    var row = Microsoft.Maui.Controls.Grid.GetRow((BindableObject)child);
                    var col = Microsoft.Maui.Controls.Grid.GetColumn((BindableObject)child);
                    var rowSpan = Microsoft.Maui.Controls.Grid.GetRowSpan((BindableObject)child);
                    var colSpan = Microsoft.Maui.Controls.Grid.GetColumnSpan((BindableObject)child);

                    layout.AddChild(skiaChild, row, col, rowSpan, colSpan);
                }
            }
        }

        return layout;
    }

    private SkiaBorder CreateSkiaBorder(Microsoft.Maui.Controls.Border border)
    {
        float cornerRadius = 0;
        if (border.StrokeShape is Microsoft.Maui.Controls.Shapes.RoundRectangle rr)
        {
            cornerRadius = (float)rr.CornerRadius.TopLeft;
        }

        var skiaBorder = new SkiaBorder
        {
            CornerRadius = cornerRadius,
            StrokeThickness = (float)border.StrokeThickness
        };

        if (border.Stroke is SolidColorBrush strokeBrush)
        {
            skiaBorder.Stroke = strokeBrush.Color.ToSKColor();
        }

        if (border.Background is SolidColorBrush bgBrush)
        {
            skiaBorder.BackgroundColor = bgBrush.Color.ToSKColor();
        }

        if (border.Content is Element content)
        {
            var skiaContent = ConvertElementToSkiaView(content);
            if (skiaContent != null)
                skiaBorder.AddChild(skiaContent);
        }

        return skiaBorder;
    }

    private SkiaLabel CreateSkiaLabel(Microsoft.Maui.Controls.Label label)
    {
        var skiaLabel = new SkiaLabel
        {
            Text = label.Text ?? "",
            FontSize = (float)label.FontSize
        };

        if (label.TextColor != null)
        {
            skiaLabel.TextColor = label.TextColor.ToSKColor();
        }

        return skiaLabel;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (_templateRoot != null && _templateApplied)
        {
            // Render the template
            _templateRoot.Draw(canvas);
        }
        else
        {
            // Render default appearance
            DrawDefaultAppearance(canvas, bounds);
        }
    }

    /// <summary>
    /// Draws the default appearance when no template is applied.
    /// Override in derived classes to provide default rendering.
    /// </summary>
    protected abstract void DrawDefaultAppearance(SKCanvas canvas, SKRect bounds);

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        if (_templateRoot != null && _templateApplied)
        {
            return _templateRoot.Measure(availableSize);
        }

        return MeasureDefaultAppearance(availableSize);
    }

    /// <summary>
    /// Measures the default appearance when no template is applied.
    /// Override in derived classes.
    /// </summary>
    protected virtual SKSize MeasureDefaultAppearance(SKSize availableSize)
    {
        return new SKSize(100, 40);
    }

    public new void Arrange(SKRect bounds)
    {
        base.Arrange(bounds);

        if (_templateRoot != null && _templateApplied)
        {
            _templateRoot.Arrange(bounds);
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y))
            return null;

        if (_templateRoot != null && _templateApplied)
        {
            var hit = _templateRoot.HitTest(x, y);
            if (hit != null)
                return hit;
        }

        return this;
    }
}

