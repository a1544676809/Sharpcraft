// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using System.Numerics;
using Sharpcraft.AuthLib;
using Sharpcraft.Client.Camera;
using Sharpcraft.Client.Game;
using Sharpcraft.Client.Input;
using Sharpcraft.Client.Ui;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Entities;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Platform;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Game;


internal sealed partial class InteractiveSession
{
    /// <summary>Reads the keyboard for the chat prompt and sends what the player submits.</summary>
    /// <param name="window">Window whose input state is read.</param>
    private void UpdateChat(WindowHost window)
    {
        var input = window.Input;
        var bindings = KeyBindings.Default;

        if (!_chat.IsOpen)
        {
            // Characters typed while the prompt is closed are discarded. They are produced by every printable key,
            // movement keys included, so without this the first thing a player sees on opening the prompt is the key
            // they pressed to open it, and whatever they typed while walking.
            input.TakeText();

            // Both keys open the prompt; the slash key leaves its slash behind so a command can be typed straight on.
            if (bindings.JustPressed(input, InputAction.OpenCommand))
            {
                _chat.Open("/");
                _chatOpenedAtMs = Environment.TickCount64;
                window.SetMouseCaptured(false);
            }
            else if (bindings.JustPressed(input, InputAction.OpenChat))
            {
                _chat.Open();
                _chatOpenedAtMs = Environment.TickCount64;
                window.SetMouseCaptured(false);
            }

            return;
        }

        if (bindings.JustPressed(input, InputAction.CloseScreen))
        {
            _chat.Close();
            _chatLog.ResetScroll();
            return;
        }

        if (bindings.JustPressed(input, InputAction.SendMessage))
        {
            if (_chat.TrySubmit(out var message, out var isCommand))
            {
                SendChat(message, isCommand);
            }

            return;
        }

        // Editing, with shift extending the selection the way the reference does.
        var model = _chat.Model;
        var extend = input.IsKeyDown(InputKey.LeftShift);
        var control = input.IsKeyDown(InputKey.LeftControl);

        if (control && input.JustPressed(InputKey.A))
        {
            model.SelectAll();
        }

        if (control && input.JustPressed(InputKey.C))
        {
            if (model.HasSelection)
            {
                window.ClipboardText = model.Value[model.SelectionStart..model.SelectionEnd];
            }
        }

        if (control && input.JustPressed(InputKey.X))
        {
            if (model.HasSelection)
            {
                window.ClipboardText = model.Value[model.SelectionStart..model.SelectionEnd];
                model.Delete();
            }
        }

        if (control && input.JustPressed(InputKey.V))
        {
            if (window.ClipboardText is { Length: > 0 } pasted)
            {
                model.Insert(pasted.AsSpan());
            }
        }

        if (input.JustPressed(InputKey.Backspace))
        {
            model.Backspace();
        }

        if (input.JustPressed(InputKey.Delete))
        {
            model.Delete();
        }

        if (input.JustPressed(InputKey.LeftArrow))
        {
            model.MoveCaret(-1, extend);
        }

        if (input.JustPressed(InputKey.RightArrow))
        {
            model.MoveCaret(1, extend);
        }

        if (input.JustPressed(InputKey.Home))
        {
            model.Home(extend);
        }

        if (input.JustPressed(InputKey.End))
        {
            model.End(extend);
        }

        // Page up walks towards older messages, page down back towards the newest; the reference moves seven entries
        // per press, the same step its wheel uses.
        if (input.JustPressed(InputKey.PageUp))
        {
            _chatLog.ScrollBy(7);
        }

        if (input.JustPressed(InputKey.PageDown))
        {
            _chatLog.ScrollBy(-7);
        }

        // The reference clamps one wheel event to a single step, then multiplies by seven unless shift is held, so a
        // fast wheel scrolls steadily instead of jumping.
        var wheel = input.TakeScroll();

        if (wheel != 0)
        {
            var step = Math.Clamp(wheel, -1d, 1d);
            _chatLog.ScrollBy((int)(step * 7d));
        }

        // Over a clickable run the pointer becomes a hand, which is how the reference signals that it can be clicked.
        window.SetHandCursor(
            !input.MouseCaptured && ChatHoverAt(input.CursorInCanvas.X, input.CursorInCanvas.Y) is not null);

        if (input.TakeRightClick())
        {
            TryClickChat(input.CursorInCanvas.X, input.CursorInCanvas.Y, input.IsKeyDown(InputKey.LeftShift), window);
        }

        if (UiFont is { } uiFont && input.TakeClick())
        {
            // A click inside the prompt places the caret; the text starts two pixels in from the inset.
            var offset = input.CursorInCanvas.X - (ChatPrompt.InsetX + 2f);

            if (offset >= 0f)
            {
                model.MoveCaretTo(TextEditRenderer.PositionAtOffset(uiFont, model.Value, offset), extend);
            }
        }

        var typed = input.TakeText();

        if (typed.Length > 0)
        {
            _chat.Append(typed.AsSpan());
        }
    }


    /// <summary>
    /// Sends one chat line when asked to by the environment, so an unattended run can verify the chat path end to
    /// end (docs/modules/ui.md 8.4). It is a verification hook in the same spirit as the auto walk: it exists so a
    /// run can be observed rather than described.
    /// </summary>
    private void MaybeSendDiagnosticChat()
    {
        if (_diagnosticChatSent || _chatTicks < 40)
        {
            return;
        }

        var line = GameOptions.Get("MC_CHAT_SAY");

        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        _diagnosticChatSent = true;
        SendChat(line, isCommand: line[0] == '/');
        Console.WriteLine($"chat sent: {line}");
    }



    /// <summary>
    /// Decorates a message with the chat type it names, or returns null when the type cannot be resolved.
    /// </summary>
    /// <param name="chatTypeId">Chat type id from the wire.</param>
    /// <param name="senderName">Name carried by the binding.</param>
    /// <param name="content">Message contents.</param>
    /// <param name="target">Target of a direct message, or empty.</param>
    /// <remarks>
    /// An empty table means the server sent no chat types, so the reference's own default shape applies. A table with
    /// entries but no match means the message names a type we were never told about, and the reference cannot display
    /// it either; returning null lets the caller drop it instead of guessing a shape.
    /// </remarks>
    /// <summary>
    /// Decorates a message with its chat type while keeping the click and hover each part carries.
    /// </summary>
    /// <param name="chatTypeId">Chat type id from the wire.</param>
    /// <param name="senderName">Sender name with its interactions.</param>
    /// <param name="content">Message with its interactions.</param>
    /// <param name="target">Target with its interactions, or empty.</param>
    /// <remarks>
    /// A decorated line is where clickable names and bodies actually appear, so the substitution has to carry the
    /// interactions. Rebuilding the runs without them made every decorated message unclickable.
    /// </remarks>
    private Sharpcraft.Common.Network.StyledRuns? DecorateLineStyled(
        int chatTypeId,
        Sharpcraft.Common.Network.StyledRuns senderName,
        Sharpcraft.Common.Network.StyledRuns content,
        Sharpcraft.Common.Network.StyledRuns target)
    {
        var decoration = _chatTypes is { Count: > 0 } table
            ? table.Get(chatTypeId)
            : Sharpcraft.Common.Network.ChatTypeDecoration.Default;

        if (decoration is null)
        {
            if (_diagEnabled)
            {
                Console.WriteLine($"chat dropped: unknown chat type {chatTypeId}");
            }

            return null;
        }

        var arguments = new System.Collections.Generic.List<Sharpcraft.Common.Network.StyledRuns>(decoration.Parameters.Length);

        for (var argument = 0; argument < decoration.Parameters.Length; argument++)
        {
            arguments.Add(decoration.Parameters[argument] switch
            {
                Sharpcraft.Common.Network.ChatTypeDecoration.Sender => senderName,
                Sharpcraft.Common.Network.ChatTypeDecoration.Content => content,
                Sharpcraft.Common.Network.ChatTypeDecoration.Target => target,
                _ => Sharpcraft.Common.Network.StyledRuns.Empty
            });
        }

        return Sharpcraft.Common.Network.ComponentRuns.DecorateStyled(
            _language,
            decoration.TranslationKey,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(arguments));
    }

    private Sharpcraft.Common.Network.TextRun[]? DecorateLine(
        int chatTypeId,
        Sharpcraft.Common.Network.TextRun[] senderName,
        Sharpcraft.Common.Network.TextRun[] content,
        Sharpcraft.Common.Network.TextRun[] target)
    {
        var decoration = _chatTypes is { Count: > 0 } table
            ? table.Get(chatTypeId)
            : Sharpcraft.Common.Network.ChatTypeDecoration.Default;

        if (decoration is null)
        {
            if (_diagEnabled)
            {
                Console.WriteLine($"chat dropped: unknown chat type {chatTypeId}");
            }

            return null;
        }

        var arguments = new System.Collections.Generic.List<Sharpcraft.Common.Network.TextRun[]>(decoration.Parameters.Length);

        for (var argument = 0; argument < decoration.Parameters.Length; argument++)
        {
            arguments.Add(decoration.Parameters[argument] switch
            {
                Sharpcraft.Common.Network.ChatTypeDecoration.Sender => senderName,
                Sharpcraft.Common.Network.ChatTypeDecoration.Content => content,
                Sharpcraft.Common.Network.ChatTypeDecoration.Target => target,
                _ => []
            });
        }

        return Sharpcraft.Common.Network.ComponentRuns.Decorate(
            _language,
            decoration.TranslationKey,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(arguments));
    }


    /// <summary>
    /// Reads a component that is a bare string instead of a compound.
    /// </summary>
    /// <param name="body">Bytes at the component.</param>
    /// <param name="runs">One run holding the text.</param>
    /// <remarks>
    /// The network form of a component omits the root name, so a plain string is a single string tag whose payload is
    /// the text. Reading only compounds dropped exactly this case.
    /// </remarks>
    private static bool TryReadBareTextComponent(ReadOnlySpan<byte> body, out Sharpcraft.Common.Network.TextRun[] runs)
    {
        runs = [];

        if (body.Length < 3 || body[0] != 8)
        {
            return false;
        }

        var length = (body[1] << 8) | body[2];

        if (length <= 0 || body.Length < 3 + length)
        {
            return false;
        }

        var text = System.Text.Encoding.UTF8.GetString(body.Slice(3, length));
        runs = [new Sharpcraft.Common.Network.TextRun(text, Sharpcraft.Common.Network.ComponentRuns.DefaultColour, false)];
        return true;
    }

    /// <summary>Joins runs into one string, for logging and for the diagnostic output.</summary>
    private static string Plain(IReadOnlyList<Sharpcraft.Common.Network.TextRun> runs)
    {
        var builder = new System.Text.StringBuilder();

        for (var index = 0; index < runs.Count; index++)
        {
            builder.Append(runs[index].Text);
        }

        return builder.ToString();
    }

    /// <summary>Joins runs into one string.</summary>
    private static string Plain(Sharpcraft.Common.Network.TextRun[] runs)
    {
        var builder = new System.Text.StringBuilder();

        for (var index = 0; index < runs.Length; index++)
        {
            builder.Append(runs[index].Text);
        }

        return builder.ToString();
    }

    /// <summary>Sends a submitted line as either a chat message or a command.</summary>
    /// <param name="message">Line the player submitted.</param>
    /// <param name="isCommand">True when it starts with a slash.</param>
    /// <summary>Opens a file the way the reference does, refusing paths that do not exist.</summary>
    /// <param name="path">Path carried by the click event.</param>
    private void OpenLinkedFile(string path)
    {
        if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
        {
            Console.WriteLine($"chat: open_file target does not exist ({path})");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Console.WriteLine($"chat: open_file failed ({exception.Message})");
        }
    }
    private void SendChat(string message, bool isCommand)
    {
        if (_live is null)
        {
            return;
        }

        // The slash marks the line as a command for the player; it is not part of the command on the wire.
        _live.Send(isCommand
            ? PlayPackets.BuildChatCommand(message[1..])
            : PlayPackets.BuildChatMessage(message, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    }

}
