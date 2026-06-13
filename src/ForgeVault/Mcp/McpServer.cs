using ForgeVault.Core;
using ForgeVault.Search;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ForgeVault.Mcp;

/// <summary>
/// OpenClaw Model Context Protocol server for ForgeVault.
/// Reads JSON-RPC messages from stdin and writes responses to stdout.
/// </summary>
public sealed class McpServer : IDisposable
{
    private readonly VaultEngine _vaultEngine;
    private readonly SearchIndex _searchIndex;
    private readonly Dictionary<string, NoteModel> _notes;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    public McpServer(VaultEngine vaultEngine, SearchIndex searchIndex, Dictionary<string, NoteModel> notes, TextReader? input = null, TextWriter? output = null)
    {
        _vaultEngine = vaultEngine;
        _searchIndex = searchIndex;
        _notes = notes;
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await _input.ReadLineAsync(cancellationToken);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var message = JsonNode.Parse(line);
                if (message == null) continue;

                var response = HandleMessage(message);
                if (response != null)
                    await WriteMessageAsync(response, cancellationToken);
            }
            catch (Exception ex)
            {
                await WriteErrorAsync(null, -32603, ex.Message, cancellationToken);
            }
        }
    }

    private JsonObject? HandleMessage(JsonNode message)
    {
        var id = message["id"]?.GetValue<int?>();
        var method = message["method"]?.GetValue<string>();
        var parameters = message["params"]?.AsObject();

        if (method == null)
            return id.HasValue ? MakeError(id.Value, -32600, "Method missing") : null;

        try
        {
            return method switch
            {
                "initialize" => HandleInitialize(id, parameters),
                "tools/list" => HandleToolsList(id),
                "tools/call" => HandleToolCall(id, parameters),
                "notifications/initialized" => null,
                _ => id.HasValue ? MakeError(id.Value, -32601, $"Method not found: {method}") : null
            };
        }
        catch (Exception ex)
        {
            return id.HasValue ? MakeError(id.Value, -32603, ex.Message) : null;
        }
    }

    private JsonObject HandleInitialize(int? id, JsonObject? parameters)
    {
        return MakeResult(id, new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "forgevault",
                ["version"] = "0.1.0"
            },
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject()
            }
        });
    }

    private JsonObject HandleToolsList(int? id)
    {
        var tools = new JsonArray
        {
            MakeTool("forgevault_search", "Search the vault with full-text query.", new JsonObject
            {
                ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Search query" }
            }),
            MakeTool("forgevault_read_note", "Read a note by file path or title.", new JsonObject
            {
                ["identifier"] = new JsonObject { ["type"] = "string", ["description"] = "File path or title of the note" }
            }),
            MakeTool("forgevault_write_note", "Create or update a note.", new JsonObject
            {
                ["path"] = new JsonObject { ["type"] = "string", ["description"] = "Relative or absolute file path" },
                ["title"] = new JsonObject { ["type"] = "string", ["description"] = "Note title" },
                ["content"] = new JsonObject { ["type"] = "string", ["description"] = "Markdown content" }
            }),
            MakeTool("forgevault_list_notes", "List all notes in the vault.", new JsonObject()),
            MakeTool("forgevault_get_graph", "Return the vault graph as nodes and edges.", new JsonObject())
        };

        return MakeResult(id, new JsonObject { ["tools"] = tools });
    }

    private JsonObject HandleToolCall(int? id, JsonObject? parameters)
    {
        var name = parameters?["name"]?.GetValue<string>();
        var args = parameters?["arguments"]?.AsObject() ?? new JsonObject();

        if (name == null)
            return MakeError(id ?? 0, -32602, "Tool name missing");

        var result = name switch
        {
            "forgevault_search" => ToolSearch(args),
            "forgevault_read_note" => ToolReadNote(args),
            "forgevault_write_note" => ToolWriteNote(args),
            "forgevault_list_notes" => ToolListNotes(),
            "forgevault_get_graph" => ToolGetGraph(),
            _ => new JsonObject { ["isError"] = true, ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = $"Unknown tool: {name}" } } }
        };

        return MakeResult(id, result);
    }

    private JsonObject ToolSearch(JsonObject args)
    {
        var query = args["query"]?.GetValue<string>() ?? string.Empty;
        var results = _searchIndex.Search(query)
            .Select(r => new JsonObject
            {
                ["title"] = r.Title,
                ["path"] = r.FilePath,
                ["rank"] = r.Rank
            })
            .ToList();

        return new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = results.Count == 0 ? "No results." : JsonSerializer.Serialize(results) }
            }
        };
    }

    private JsonObject ToolReadNote(JsonObject args)
    {
        var identifier = args["identifier"]?.GetValue<string>() ?? string.Empty;

        var note = _notes.Values.FirstOrDefault(n =>
            string.Equals(n.FilePath, identifier, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Title, identifier, StringComparison.OrdinalIgnoreCase));

        if (note == null)
        {
            var fullPath = Path.Combine(_vaultEngine.VaultPath, identifier);
            if (File.Exists(fullPath))
                note = LoadNoteFromDisk(fullPath);
        }

        if (note == null)
            return new JsonObject { ["isError"] = true, ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = $"Note not found: {identifier}" } } };

        return new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = JsonSerializer.Serialize(note) }
            }
        };
    }

    private JsonObject ToolWriteNote(JsonObject args)
    {
        var path = args["path"]?.GetValue<string>() ?? string.Empty;
        var title = args["title"]?.GetValue<string>() ?? string.Empty;
        var content = args["content"]?.GetValue<string>() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(title))
            return new JsonObject { ["isError"] = true, ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = "path and title are required" } } };

        var fullPath = Path.IsPathFullyQualified(path) ? path : Path.Combine(_vaultEngine.VaultPath, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var noteContent = $"---\ntitle: {title}\n---\n\n{content}";
        File.WriteAllText(fullPath, noteContent);

        var note = LoadNoteFromDisk(fullPath);
        _notes[fullPath] = note;
        _searchIndex.IndexDocument(fullPath, note.Title, note.Content);

        return new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = $"Note written: {fullPath}" }
            }
        };
    }

    private JsonObject ToolListNotes()
    {
        var list = _notes.Values.Select(n => new JsonObject
        {
            ["title"] = n.Title,
            ["path"] = n.FilePath
        }).ToList();

        return new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = JsonSerializer.Serialize(list) }
            }
        };
    }

    private JsonObject ToolGetGraph()
    {
        var nodes = _notes.Values.Select(n => new JsonObject
        {
            ["id"] = n.Title,
            ["path"] = n.FilePath
        }).ToList();

        var edges = new List<JsonObject>();
        foreach (var note in _notes.Values)
        {
            foreach (var link in note.OutgoingLinks)
            {
                edges.Add(new JsonObject
                {
                    ["source"] = note.Title,
                    ["target"] = link
                });
            }
        }

        var nodesArray = new JsonArray();
        foreach (var node in nodes) nodesArray.Add(node);
        var edgesArray = new JsonArray();
        foreach (var edge in edges) edgesArray.Add(edge);

        return new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = JsonSerializer.Serialize(new JsonObject { ["nodes"] = nodesArray, ["edges"] = edgesArray }) }
            }
        };
    }

    private NoteModel LoadNoteFromDisk(string filePath)
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

    private static JsonObject MakeTool(string name, string description, JsonObject parameters)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = parameters
            }
        };
    }

    private static JsonObject MakeResult(int? id, JsonNode result)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        };
    }

    private static JsonObject MakeError(int id, int code, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
    }

    private async Task WriteMessageAsync(JsonObject message, CancellationToken cancellationToken)
    {
        var json = message.ToJsonString() + "\n";
        await _output.WriteAsync(json.AsMemory(), cancellationToken);
        await _output.FlushAsync();
    }

    private async Task WriteErrorAsync(int? id, int code, string message, CancellationToken cancellationToken)
    {
        if (!id.HasValue) return;
        await WriteMessageAsync(MakeError(id.Value, code, message), cancellationToken);
    }

    public void Dispose()
    {
        _searchIndex.Dispose();
    }
}
