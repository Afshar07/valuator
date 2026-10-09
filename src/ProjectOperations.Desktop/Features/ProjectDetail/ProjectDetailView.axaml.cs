using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ProjectOperations.Desktop.Features.ProjectDetail;

public partial class ProjectDetailView : UserControl
{
    public ProjectDetailView()
    {
        InitializeComponent();
        // Long names end in an ellipsis instead of wrapping; the pills and edit button stay beside the title.
        Identity.SizeChanged += (_, e) =>
        {
            var others = TitleLine.Children.Where(child => child != ProjectTitle && child.IsVisible).ToList();
            ProjectTitle.MaxWidth = Math.Max(80, e.NewSize.Width - others.Sum(child => child.DesiredSize.Width) - TitleLine.Spacing * others.Count);
        };
        HeaderRow.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 680;
            Grid.SetColumn(HeaderEnd, narrow ? 0 : 1); Grid.SetRow(HeaderEnd, narrow ? 1 : 0);
            HeaderEnd.Margin = new Thickness(0, narrow ? 12 : 0, 0, 0); HeaderEnd.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        };
    }
}
