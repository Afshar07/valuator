using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Onboarding;

/// <summary>First launch with an empty database: start a real project or look around a sample workspace.</summary>
internal sealed partial class WelcomeViewModel : ViewModelBase
{
    private readonly IOnboardingHost _host;

    public WelcomeViewModel(IOnboardingHost host, LocalizedStrings strings, DisplayOptionsViewModel display)
    {
        _host = host; L = strings; Display = display;
        RefreshOnLanguageChange(strings);
    }

    public LocalizedStrings L { get; }
    public DisplayOptionsViewModel Display { get; }

    [RelayCommand]
    private Task StartOwnAsync() => _host.RunAsync(() => { _host.ShowWizard(fromWelcome: true); return Task.CompletedTask; });

    [RelayCommand]
    private Task StartSampleAsync() => _host.RunAsync(_host.StartSampleAsync);
}
