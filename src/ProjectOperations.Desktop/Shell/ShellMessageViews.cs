using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Shell;

/// <summary>Dismissible error strip above the page, showing <see cref="ShellMessages.ErrorKey"/>.</summary>
internal sealed class ErrorBanner : Border
{
    public TextBlock Text { get; } = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Start, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };

    public ErrorBanner(ShellMessages messages, ILocalizationService text, LocalizedControls localized)
    {
        Name = "ErrorBanner"; IsVisible = false; Padding = new Thickness(14, 8, 8, 8); Margin = new Thickness(32, 16, 32, 0);
        CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium); BorderThickness = new Thickness(1);
        this.Paint(BackgroundProperty, "ErrorSoft").Paint(BorderBrushProperty, "Error");
        PresentationTheme.Typeset(Text, "Small", "TextPrimary");
        localized.Bind(Text, control => control.Text = messages.ErrorKey is { } key ? text.Get(key) : "");
        var dismiss = new Button { Content = Icons.Glyph(Icons.X, 14, "TextSecondary"), VerticalAlignment = VerticalAlignment.Top }; dismiss.Classes.Add("icon");
        localized.Bind(dismiss, control => AutomationProperties.SetName(control, text.Get("action.close")));
        dismiss.Click += (_, _) => messages.HideError();
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        var icon = Icons.Glyph(Icons.WarningCircle, 17, "Error"); icon.VerticalAlignment = VerticalAlignment.Top; icon.Margin = new Thickness(0, 1, 0, 0);
        row.Children.Add(icon); Grid.SetColumn(Text, 1); row.Children.Add(Text); Grid.SetColumn(dismiss, 2); row.Children.Add(dismiss);
        Child = row;
        messages.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ShellMessages.ErrorKey)) return;
            var shown = messages.ErrorKey is not null;
            if (shown) Text.Text = text.Get(messages.ErrorKey!);
            Text.IsVisible = shown; IsVisible = shown;
        };
    }
}

/// <summary>Short notice at the bottom of the window, hidden again after a few seconds.</summary>
internal sealed class ToastView : Border
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(4.8);
    private readonly DispatcherTimer _timer = new() { Interval = Duration };

    public ToastView(ShellMessages messages, ILocalizationService text)
    {
        Name = "Toast"; IsVisible = false; ZIndex = 40; IsHitTestVisible = false; MaxWidth = 540; Padding = new Thickness(14, 10);
        CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium);
        HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Bottom; Margin = new Thickness(20, 0, 20, 20);
        var caption = new TextBlock { TextWrapping = TextWrapping.Wrap, Name = "ToastText", VerticalAlignment = VerticalAlignment.Center };
        PresentationTheme.Typeset(caption, "Small", "BackgroundApp");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(Icons.Glyph(Icons.Info, 16, "BackgroundApp", IconWeight.Fill)); row.Children.Add(caption);
        Child = row;
        this.Paint(BackgroundProperty, "TextPrimary").RaisedShadowed();
        _timer.Tick += (_, _) => messages.DismissToast();
        messages.ToastShown += (_, key) => { caption.Text = text.Get(key); IsVisible = true; _timer.Stop(); _timer.Start(); };
        messages.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellMessages.ToastKey) && messages.ToastKey is null) { _timer.Stop(); IsVisible = false; }
        };
    }
}
