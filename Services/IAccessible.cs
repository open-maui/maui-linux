// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public interface IAccessible
{
    string AccessibleId { get; }
    string AccessibleName { get; }
    string AccessibleDescription { get; }
    AccessibleRole Role { get; }
    AccessibleStates States { get; }
    IAccessible? Parent { get; }
    IReadOnlyList<IAccessible> Children { get; }
    AccessibleRect Bounds { get; }
    IReadOnlyList<AccessibleAction> Actions { get; }
    double? Value { get; }
    double? MinValue { get; }
    double? MaxValue { get; }

    bool DoAction(string actionName);
    bool SetValue(double value);
}
