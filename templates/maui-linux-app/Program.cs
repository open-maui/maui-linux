// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux;

namespace MauiLinuxApp;

public class Program
{
    public static void Main(string[] args)
    {
        var app = LinuxApplication.CreateBuilder()
            .UseApp<App>()
            .Build();

        app.Run();
    }
}
