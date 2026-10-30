// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;

namespace Sharpcraft.Common.Network;

/// <summary>
/// How a chat type decorates a message: a translation key plus the order its arguments appear in
/// (docs/decisions/0012-chat-type-registry.md).
/// </summary>
/// <remarks>
/// The form of a player message is data, not code. A server sending <c>minecraft:chat</c> gets
/// <c>&lt;name&gt; text</c>, while a whisper gets something else entirely, and both come from this
/// record. Assuming one shape is why the client showed <c>name: text</c> where the reference showed
/// <c>&lt;name&gt; text</c>.
/// </remarks>
public sealed class ChatTypeDecoration
{
    /// <summary>Parameter that resolves to the sender's display name.</summary>
    public const string Sender = "sender";

    /// <summary>Parameter that resolves to the message itself.</summary>
    public const string Content = "content";

    /// <summary>Parameter that resolves to the recipient of a direct message.</summary>
    public const string Target = "target";

    /// <summary>
    /// The decoration the reference uses when no registry was consulted, which is the ordinary chat shape.
    /// </summary>
    public static ChatTypeDecoration Default { get; } = new("chat.type.text", [Sender, Content]);

    /// <summary>Translation key of the decoration, such as <c>chat.type.text</c>.</summary>
    public string TranslationKey { get; }

    /// <summary>Parameters in the order the translation key expects them.</summary>
    public string[] Parameters { get; }

    /// <summary>Creates a decoration.</summary>
    /// <param name="translationKey">Translation key.</param>
    /// <param name="parameters">Parameter names in order.</param>
    public ChatTypeDecoration(string translationKey, string[] parameters)
    {
        ArgumentNullException.ThrowIfNull(translationKey);
        ArgumentNullException.ThrowIfNull(parameters);

        TranslationKey = translationKey;
        Parameters = parameters;
    }

    /// <summary>
    /// Reads a decoration out of a chat type's payload.
    /// </summary>
    /// <param name="data">Payload of one registry entry, or null.</param>
    /// <returns>The decoration, or null when the payload does not carry one.</returns>
    /// <remarks>
    /// The payload holds <c>chat { translation_key, parameters[], style }</c>; only the key and the
    /// parameter order matter for display, because the style is applied by the message itself.
    /// </remarks>
    public static ChatTypeDecoration? TryRead(NbtCompound? data)
    {
        if (data?.GetCompound("chat") is not { } chat)
        {
            return null;
        }

        if (chat.GetString("translation_key") is not { } translationKey)
        {
            return null;
        }

        var parameters = Array.Empty<string>();

        if (chat.GetList("parameters") is { } list)
        {
            parameters = new string[list.Items.Count];

            for (var index = 0; index < list.Items.Count; index++)
            {
                parameters[index] = list.Items[index] is NbtString name ? name.Value : string.Empty;
            }
        }

        return new ChatTypeDecoration(translationKey, parameters);
    }
}

/// <summary>
/// The chat types a connection has been told about, addressed by the id used on the wire
/// (docs/decisions/0012-chat-type-registry.md).
/// </summary>
/// <remarks>
/// Ids are positional, so this table has to be rebuilt from the configuration phase of each
/// connection; a stale table would decorate messages with the wrong shape rather than fail visibly.
/// </remarks>
public sealed class ChatTypeTable
{
    private ChatTypeDecoration?[] _entries = [];

    /// <summary>Number of chat types known.</summary>
    public int Count => _entries.Length;

    /// <summary>Builds the table from the registries received during configuration.</summary>
    /// <param name="registries">Configuration-phase registries.</param>
    public void Load(DynamicRegistries registries)
    {
        ArgumentNullException.ThrowIfNull(registries);

        var entries = registries.Get(new Identifiers.Identifier("minecraft", "chat_type"));

        if (entries is null)
        {
            _entries = [];
            return;
        }

        var decorations = new ChatTypeDecoration?[entries.Count];

        for (var index = 0; index < entries.Count; index++)
        {
            decorations[index] = ChatTypeDecoration.TryRead(entries[index].Data);
        }

        _entries = decorations;
    }

    /// <summary>Decoration for a chat type id, or null when the id is unknown.</summary>
    /// <param name="id">Id as it appeared on the wire.</param>
    public ChatTypeDecoration? Get(int id) =>
        id >= 0 && id < _entries.Length ? _entries[id] : null;
}
