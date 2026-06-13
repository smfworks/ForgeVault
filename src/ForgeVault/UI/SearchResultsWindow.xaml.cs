using ForgeVault.Search;
using System.Windows;

namespace ForgeVault;

public partial class SearchResultsWindow : Window
{
    private readonly Action<string> _onOpen;

    public SearchResultsWindow(List<SearchResult> results, Action<string> onOpen)
    {
        InitializeComponent();
        _onOpen = onOpen;
        ResultsList.ItemsSource = results;
    }

    private void ResultsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is SearchResult result)
        {
            _onOpen(result.FilePath);
            Close();
        }
    }
}
