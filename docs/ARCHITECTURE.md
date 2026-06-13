# ForgeVault Architecture

## Overview

ForgeVault is a Windows desktop application built on .NET 8 WPF. It treats a local folder of Markdown files as the single source of truth, indexing them for search and graph views while leaving the raw files untouched and portable.

## Layers

```
UI (WPF)
  |
  +-- Editor / Preview
  +-- File tree
  +-- Graph view (SkiaSharp)
  +-- AI chat panel
  |
Core
  +-- VaultEngine: folder scanning, note parsing, link extraction
  +-- NoteModel: frontmatter, content, backlinks
  |
Search
  +-- SearchIndex: SQLite FTS5 index
  |
Graph
  +-- GraphRenderer: 2D force-directed layout
  |
AI
  +-- OllamaClient: local LLM streaming
  |
MCP
  +-- McpServer: OpenClaw agent integration
```

## Data Flow

1. User opens a folder vault.
2. `VaultEngine` enumerates `.md` files and parses frontmatter + wikilinks.
3. `SearchIndex` populates SQLite FTS5.
4. `GraphRenderer` builds a node-link graph from link relationships.
5. AI chat queries use `OllamaClient` with optional vault context from search.
6. OpenClaw agents interact via `McpServer` tools.

## Security Model

- All data stays on disk in the vault folder.
- The search index is stored alongside the vault (`.forgevault/search.db`) and can be regenerated at any time.
- MCP operations will require explicit user confirmation scopes before write access.

## Portability

- Notes are plain Markdown with YAML frontmatter.
- No proprietary database holds user content.
- A future cross-platform head can reuse `Core`, `Search`, `Graph`, `AI`, and `Mcp` as class libraries.
