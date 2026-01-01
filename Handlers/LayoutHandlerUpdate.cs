namespace Microsoft.Maui.Platform.Linux.Handlers;

public class LayoutHandlerUpdate
{
	public int Index { get; }

	public IView? View { get; }

	public LayoutHandlerUpdate(int index, IView? view)
	{
		Index = index;
		View = view;
	}
}
