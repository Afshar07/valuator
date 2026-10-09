using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace ProjectOperations.Desktop.Common;

/// <summary>Value converters for the window itself: the font of the language, the theme variant of the stored preference.</summary>
internal static class PresentationConverters
{
    /// <summary>True for Persian: the Persian UI font, otherwise the Latin one.</summary>
    public static readonly IValueConverter UiFont = new FuncValueConverter<bool, FontFamily?>(persian => persian ? PresentationTheme.PersianFontFamily : PresentationTheme.LatinFontFamily);

    /// <summary>True when Persian is current: the font to write the other language's name in (the Latin one), otherwise Persian.</summary>
    public static readonly IValueConverter OtherLanguageFont = new FuncValueConverter<bool, FontFamily?>(persian => persian ? PresentationTheme.LatinFontFamily : PresentationTheme.PersianFontFamily);

    /// <summary>"light", "dark" or "auto" to the theme variant (auto follows the operating system).</summary>
    public static readonly IValueConverter Theme = new FuncValueConverter<string?, ThemeVariant?>(preference => PresentationTheme.Variant(preference ?? "light"));
}
