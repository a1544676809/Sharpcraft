// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json.Nodes;

namespace Sharpcraft.Client.Diagnostics;

/// <summary>
/// Entry point the client loads by name when the developer asks for the debug channel.
/// </summary>
/// <remarks>
/// The client passes plain delegates and strings, so it never references a type from this assembly at compile time and
/// the whole project can be deleted without touching game code (AGENTS.md principle 13). Everything here is Debug-only
/// (docs/specs/debug-control.md 7.1).
/// </remarks>
public static class DebugChannel
{
    /// <summary>Starts the MCP server and returns the port it listens on.</summary>
    /// <param name="describeState">
    /// Callback the <c>state</c> tool forwards to; the client formats its own snapshot so this assembly stays free of
    /// game knowledge.
    /// </param>
    public static int Start(Func<string> describeState)
    {
        ArgumentNullException.ThrowIfNull(describeState);

        var server = McpServer.Start();
        server.AddTool("ping", "Returns pong when the client is alive.", _ => JsonValue.Create("pong"));
        server.AddTool("state", "Returns the client's current state snapshot as JSON text.", _ => JsonValue.Create(describeState()));

        // The server owns a background thread; the process exit is what tears it down in practice.
        Console.WriteLine($"[mcp] debug channel listening on http://127.0.0.1:{server.Port}/mcp");
        return server.Port;
    }
}
