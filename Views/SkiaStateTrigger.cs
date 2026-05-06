// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for state triggers that automatically activate visual states.
/// </summary>
public abstract class SkiaStateTriggerBase
{
    private bool _isActive;
    private SkiaVisualState? _ownerState;
    private SkiaView? _ownerView;

    /// <summary>
    /// Gets whether this trigger is currently active.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        protected set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnIsActiveChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the visual state this trigger belongs to.
    /// </summary>
    internal SkiaVisualState? OwnerState
    {
        get => _ownerState;
        set => _ownerState = value;
    }

    /// <summary>
    /// Gets or sets the view this trigger is attached to.
    /// </summary>
    internal SkiaView? OwnerView
    {
        get => _ownerView;
        set
        {
            _ownerView = value;
            OnAttached();
        }
    }

    /// <summary>
    /// Called when the trigger is attached to a view.
    /// </summary>
    protected virtual void OnAttached()
    {
    }

    /// <summary>
    /// Called when IsActive changes.
    /// </summary>
    protected virtual void OnIsActiveChanged()
    {
        if (_isActive && _ownerState != null && _ownerView != null)
        {
            SkiaVisualStateManager.GoToState(_ownerView, _ownerState.Name);
        }
    }
}

/// <summary>
/// A trigger that activates based on a boolean property.
/// Maps to MAUI StateTrigger.
/// </summary>
public class SkiaStateTrigger : SkiaStateTriggerBase
{
    private bool _isActiveValue;

    /// <summary>
    /// Gets or sets whether this trigger should be active.
    /// </summary>
    public bool IsActiveValue
    {
        get => _isActiveValue;
        set
        {
            _isActiveValue = value;
            IsActive = value;
        }
    }
}

/// <summary>
/// A trigger that activates based on window size thresholds.
/// Maps to MAUI AdaptiveTrigger.
/// </summary>
public class SkiaAdaptiveTrigger : SkiaStateTriggerBase
{
    private double _minWindowWidth = -1;
    private double _minWindowHeight = -1;

    /// <summary>
    /// Gets or sets the minimum window width for this trigger to activate.
    /// </summary>
    public double MinWindowWidth
    {
        get => _minWindowWidth;
        set
        {
            _minWindowWidth = value;
            UpdateIsActive();
        }
    }

    /// <summary>
    /// Gets or sets the minimum window height for this trigger to activate.
    /// </summary>
    public double MinWindowHeight
    {
        get => _minWindowHeight;
        set
        {
            _minWindowHeight = value;
            UpdateIsActive();
        }
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        // Subscribe to window size changes if needed
        UpdateIsActive();
    }

    private void UpdateIsActive()
    {
        if (OwnerView == null)
        {
            IsActive = false;
            return;
        }

        // Get current window size from the view's bounds
        var width = OwnerView.Bounds.Width;
        var height = OwnerView.Bounds.Height;

        bool widthMet = _minWindowWidth < 0 || width >= _minWindowWidth;
        bool heightMet = _minWindowHeight < 0 || height >= _minWindowHeight;

        IsActive = widthMet && heightMet;
    }
}

/// <summary>
/// A trigger that activates when a property equals a specific value.
/// Maps to MAUI CompareStateTrigger.
/// </summary>
public class SkiaCompareStateTrigger : SkiaStateTriggerBase
{
    private object? _property;
    private object? _value;

    /// <summary>
    /// Gets or sets the property value to compare.
    /// </summary>
    public object? Property
    {
        get => _property;
        set
        {
            _property = value;
            UpdateIsActive();
        }
    }

    /// <summary>
    /// Gets or sets the value to compare against.
    /// </summary>
    public object? Value
    {
        get => _value;
        set
        {
            _value = value;
            UpdateIsActive();
        }
    }

    private void UpdateIsActive()
    {
        if (_property == null && _value == null)
        {
            IsActive = true;
            return;
        }

        if (_property == null || _value == null)
        {
            IsActive = _property == _value;
            return;
        }

        // Try to compare values
        IsActive = _property.Equals(_value);
    }
}

/// <summary>
/// A trigger that activates based on device idiom (Desktop, Phone, Tablet, etc.).
/// </summary>
public class SkiaDeviceStateTrigger : SkiaStateTriggerBase
{
    private string _deviceType = "";

    /// <summary>
    /// Gets or sets the device type to match (Desktop, Phone, Tablet, Watch, TV).
    /// </summary>
    public string DeviceType
    {
        get => _deviceType;
        set
        {
            _deviceType = value;
            UpdateIsActive();
        }
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        UpdateIsActive();
    }

    private void UpdateIsActive()
    {
        // On Linux, we're always Desktop
        IsActive = string.Equals(_deviceType, "Desktop", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// A trigger that activates based on orientation (Portrait or Landscape).
/// </summary>
public class SkiaOrientationStateTrigger : SkiaStateTriggerBase
{
    private SkiaDisplayOrientation _orientation = SkiaDisplayOrientation.Portrait;

    /// <summary>
    /// Gets or sets the orientation to match.
    /// </summary>
    public SkiaDisplayOrientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            UpdateIsActive();
        }
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        UpdateIsActive();
    }

    private void UpdateIsActive()
    {
        if (OwnerView == null)
        {
            IsActive = false;
            return;
        }

        var width = OwnerView.Bounds.Width;
        var height = OwnerView.Bounds.Height;

        var currentOrientation = width > height
            ? SkiaDisplayOrientation.Landscape
            : SkiaDisplayOrientation.Portrait;

        IsActive = currentOrientation == _orientation;
    }
}

/// <summary>
/// Display orientation values for state triggers.
/// </summary>
public enum SkiaDisplayOrientation
{
    Portrait,
    Landscape
}
