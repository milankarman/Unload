using System.Globalization;
using System.Windows;

namespace unload;

public partial class App : Application
{
    public App()
    {
        // Numbers are always read and written with a "." decimal separator, regardless of the user's locale.
        // Default* covers threads created later. Current* covers this already running UI thread.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }
}
