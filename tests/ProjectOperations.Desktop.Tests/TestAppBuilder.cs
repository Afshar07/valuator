using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(ProjectOperations.Desktop.Tests.TestAppBuilder))]

namespace ProjectOperations.Desktop.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var capture = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PROJECTOPS_UI_CAPTURE_DIR"));
        var builder = AppBuilder.Configure<TestApplication>();
        if (capture) builder = builder.UseSkia();
        return builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = !capture });
    }
}

public sealed class TestApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
