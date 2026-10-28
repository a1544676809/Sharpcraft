// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.Network;

/// <summary>Connection phase of the Minecraft protocol.</summary>
public enum ConnectionState
{
    /// <summary>First state; only the client intention packet is legal.</summary>
    Handshaking,

    /// <summary>Server list ping.</summary>
    Status,

    /// <summary>Authentication and compression negotiation.</summary>
    Login,

    /// <summary>Registry/resource synchronisation.</summary>
    Configuration,

    /// <summary>Normal gameplay.</summary>
    Play
}

/// <summary>
/// Protocol constants for the pinned baseline (docs/specs/network/README.md).
/// </summary>
public static class ProtocolConstants
{
    /// <summary>Protocol version of the 26.3 baseline.</summary>
    public const int Version = 777;

    /// <summary>Host name limit in the handshake packet (not the 32767 default).</summary>
    public const int MaxHostLength = 255;

    /// <summary>Largest packet body after decompression (8 MiB).</summary>
    public const int MaxUncompressedBody = 8 * 1024 * 1024;

    /// <summary>Largest packet body before decompression (2 MiB).</summary>
    public const int MaxCompressedBody = 2 * 1024 * 1024;

    /// <summary>Intention value that requests a status ping.</summary>
    public const int IntentStatus = 1;

    /// <summary>Intention value that requests a login.</summary>
    public const int IntentLogin = 2;
}

/// <summary>
/// Packet ids per direction and state. Ids come from <b>registration order</b> in vanilla,
/// never from hand-written tables (docs/specs/network/protocol-phases.md 2).
/// </summary>
public static class PacketIds
{
    /// <summary>Client to server, <see cref="ConnectionState.Handshaking"/>.</summary>
    public static class HandshakeServerbound
    {
        /// <summary>Client intention (handshake).</summary>
        public const int ClientIntention = 0;
    }

    /// <summary>Client to server, <see cref="ConnectionState.Status"/>.</summary>
    public static class StatusServerbound
    {
        /// <summary>Status request.</summary>
        public const int StatusRequest = 0;

        /// <summary>Ping request carrying an opaque timestamp.</summary>
        public const int PingRequest = 1;
    }

    /// <summary>Client to server, <see cref="ConnectionState.Login"/>.</summary>
    public static class LoginServerbound
    {
        /// <summary>Login start with name and profile id.</summary>
        public const int LoginStart = 0;

        /// <summary>Encrypted shared secret and challenge.</summary>
        public const int Key = 1;

        /// <summary>Answer to a custom query.</summary>
        public const int CustomQueryAnswer = 2;

        /// <summary>Acknowledgement that login finished.</summary>
        public const int LoginAcknowledged = 3;

        /// <summary>Cookie response.</summary>
        public const int CookieResponse = 4;
    }

    /// <summary>Server to client, <see cref="ConnectionState.Login"/>.</summary>
    public static class LoginClientbound
    {
        /// <summary>Disconnect with a JSON reason.</summary>
        public const int LoginDisconnect = 0;

        /// <summary>Encryption request.</summary>
        public const int Hello = 1;

        /// <summary>Login finished.</summary>
        public const int LoginFinished = 2;

        /// <summary>Compression threshold advertisement.</summary>
        public const int LoginCompression = 3;

        /// <summary>Custom query.</summary>
        public const int CustomQuery = 4;

        /// <summary>Cookie request.</summary>
        public const int CookieRequest = 5;
    }

    /// <summary>Server to client, <see cref="ConnectionState.Status"/>.</summary>
    public static class StatusClientbound
    {
        /// <summary>Status response with a JSON document.</summary>
        public const int StatusResponse = 0;

        /// <summary>Pong response echoing the ping payload.</summary>
        public const int PongResponse = 1;
    }
}
