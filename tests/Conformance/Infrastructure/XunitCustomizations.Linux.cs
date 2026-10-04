// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

// MAUI's xUnitCustomizations.cs (src/TestUtils/src/DeviceTests) points its
// discoverers at the assembly "Microsoft.Maui.TestUtils.DeviceTests", which the
// device runners load and a plain `dotnet test` run does not: with it compiled
// in, xunit discovers nothing. This is the part of it the handler tests use:
// [Category(...)] as a "Category" trait (filter with Category=Label etc.).
// [Fact]/[Theory] then bind to xunit's own attributes.
namespace Microsoft.Maui
{
	public class CategoryDiscoverer : ITraitDiscoverer
	{
		public const string Category = "Category";

		public CategoryDiscoverer(IMessageSink diagnosticMessageSink)
		{
		}

		public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
		{
			var args = traitAttribute.GetConstructorArguments().FirstOrDefault();
			if (args is string[] categories)
			{
				foreach (var category in categories)
					yield return new KeyValuePair<string, string>(Category, category);
			}
		}
	}

	[TraitDiscoverer("Microsoft.Maui.CategoryDiscoverer", "Microsoft.Maui.Core.DeviceTests")]
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
	public class CategoryAttribute : Attribute, ITraitAttribute
	{
		public CategoryAttribute(params string[] categories) { }
	}
}
