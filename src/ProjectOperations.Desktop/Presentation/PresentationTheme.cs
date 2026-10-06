using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace ProjectOperations.Desktop;

/// <summary>
/// Semantic design tokens translated from the Figma MVP foundations. Presentation code asks for tokens by
/// name; raw colors, radii, spacing and type sizes live only here.
/// </summary>
internal static class PresentationTheme
{
    public static readonly FontFamily PersianFontFamily = new("avares://ProjectOperations.Desktop/Assets/Fonts/IRANYekanX#IRANYekanX");

    private static readonly IReadOnlyDictionary<string, string> Colors = new Dictionary<string, string>
    {
        ["BackgroundApp"] = "#F5F7F8",
        ["BackgroundSidebar"] = "#EEF2F5",
        ["BackgroundCard"] = "#FFFFFF",
        ["BackgroundMuted"] = "#F8FAFC",
        ["BackgroundTrack"] = "#EDF1F5",
        ["TextPrimary"] = "#183153",
        ["TextSecondary"] = "#61758A",
        ["TextTertiary"] = "#8B9AAF",
        ["BorderDefault"] = "#E3E8EF",
        ["BorderSubtle"] = "#E9EDF2",
        ["BrandPrimary"] = "#2563EB",
        ["BrandSoft"] = "#EAF2FF",
        ["Success"] = "#169B62",
        ["SuccessSoft"] = "#E9F8F1",
        ["Warning"] = "#E89A17",
        ["WarningSoft"] = "#FFF4DE",
        ["Error"] = "#DE4B55",
        ["ErrorSoft"] = "#FDECEF",
        ["Info"] = "#6D5CE7",
        ["InfoSoft"] = "#F0EEFF",
        ["Neutral"] = "#61758A",
        ["NeutralSoft"] = "#EDF1F5",
        ["Accent"] = "#0F8B78",
        ["OnAccent"] = "#FFFFFF"
    };

    public static readonly double[] Spacing = [4, 8, 12, 16, 24, 32, 48];
    public const double RadiusSmall = 8, RadiusMedium = 12, RadiusLarge = 16, RadiusExtraLarge = 20, RadiusPill = 999;

    private static readonly IReadOnlyDictionary<string, (double Size, FontWeight Weight)> Typography = new Dictionary<string, (double, FontWeight)>
    {
        ["Hero"] = (32, FontWeight.Bold),
        ["HeadingLarge"] = (28, FontWeight.Bold),
        ["HeadingMedium"] = (22, FontWeight.SemiBold),
        ["HeadingSmall"] = (15, FontWeight.SemiBold),
        ["Body"] = (14, FontWeight.Normal),
        ["BodySmall"] = (12, FontWeight.Normal),
        ["Label"] = (12, FontWeight.Medium),
        ["Caption"] = (10, FontWeight.Normal)
    };

    public static IBrush Brush(string token) => new SolidColorBrush(Color.Parse(Colors[token]));
    public static CornerRadius Radius(double value) => new(value);
    public static Thickness Inset(double horizontal, double vertical) => new(horizontal, vertical);

    /// <summary>Applies one of the named typography styles to a text block.</summary>
    public static TextBlock Typeset(TextBlock text, string style, string color = "TextPrimary")
    {
        var (size, weight) = Typography[style];
        text.FontSize = size; text.FontWeight = weight; text.Foreground = Brush(color); return text;
    }

    public static string SoftToken(string tone) => tone == "BrandPrimary" ? "BrandSoft" : tone + "Soft";

    public static void Apply(Window window)
    {
        foreach (var color in Colors) window.Resources[color.Key + "Color"] = Color.Parse(color.Value);
        foreach (var color in Colors) window.Resources[color.Key + "Brush"] = Brush(color.Key);
        foreach (var space in Spacing) window.Resources["Space" + space] = space;
        window.Resources["RadiusSmall"] = RadiusSmall; window.Resources["RadiusMedium"] = RadiusMedium;
        window.Resources["RadiusLarge"] = RadiusLarge; window.Resources["RadiusExtraLarge"] = RadiusExtraLarge; window.Resources["RadiusPill"] = RadiusPill;
        foreach (var style in Typography) window.Resources["Type" + style.Key] = style.Value.Size;

        window.Styles.Add(Rule(selector => selector.OfType<CheckBox>(), (CheckBox.ForegroundProperty, Brush("TextPrimary"))));
        window.Styles.Add(Rule(selector => selector.OfType<TabControl>(), (TabControl.BackgroundProperty, Brushes.Transparent), (TabControl.PaddingProperty, new Thickness(0, 16, 0, 0))));
        window.Styles.Add(Rule(selector => selector.OfType<TabItem>(), (TabItem.FontSizeProperty, 12d), (TabItem.FontWeightProperty, FontWeight.Medium),
            (TabItem.ForegroundProperty, Brush("TextSecondary")), (TabItem.PaddingProperty, new Thickness(12, 8))));
        window.Styles.Add(Rule(selector => selector.OfType<TabItem>().Class(":selected"), (TabItem.ForegroundProperty, Brush("BrandPrimary")), (TabItem.FontWeightProperty, FontWeight.SemiBold)));
        window.Styles.Add(Rule(selector => selector.OfType<TextBox>(), (TextBox.BackgroundProperty, Brush("BackgroundMuted")), (TextBox.BorderBrushProperty, Brush("BorderDefault")),
            (TextBox.CornerRadiusProperty, Radius(RadiusMedium)), (TextBox.PaddingProperty, new Thickness(12, 8)), (TextBox.FontSizeProperty, 12d)));
        window.Styles.Add(new Style(selector => selector.OfType<TextBox>().Class(":disabled").Template().OfType<Border>().Name("PART_BorderElement"))
        {
            Setters = { new Setter(Border.BackgroundProperty, Brush("BackgroundCard")), new Setter(Border.BorderBrushProperty, Brush("BorderDefault")) }
        });
        window.Styles.Add(Rule(selector => selector.OfType<ComboBox>(), (ComboBox.BackgroundProperty, Brush("BackgroundCard")), (ComboBox.BorderBrushProperty, Brush("BorderDefault")),
            (ComboBox.CornerRadiusProperty, Radius(RadiusMedium)), (ComboBox.FontSizeProperty, 12d)));
        window.Styles.Add(Rule(selector => selector.OfType<Expander>(), (Expander.BackgroundProperty, Brushes.Transparent)));

        // Secondary (default) action: calm outlined button.
        window.Styles.Add(Rule(selector => selector.Is<Button>(), (Button.BackgroundProperty, Brush("BackgroundCard")), (Button.ForegroundProperty, Brush("TextPrimary")),
            (Button.BorderBrushProperty, Brush("BorderDefault")), (Button.BorderThicknessProperty, new Thickness(1)), (Button.CornerRadiusProperty, Radius(RadiusSmall)),
            (Button.PaddingProperty, new Thickness(16, 8)), (Button.FontSizeProperty, 12d), (Button.FontWeightProperty, FontWeight.Medium)));
        window.Styles.Add(PresenterRule(selector => selector.Is<Button>().Class(":pointerover"), (ContentPresenter.BackgroundProperty, Brush("BackgroundMuted")),
            (ContentPresenter.BorderBrushProperty, Brush("BrandPrimary"))));
        window.Styles.Add(PresenterRule(selector => selector.Is<Button>().Class(":focus-visible"), (ContentPresenter.BorderBrushProperty, Brush("BrandPrimary"))));

        // Primary action.
        window.Styles.Add(Rule(selector => selector.Is<Button>().Class("primary"), (Button.BackgroundProperty, Brush("BrandPrimary")), (Button.ForegroundProperty, Brush("BackgroundCard")),
            (Button.BorderBrushProperty, Brush("BrandPrimary")), (Button.FontWeightProperty, FontWeight.SemiBold)));
        window.Styles.Add(PresenterRule(selector => selector.Is<Button>().Class("primary").Class(":pointerover"), (ContentPresenter.BackgroundProperty, Brush("BrandPrimary")),
            (ContentPresenter.BorderBrushProperty, Brush("TextPrimary")), (ContentPresenter.ForegroundProperty, Brush("BackgroundCard"))));
        window.Styles.Add(PresenterRule(selector => selector.Is<Button>().Class("primary").Class(":disabled"), (ContentPresenter.BackgroundProperty, Brush("BackgroundTrack")),
            (ContentPresenter.ForegroundProperty, Brush("TextTertiary")), (ContentPresenter.BorderBrushProperty, Brush("BorderDefault"))));

        // Destructive/stop action stays visibly distinct.
        window.Styles.Add(Rule(selector => selector.Is<Button>().Class("danger"), (Button.BackgroundProperty, Brush("ErrorSoft")), (Button.ForegroundProperty, Brush("Error")),
            (Button.BorderBrushProperty, Brush("Error")), (Button.FontWeightProperty, FontWeight.SemiBold)));
        window.Styles.Add(PresenterRule(selector => selector.Is<Button>().Class("danger").Class(":pointerover"), (ContentPresenter.BackgroundProperty, Brush("ErrorSoft")),
            (ContentPresenter.BorderBrushProperty, Brush("Error")), (ContentPresenter.ForegroundProperty, Brush("Error"))));

        window.Styles.Add(Rule(selector => selector.Is<Button>().Class("link"), (Button.BackgroundProperty, Brushes.Transparent), (Button.BorderThicknessProperty, new Thickness(0)),
            (Button.ForegroundProperty, Brush("BrandPrimary")), (Button.PaddingProperty, new Thickness(8, 4))));
        window.Styles.Add(PresenterRule(selector => selector.Is<Button>().Class("link").Class(":pointerover"), (ContentPresenter.BackgroundProperty, Brush("BrandSoft")),
            (ContentPresenter.ForegroundProperty, Brush("BrandPrimary")), (ContentPresenter.BorderBrushProperty, Brushes.Transparent)));

        // Sidebar navigation entry.
        window.Styles.Add(Rule(selector => selector.Is<Button>().Class("nav"), (Button.BackgroundProperty, Brushes.Transparent), (Button.BorderThicknessProperty, new Thickness(0)),
            (Button.ForegroundProperty, Brush("TextSecondary")), (Button.CornerRadiusProperty, Radius(10)), (Button.MinHeightProperty, 40d),
            (Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch), (Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
            (Button.VerticalContentAlignmentProperty, VerticalAlignment.Center), (Button.PaddingProperty, new Thickness(12, 0))));
        window.Styles.Add(PresenterRule(selector => selector.Is<Button>().Class("nav").Class(":pointerover"), (ContentPresenter.BackgroundProperty, Brush("BackgroundCard")),
            (ContentPresenter.BorderBrushProperty, Brushes.Transparent)));
        window.Styles.Add(Rule(selector => selector.Is<Button>().Class("nav").Class("selected"), (Button.BackgroundProperty, Brush("BrandSoft")),
            (Button.ForegroundProperty, Brush("TextPrimary")), (Button.FontWeightProperty, FontWeight.SemiBold)));
        window.Styles.Add(PresenterRule(selector => selector.Is<Button>().Class("nav").Class("selected"), (ContentPresenter.BackgroundProperty, Brush("BrandSoft")),
            (ContentPresenter.BorderBrushProperty, Brushes.Transparent)));
        window.Styles.Add(PresenterRule(selector => selector.Is<Button>().Class("nav").Class(":disabled"), (ContentPresenter.BackgroundProperty, Brushes.Transparent),
            (ContentPresenter.ForegroundProperty, Brush("TextTertiary")), (ContentPresenter.BorderBrushProperty, Brushes.Transparent)));

        // Whole-row buttons (project, requirement and task rows).
        window.Styles.Add(Rule(selector => selector.Is<Button>().Class("row"), (Button.BackgroundProperty, Brushes.Transparent), (Button.BorderThicknessProperty, new Thickness(0)),
            (Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch), (Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
            (Button.CornerRadiusProperty, Radius(RadiusSmall)), (Button.PaddingProperty, new Thickness(8, 10))));
        window.Styles.Add(PresenterRule(selector => selector.Is<Button>().Class("row").Class(":pointerover"), (ContentPresenter.BackgroundProperty, Brush("BackgroundMuted")),
            (ContentPresenter.BorderBrushProperty, Brushes.Transparent)));

        // AI quick action.
        window.Styles.Add(Rule(selector => selector.Is<Button>().Class("aiAction"), (Button.BackgroundProperty, Brush("BackgroundCard")), (Button.CornerRadiusProperty, Radius(10)), (Button.BorderBrushProperty, Brush("BorderDefault")), (Button.BorderThicknessProperty, new Thickness(1)),
            (Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Left), (Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch), (Button.PaddingProperty, new Thickness(12, 10))));
    }

    private static Style Rule(Func<Selector?, Selector> selector, params (AvaloniaProperty Property, object Value)[] values)
    {
        var style = new Style(selector);
        foreach (var (property, value) in values) style.Setters.Add(new Setter(property, value));
        return style;
    }

    /// <summary>Fluent draws button state on the template presenter, so state colors must target it.</summary>
    private static Style PresenterRule(Func<Selector?, Selector> selector, params (AvaloniaProperty Property, object Value)[] values)
    {
        var style = new Style(parent => selector(parent).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
        foreach (var (property, value) in values) style.Setters.Add(new Setter(property, value));
        return style;
    }
}
