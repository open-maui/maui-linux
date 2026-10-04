// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// The test assembly's name, which xunit needs to find the framework and the
	/// trait discoverer. The shared infrastructure (LinuxTestFramework,
	/// XunitCustomizations) is compiled into the Core and the Controls suites;
	/// each declares its own name.
	/// </summary>
	static class ConformanceAssembly
	{
		public const string Name = "Microsoft.Maui.Core.DeviceTests";
	}
}
