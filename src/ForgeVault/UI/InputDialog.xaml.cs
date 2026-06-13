using System.Windows;

namespace ForgeVault;

public partial class InputDialog : Window
{
    public string ResponseText { get; private set; } = string.Empty;

    public InputDialog(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        ResponseText = ResponseTextBox.Text;
        DialogResult = true;
        Close();
    }
}
