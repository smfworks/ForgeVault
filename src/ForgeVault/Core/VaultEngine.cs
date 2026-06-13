using System.IO;

namespace ForgeVault.Core;

/// <summary>
/// Scans a local folder vault, indexes Markdown files, and provides access
/// to note metadata, links, and content.
/// </summary>
public sealed class VaultEngine
{
    public string VaultPath { get; }

    public VaultEngine(string vaultPath)
    {
        VaultPath = vaultPath ?? throw new ArgumentNullException(nameof(vaultPath));
    }

    public IEnumerable<string> EnumerateMarkdownFiles()
    {
        if (!Directory.Exists(VaultPath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(VaultPath, "*.md", SearchOption.AllDirectories))
        {
            yield return file;
        }
    }
}
