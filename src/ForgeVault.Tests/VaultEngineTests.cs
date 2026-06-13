using ForgeVault.Core;
using ForgeVault.Search;
using Xunit;

namespace ForgeVault.Tests;

public class VaultEngineTests : IDisposable
{
    private readonly string _testDir;

    public VaultEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"forgevault-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void VaultEngine_EnumeratesMarkdownFiles()
    {
        File.WriteAllText(Path.Combine(_testDir, "a.md"), "# A");
        File.WriteAllText(Path.Combine(_testDir, "b.md"), "# B");
        File.WriteAllText(Path.Combine(_testDir, "ignore.txt"), "not markdown");

        var engine = new VaultEngine(_testDir);
        var files = engine.EnumerateMarkdownFiles().ToList();

        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void SearchIndex_IndexesAndSearches()
    {
        var dbPath = Path.Combine(_testDir, "search.db");
        using var index = new SearchIndex(dbPath);
        index.IndexDocument(Path.Combine(_testDir, "note.md"), "My Note", "ForgeVault is a local-first knowledge vault.");

        var results = index.Search("ForgeVault").ToList();
        Assert.Single(results);
        Assert.Equal("My Note", results[0].Title);
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
            // Best-effort cleanup of locked SQLite temp files
        }
    }
}
