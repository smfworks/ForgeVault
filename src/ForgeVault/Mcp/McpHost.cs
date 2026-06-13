using ForgeVault.Core;
using ForgeVault.Search;
using System.IO;

namespace ForgeVault.Mcp;

/// <summary>
/// Handles launching ForgeVault as an MCP server from the command line.
/// Usage: ForgeVault.exe --mcp "C:\path\to\vault"
/// </summary>
public static class McpHost
{
    public static async Task RunAsync(string vaultPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(vaultPath))
        {
            await Console.Error.WriteLineAsync($"Vault path not found: {vaultPath}");
            Environment.ExitCode = 1;
            return;
        }

        var engine = new VaultEngine(vaultPath);
        var searchDbPath = Path.Combine(vaultPath, ".forgevault", "mcp-search.db");
        Directory.CreateDirectory(Path.GetDirectoryName(searchDbPath)!);
        var searchIndex = new SearchIndex(searchDbPath);
        var notes = new Dictionary<string, NoteModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in engine.EnumerateMarkdownFiles())
        {
            var note = LoadNote(file);
            notes[file] = note;
            searchIndex.IndexDocument(file, note.Title, note.Content);
        }

        using var server = new McpServer(engine, searchIndex, notes);
        await server.RunAsync(cancellationToken);
    }

    private static NoteModel LoadNote(string filePath)
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
            OutgoingLinks = MainWindow.ExtractWikiLinks(content),
            LastModifiedUtc = File.GetLastWriteTimeUtc(filePath)
        };
    }
}
