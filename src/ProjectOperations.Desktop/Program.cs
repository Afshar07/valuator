using Avalonia;
using Velopack;

namespace ProjectOperations.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: Velopack launches the app with hook arguments during install, update and uninstall and expects a quick exit.
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
