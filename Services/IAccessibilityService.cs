namespace Microsoft.Maui.Platform.Linux.Services;

public interface IAccessibilityService
{
	bool IsEnabled { get; }

	void Initialize();

	void Register(IAccessible accessible);

	void Unregister(IAccessible accessible);

	void NotifyFocusChanged(IAccessible? accessible);

	void NotifyPropertyChanged(IAccessible accessible, AccessibleProperty property);

	void NotifyStateChanged(IAccessible accessible, AccessibleState state, bool value);

	void Announce(string text, AnnouncementPriority priority = AnnouncementPriority.Polite);

	void Shutdown();
}
