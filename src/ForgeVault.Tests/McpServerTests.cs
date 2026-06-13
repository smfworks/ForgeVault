using ForgeVault.Core;
using ForgeVault.Mcp;
using ForgeVault.Search;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ForgeVault.Tests;

public class McpServerTests : IDisposable
{
    private readonly string _testDir;

    public McpServerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"forgevault-mcp-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public async Task McpServer_ListsTools_And_CallsSearch()
    {
        var vaultFile = Path.Combine(_testDir, "hello.md");
        File.WriteAllText(vaultFile, "---\ntitle: Hello\n---\n\nForgeVault local-first knowledge vault.");

        var engine = new VaultEngine(_testDir);
        var dbPath = Path.Combine(_testDir, ".forgevault", "mcp-search.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var searchIndex = new SearchIndex(dbPath);
        var notes = new Dictionary<string, NoteModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in engine.EnumerateMarkdownFiles())
        {
            var note = LoadNote(file);
            notes[file] = note;
            searchIndex.IndexDocument(file, note.Title, note.Content);
        }

        var input = new StringBuilder();
        input.AppendLine(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { } }));
        input.AppendLine(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 2, method = "tools/list" }));
        input.AppendLine(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new { name = "forgevault_search", arguments = new { query = "ForgeVault" } }
        }));

        var inputReader = new StringReader(input.ToString());
        var outputWriter = new StringWriter();

        using var server = new McpServer(engine, searchIndex, notes, inputReader, outputWriter);
        await server.RunAsync(CancellationToken.None);

        var output = outputWriter.ToString();
        var lines = output.Split(['\n'], StringSplitOptions.RemoveEmptyEntries).ToList();
        Assert.True(lines.Count >= 2, $"Expected at least 2 response lines, got {lines.Count}. Output: {output}");
        Assert.Contains("forgevault_search", output);
        Assert.Contains("Hello", output);
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

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch (IOException)
        {
        }
    }
}
