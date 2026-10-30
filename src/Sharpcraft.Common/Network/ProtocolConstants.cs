// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

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

    /// <summary>Client to server, <see cref="ConnectionState.Configuration"/>.</summary>
    public static class ConfigurationServerbound
    {
        /// <summary>Client information.</summary>
        public const int ClientInformation = 0;

        /// <summary>Cookie response.</summary>
        public const int CookieResponse = 1;

        /// <summary>Custom payload.</summary>
        public const int CustomPayload = 2;

        /// <summary>Finish configuration.</summary>
        public const int FinishConfiguration = 3;

        /// <summary>Keep alive reply.</summary>
        public const int KeepAlive = 4;

        /// <summary>Pong reply.</summary>
        public const int Pong = 5;

        /// <summary>Resource pack status.</summary>
        public const int ResourcePack = 6;

        /// <summary>Select known packs.</summary>
        public const int SelectKnownPacks = 7;

        /// <summary>Custom click action.</summary>
        public const int CustomClickAction = 8;

        /// <summary>Accept code of conduct.</summary>
        public const int AcceptCodeOfConduct = 9;
    }

    /// <summary>Client to server, <see cref="ConnectionState.Play"/> (only used ids listed).</summary>
    public static class PlayServerbound
    {
        /// <summary>Chat message.</summary>
        public const int Chat = 9;

        /// <summary>A command, sent as one line with its leading slash.</summary>
        public const int ChatCommand = 7;

        /// <summary>
        /// End of a client tick. The server resets its per-tick movement guards here, so a client that
        /// never sends it is disconnected on its second position packet.
        /// </summary>
        public const int ClientTickEnd = 13;

        /// <summary>Block interaction: start or stop destroying, drops, item use.</summary>
        public const int PlayerAction = 41;

        /// <summary>Asks the server for the next chunk batch; the server waits for it.</summary>
        public const int ChunkBatchReceived = 11;

        /// <summary>Client information update.</summary>
        public const int ClientInformation = 14;

        /// <summary>
        /// Keep alive reply. Ids in this table are the position in the play registration chain, and that
        /// chain also contains the shared "common" packets, so a count that only looks at game packets is
        /// off by the number of common packets registered before it.
        /// </summary>
        public const int KeepAlive = 28;

        /// <summary>Accept a server teleport, carrying the id and the accepted position.</summary>
        public const int AcceptTeleportation = 0;

        /// <summary>Position update.</summary>
        public const int MovePlayerPos = 30;

        /// <summary>Position and rotation update, sent while turning and walking.</summary>
        public const int MovePlayerPosRot = 31;

        /// <summary>Rotation-only update.</summary>
        public const int MovePlayerRot = 32;

        /// <summary>Status-only update: the ground and collision flags changed.</summary>
        public const int MovePlayerStatusOnly = 33;

        /// <summary>Pong reply.</summary>
        public const int Pong = 45;
    }

    /// <summary>Server to client, <see cref="ConnectionState.Play"/> (only used ids listed).</summary>
    public static class PlayClientbound
    {
        /// <summary>Disconnect with a component reason.</summary>
        public const int Disconnect = 32;

        /// <summary>Keep alive ping.</summary>
        public const int KeepAlive = 45;

        /// <summary>Absolute player position: a teleport or a server correction.</summary>
        public const int PlayerPosition = 73;

        /// <summary>Rotation-only update, used for server-driven turning such as a forced facing.</summary>
        public const int PlayerRotation = 74;

        /// <summary>Introduces an entity: identity, type, position, velocity, three angles.</summary>
        public const int AddEntity = 1;

        /// <summary>Drops entities by id.</summary>
        public const int RemoveEntities = 78;

        /// <summary>Delta movement for an existing entity.</summary>
        public const int MoveEntityPos = 54;

        /// <summary>Delta movement plus rotation.</summary>
        public const int MoveEntityPosRot = 55;

        /// <summary>Rotation only; unlike its siblings it carries a plain ground flag.</summary>
        public const int MoveEntityRot = 57;

        /// <summary>Head rotation, driven separately from body rotation.</summary>
        public const int RotateHead = 85;

        /// <summary>Absolute velocity for an existing entity.</summary>
        public const int SetEntityMotion = 103;

        /// <summary>Absolute repositioning with velocity, angles and relative flags.</summary>
        public const int TeleportEntity = 128;

        /// <summary>Single block change: packed position plus a state id.</summary>
        public const int BlockUpdate = 8;

        /// <summary>Multi-block change inside one section.</summary>
        public const int SectionBlocksUpdate = 86;

        /// <summary>Chunk batch start marker.</summary>
        public const int ChunkBatchStart = 12;

        /// <summary>Chunk batch finished marker, carrying the batch size.</summary>
        public const int ChunkBatchFinished = 11;

        /// <summary>Drops a chunk the server no longer tracks.</summary>
        public const int ForgetLevelChunk = 38;

        /// <summary>JoinGame.</summary>
        public const int Login = 50;

        /// <summary>Chunk with block states, biomes, heightmaps and light.</summary>
        public const int LevelChunkWithLight = 46;

        /// <summary>Player list additions and updates.</summary>
        /// <remarks>Ids come from the reference's own registration dump
        /// (ref://mc-26.3-analysis/vectors/packet-tables.txt, play/clientbound). That dump also reproduces every
        /// id already used here, which is how the mapping was checked rather than assumed.</remarks>
        public const int PlayerInfoUpdate = 71;

        /// <summary>Player list removals.</summary>
        public const int PlayerInfoRemove = 70;

        /// <summary>A chat line from the server itself.</summary>
        public const int SystemChat = 124;

        /// <summary>A chat line from another player.</summary>
        public const int PlayerChat = 66;

        /// <summary>Server-sent message on a player's behalf, which is what <c>/me</c> produces.</summary>
        public const int DisguisedChat = 33;

        /// <summary>Tells the client to remove a message it was shown, named by signature.</summary>
        public const int DeleteChat = 31;

        /// <summary>Text shown above the hotbar, which is what a title command's action bar produces.</summary>
        public const int SetActionBarText = 89;

        /// <summary>Ping.</summary>
        public const int Ping = 62;

        /// <summary>Start configuration (reconfiguration handshake).</summary>
        public const int StartConfiguration = 120;

        /// <summary>Transfer to another server.</summary>
        public const int Transfer = 132;
    }

    /// <summary>Server to client, <see cref="ConnectionState.Configuration"/>.</summary>
    public static class ConfigurationClientbound
    {
        /// <summary>Cookie request.</summary>
        public const int CookieRequest = 0;

        /// <summary>Custom payload.</summary>
        public const int CustomPayload = 1;

        /// <summary>Disconnect.</summary>
        public const int Disconnect = 2;

        /// <summary>Finish configuration.</summary>
        public const int FinishConfiguration = 3;

        /// <summary>Keep alive ping.</summary>
        public const int KeepAlive = 4;

        /// <summary>Ping.</summary>
        public const int Ping = 5;

        /// <summary>Reset chat.</summary>
        public const int ResetChat = 6;

        /// <summary>Registry data.</summary>
        public const int RegistryData = 7;

        /// <summary>Resource pack pop.</summary>
        public const int ResourcePackPop = 8;

        /// <summary>Resource pack push.</summary>
        public const int ResourcePackPush = 9;

        /// <summary>Post effects.</summary>
        public const int PostEffects = 10;

        /// <summary>Store cookie.</summary>
        public const int StoreCookie = 11;

        /// <summary>Transfer.</summary>
        public const int Transfer = 12;

        /// <summary>Update enabled features.</summary>
        public const int UpdateEnabledFeatures = 13;

        /// <summary>Update tags.</summary>
        public const int UpdateTags = 14;

        /// <summary>Select known packs.</summary>
        public const int SelectKnownPacks = 15;

        /// <summary>Custom report details.</summary>
        public const int CustomReportDetails = 16;

        /// <summary>Server links.</summary>
        public const int ServerLinks = 17;

        /// <summary>Clear dialog.</summary>
        public const int ClearDialog = 18;

        /// <summary>Show dialog.</summary>
        public const int ShowDialog = 19;

        /// <summary>Code of conduct.</summary>
        public const int CodeOfConduct = 20;
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
