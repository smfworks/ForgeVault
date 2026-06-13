# ForgeVault MCP Configuration

ForgeVault exposes a Model Context Protocol (MCP) server so OpenClaw agents can query and update the vault.

## Running the MCP Server

```bash
ForgeVault.exe --mcp "C:\path\to\vault"
```

The server communicates over stdio using JSON-RPC 2.0.

## Available Tools

| Tool | Description |
|------|-------------|
| `forgevault_search` | Full-text search the vault |
| `forgevault_read_note` | Read a note by title or file path |
| `forgevault_write_note` | Create or update a note |
| `forgevault_list_notes` | List all notes in the vault |
| `forgevault_get_graph` | Return nodes and edges of the vault graph |

## OpenClaw Configuration

Add to your OpenClaw MCP config:

```json
{
  "mcpServers": {
    "forgevault": {
      "command": "C:\\path\\to\\ForgeVault.exe",
      "args": ["--mcp", "C:\\path\\to\\vault"]
    }
  }
}
```

## Security

- The MCP server only operates on the vault path provided at startup.
- Write operations create or overwrite `.md` files in that vault.
- Run the server only with vaults you intend agents to access.
