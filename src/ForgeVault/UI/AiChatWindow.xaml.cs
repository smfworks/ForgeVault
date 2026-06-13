using ForgeVault.AI;
using ForgeVault.Core;
using System.Text;
using System.Windows;

namespace ForgeVault;

public partial class AiChatWindow : Window
{
    private readonly OllamaClient _client;
    private readonly List<NoteModel> _notes;

    public AiChatWindow(OllamaClient client, List<NoteModel> notes)
    {
        InitializeComponent();
        _client = client;
        _notes = notes;
    }

    private void ChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            Send();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        Send();
    }

    private async void Send()
    {
        var prompt = ChatInput.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        ChatInput.Text = string.Empty;
        AppendLine($"You: {prompt}");

        var fullPrompt = BuildPrompt(prompt);
        Append("AI: ");

        var stream = await _client.GenerateStreamAsync(ModelBox.Text, fullPrompt);
        if (stream != null)
        {
            await foreach (var fragment in stream)
            {
                Append(fragment);
            }
        }

        AppendLine("");
        ChatScroll.ScrollToBottom();
    }

    private string BuildPrompt(string userPrompt)
    {
        if (UseVaultContext.IsChecked != true || _notes.Count == 0)
            return userPrompt;

        var context = new StringBuilder();
        context.AppendLine("You are a helpful assistant with access to the user's local knowledge vault. Use the notes below to answer if relevant.");
        context.AppendLine("Vault notes:");

        foreach (var note in _notes.Take(20))
        {
            var snippet = note.Content.Length > 500 ? note.Content[..500] + "..." : note.Content;
            context.AppendLine($"- {note.Title}: {snippet.Replace('\n', ' ')}");
        }

        context.AppendLine();
        context.AppendLine($"User question: {userPrompt}");
        return context.ToString();
    }

    private void Append(string text)
    {
        ChatHistory.Text += text;
        ChatScroll.ScrollToBottom();
    }

    private void AppendLine(string text)
    {
        ChatHistory.Text += text + "\n";
        ChatScroll.ScrollToBottom();
    }
}
