using ForgeVault.Core;
using ForgeVault.Graph;
using System.Windows;

namespace ForgeVault;

public partial class GraphWindow : Window
{
    public GraphWindow(List<NoteModel> notes)
    {
        InitializeComponent();
        Loaded += (s, e) => Render(notes);
    }

    private void Render(List<NoteModel> notes)
    {
        var renderer = new GraphRenderer();
        var bitmap = renderer.RenderGraph(notes, (int)ActualWidth, (int)ActualHeight);
        GraphImage.Source = bitmap;
    }
}
