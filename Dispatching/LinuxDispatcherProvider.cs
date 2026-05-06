using Microsoft.Maui.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Dispatching;

public class LinuxDispatcherProvider : IDispatcherProvider
{
    private static LinuxDispatcherProvider? _instance;

    public static LinuxDispatcherProvider Instance => _instance ?? (_instance = new LinuxDispatcherProvider());

    public IDispatcher? GetForCurrentThread()
    {
        return LinuxDispatcher.Main;
    }
}
