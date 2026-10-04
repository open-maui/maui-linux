// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("Microsoft.Maui.DeviceTests.LinuxTestFramework", Microsoft.Maui.DeviceTests.ConformanceAssembly.Name)]

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
					return base.FindTestsForMethod(testMethod, includeSourceInformation,
						new DataRowSkipBus(messageBus, testMethod, DiagnosticMessageSink, discoveryOptions), discoveryOptions);

				var skipped = new XunitSkippedDataRowTestCase(
					DiagnosticMessageSink,
					discoveryOptions.MethodDisplayOrDefault(),
					discoveryOptions.MethodDisplayOptionsOrDefault(),
					testMethod,
					"Linux: " + reason);
				return ReportDiscoveredTestCase(skipped, includeSourceInformation, messageBus);
			}
		}

		/// <summary>
		/// Skips single data rows of a theory (KnownSkips.ReasonForCase, matched on the
		/// row's display name), for a row that needs a mechanism Linux lacks while the
		/// theory's other rows run.
		/// </summary>
		sealed class DataRowSkipBus : IMessageBus
		{
			readonly IMessageBus _inner;
			readonly ITestMethod _method;
			readonly IMessageSink _diagnostics;
			readonly ITestFrameworkDiscoveryOptions _options;

			public DataRowSkipBus(IMessageBus inner, ITestMethod method, IMessageSink diagnostics, ITestFrameworkDiscoveryOptions options)
			{
				_inner = inner;
				_method = method;
				_diagnostics = diagnostics;
				_options = options;
			}

			public bool QueueMessage(IMessageSinkMessage message)
			{
				if (message is ITestCaseDiscoveryMessage discovered &&
					KnownSkips.ReasonForCase(_method.TestClass.Class.Name, _method.Method.Name, discovered.TestCase.DisplayName) is string reason)
				{
					var skipped = new XunitSkippedDataRowTestCase(
						_diagnostics,
						_options.MethodDisplayOrDefault(),
						_options.MethodDisplayOptionsOrDefault(),
						_method,
						"Linux: " + reason,
						discovered.TestCase.TestMethodArguments);
					return _inner.QueueMessage(new TestCaseDiscoveryMessage(skipped));
				}
				return _inner.QueueMessage(message);
			}

			public void Dispose() { }
		}
	}
}
