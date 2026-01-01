using System.Collections.Generic;

namespace Microsoft.Maui.Platform;

public interface ISkiaQueryAttributable
{
    void ApplyQueryAttributes(IDictionary<string, object> query);
}
