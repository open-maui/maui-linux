// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public static class AccessibilityServiceFactory
{
    private static IAccessibilityService? _instance;
    private static readonly Lock _lock = new();

    public static IAccessibilityService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= CreateService();
                }
            }
            return _instance;
        }
    }

    private static IAccessibilityService CreateService()
    {
        try
        {
            var service = new AtSpi2AccessibilityService();
            service.Initialize();
            return service;
        }
        catch
        {
            return new NullAccessibilityService();
        }
    }

    public static void Reset()
    {
        lock (_lock)
        {
            _instance?.Shutdown();
            _instance = null;
        }
    }
}
