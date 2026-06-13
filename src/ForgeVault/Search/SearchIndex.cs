using Microsoft.Data.Sqlite;

namespace ForgeVault.Search;

/// <summary>
/// SQLite FTS5 full-text search index over the vault.
/// </summary>
public sealed class SearchIndex : IDisposable
{
    private readonly SqliteConnection _connection;

    public SearchIndex(string indexPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = indexPath
        };
        _connection = new SqliteConnection(builder.ConnectionString);
        _connection.Open();
        Initialize();
    }

    private void Initialize()
    {
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = DELETE;";
        pragma.ExecuteNonQuery();

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE VIRTUAL TABLE IF NOT EXISTS notes_fts USING fts5(
                file_path UNINDEXED,
                title,
                content,
                tokenize='porter'
            );
        ";
        command.ExecuteNonQuery();
    }

    public void IndexDocument(string filePath, string title, string content)
    {
        using var deleteCommand = _connection.CreateCommand();
        deleteCommand.CommandText = "DELETE FROM notes_fts WHERE file_path = $path;";
        deleteCommand.Parameters.AddWithValue("$path", filePath);
        deleteCommand.ExecuteNonQuery();

        using var insertCommand = _connection.CreateCommand();
        insertCommand.CommandText = "INSERT INTO notes_fts (file_path, title, content) VALUES ($path, $title, $content);";
        insertCommand.Parameters.AddWithValue("$path", filePath);
        insertCommand.Parameters.AddWithValue("$title", title);
        insertCommand.Parameters.AddWithValue("$content", content);
        insertCommand.ExecuteNonQuery();
    }

    public IEnumerable<SearchResult> Search(string query)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT file_path, title, rank
            FROM notes_fts
            WHERE notes_fts MATCH $query
            ORDER BY rank;
        ";
        command.Parameters.AddWithValue("$query", query);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new SearchResult
            {
                FilePath = reader.GetString(0),
                Title = reader.GetString(1),
                Rank = reader.GetDouble(2)
            };
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

public sealed class SearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double Rank { get; set; }
}
