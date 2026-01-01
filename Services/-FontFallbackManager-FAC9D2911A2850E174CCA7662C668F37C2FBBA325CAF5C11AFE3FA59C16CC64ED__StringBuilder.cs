using System.Collections.Generic;

namespace Microsoft.Maui.Platform.Linux.Services;

internal class _003CFontFallbackManager_003EFAC9D2911A2850E174CCA7662C668F37C2FBBA325CAF5C11AFE3FA59C16CC64ED__StringBuilder
{
	private readonly List<char> _chars = new List<char>();

	public int Length => _chars.Count;

	public void Append(string s)
	{
		_chars.AddRange(s);
	}

	public void Clear()
	{
		_chars.Clear();
	}

	public override string ToString()
	{
		return new string(_chars.ToArray());
	}
}
