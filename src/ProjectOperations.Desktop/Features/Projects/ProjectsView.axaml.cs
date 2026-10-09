using Avalonia.Controls;
using ProjectOperations.Desktop.Common;

namespace ProjectOperations.Desktop.Features.Projects;

public partial class ProjectsView : UserControl
{
    /// <summary>Below this width the table drops the stage and owner columns.</summary>
    private const double CompactBelow = 640;

    public ProjectsView()
    {
        InitializeComponent();
        ProjectsCard.SizeChanged += (_, e) => TableGrid.SetIsCompact(this, e.NewSize.Width < CompactBelow);
    }
}
