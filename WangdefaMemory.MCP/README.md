<!-- mcp-name: io.github.VinsonWild/wangdefa-memory -->

# Wangdefa.Memory MCP Server

[中文](./README.zh.md) | English

**Local-first long-term memory for agents, exposed over MCP (stdio).**

This package runs the Wangdefa.Memory engine as a local MCP server, so any
MCP-capable client can give its agent persistent memory: what you discussed,
what you decided, and what you prefer — all stored on your own machine with no
cloud dependency.

The server speaks stdio and is started by your client on demand. Installing
this package pulls the `Wangdefa.Memory` engine automatically; there is nothing
else to install.

## Configure your client

Add a server entry pointing at this package. Most clients accept the same shape:

```json
{
  "mcpServers": {
    "wangdefa-memory": {
      "command": "dnx",
      "args": ["Wangdefa.Memory.Mcp"]
    }
  }
}
```

Some clients require the full command instead:

```json
{
  "mcpServers": {
    "wangdefa-memory": {
      "command": "dotnet",
      "args": ["dnx", "Wangdefa.Memory.Mcp"]
    }
  }
}
```

Requires the [.NET 10 runtime](https://dotnet.microsoft.com/download). The first
run restores the packages, so it may take a moment; later starts are immediate.

## Tools

Once connected, the server exposes two tools:

| Tool | Purpose |
| --- | --- |
| `process_message` | Recall relevant past memories for the current user input. Call it before answering. |
| `save_memory` | Persist the completed turn. Call it once the reply is finished. |

They are meant to be driven by the client automatically — recall before the
model answers, save when the turn ends — but you can also call them directly.

## Where your data lives

All memory stays on your machine, under the MCP server's own working directory:

```
memory/
├── cognitive/     memory cards
├── experience/    conversations, events and overviews
├── thinking/      routing indexes
└── *.db           tag pool, scene library, main records
```

Nothing is sent anywhere unless you configure a remote model provider. The
storage format is plain SQLite and JSON, so it is readable and portable.

## Related

- [Wangdefa.Memory](https://www.nuget.org/packages/Wangdefa.Memory) — the engine library, installed automatically as a dependency
- [Source and documentation](https://github.com/VinsonWild/Wangdefa.Memory)

## License

Apache-2.0
