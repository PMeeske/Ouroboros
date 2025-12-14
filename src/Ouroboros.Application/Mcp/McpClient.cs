// <copyright file="McpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Application.Mcp;

/// <summary>
/// MCP (Model Context Protocol) client for connecting to external MCP servers.
/// Enables Ouroboros to use tools from MCP-compatible servers like Playwright.
/// </summary>
public class McpClient : IDisposable
{
    private readonly string _command;
    private readonly string[] _args;
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private int _requestId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpClient"/> class.
    /// </summary>
    /// <param name="command">The command to start the MCP server (e.g., "npx").</param>
    /// <param name="args">Arguments for the command (e.g., "@anthropic-ai/mcp-server-playwright").</param>
    public McpClient(string command, params string[] args)
    {
        _command = command;
        _args = args;
    }

    /// <summary>
    /// Gets whether the client is connected to the MCP server.
    /// </summary>
    public bool IsConnected => _process != null && !_process.HasExited;

    /// <summary>
    /// Disconnects and cleans up the current connection (if any) to allow reconnection.
    /// </summary>
    public void Disconnect()
    {
        _stdin?.Dispose();
        _stdin = null;
        _stdout?.Dispose();
        _stdout = null;

        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
            }
            catch
            {
                // Ignore kill errors
            }
        }

        _process?.Dispose();
        _process = null;
    }

    /// <summary>
    /// Starts the MCP server and establishes a connection.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // Clean up any existing dead connection
        if (_process != null && _process.HasExited)
        {
            Disconnect();
        }

        if (IsConnected) return;

        // On Windows, npx is a .cmd file that needs to be run via cmd.exe
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        var fileName = isWindows ? "cmd.exe" : _command;
        var arguments = isWindows
            ? $"/c {_command} {string.Join(" ", _args)}"
            : string.Join(" ", _args);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start MCP server process");

        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;

        // Initialize the MCP connection
        var initRequest = new McpRequest
        {
            JsonRpc = "2.0",
            Id = ++_requestId,
            Method = "initialize",
            Params = new
            {
                protocolVersion = "2025-03-26",
                capabilities = new { },
                clientInfo = new { name = "Ouroboros", version = "1.0.0" },
            },
        };

        var response = await SendRequestAsync(initRequest, ct);

        // Send initialized notification
        var initializedNotification = new McpRequest
        {
            JsonRpc = "2.0",
            Method = "notifications/initialized",
        };

        await SendNotificationAsync(initializedNotification, ct);
    }

    /// <summary>
    /// Lists all available tools from the MCP server.
    /// </summary>
    public async Task<List<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
    {
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Id = ++_requestId,
            Method = "tools/list",
        };

        var response = await SendRequestAsync(request, ct);
        var tools = new List<McpToolInfo>();

        if (response.TryGetProperty("result", out var result) &&
            result.TryGetProperty("tools", out var toolsArray))
        {
            foreach (var tool in toolsArray.EnumerateArray())
            {
                tools.Add(new McpToolInfo
                {
                    Name = tool.GetProperty("name").GetString() ?? "",
                    Description = tool.TryGetProperty("description", out var desc)
                        ? desc.GetString() ?? ""
                        : "",
                    InputSchema = tool.TryGetProperty("inputSchema", out var schema)
                        ? schema.GetRawText()
                        : "{}",
                });
            }
        }

        return tools;
    }

    /// <summary>
    /// Calls a tool on the MCP server.
    /// </summary>
    /// <param name="toolName">The name of the tool to call.</param>
    /// <param name="arguments">The arguments to pass to the tool.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tool's response.</returns>
    public async Task<McpToolResult> CallToolAsync(
        string toolName,
        Dictionary<string, object?>? arguments = null,
        CancellationToken ct = default)
    {
        var request = new McpRequest
        {
            JsonRpc = "2.0",
            Id = ++_requestId,
            Method = "tools/call",
            Params = new
            {
                name = toolName,
                arguments = arguments ?? new Dictionary<string, object?>(),
            },
        };

        var response = await SendRequestAsync(request, ct);

        if (response.TryGetProperty("error", out var error))
        {
            return new McpToolResult
            {
                IsError = true,
                Content = error.TryGetProperty("message", out var msg)
                    ? msg.GetString() ?? "Unknown error"
                    : "Unknown error",
            };
        }

        if (response.TryGetProperty("result", out var result))
        {
            var contentBuilder = new StringBuilder();

            if (result.TryGetProperty("content", out var content))
            {
                foreach (var item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var text))
                    {
                        contentBuilder.AppendLine(text.GetString());
                    }
                    else if (item.TryGetProperty("data", out var data))
                    {
                        contentBuilder.AppendLine($"[Binary data: {item.GetProperty("mimeType").GetString()}]");
                    }
                }
            }

            return new McpToolResult
            {
                IsError = result.TryGetProperty("isError", out var isErr) && isErr.GetBoolean(),
                Content = contentBuilder.ToString().TrimEnd(),
            };
        }

        return new McpToolResult { IsError = true, Content = "Invalid response format" };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private async Task<JsonElement> SendRequestAsync(McpRequest request, CancellationToken ct)
    {
        if (!IsConnected || _stdin == null || _stdout == null)
        {
            throw new InvalidOperationException("Not connected to MCP server");
        }

        var json = JsonSerializer.Serialize(request, JsonOptions);
        await _stdin.WriteLineAsync(json);
        await _stdin.FlushAsync(ct);

        var responseLine = await _stdout.ReadLineAsync(ct)
            ?? throw new InvalidOperationException("No response from MCP server");

        return JsonSerializer.Deserialize<JsonElement>(responseLine);
    }

    private async Task SendNotificationAsync(McpRequest notification, CancellationToken ct)
    {
        if (!IsConnected || _stdin == null)
        {
            throw new InvalidOperationException("Not connected to MCP server");
        }

        var json = JsonSerializer.Serialize(notification, JsonOptions);
        await _stdin.WriteLineAsync(json);
        await _stdin.FlushAsync(ct);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnect();
    }
}

/// <summary>
/// Information about an MCP tool.
/// </summary>
public record McpToolInfo
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the tool description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the JSON schema for the tool's input.
    /// </summary>
    public required string InputSchema { get; set; }
}

/// <summary>
/// Result from calling an MCP tool.
/// </summary>
public record McpToolResult
{
    /// <summary>
    /// Gets or sets whether the result is an error.
    /// </summary>
    public bool IsError { get; set; }

    /// <summary>
    /// Gets or sets the content of the result.
    /// </summary>
    public required string Content { get; set; }
}

/// <summary>
/// An MCP JSON-RPC request.
/// </summary>
public record McpRequest
{
    /// <summary>
    /// Gets or sets the JSON-RPC version.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// Gets or sets the request ID (null for notifications).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Id { get; set; }

    /// <summary>
    /// Gets or sets the method name.
    /// </summary>
    [JsonPropertyName("method")]
    public required string Method { get; set; }

    /// <summary>
    /// Gets or sets the parameters.
    /// </summary>
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }
}
