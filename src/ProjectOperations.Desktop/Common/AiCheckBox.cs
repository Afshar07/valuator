using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// A check box whose checked state uses the assistant colour, so selecting a proposal (or consenting to a run) never reads as committed data.
/// The colour comes from the theme token for each variant; Fluent reads the box fill from these resources.
/// </summary>
internal sealed class AiCheckBox : CheckBox
{
    private static readonly string[] Keys =
    [
        "CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckBackgroundFillCheckedPointerOver", "CheckBoxCheckBackgroundFillCheckedPressed",
        "CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "CheckBoxCheckBackgroundStrokeCheckedPressed"
    ];

    public AiCheckBox()
    {
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top;
        foreach (var (variant, dark) in new[] { (ThemeVariant.Light, false), (ThemeVariant.Dark, true) })
        {
            var color = PresentationTheme.TokenColor("Ai", dark);
            var resources = new ResourceDictionary();
            foreach (var key in Keys) resources[key] = new SolidColorBrush(color);
            Resources.ThemeDictionaries[variant] = resources;
        }
    }

    protected override Type StyleKeyOverride => typeof(CheckBox);
}
