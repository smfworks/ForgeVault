using ForgeVault.AI;
using ForgeVault.Core;
using ForgeVault.Graph;
using ForgeVault.Search;
using Markdig;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ForgeVault;

public partial class MainWindow : Window
{
    private VaultEngine? _vaultEngine;
    private SearchIndex? _searchIndex;
    private OllamaClient? _ollamaClient;
    private NoteModel? _currentNote;
    private readonly Dictionary<string, NoteModel> _notes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<TreeViewItem> _treeItems = new();
    private bool _isDirty;
    private readonly string _settingsPath;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        VaultTree.ItemsSource = _treeItems;
        _ollamaClient = new OllamaClient();
        _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ForgeVault", "settings.json");

        UpdateToolbarState();
        Loaded += MainWindow_Loaded;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateToolbarState();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S:
                    e.Handled = true;
                    SaveCurrentNote();
                    break;
                case Key.N:
                    e.Handled = true;
                    CreateNewNote();
                    break;
                case Key.F:
                    e.Handled = true;
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                    break;
            }
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var lastVault = LoadSetting("lastVault");
        if (!string.IsNullOrEmpty(lastVault) && Directory.Exists(lastVault))
        {
            OpenVault(lastVault);
        }
    }

    private void OpenVaultButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select Vault Folder" };
        if (dialog.ShowDialog() == true)
        {
            OpenVault(dialog.FolderName);
            SaveSetting("lastVault", dialog.FolderName);
        }
    }

    private void OpenVault(string path)
    {
        _vaultEngine = new VaultEngine(path);
        _notes.Clear();
        _treeItems.Clear();
        Editor.Text = string.Empty;
        PreviewBrowser.NavigateToString("<html><body></body></html>");
        _currentNote = null;

        var searchDbPath = Path.Combine(path, ".forgevault", "search.db");
        Directory.CreateDirectory(Path.GetDirectoryName(searchDbPath)!);
        _searchIndex?.Dispose();
        _searchIndex = new SearchIndex(searchDbPath);

        var rootItem = new TreeViewItem { Header = Path.GetFileName(path), Tag = path, IsExpanded = true };
        _treeItems.Add(rootItem);

        foreach (var file in _vaultEngine.EnumerateMarkdownFiles())
        {
            var note = ParseNote(file);
            _notes[file] = note;
            _searchIndex.IndexDocument(file, note.Title, note.Content);
            AddFileToTree(rootItem, file, path);
        }

        ResolveBacklinks();
        Title = $"ForgeVault - {path}";
        StatusText.Text = $"Vault: {path} — {_notes.Count} notes";
        Editor.IsEnabled = true;
        UpdateToolbarState();
    }

    private NoteModel ParseNote(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var title = Path.GetFileNameWithoutExtension(filePath);
        var frontmatter = new Dictionary<string, string>();

        if (content.StartsWith("---"))
        {
            var end = content.IndexOf("---", 3);
            if (end > 0)
            {
                var fm = content[3..end];
                foreach (var line in fm.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                {
                    var colon = line.IndexOf(':');
                    if (colon > 0)
                        frontmatter[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                }
                content = content[(end + 3)..].TrimStart();
            }
        }

        if (frontmatter.TryGetValue("title", out var fmTitle))
            title = fmTitle;

        return new NoteModel
        {
            FilePath = filePath,
            Title = title,
            Content = content,
            Frontmatter = frontmatter,
            OutgoingLinks = ExtractWikiLinks(content),
            LastModifiedUtc = File.GetLastWriteTimeUtc(filePath)
        };
    }

    public static IReadOnlyList<string> ExtractWikiLinks(string content)
    {
        var matches = Regex.Matches(content, @"\[\[([^\]]+)\]\]");
        return matches.Select(m => m.Groups[1].Value).ToList();
    }

    private void ResolveBacklinks()
    {
        foreach (var note in _notes.Values)
        {
            var backlinks = new List<string>();
            foreach (var other in _notes.Values)
            {
                if (string.Equals(other.FilePath, note.FilePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (other.OutgoingLinks.Any(link =>
                    link.Equals(note.Title, StringComparison.OrdinalIgnoreCase) ||
                    link.Equals(Path.GetFileNameWithoutExtension(note.FilePath), StringComparison.OrdinalIgnoreCase)))
                {
                    backlinks.Add(other.Title);
                }
            }
            note.Backlinks = backlinks;
        }
    }

    private static void AddFileToTree(TreeViewItem root, string filePath, string vaultPath)
    {
        var relative = Path.GetRelativePath(vaultPath, filePath);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = root;

        for (int i = 0; i < parts.Length; i++)
        {
            var isFile = i == parts.Length - 1;
            var header = isFile ? Path.GetFileNameWithoutExtension(parts[i]) : parts[i];

            var existing = current.Items.Cast<TreeViewItem>().FirstOrDefault(x => x.Header?.ToString() == header);
            if (existing == null)
            {
                existing = new TreeViewItem { Header = header, Tag = isFile ? filePath : null };
                current.Items.Add(existing);
            }
            current = existing;
        }
    }

    private void VaultTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem { Tag: string path } && File.Exists(path))
        {
            LoadNote(path);
        }
    }

    private void LoadNote(string path)
    {
        if (_isDirty)
        {
            var result = MessageBox.Show("Save changes?", "ForgeVault", MessageBoxButton.YesNoCancel);
            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes) SaveCurrentNote();
        }

        if (_notes.TryGetValue(path, out var note))
        {
            _currentNote = note;
            Editor.Text = note.Content;
            _isDirty = false;
            RenderPreview();
            StatusText.Text = $"Editing: {note.Title}";
            UpdateToolbarState();
        }
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        _isDirty = true;
        RenderPreview();
        UpdateToolbarState();
    }

    private void RenderPreview()
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var html = Markdown.ToHtml(Editor.Text ?? string.Empty, pipeline);
        var fullHtml = $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; line-height: 1.6; padding: 20px; background: #1E1E1E; color: #D4D4D4; }}
a {{ color: #4EC9B0; text-decoration: none; }}
a:hover {{ text-decoration: underline; }}
code {{ background: #333; padding: 2px 4px; border-radius: 3px; font-family: Consolas, monospace; }}
pre {{ background: #333; padding: 10px; border-radius: 5px; overflow-x: auto; }}
pre code {{ background: transparent; padding: 0; }}
blockquote {{ border-left: 4px solid #007ACC; margin-left: 0; padding-left: 16px; color: #AAA; }}
table {{ border-collapse: collapse; width: 100%; }}
th, td {{ border: 1px solid #444; padding: 6px; }}
th {{ background: #2A2A2A; }}
img {{ max-width: 100%; }}
</style>
</head>
<body>{html}</body>
</html>";
        PreviewBrowser.NavigateToString(fullHtml);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentNote();
    }

    private void SaveCurrentNote()
    {
        if (_currentNote == null) return;
        File.WriteAllText(_currentNote.FilePath, Editor.Text);
        _currentNote.Content = Editor.Text;
        _currentNote.OutgoingLinks = ExtractWikiLinks(Editor.Text);
        _currentNote.LastModifiedUtc = File.GetLastWriteTimeUtc(_currentNote.FilePath);
        _searchIndex?.IndexDocument(_currentNote.FilePath, _currentNote.Title, _currentNote.Content);
        ResolveBacklinks();
        _isDirty = false;
        StatusText.Text = $"Saved: {_currentNote.Title}";
        UpdateToolbarState();
    }

    private void NewNoteButton_Click(object sender, RoutedEventArgs e)
    {
        CreateNewNote();
    }

    private void CreateNewNote()
    {
        if (_vaultEngine == null)
        {
            MessageBox.Show("Open a vault first.");
            return;
        }

        var dialog = new InputDialog("New Note", "Enter note title:") { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResponseText)) return;

        CreateNoteFile(dialog.ResponseText);
    }

    private void DailyNoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vaultEngine == null)
        {
            MessageBox.Show("Open a vault first.");
            return;
        }

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var title = $"Daily Note {date}";
        var fileName = $"Daily/{date}.md";
        var filePath = Path.Combine(_vaultEngine.VaultPath, fileName);

        if (!File.Exists(filePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, $"---\ntitle: {title}\ndate: {date}\n---\n\n# {title}\n\n");
        }

        var note = ParseNote(filePath);
        _notes[filePath] = note;
        _searchIndex?.IndexDocument(filePath, note.Title, note.Content);
        RefreshTree();
        LoadNote(filePath);
    }

    private void CreateNoteFile(string title)
    {
        if (_vaultEngine == null) return;

        var safeName = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        var filePath = Path.Combine(_vaultEngine.VaultPath, $"{safeName}.md");
        File.WriteAllText(filePath, $"---\ntitle: {title}\n---\n\n");
        var note = ParseNote(filePath);
        _notes[filePath] = note;
        _searchIndex?.IndexDocument(filePath, note.Title, note.Content);
        RefreshTree();
        LoadNote(filePath);
    }

    private void RefreshTree()
    {
        if (_vaultEngine == null || _treeItems.Count == 0) return;
        _treeItems[0].Items.Clear();
        foreach (var file in _notes.Keys.OrderBy(x => x))
            AddFileToTree(_treeItems[0], file, _vaultEngine.VaultPath);
    }

    private void GraphButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vaultEngine == null)
        {
            MessageBox.Show("Open a vault first.");
            return;
        }

        var graphWindow = new GraphWindow(_notes.Values.ToList()) { Owner = this };
        graphWindow.Show();
    }

    private void AiChatButton_Click(object sender, RoutedEventArgs e)
    {
        var chatWindow = new AiChatWindow(_ollamaClient!, _notes.Values.ToList()) { Owner = this };
        chatWindow.Show();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        Search();
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Search();
    }

    private void Search()
    {
        if (_searchIndex == null || string.IsNullOrWhiteSpace(SearchBox.Text))
            return;

        var results = _searchIndex.Search(SearchBox.Text).ToList();
        var resultWindow = new SearchResultsWindow(results, LoadNote) { Owner = this };
        resultWindow.Show();
    }

    private void UpdateToolbarState()
    {
        SaveButton.IsEnabled = _isDirty && _currentNote != null;
        NewNoteButton.IsEnabled = _vaultEngine != null;
        DailyNoteButton.IsEnabled = _vaultEngine != null;
        SearchButton.IsEnabled = _searchIndex != null && !string.IsNullOrWhiteSpace(SearchBox.Text);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isDirty)
        {
            var result = MessageBox.Show("Save changes before closing?", "ForgeVault", MessageBoxButton.YesNoCancel);
            if (result == MessageBoxResult.Cancel) e.Cancel = true;
            else if (result == MessageBoxResult.Yes) SaveCurrentNote();
        }
        _searchIndex?.Dispose();
        _ollamaClient?.Dispose();
    }

    private string? LoadSetting(string key)
    {
        try
        {
            if (!File.Exists(_settingsPath)) return null;
            var json = File.ReadAllText(_settingsPath);
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return dict != null && dict.TryGetValue(key, out var value) ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private void SaveSetting(string key, string value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var dict = new Dictionary<string, string>();
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? dict;
            }
            dict[key] = value;
            File.WriteAllText(_settingsPath, System.Text.Json.JsonSerializer.Serialize(dict));
        }
        catch
        {
            // Settings are best-effort
        }
    }
}
