// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sharpcraft.Client.Diagnostics;

/// <summary>
/// A minimal MCP server over Streamable HTTP for driving a running client during development.
/// </summary>
/// <remarks>
/// The agent side speaks MCP (JSON-RPC 2.0) directly against <c>POST /mcp</c>; there is no bridge process and no custom
/// wire format (docs/specs/debug-control.md). This assembly is loaded by name from the client entry point only in Debug
/// builds and only when the developer asks for it, so deleting it leaves the game assemblies untouched
/// (AGENTS.md principle 13).
/// </remarks>
public sealed class McpServer : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Dictionary<string, Func<JsonObject?, JsonNode?>> _tools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _descriptions = new(StringComparer.Ordinal);
    private readonly Thread _acceptThread;
    private bool _disposed;

    private McpServer(TcpListener listener)
    {
        _listener = listener;
        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "mcp-accept" };
        _acceptThread.Start();
    }

    /// <summary>Port the server is listening on.</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Starts a server on a loopback port.</summary>
    public static McpServer Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new McpServer(listener);
    }

    /// <summary>Registers a tool that takes optional arguments and returns a JSON result.</summary>
    /// <param name="name">Tool name exposed to MCP clients.</param>
    /// <param name="description">Human readable description.</param>
    /// <param name="handler">Handler; the argument object may be null when the caller sent none.</param>
    public void AddTool(string name, string description, Func<JsonObject?, JsonNode?> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(handler);
        _tools[name] = handler;
        _descriptions[name] = description;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _listener.Stop();
        _shutdown.Dispose();
    }

    private void AcceptLoop()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = _listener.AcceptTcpClient();
            }
            catch (Exception) when (_shutdown.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            try
            {
                HandleConnection(client);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"[mcp] connection failed: {error.GetType().Name}: {error.Message}");
            }
            finally
            {
                client.Dispose();
            }
        }
    }

    private void HandleConnection(TcpClient client)
    {
        using var stream = client.GetStream();
        var body = ReadRequestBody(stream);
        if (body is null)
        {
            return;
        }

        var response = Dispatch(body);
        WriteResponse(stream, response);
    }

    /// <summary>Reads one HTTP request and returns its body, or null when the request is not a POST with content.</summary>
    private static string? ReadRequestBody(NetworkStream stream)
    {
        var header = new StringBuilder();
        var buffer = new byte[1];
        while (true)
        {
            if (stream.Read(buffer, 0, 1) == 0)
            {
                return null;
            }

            header.Append((char)buffer[0]);
            if (header.Length >= 4 && header[^4] == '\r' && header[^3] == '\n' && header[^2] == '\r' && header[^1] == '\n')
            {
                break;
            }

            if (header.Length > 64 * 1024)
            {
                return null;
            }
        }

        var lines = header.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return null;
        }

        var contentLength = 0;
        foreach (var line in lines)
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator > 0 && line[..separator].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(line[(separator + 1)..].Trim(), out contentLength);
            }
        }

        if (contentLength <= 0)
        {
            return string.Empty;
        }

        var payload = new byte[contentLength];
        var read = 0;
        while (read < contentLength)
        {
            var chunk = stream.Read(payload, read, contentLength - read);
            if (chunk == 0)
            {
                break;
            }

            read += chunk;
        }

        return Encoding.UTF8.GetString(payload, 0, read);
    }

    private static void WriteResponse(NetworkStream stream, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var head = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: application/json\r\n" +
            $"Content-Length: {bytes.Length}\r\n" +
            "Connection: close\r\n\r\n");
        stream.Write(head, 0, head.Length);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private string Dispatch(string body)
    {
        JsonObject? request;
        try
        {
            request = JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            return Error(null, -32700, "parse error");
        }

        if (request is null)
        {
            return Error(null, -32600, "invalid request");
        }

        var id = request["id"]?.DeepClone();
        var method = request["method"]?.GetValue<string>();
        if (method is null)
        {
            // A notification (no id) is accepted and ignored, which is what the specification asks for.
            return id is null ? "{}" : Error(id, -32600, "missing method");
        }

        switch (method)
        {
            case "initialize":
                return Result(id, new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = "sharpcraft-client", ["version"] = "0.1" }
                });
            case "tools/list":
                return Result(id, new JsonObject { ["tools"] = BuildToolList() });
            case "tools/call":
                return Result(id, CallTool(request["params"] as JsonObject));
            case "notifications/initialized":
            case "notifications/cancelled":
                return "{}";
            default:
                return Error(id, -32601, $"unknown method {method}");
        }
    }

    private JsonArray BuildToolList()
    {
        var tools = new JsonArray();
        foreach (var (name, description) in _descriptions)
        {
            tools.Add(new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["inputSchema"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() }
            });
        }

        return tools;
    }

    private JsonObject CallTool(JsonObject? parameters)
    {
        var name = parameters?["name"]?.GetValue<string>();
        if (name is null || !_tools.TryGetValue(name, out var handler))
        {
            return new JsonObject
            {
                ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = $"unknown tool {name}" } },
                ["isError"] = true
            };
        }

        try
        {
            var result = handler(parameters?["arguments"] as JsonObject);
            return new JsonObject
            {
                ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = result?.ToJsonString(SerializerOptions) ?? "null" } },
                ["isError"] = false
            };
        }
        catch (Exception error)
        {
            return new JsonObject
            {
                ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = $"{error.GetType().Name}: {error.Message}" } },
                ["isError"] = true
            };
        }
    }

    private static string Result(JsonNode? id, JsonNode result) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }.ToJsonString(SerializerOptions);

    private static string Error(JsonNode? id, int code, string message) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
        }.ToJsonString(SerializerOptions);
}
