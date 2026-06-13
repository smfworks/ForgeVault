using System.Windows;

namespace ForgeVault;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenVaultButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Open vault clicked (not yet implemented)";
    }

    private void NewNoteButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "New note clicked (not yet implemented)";
    }

    private void GraphButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Graph clicked (not yet implemented)";
    }

    private void AiChatButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "AI chat clicked (not yet implemented)";
    }
}
