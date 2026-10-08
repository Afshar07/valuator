using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ProjectOperations.Desktop;

internal enum IconWeight { Regular, Bold, Fill }

/// <summary>Phosphor icon glyphs (MIT, bundled under Assets/Fonts/Phosphor). Codepoints are shared by every weight.</summary>
internal static class Icons
{
    private const string Fonts = "avares://ProjectOperations.Desktop/Assets/Fonts/Phosphor";
    private static readonly FontFamily Regular = new(Fonts + "#Phosphor");
    private static readonly FontFamily Bold = new(Fonts + "#Phosphor-Bold");
    private static readonly FontFamily Fill = new(Fonts + "#Phosphor-Fill");

    public const string ArrowBendUpRight = "", ArrowLeft = "", ArrowRight = "", ArrowSquareOut = "";
    public const string BellSimple = "", Briefcase = "", CalendarBlank = "", CaretLeft = "", CaretRight = "";
    public const string Check = "", CheckCircle = "", Circle = "", CircleHalf = "", CircleDashed = "", CircleNotch = "";
    public const string Clock = "", CloudArrowUp = "", DotOutline = "", Eye = "";
    public const string File = "", FileDashed = "", FileDoc = "", FileImage = "", FilePdf = "", FileText = "", FileXls = "", FileZip = "", Files = "";
    public const string Flag = "", FlagCheckered = "", FolderOpen = "", Folders = "", GearSix = "", HardDrives = "", HourglassMedium = "";
    public const string Info = "", LinkBreak = "", LinkSimple = "", LockKey = "", MagnifyingGlass = "";
    public const string Play = "", Plus = "", Plugs = "", Prohibit = "", Question = "", RadioButton = "";
    public const string Scales = "", Scan = "", Sparkle = "", Stop = "", Translate = "";
    public const string Warning = "", WarningCircle = "", X = "", XCircle = "";

    public const string CaretDown = "", CaretUp = "", CheckSquareOffset = "", Flask = "", Lightbulb = "", ListChecks = "", PencilSimple = "", Trash = "";

    /// <summary>Icon text block; icons are decorative, so they never carry accessible names or flow-dependent glyphs.</summary>
    public static TextBlock Glyph(string glyph, double size = 16, string color = "TextSecondary", IconWeight weight = IconWeight.Regular)
    {
        var text = new TextBlock
        {
            Text = glyph,
            FontSize = size,
            FontFamily = weight switch { IconWeight.Bold => Bold, IconWeight.Fill => Fill, _ => Regular },
            FontWeight = FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            LineHeight = size,
            FlowDirection = FlowDirection.LeftToRight
        };
        text.Paint(TextBlock.ForegroundProperty, color);
        return text;
    }

    /// <summary>File-type glyph from a referenced path's extension (no file content is read).</summary>
    public static string ForFile(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => FilePdf,
        ".xls" or ".xlsx" or ".csv" or ".numbers" => FileXls,
        ".zip" or ".rar" or ".7z" => FileZip,
        ".doc" or ".docx" or ".rtf" or ".odt" => FileDoc,
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" => FileImage,
        ".txt" or ".md" => FileText,
        _ => File
    };
}
