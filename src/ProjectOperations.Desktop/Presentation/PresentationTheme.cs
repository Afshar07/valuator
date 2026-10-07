using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace ProjectOperations.Desktop;

/// <summary>Foreground/background token pair used by pills, badges and status marks.</summary>
internal readonly record struct Tone(string Foreground, string Background)
{
    public static readonly Tone Error = new("Error", "ErrorSoft");
    public static readonly Tone Warning = new("Warning", "WarningSoft");
    public static readonly Tone Success = new("Success", "SuccessSoft");
    public static readonly Tone Accent = new("AccentText", "AccentSoft");
    public static readonly Tone Neutral = new("TextSecondary", "BackgroundTrack");
    public static readonly Tone Ai = new("Ai", "AiSoft");
}

/// <summary>
/// Semantic design tokens from the Valuator design. Light tokens follow the Figma MVP foundations; dark is derived.
/// Presentation code asks for tokens by name and binds them as dynamic resources, so theme switching is live.
/// Raw colors, radii and type sizes live only here.
/// </summary>
internal static class PresentationTheme
{
    public static readonly FontFamily PersianFontFamily = new("avares://ProjectOperations.Desktop/Assets/Fonts/IRANYekanX#IRANYekanX");
    public static readonly FontFamily LatinFontFamily = new("avares://ProjectOperations.Desktop/Assets/Fonts/Inter#Inter");

    private static readonly IReadOnlyDictionary<string, (string Light, string Dark)> Colors = new Dictionary<string, (string, string)>
    {
        ["BackgroundApp"] = ("#F5F7F8", "#161826"),
        ["BackgroundSidebar"] = ("#EEF2F5", "#1B1D2B"),
        ["BackgroundCard"] = ("#FFFFFF", "#212331"),
        ["BackgroundMuted"] = ("#F8FAFC", "#1C1E2C"),
        ["BackgroundTrack"] = ("#EDF1F5", "#2C2F3B"),
        ["TextPrimary"] = ("#183153", "#E9E9ED"),
        ["TextSecondary"] = ("#61758A", "#B2B6CA"),
        ["TextTertiary"] = ("#8B9AAF", "#83879A"),
        ["BorderDefault"] = ("#E3E8EF", "#343744"),
        ["BorderSubtle"] = ("#E9EDF2", "#2C2F3B"),
        ["Accent"] = ("#0F8B78", "#3FB9A3"),
        ["AccentSoft"] = ("#E7F5F2", "#18332F"),
        ["AccentText"] = ("#0B6B5D", "#7FD6C6"),
        ["OnAccent"] = ("#FFFFFF", "#212331"),
        ["Success"] = ("#169B62", "#4CC38A"),
        ["SuccessSoft"] = ("#E9F8F1", "#183226"),
        ["Warning"] = ("#B87508", "#F0B44C"),
        ["WarningSoft"] = ("#FFF4DE", "#352B19"),
        ["Error"] = ("#C93B46", "#F07178"),
        ["ErrorSoft"] = ("#FDECEF", "#3A2128"),
        ["Ai"] = ("#6D5CE7", "#B5ABFC"),
        ["AiSoft"] = ("#F0EEFF", "#2B2741"),
        ["AiBorder"] = ("#B9B0F3", "#5D5294"),
        ["Scrim"] = ("#730A0E18", "#990A0E18")
    };

    private static readonly (string Light, string Dark) CardShadow = ("0 1 2 0 #0D183153", "0 0 0 1 #33000000");
    private static readonly (string Light, string Dark) RaisedShadow = ("0 12 40 0 #40000000", "0 12 40 0 #66000000");

    public const double RadiusSmall = 6, RadiusControl = 8, RadiusMedium = 10, RadiusCard = 12, RadiusDialog = 14, RadiusPill = 99;

    private static readonly IReadOnlyDictionary<string, (double Size, FontWeight Weight)> Typography = new Dictionary<string, (double, FontWeight)>
    {
        ["Title"] = (24, FontWeight.SemiBold),
        ["Stat"] = (26, FontWeight.SemiBold),
        ["Dialog"] = (16, FontWeight.SemiBold),
        ["PanelTitle"] = (15, FontWeight.SemiBold),
        ["Heading"] = (14, FontWeight.SemiBold),
        ["Body"] = (13, FontWeight.Normal),
        ["BodyMedium"] = (13, FontWeight.Medium),
        ["BodyStrong"] = (13, FontWeight.SemiBold),
        ["Small"] = (12.5, FontWeight.Normal),
        ["SmallStrong"] = (12.5, FontWeight.SemiBold),
        ["Caption"] = (12, FontWeight.Normal),
        ["CaptionMedium"] = (12, FontWeight.Medium),
        ["Meta"] = (11.5, FontWeight.Normal),
        ["MetaMedium"] = (11.5, FontWeight.Medium),
        ["Micro"] = (11, FontWeight.Medium)
    };

    /// <summary>Dynamic brush binding for a color token; follows the active theme variant.</summary>
    public static IBinding Dynamic(string token) => new DynamicResourceExtension(token + "Brush");

    /// <summary>Binds a brush property to a color token.</summary>
    public static T Paint<T>(this T target, AvaloniaProperty property, string token) where T : AvaloniaObject
    {
        target[!property] = Dynamic(token); return target;
    }

    public static T CardShadowed<T>(this T border) where T : Border
    {
        border[!Border.BoxShadowProperty] = new DynamicResourceExtension("CardShadow"); return border;
    }

    public static T RaisedShadowed<T>(this T border) where T : Border
    {
        border[!Border.BoxShadowProperty] = new DynamicResourceExtension("RaisedShadow"); return border;
    }

    /// <summary>Applies one of the named typography styles to a text block.</summary>
    public static TextBlock Typeset(TextBlock text, string style, string color = "TextPrimary")
    {
        var (size, weight) = Typography[style];
        text.FontSize = size; text.FontWeight = weight; text.Paint(TextBlock.ForegroundProperty, color);
        if (style == "Title") text.LetterSpacing = -0.24;
        return text;
    }

    /// <summary>Frozen token color for a variant, for resources that cannot bind dynamically (e.g. per-control theme dictionaries).</summary>
    public static Color TokenColor(string token, bool dark) => Color.Parse(dark ? Colors[token].Dark : Colors[token].Light);

    public static ThemeVariant Variant(string preference) => preference switch
    {
        "dark" => ThemeVariant.Dark,
        "light" => ThemeVariant.Light,
        _ => ThemeVariant.Default
    };

    public static void Apply(Window window)
    {
        window.Resources.ThemeDictionaries[ThemeVariant.Light] = Palette(dark: false);
        window.Resources.ThemeDictionaries[ThemeVariant.Dark] = Palette(dark: true);

        NeutralizeFluentStates(window);

        window.Styles.Add(Rule(s => s.OfType<CheckBox>(), (CheckBox.ForegroundProperty, Dynamic("TextPrimary")), (CheckBox.FontSizeProperty, 12.5)));

        // Tabs: underline accent on the selected item, secondary text otherwise.
        window.Styles.Add(Rule(s => s.OfType<TabControl>(), (TabControl.BackgroundProperty, Brushes.Transparent), (TabControl.PaddingProperty, new Thickness(0, 20, 0, 0))));
        window.Styles.Add(Rule(s => s.OfType<TabItem>(), (TabItem.FontSizeProperty, 13d), (TabItem.FontWeightProperty, FontWeight.Medium), (TabItem.MinHeightProperty, 36d),
            (TabItem.ForegroundProperty, Dynamic("TextSecondary")), (TabItem.PaddingProperty, new Thickness(12, 8)), (TabItem.MarginProperty, new Thickness(0, 0, 4, 0))));
        window.Styles.Add(Rule(s => s.OfType<TabItem>().Class(":selected"), (TabItem.ForegroundProperty, Dynamic("TextPrimary")), (TabItem.FontWeightProperty, FontWeight.SemiBold)));
        window.Styles.Add(Rule(s => s.OfType<TabItem>().Class(":pointerover"), (TabItem.ForegroundProperty, Dynamic("TextPrimary"))));
        foreach (var state in new[] { ":pointerover", ":pressed", ":selected" })
            window.Styles.Add(Rule(s => s.OfType<TabItem>().Class(state).Template().OfType<Border>().Name("PART_LayoutRoot"),
                (Border.BackgroundProperty, Brushes.Transparent), (TextElement.ForegroundProperty, new TemplateBinding(TabItem.ForegroundProperty))));
        // Fluent sets the pipe from its template and a placement trigger; a :selected trigger outranks both.
        window.Styles.Add(Rule(s => s.OfType<TabItem>().Class(":selected").Template().OfType<Border>().Name("PART_SelectedPipe"),
            (Border.BackgroundProperty, Dynamic("Accent")), (Border.HeightProperty, 2d), (Border.CornerRadiusProperty, new CornerRadius(1)),
            (Border.VerticalAlignmentProperty, VerticalAlignment.Bottom), (Border.MarginProperty, new Thickness(0, 0, 0, -8))));

        // Inputs.
        window.Styles.Add(Rule(s => s.OfType<TextBox>(), (TextBox.BackgroundProperty, Dynamic("BackgroundMuted")), (TextBox.BorderBrushProperty, Dynamic("BorderDefault")),
            (TextBox.ForegroundProperty, Dynamic("TextPrimary")), (TextBox.CornerRadiusProperty, new CornerRadius(RadiusControl)), (TextBox.PaddingProperty, new Thickness(10, 7)),
            (TextBox.FontSizeProperty, 13d), (TextBox.MinHeightProperty, 34d), (TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center)));
        foreach (var state in new[] { ":pointerover", ":focus", ":disabled" })
            window.Styles.Add(Rule(s => s.OfType<TextBox>().Class(state).Template().OfType<Border>().Name("PART_BorderElement"),
                (Border.BackgroundProperty, Dynamic(state == ":disabled" ? "BackgroundCard" : "BackgroundMuted")),
                (Border.BorderBrushProperty, Dynamic(state == ":focus" ? "Accent" : "BorderDefault"))));
        window.Styles.Add(Rule(s => s.OfType<TextBox>().Class(":focus"), (TextBox.ForegroundProperty, Dynamic("TextPrimary"))));
        window.Styles.Add(Rule(s => s.OfType<ComboBox>(), (ComboBox.BackgroundProperty, Dynamic("BackgroundMuted")), (ComboBox.BorderBrushProperty, Dynamic("BorderDefault")),
            (ComboBox.ForegroundProperty, Dynamic("TextPrimary")), (ComboBox.CornerRadiusProperty, new CornerRadius(RadiusControl)), (ComboBox.FontSizeProperty, 13d), (ComboBox.MinHeightProperty, 34d)));
        window.Styles.Add(Rule(s => s.OfType<Expander>(), (Expander.BackgroundProperty, Brushes.Transparent)));

        // Default (secondary) button: calm outline.
        window.Styles.Add(Rule(s => s.OfType<Button>(), (Button.BorderThicknessProperty, new Thickness(1)), (Button.CornerRadiusProperty, new CornerRadius(RadiusControl)),
            (Button.PaddingProperty, new Thickness(12, 0)), (Button.MinHeightProperty, 32d), (Button.FontSizeProperty, 12.5), (Button.FontWeightProperty, FontWeight.Medium),
            (Button.VerticalContentAlignmentProperty, VerticalAlignment.Center), (Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Center)));
        ButtonClass(window, null, background: "BackgroundCard", foreground: "TextPrimary", border: "BorderDefault", hoverBorder: "Accent");
        window.Styles.Add(Rule(s => s.OfType<Button>().Class(":disabled"), (Button.OpacityProperty, 0.5)));

        ButtonClass(window, "primary", background: "Accent", foreground: "OnAccent", border: "Accent", hoverBorder: "AccentText");
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("primary"), (Button.FontWeightProperty, FontWeight.SemiBold)));
        ButtonClass(window, "ai", background: "BackgroundCard", foreground: "Ai", border: "AiBorder", hoverBackground: "AiSoft");
        ButtonClass(window, "aiPrimary", background: "Ai", foreground: "OnAccent", border: "Ai");
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("aiPrimary"), (Button.FontWeightProperty, FontWeight.SemiBold), (Button.MinHeightProperty, 36d)));
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("aiPrimary").Class(":disabled"), (Button.OpacityProperty, 0.45)));
        ButtonClass(window, "danger", background: "ErrorSoft", foreground: "Error", border: "Error");
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("danger"), (Button.FontWeightProperty, FontWeight.SemiBold), (Button.MinHeightProperty, 36d)));
        ButtonClass(window, "ghost", background: null, foreground: "TextSecondary", border: null, hoverForeground: "TextPrimary");
        ButtonClass(window, "link", background: null, foreground: "TextSecondary", border: null, hoverForeground: "Accent");
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("link"), (Button.PaddingProperty, new Thickness(0, 2)), (Button.MinHeightProperty, 0d), (Button.FontSizeProperty, 12d),
            (Button.FontWeightProperty, FontWeight.Normal), (Button.HorizontalAlignmentProperty, HorizontalAlignment.Left)));
        ButtonClass(window, "icon", background: null, foreground: "TextSecondary", border: null, hoverBackground: "BackgroundTrack");
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("icon"), (Button.PaddingProperty, new Thickness(0)), (Button.WidthProperty, 28d), (Button.HeightProperty, 28d),
            (Button.MinHeightProperty, 0d), (Button.CornerRadiusProperty, new CornerRadius(7))));
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("square"), (Button.PaddingProperty, new Thickness(0)), (Button.WidthProperty, 30d), (Button.HeightProperty, 30d), (Button.MinHeightProperty, 0d)));

        // Sidebar navigation entry.
        ButtonClass(window, "nav", background: null, foreground: "TextSecondary", border: null, hoverBackground: "BackgroundCard");
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("nav"), (Button.MinHeightProperty, 38d), (Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
            (Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch), (Button.PaddingProperty, new Thickness(10, 0)), (Button.FontSizeProperty, 13d)));
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("nav").Class("selected"), (Button.BackgroundProperty, Dynamic("BackgroundCard")), (Button.BorderBrushProperty, Dynamic("BorderDefault")),
            (Button.ForegroundProperty, Dynamic("TextPrimary")), (Button.FontWeightProperty, FontWeight.SemiBold)));
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("nav").Class(":disabled"), (Button.ForegroundProperty, Dynamic("TextTertiary")), (Button.OpacityProperty, 1d)));

        // Whole-row buttons inside cards (project, requirement, task, document rows).
        ButtonClass(window, "row", background: null, foreground: "TextPrimary", border: null, hoverBackground: "BackgroundMuted");
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("row"), (Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch), (Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
            (Button.CornerRadiusProperty, new CornerRadius(0)), (Button.PaddingProperty, new Thickness(16, 10)), (Button.FontWeightProperty, FontWeight.Normal), (Button.FontSizeProperty, 13d),
            (Button.BorderThicknessProperty, new Thickness(0))));
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("row").Class("selected"), (Button.BackgroundProperty, Dynamic("BackgroundMuted"))));

        // Segmented control option and filter chip.
        ButtonClass(window, "segment", background: null, foreground: "TextSecondary", border: null);
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("segment"), (Button.MinHeightProperty, 28d), (Button.HeightProperty, 28d), (Button.CornerRadiusProperty, new CornerRadius(7)),
            (Button.FontSizeProperty, 12d), (Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch), (Button.BorderThicknessProperty, new Thickness(0))));
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("segment").Class("selected"), (Button.BackgroundProperty, Dynamic("BackgroundCard")), (Button.ForegroundProperty, Dynamic("TextPrimary"))));
        ButtonClass(window, "chip", background: "BackgroundCard", foreground: "TextSecondary", border: "BorderDefault", hoverBorder: "Accent");
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("chip"), (Button.MinHeightProperty, 28d), (Button.CornerRadiusProperty, new CornerRadius(RadiusPill)), (Button.FontSizeProperty, 12d)));
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("chip").Class("selected"), (Button.BackgroundProperty, Dynamic("AccentSoft")), (Button.ForegroundProperty, Dynamic("AccentText")),
            (Button.BorderBrushProperty, Dynamic("Accent"))));

        // Selectable option cards (assistant starting points, template choice).
        ButtonClass(window, "option", background: "BackgroundCard", foreground: "TextPrimary", border: "BorderDefault", hoverBorder: "AiBorder");
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("option"), (Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch), (Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
            (Button.PaddingProperty, new Thickness(10, 8)), (Button.FontSizeProperty, 12.5)));
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("option").Class("selected"), (Button.BackgroundProperty, Dynamic("AiSoft")), (Button.BorderBrushProperty, Dynamic("Ai"))));
        window.Styles.Add(Rule(s => s.OfType<Button>().Class("option").Class("committed").Class("selected"), (Button.BackgroundProperty, Dynamic("AccentSoft")), (Button.BorderBrushProperty, Dynamic("Accent"))));
    }

    private static ResourceDictionary Palette(bool dark)
    {
        var resources = new ResourceDictionary();
        foreach (var (token, value) in Colors)
        {
            var color = Color.Parse(dark ? value.Dark : value.Light);
            resources[token + "Color"] = color; resources[token + "Brush"] = new SolidColorBrush(color);
        }
        resources["CardShadow"] = BoxShadows.Parse(dark ? CardShadow.Dark : CardShadow.Light);
        resources["RaisedShadow"] = BoxShadows.Parse(dark ? RaisedShadow.Dark : RaisedShadow.Light);
        // Fluent derives checkbox, focus and selection accents from the system accent; use the product accent instead.
        var accent = Color.Parse(dark ? Colors["Accent"].Dark : Colors["Accent"].Light);
        foreach (var key in new[] { "SystemAccentColor", "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3", "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3" })
            resources[key] = accent;
        return resources;
    }

    /// <summary>Fluent paints button states on the template presenter; route those back to the button so class styles own every state.</summary>
    private static void NeutralizeFluentStates(Window window)
    {
        foreach (var state in new[] { ":pointerover", ":pressed", ":disabled" })
            window.Styles.Add(new Style(s => s.OfType<Button>().Class(state).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
            {
                Setters =
                {
                    new Setter(ContentPresenter.BackgroundProperty, new TemplateBinding(Button.BackgroundProperty)),
                    new Setter(ContentPresenter.BorderBrushProperty, new TemplateBinding(Button.BorderBrushProperty)),
                    new Setter(ContentPresenter.ForegroundProperty, new TemplateBinding(Button.ForegroundProperty))
                }
            });
    }

    private static void ButtonClass(Window window, string? name, string? background, string foreground, string? border,
        string? hoverBackground = null, string? hoverBorder = null, string? hoverForeground = null)
    {
        Selector Base(Selector? s) => name is null ? s.OfType<Button>() : s.OfType<Button>().Class(name);
        window.Styles.Add(Rule(Base,
            (Button.BackgroundProperty, background is null ? Brushes.Transparent : Dynamic(background)),
            (Button.ForegroundProperty, Dynamic(foreground)),
            (Button.BorderBrushProperty, border is null ? Brushes.Transparent : Dynamic(border))));
        var hover = new List<(AvaloniaProperty, object)>();
        if (hoverBackground is not null) hover.Add((Button.BackgroundProperty, Dynamic(hoverBackground)));
        if (hoverBorder is not null) hover.Add((Button.BorderBrushProperty, Dynamic(hoverBorder)));
        if (hoverForeground is not null) hover.Add((Button.ForegroundProperty, Dynamic(hoverForeground)));
        if (hover.Count > 0) window.Styles.Add(Rule(s => Base(s).Class(":pointerover"), [.. hover]));
    }

    private static Style Rule(Func<Selector?, Selector> selector, params (AvaloniaProperty Property, object Value)[] values)
    {
        var style = new Style(selector);
        foreach (var (property, value) in values) style.Setters.Add(new Setter(property, value));
        return style;
    }
}
