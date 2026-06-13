# ForgeVault

An open-source, local-first knowledge vault with native AI and OpenClaw MCP integration.

## Why ForgeVault?

- **Local-first:** Your notes are plain Markdown files in a folder you control.
- **Native local AI:** Powered by [Ollama](https://ollama.com/) — no cloud API keys required.
- **OpenClaw-ready:** Exposes a Model Context Protocol (MCP) server so OpenClaw agents can search, read, and write your vault.
- **Clean graph:** Backlinks and a lightweight force-directed graph view.
- **Fast search:** SQLite FTS5 index over your entire vault.

## Status

Early development — MVP targeting Windows with .NET 8 WPF.

## Tech Stack

- C# / .NET 8 / WPF
- Ollama API for local LLM inference
- SQLite FTS5 for full-text search
- SkiaSharp for graph rendering
- OpenClaw MCP server for agent integration

## Getting Started

Requires:
- Windows 10/11 x64
- .NET 8 SDK
- Ollama installed locally

Build:
```bash
dotnet build src/ForgeVault.sln
```

Run:
```bash
dotnet run --project src/ForgeVault/ForgeVault.csproj
```

## License

MIT — see [LICENSE](./LICENSE).

---

Built by the SMF Works Project.
