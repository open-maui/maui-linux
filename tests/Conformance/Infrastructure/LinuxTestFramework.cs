// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("Microsoft.Maui.DeviceTests.LinuxTestFramework", "Microsoft.Maui.Core.DeviceTests")]

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// xunit's own framework, plus one thing: MAUI tests listed in
	/// <see cref="KnownSkips"/> are reported as skipped with the listed reason
	/// instead of being run. That is how a MAUI test that needs a capability the
	/// Linux platform genuinely cannot have is marked, without touching MAUI's
	/// source. Everything else (including every known parity gap) runs and
	/// fails for real.
	/// </summary>
	public class LinuxTestFramework : XunitTestFramework
	{
		public LinuxTestFramework(IMessageSink messageSink) : base(messageSink)
		{
		}

		protected override ITestFrameworkDiscoverer CreateDiscoverer(IAssemblyInfo assemblyInfo) =>
			new Discoverer(assemblyInfo, SourceInformationProvider, DiagnosticMessageSink);

		sealed class Discoverer : XunitTestFrameworkDiscoverer
		{
			public Discoverer(IAssemblyInfo assemblyInfo, ISourceInformationProvider sourceProvider, IMessageSink diagnosticMessageSink)
				: base(assemblyInfo, sourceProvider, diagnosticMessageSink)
			{
			}

			protected override bool FindTestsForMethod(ITestMethod testMethod, bool includeSourceInformation, IMessageBus messageBus, ITestFrameworkDiscoveryOptions discoveryOptions)
			{
				var isTest = testMethod.Method.GetCustomAttributes(typeof(FactAttribute)).Any();
				var reason = isTest ? KnownSkips.ReasonFor(testMethod.TestClass.Class.Name, testMethod.Method.Name) : null;
				if (reason is null)
					return base.FindTestsForMethod(testMethod, includeSourceInformation, messageBus, discoveryOptions);

				var skipped = new XunitSkippedDataRowTestCase(
					DiagnosticMessageSink,
					discoveryOptions.MethodDisplayOrDefault(),
					discoveryOptions.MethodDisplayOptionsOrDefault(),
					testMethod,
					"Linux: " + reason);
				return ReportDiscoveredTestCase(skipped, includeSourceInformation, messageBus);
			}
		}
	}
}
