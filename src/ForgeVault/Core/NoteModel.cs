namespace ForgeVault.Core;

public sealed class NoteModel
{
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Frontmatter { get; set; } = new Dictionary<string, string>();
    public IReadOnlyList<string> OutgoingLinks { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Backlinks { get; set; } = Array.Empty<string>();
    public DateTime LastModifiedUtc { get; set; }
}
