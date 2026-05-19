# Kronk

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=.net)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](LICENSE)

**Kronk** is a .NET 10 Model Context Protocol (MCP) server that provides AI agents with file system, shell, and web browsing tooling via a JSON-RPC interface.

## Overview

Kronk implements the [MCP specification](https://modelcontextprotocol.io/) (protocol version `2024-11-05`), exposing a suite of tools that large language models can invoke to interact with the local machine and the web. It runs as a lightweight ASP.NET Core web service, accepting JSON-RPC 2.0 POST requests over HTTP.

## Architecture

```
┌──────────────┐     JSON-RPC 2.0      ┌─────────────────┐
│   MCP Client  │ ◄───────────────────► │  McpController   │
│ (e.g., Claude)│   HTTP POST /        │  (ASP.NET Core)  │
└──────────────┘                       └────────┬────────┘
                                                │
                                       ┌────────▼────────┐
                                       │   ToolManager    │
                                       │  (Factory/Router)│
                                       └────────┬────────┘
                                                │
                  ┌─────────────────────────────┼─────────────────────────────┐
                  │                             │                             │
          ┌───────▼──────┐              ┌───────▼──────┐              ┌───────▼──────┐
          │ FetchUrlTool │              │  ReadFiles   │              │  WriteFile   │
          └──────────────┘              └──────────────┘              └──────────────┘
          ┌───────▼──────┐              ┌───────▼──────┐
          │  ReplaceIn   │              │ RunCommand   │
          │   FileTool   │              │    Tool      │
          └──────────────┘              └──────────────┘
```

### Core Components

| Component | Description |
|-----------|-------------|
| `McpController` | HTTP endpoint handling JSON-RPC 2.0 requests (`initialize`, `tools/list`, `tools/call`) |
| `Tool<TArgs, TSchema>` | Abstract generic base class for all tools, handling argument deserialization and schema generation |
| `ToolManager` | Static registry and dispatcher that resolves tool names and delegates execution |
| `JsonRpc` models | Records/classes for serializing/deserializing JSON-RPC 2.0 protocol messages |

### Available Tools

#### Web

| Tool | Description |
|------|-------------|
| `fetch_url` | Fetches a URL and converts HTML to clean Markdown using HtmlAgilityPack. Handles headers, links, images, code blocks, blockquotes, and more. |

#### File I/O

| Tool | Description |
|------|-------------|
| `read_files` | Reads one or more files from the local file system and returns their contents. |
| `read_file_range` | Reads a specific range of lines from a file. Useful for large files without loading the entire content. |
| `write_file` | Creates or overwrites a file with the given content. Creates parent directories as needed. |
| `replace_in_file` | Replaces an exact text block in a file. Requires strict whitespace/indentation matching. |
| `append_to_file` | Appends, prepends, or inserts content into a file at a specific line. Creates the file if it doesn't exist. |

#### Directory

| Tool | Description |
|------|-------------|
| `list_directory` | Lists the contents of a directory, with optional recursive subdirectory traversal. |
| `create_directory` | Creates a directory, optionally creating parent directories as needed. |
| `search_files` | Finds files matching a glob pattern. Supports `*`, `**`, and `?` wildcards. |
| `get_file_info` | Returns metadata about a file or directory (type, size, timestamps, permissions). |
| `delete_file` | Deletes a file or directory. Supports recursive and forced deletion. |

#### Search

| Tool | Description |
|------|-------------|
| `search_in_files` | Searches for a regex pattern across files. Automatically skips binary files by checking headers for null bytes. |

#### Shell

| Tool | Description |
|------|-------------|
| `run_command` | Executes a shell command via `/bin/bash -c`, capturing stdout and stderr concurrently. |
| `run_tests` | Runs tests using `dotnet test`, `npm test`, or `yarn test`. Supports filtering by test name, target framework, and verbosity. |

#### Git

| Tool | Description |
|------|-------------|
| `git_status` | Shows the working tree status of a git repository (staged, unstaged, and untracked files). |
| `git_diff` | Shows differences between commits, the working tree, and the index. |
| `git_log` | Shows the commit history of a git repository, with filtering by file, author, and date range. |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A JSON-RPC 2.0 MCP-compatible client (e.g., Claude Desktop, Cursor, Windsurf)

### Building

```bash
cd Kronk
dotnet restore
dotnet build
```

### Running

```bash
dotnet run
```

The server starts listening on `https://localhost:5001` (or the port configured in `appsettings.json`).

### Configuration

The `appsettings.json` file controls CORS origins and logging:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "CorsOrigins": [
    "http://127.0.0.1:8080",
    "http://localhost:8080"
  ]
}
```

## Protocol Details

Kronk implements three MCP methods:

### `initialize`

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {}
}
```

Returns the server's protocol version, capabilities, and identity:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2024-11-05",
    "capabilities": { "tools": {} },
    "serverInfo": {
      "name": "Kronk MCP Server",
      "version": "1.0.0"
    }
  }
}
```

### `tools/list`

Returns all available tools with their names, descriptions, and JSON Schema input schemas:

```json
{ "jsonrpc": "2.0", "id": 2, "method": "tools/list" }
```

### `tools/call`

Invokes a tool by name with JSON-serialized arguments:

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "run_command",
    "arguments": { "command": "echo hello" }
  }
}
```

## Development

### Adding a New Tool

1. Define argument and schema property types.
2. Create an input schema class inheriting from `ToolParameters<TProperties>`.
3. Create an arguments record with `required` properties.
4. Create a class inheriting from `Tool<TArguments, TInputSchema>`.
5. Register the tool in `ToolManager.Tools`.

```csharp
public class MyToolProperties
{
    [JsonPropertyName("input")]
    public StringToolProperty Input { get; set; } = new("The input value");
}

public class MyToolInputSchema : ToolParameters<MyToolProperties>
{
    public MyToolInputSchema() : base([nameof(MyToolProperties.Input)]) { }
}

public class MyToolArguments
{
    public required string Input { get; set; }
}

public class MyTool : Tool<MyToolArguments, MyToolInputSchema>
{
    public MyTool() : base("my_tool", "Description of my tool.") { }

    public override async Task<string> Run(MyToolArguments arguments, CancellationToken cancellationToken)
    {
        return $"You passed: {arguments.Input}";
    }
}
```

## License

This project is licensed under the [MIT License](LICENSE).
