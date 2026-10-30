// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using System.Numerics;
using Sharpcraft.Client.Camera;
using Sharpcraft.Client.Game;
using Sharpcraft.Client.Input;
using Sharpcraft.Client.Ui;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Entities;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Platform;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Launcher;

/// <summary>
/// Glue between the window, the input mapper, the player entity and the camera. Kept here rather than in
/// the client so the client stays free of window and renderer types
/// (docs/modules/input.md 3: platform capabilities live in the engine layer).
/// </summary>
internal sealed class InteractiveSession
{
    private readonly PlayerEntity _player;
    private readonly InputMapper _mapper = new();
    private readonly CameraController _camera = new();
    private readonly TickLoop _loop = new();
    private readonly PlayerInterpolator _interpolator;
    private readonly LivePlaySession? _live;
    private readonly ClientWorld? _world;
    private bool _streamingRequested;
    private bool _geometryDirty;
    private readonly ChunkWorkerPool? _pool;
    private readonly EntityTracker _entities = new();
    private readonly PlayerListRoster _playerList = new();
    private readonly ChatInput _chat = new();
    private readonly ChatLog _chatLog = new();
    private readonly Sharpcraft.Common.Localization.LanguageTable _language;
    private long _chatTicks;
    private long _chatOpenedAtMs;
    private readonly Sharpcraft.Client.Ui.ActionBar _actionBar = new();
    private readonly Sharpcraft.Client.Ui.Title _title = new();
    private bool _diagnosticChatSent;
    private readonly bool _diagEnabled = Environment.GetEnvironmentVariable("MC_DIAG") == "1";
    private int _diagFrames;

    /// <summary>
    /// Chunks of view distance requested from the server. It bounds how much terrain can be resident, which is
    /// what the geometry arena presizes for (docs/specs/network/play-movement.md 7.27).
    /// </summary>
    public const int ViewDistanceChunks = 8;

    /// <summary>Chunks a square view of <see cref="ViewDistanceChunks"/> can hold.</summary>
    public static int ExpectedChunkCount()
    {
        var side = (2 * ViewDistanceChunks) + 1;
        return side * side;
    }

    /// <summary>Decoded chunks installed per frame; keeps one batch from stalling the frame.</summary>
    private const int ParsedChunksPerFrame = 8;
    private int _jumpPressed;
    private int _jumpAccepted;
    private int _jumpInAir;
    private int _tickCounter;
    private readonly PlayerSync? _sync;
    private readonly Sharpcraft.Common.Network.ChatTypeTable? _chatTypes;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _last;

    private InteractiveSession(
        PlayerEntity player,
        LivePlaySession? live,
        ClientWorld? world,
        ChunkWorkerPool? pool,
        Sharpcraft.Common.Localization.LanguageTable? language = null,
        Sharpcraft.Common.Network.ChatTypeTable? chatTypes = null)
    {
        _chatTypes = chatTypes;
        _player = player;
        _language = language ?? Sharpcraft.Common.Localization.LanguageTable.Empty;
        _world = world;
        _pool = pool;
        _interpolator = new PlayerInterpolator(player, _camera.Yaw);
        _live = live;
        _sync = live is null ? null : new PlayerSync();
    }

    /// <summary>Creates a session with the player standing on the highest block under the view centre.</summary>
    /// <param name="world">World the player walks in.</param>
    /// <param name="request">Frame request, used for the spawn area and the vertex count.</param>
    public static InteractiveSession Create(
        ClientWorld world,
        FrameRequest request,
        LivePlaySession? live = null,
        ChunkWorkerPool? pool = null,
        Sharpcraft.Common.Localization.LanguageTable? language = null,
        Sharpcraft.Common.Network.ChatTypeTable? chatTypes = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);

        var adapter = new EntityWorldAdapter(world);
        var spawn = FindSpawn(world, request);
        var session = new InteractiveSession(new PlayerEntity(adapter, spawn), live, world, pool, language, chatTypes);
        session.PrepareDebugPrompt();
        return session;
    }

    /// <summary>
    /// True once and then false: reports that a block changed since the last call, so the renderer knows to
    /// rebuild the geometry (docs/specs/network/play-movement.md 7.17).
    /// </summary>
    /// <summary>Diagnostic counters: jump ticks seen, accepted on the ground, and ignored while airborne.</summary>
    public (int Pressed, int Accepted, int InAir) JumpStats => (_jumpPressed, _jumpAccepted, _jumpInAir);

    /// <summary>Entities the server told us about, updated as packets arrive.</summary>
    public EntityTracker Entities => _entities;

    /// <summary>The player list the server described.</summary>
    public PlayerListRoster PlayerList => _playerList;

    /// <summary>The chat prompt.</summary>
    public ChatInput Chat => _chat;

    /// <summary>The chat log on screen.</summary>
    public ChatLog ChatLog => _chatLog;

    /// <summary>Ticks elapsed, used by the chat fade.</summary>
    public long ChatTicks => _chatTicks;

    /// <summary>Height the interface was last drawn at, needed to know where the history sits.</summary>
    public float CanvasHeight { get; set; }

    /// <summary>
    /// Runs a chat click at a point, following the reference: the right button acts, and holding shift inserts instead.
    /// </summary>
    /// <param name="x">Pointer x in canvas pixels.</param>
    /// <param name="y">Pointer y in canvas pixels.</param>
    /// <param name="shift">True when shift is held, which turns the click into an insertion.</param>
    /// <param name="window">Window that owns the clipboard, used by the copy action.</param>
    /// <returns>True when the click landed on an interactive run.</returns>
    /// <remarks>
    /// The reference resolves the style under the pointer and then acts on its click event, so the sequence here is the
    /// same: find the entry, find the run, then act. An action this client cannot perform is reported rather than
    /// silently ignored.
    /// </remarks>
    public bool TryClickChat(float x, float y, bool shift, Sharpcraft.Framework.Platform.WindowHost? window = null)
    {
        if (UiFont is not { } font || CanvasHeight <= 0f || _chatLog.Count == 0)
        {
            return false;
        }

        var bottom = CanvasHeight - Sharpcraft.Client.Ui.ChatLog.BottomMargin;
        var entry = Sharpcraft.Client.Ui.ChatHitTest.EntryAt(
            x, y, Sharpcraft.Client.Ui.ChatLog.LeftInset, Sharpcraft.Client.Ui.ChatLog.ColumnWidth,
            bottom, Sharpcraft.Client.Ui.ChatLog.EntryHeight, Sharpcraft.Client.Ui.ChatLog.LinesPerPage,
            _chatLog.Count, _chatLog.Scroll.Offset);

        if (entry < 0)
        {
            return false;
        }

        var index = _chatLog.Count - 1 - entry;
        var runs = _chatLog.RunsAt(index);

        if (runs.Length == 0)
        {
            return false;
        }

        var widths = new float[runs.Length];

        for (var run = 0; run < runs.Length; run++)
        {
            widths[run] = font.MeasureWidth(runs[run].Text);
        }

        var hit = Sharpcraft.Client.Ui.ChatHitTest.RunAt(x, Sharpcraft.Client.Ui.ChatLog.MessageIndent, widths);

        if (hit < 0)
        {
            return false;
        }

        var click = _chatLog.ClickFor(index, hit);

        if (click is null)
        {
            return false;
        }

        if (shift)
        {
            // Holding shift turns the click into an insertion, using the text the style carries for that purpose.
            if (click.Insertion is { Length: > 0 } insertionText)
            {
                _chat.Open();
                _chat.Append(insertionText.AsSpan());
            }

            return true;
        }

        switch (click.Action)
        {
            case Sharpcraft.Common.Network.ChatClickAction.OpenUrl:
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(click.Value)
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    if (_diagEnabled)
                    {
                        Console.WriteLine($"chat open url failed: {exception.Message}");
                    }
                }

                return true;

            case Sharpcraft.Common.Network.ChatClickAction.RunCommand:
                SendChat(click.Value, isCommand: true);
                return true;

            case Sharpcraft.Common.Network.ChatClickAction.SuggestCommand:
                _chat.Open(click.Value);
                return true;

            case Sharpcraft.Common.Network.ChatClickAction.CopyToClipboard:
                if (window is null)
                {
                    if (_diagEnabled)
                    {
                        Console.WriteLine("chat copy: no window to copy through");
                    }

                    return true;
                }

                window.ClipboardText = click.Value;
                return true;

            default:
                return false;
        }
    }

    /// <summary>Window handed to the probe so it can run a click, set when the interface is created.</summary>
    private Sharpcraft.Framework.Platform.WindowHost? _probeWindow;

    /// <summary>Remembers the window for the probe.</summary>
    /// <param name="window">Window that owns the clipboard.</param>
    public void AttachProbeWindow(Sharpcraft.Framework.Platform.WindowHost window) => _probeWindow = window;

    /// <summary>
    /// Reports what a point resolves to, so an unattended run can prove the hit test without moving a mouse.
    /// </summary>
    /// <param name="x">Pointer x in canvas pixels.</param>
    /// <param name="y">Pointer y in canvas pixels.</param>
    public void ProbeChat(float x, float y)
    {
        var hover = ChatHoverAt(x, y);
        Console.WriteLine(hover is null
            ? $"chat probe ({x},{y}): no hover"
            : $"chat probe ({x},{y}): hover='{Plain(hover)}'");

        if (UiFont is not { } font || CanvasHeight <= 0f || _chatLog.Count == 0)
        {
            return;
        }

        var bottom = CanvasHeight - Sharpcraft.Client.Ui.ChatLog.BottomMargin;
        var entry = Sharpcraft.Client.Ui.ChatHitTest.EntryAt(
            x, y, Sharpcraft.Client.Ui.ChatLog.LeftInset, Sharpcraft.Client.Ui.ChatLog.ColumnWidth,
            bottom, Sharpcraft.Client.Ui.ChatLog.EntryHeight, Sharpcraft.Client.Ui.ChatLog.LinesPerPage,
            _chatLog.Count, _chatLog.Scroll.Offset);

        if (entry < 0)
        {
            return;
        }

        var index = _chatLog.Count - 1 - entry;
        var runs = _chatLog.RunsAt(index);
        var widths = new float[runs.Length];

        for (var run = 0; run < runs.Length; run++)
        {
            widths[run] = font.MeasureWidth(runs[run].Text);
        }

        var hit = Sharpcraft.Client.Ui.ChatHitTest.RunAt(x, Sharpcraft.Client.Ui.ChatLog.MessageIndent, widths);
        var click = _chatLog.ClickFor(index, hit);
        Console.WriteLine($"chat probe: entry={entry} line={index} run={hit} click={(click is null ? "none" : click.Action + ":" + click.Value)}");

        if (click is not null && Environment.GetEnvironmentVariable("MC_CHAT_PROBE_CLICK") == "1")
        {
            var shift = Environment.GetEnvironmentVariable("MC_CHAT_PROBE_SHIFT") == "1";
            var handled = TryClickChat(x, y, shift);
            Console.WriteLine($"chat probe: shift={shift} acted={handled} prompt='{_chat.Text}' open={_chat.IsOpen}");
        }
    }

    /// <summary>
    /// Hover runs under a point, or null when the pointer is not over an interactive run.
    /// </summary>
    /// <param name="x">Pointer x in canvas pixels.</param>
    /// <param name="y">Pointer y in canvas pixels.</param>
    public Sharpcraft.Common.Network.TextRun[]? ChatHoverAt(float x, float y)
    {
        if (UiFont is not { } font || CanvasHeight <= 0f || _chatLog.Count == 0)
        {
            return null;
        }

        var bottom = CanvasHeight - Sharpcraft.Client.Ui.ChatLog.BottomMargin;
        var entry = Sharpcraft.Client.Ui.ChatHitTest.EntryAt(
            x, y, Sharpcraft.Client.Ui.ChatLog.LeftInset, Sharpcraft.Client.Ui.ChatLog.ColumnWidth,
            bottom, Sharpcraft.Client.Ui.ChatLog.EntryHeight, Sharpcraft.Client.Ui.ChatLog.LinesPerPage,
            _chatLog.Count, _chatLog.Scroll.Offset);

        if (entry < 0)
        {
            return null;
        }

        var index = _chatLog.Count - 1 - entry;
        var runs = _chatLog.RunsAt(index);

        if (runs.Length == 0)
        {
            return null;
        }

        var widths = new float[runs.Length];

        for (var run = 0; run < runs.Length; run++)
        {
            widths[run] = font.MeasureWidth(runs[run].Text);
        }

        var hit = Sharpcraft.Client.Ui.ChatHitTest.RunAt(x, Sharpcraft.Client.Ui.ChatLog.MessageIndent, widths);

        return hit < 0 ? null : _chatLog.HoverFor(index, hit);
    }

    /// <summary>The transient line above the hotbar, which is where overlay chat goes.</summary>
    public Sharpcraft.Client.Ui.ActionBar ActionBar => _actionBar;

    /// <summary>The large centred title, which the title command shows.</summary>
    public Sharpcraft.Client.Ui.Title Title => _title;

    /// <summary>Milliseconds since the prompt was opened, which drives the caret blink.</summary>
    public long ChatElapsedMs => Environment.TickCount64 - _chatOpenedAtMs;

    /// <summary>
    /// Opens the prompt with text already selected, so a screenshot can show the selection without typing.
    /// </summary>
    /// <remarks>
    /// Driven by <c>MC_CHAT_DEBUG_TEXT</c>. It exists because the only way to check a selection's colour against the
    /// reference is to see it, and typing into a window from an automated check is not possible.
    /// </remarks>
    private void PrepareDebugPrompt()
    {
        var text = Environment.GetEnvironmentVariable("MC_CHAT_DEBUG_TEXT");

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _chat.Open();
        _chat.Append(text.AsSpan());
        _chat.Model.SelectAll();
        _chatOpenedAtMs = Environment.TickCount64;
    }

    /// <summary>Glyph metrics, needed to turn a click into a caret position.</summary>
    public Sharpcraft.Client.Ui.BitmapFont? UiFont { get; set; }

    /// <summary>Fraction into the current tick, for callers that interpolate between ticks.</summary>
    public float PartialTick => _loop.Alpha;

    /// <summary>Entity render states interpolated for this frame.</summary>
    /// <param name="alpha">Fraction into the current tick.</param>
    public ReadOnlySpan<EntityRenderState> EntityStates(float alpha) => _entities.Interpolate(alpha);

    public bool TakeGeometryDirty()
    {
        if (!_geometryDirty)
        {
            return false;
        }

        _geometryDirty = false;
        return true;
    }

    /// <summary>Player being simulated.</summary>
    public PlayerEntity Player => _player;


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

        var line = Environment.GetEnvironmentVariable("MC_CHAT_SAY");

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

    /// <summary>
    /// Advances the simulation from the current input and returns the camera for this frame.
    /// </summary>
    /// <param name="window">Window whose input state is read.</param>
    public (Vector3 Eye, Vector3 Target) UpdateCamera(WindowHost window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Chat runs before movement so a key that opened the prompt cannot also move the player on the same frame.
        UpdateChat(window);
        MaybeSendDiagnosticChat();

        var frame = _mapper.Map(window.Input);

        if (_chat.IsOpen)
        {
            // While typing, the player does not move or look: the reference gives the screen the keyboard, and a
            // stray step while writing a message is the sort of thing that gets people killed.
            frame = FrameInput.None with { LookDelta = Vector2.Zero };
        }

        // Verification hook: walk forward without a keyboard so an unattended run exercises the network path.
        if (Environment.GetEnvironmentVariable("MC_WINDOW_AUTOWALK") == "1")
        {
            frame = frame with { Forward = 1f };
        }
        // Objective record of the input state, so a capture problem can be diagnosed from the log instead of from
        // impressions (docs/modules/input.md 7).
        if (_diagEnabled && (_diagFrames++ % 60) == 0)
        {
            Console.WriteLine(
                $"look diag: captured={window.Input.MouseCaptured} focus={window.Input.HasFocus} "
                + $"delta=({frame.LookDelta.X:F2},{frame.LookDelta.Y:F2}) release={frame.ReleaseCursor}");
        }

        _camera.ApplyLook(frame.LookDelta, _mapper.LookSensitivity);

        var now = _clock.Elapsed;
        var elapsed = now - _last;
        _last = now;

        EnsureStreaming();
        DrainIncoming();
        ApplyParsedChunks();

        _loop.Advance(
            elapsed,
            () =>
            {
                if (frame.Jump)
                {
                    _jumpPressed++;
                    if (_player.OnGround)
                    {
                        _jumpAccepted++;
                    }
                    else
                    {
                        _jumpInAir++;
                    }
                }

                _tickCounter++;

                // Verification hook: print the tracked entities so their positions can be compared against the
                // other client's own report of where it is (docs/specs/network/entity-sync.md 7).
                if (Environment.GetEnvironmentVariable("MC_ENTITY_LOG") == "1" && _tickCounter % 20 == 0)
                {
                    var states = _entities.Interpolate(0f);
                    var self = _player.Position;
                    var stamp = Environment.TickCount64;
                    Console.WriteLine(
                        $"entities: {states.Length} tracked at={stamp} self=({self.X:F3},{self.Y:F3},{self.Z:F3})");

                    foreach (var state in states)
                    {
                        Console.WriteLine(
                            $"entity {state.Id} type={state.TypeId} at={stamp} "
                            + $"pos=({state.X:F3},{state.Y:F3},{state.Z:F3}) yaw={state.Yaw:F1}");
                    }
                }

                // Console writes block for tens of milliseconds on Windows, and this runs on the tick thread,
                // where a stall shows up as a hitch once per interval. Diagnostics are therefore opt in, so the
                // shipping path never performs I/O while ticking.
                if (Environment.GetEnvironmentVariable("MC_DIAG") == "1" && _tickCounter % 40 == 0)
                {
                    var (pressed, accepted, inAir) = JumpStats;
                    Console.WriteLine(
                        $"diag: tick={_tickCounter} fwd={frame.Forward:F0} sprint={frame.Sprint} sneak={frame.Sneak} "
                        + $"jump presses={pressed} accepted={accepted} inAir={inAir} ground={_player.OnGround} "
                        + $"y={_player.Position.Y:F2} vy={_player.DeltaMovement.Y:F3}");
                }

                // Diagnostic hook: sweep the pitch so the head direction can be checked from a second client
                // without driving two windows at once.
                if (Environment.GetEnvironmentVariable("MC_CAMERA_SWEEP") == "1")
                {
                    var sweep = MathF.Sin(_tickCounter * 0.05f * 0.9f) * (80f * (MathF.PI / 180f));
                    _camera.SetLook(_camera.Yaw, sweep);
                }

                _player.Tick(frame.Forward, frame.Strafe, frame.Jump, frame.Sprint, _camera.Yaw, frame.Sneak);

                // Remote entities approach the position the server reported over a few ticks, so the tracker
                // has to advance once per tick (docs/specs/network/entity-sync.md 9.4).
                _entities.Tick();
            _chatTicks++;
        _actionBar.Tick();
        _title.Tick();
                _interpolator.Push(_player, _camera.Yaw);
                PublishTick();
            });

        if (frame.ReleaseCursor && window.Input.MouseCaptured)
        {
            // Escape hands the cursor back; the camera stops following because the input layer only accumulates
            // movement while captured.
            window.SetMouseCaptured(false);
        }

        // Frames arrive faster than ticks; drawing the interpolated eye is what keeps walking smooth.
        var eye = _interpolator.Eye(_loop.Alpha);
        return (eye, _camera.Target(eye));
    }

    /// <summary>Captures the mouse once, and writes the diagnostic title.</summary>
    /// <param name="window">Window to configure.</param>
    /// <param name="first">True on the first frame.</param>
    public void CaptureMouse(WindowHost window, bool first)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Capture on the first frame only when the window already has focus, and afterwards only when the player
        // clicks inside it. Capturing without focus would lock the pointer into a window they are not using.
        // While the chat prompt is open the click belongs to the prompt: it places the caret, so it must not be
        // swallowed here, and the pointer must not be captured either or the player could not type comfortably.
        if (!_chat.IsOpen && !window.Input.MouseCaptured && window.Input.HasFocus && (first || window.Input.TakeClick()))
        {
            window.SetMouseCaptured(true);
        }

        var position = _player.Position;
        // The process id is in the title so that, when several copies are running, it is obvious which
        // window is which.
        window.Title =
            $"Sharpcraft #{Environment.ProcessId} | {position.X:F1} {position.Y:F1} {position.Z:F1} | " +
            $"{(_player.OnGround ? "ground" : "air")} | {window.Input.PressedKeys.Count} keys";
    }

    private static Vector3 FindSpawn(ClientWorld world, FrameRequest request)
    {
        // Drop onto the highest solid block near the centre of what will be visible.
        var centre = new Vector3(
            (request.CameraTarget.X + request.CameraPosition.X) / 2f,
            0f,
            (request.CameraTarget.Z + request.CameraPosition.Z) / 2f);

        var x = MathF.Floor(centre.X);
        var z = MathF.Floor(centre.Z);

        for (var y = 319; y > -64; y--)
        {
            if (world.GetBlockState((int)x, y, (int)z) != 0)
            {
                return new Vector3(x + 0.5f, y + 1f, z + 0.5f);
            }
        }

        return new Vector3(x + 0.5f, 1f, z + 0.5f);
    }

    /// <summary>
    /// Sends this tick's movement and the end-of-tick marker. The marker is not optional: the server resets
    /// its per-tick movement guards there and disconnects a client whose second position packet arrives
    /// without it (docs/specs/network/play-movement.md 6.5).
    /// </summary>
    /// <summary>
    /// Tells the server how far we want to see, then asks for the first chunk batch. Without this the server
    /// never streams terrain and the player walks off the end of whatever was downloaded at join
    /// (docs/specs/network/play-movement.md 7.12).
    /// </summary>
    private void EnsureStreaming()
    {
        if (_live is null || _streamingRequested)
        {
            return;
        }

        _streamingRequested = true;
        _live.Send(PlayPackets.BuildClientInformation(
            locale: "en_us",
            viewDistance: ViewDistanceChunks,
            chatVisibility: 0,
            chatColors: true,
            skinParts: 0x7F,
            mainHand: 1,
            textFiltering: false,
            allowListing: true));
        _live.Send(PlayPackets.BuildChunkBatchReceived(8f));
    }

    /// <summary>
    /// Adopts the rotation the server just sent. Vanilla sets yaw and pitch outright on a teleport, including
    /// the join teleport, so a client that keeps its own heading after joining reports a facing the server
    /// never gave it (docs/specs/network/play-movement.md 7.14).
    /// </summary>
    private void AdoptServerRotation(ServerPositionOutcome outcome) =>
        AdoptRotation(outcome.YawDegrees, outcome.PitchDegrees);

    /// <summary>Adopts a facing the server dictated, converting degrees to the camera's radians.</summary>
    /// <param name="yawDegrees">Yaw in degrees.</param>
    /// <param name="pitchDegrees">Pitch in degrees.</param>
    private void AdoptRotation(float yawDegrees, float pitchDegrees)
    {
        _camera.SetLook(LookConvention.FromWireYawDegrees(yawDegrees), LookConvention.FromWirePitchDegrees(pitchDegrees));
    }

    private void PublishTick()
    {
        if (_live is null || _sync is null || !_live.IsAlive)
        {
            return;
        }

        var yawDegrees = LookConvention.ToWireYawDegrees(_camera.Yaw);
        var pitchDegrees = LookConvention.ToWirePitchDegrees(_camera.Pitch);

        if (_sync.Build(_sync.Decide(_player, yawDegrees, pitchDegrees), _player, yawDegrees, pitchDegrees) is { } packet)
        {
            _live.Send(packet);
        }

        _live.Send(PlayPackets.BuildClientTickEnd());
    }

    /// <summary>
    /// Applies everything the server sent since the previous frame: keep-alives, pings, position
    /// corrections and a disconnect. Runs on the render thread so the player is never touched concurrently.
    /// </summary>
    /// <summary>
    /// Installs the chunks the pool decoded, a few per frame so a burst cannot stall the frame. Installing is
    /// the only world mutation here, and it happens on the game thread.
    /// </summary>
    private void ApplyParsedChunks()
    {
        if (_pool is null || _world is null)
        {
            return;
        }

        for (var installed = 0; installed < ParsedChunksPerFrame && _pool.TryTakeParsed(out var parsed); installed++)
        {
            _world.AddChunk(parsed);
            _geometryDirty = true;
        }
    }

    private void DrainIncoming()
    {
        if (_live is null)
        {
            return;
        }

        while (_live.TryDequeue(out var body))
        {
            var reader = new PayloadReader(body);
            if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success)
            {
                continue;
            }

            switch (packetId)
            {
                case PacketIds.PlayClientbound.KeepAlive:
                    reader.TryReadLong(out var keepAliveId);
                    _live.Send(PlayPackets.BuildKeepAliveReply(keepAliveId));
                    break;

                case PacketIds.PlayClientbound.Ping:
                    reader.TryReadInt(out var pingId);
                    _live.Send(PlayPackets.BuildPongReply(pingId));
                    break;

                case PacketIds.PlayClientbound.PlayerPosition:
                    if (_sync is not null &&
                        PlayMovementPackets.TryReadPlayerPosition(body[reader.Position..], out var correction) == DecodeStatus.Success &&
                        correction is not null)
                    {
                        var outcome = _sync.ApplyServerPosition(_player, correction, isTeleport: true);
                        AdoptServerRotation(outcome);
                        _interpolator.Snap(_player, _camera.Yaw);

                        if (outcome.TeleportId is { } teleportId)
                        {
                            _live.Send(PlayMovementPackets.BuildAcceptTeleportation(
                                teleportId,
                                _player.Position.X,
                                _player.Position.Y,
                                _player.Position.Z,
                                outcome.YawDegrees,
                                outcome.PitchDegrees));
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.LevelChunkWithLight:
                    if (_world is null)
                    {
                        break;
                    }

                    // Decoding a chunk is the expensive half of streaming, so it runs on the pool. The world
                    // itself is only ever written from this thread, which then installs the decoded chunk
                    // (docs/specs/network/play-movement.md 7.24).
                    if (_pool is not null && _pool.TryEnqueueParse(body))
                    {
                        break;
                    }

                    if (ChunkPacketParser.TryRead(body, _world.SectionCount, _world.MinSectionY, airState: 0, out var parsed) == DecodeStatus.Success &&
                        parsed is not null)
                    {
                        _world.AddChunk(parsed);
                        _geometryDirty = true;
                    }

                    break;


                case PacketIds.PlayClientbound.PlayerChat:
                    if (PlayerChatPackets.TryReadPlayerChat(
                            body[reader.Position..], out var chatSender, out var chatMessage, out var chatSigned,
                            out var chatTypeId, out var chatTarget, _language) == DecodeStatus.Success)
                    {
                        // The shape of a player message comes from the chat type registry, never from an assumption:
                        // the same packet renders as <name> text, name >> text or [name -> target] text depending on
                        // the entry the server named.
                        var decorated = DecorateLine(chatTypeId, chatSender, chatMessage, chatTarget);

                        if (decorated is null)
                        {
                            break;
                        }

                        // A message without a signature comes from a server that does not sign chat; the reference
                        // marks those as not secure, which shows the same pale bar a system line gets.
                        _chatLog.Add(
                            decorated,
                            _chatTicks,
                            chatSigned ? Sharpcraft.Client.Ui.ChatLog.MessageTag.None : Sharpcraft.Client.Ui.ChatLog.MessageTag.NotSecure);

                        if (_diagEnabled)
                        {
                            Console.WriteLine($"chat received [type {chatTypeId}]: {Plain(decorated)}");
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.DisguisedChat:
                    if (DisguisedChatPackets.TryRead(
                            body[reader.Position..], out var disguisedText, out var disguisedType,
                            out var disguisedName, out var disguisedTarget, _language) == DecodeStatus.Success)
                    {
                        // The server is speaking on someone's behalf, which is what /me produces. The reference tags
                        // these as a system message, so they carry the same pale bar.
                        var decoration = DecorateLine(disguisedType, disguisedName, disguisedText, disguisedTarget);

                        if (decoration is not null)
                        {
                            _chatLog.Add(decoration, _chatTicks, Sharpcraft.Client.Ui.ChatLog.MessageTag.System);

                            if (_diagEnabled)
                            {
                                Console.WriteLine($"chat received [disguised type {disguisedType}]: {Plain(decoration)}");
                            }
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.DeleteChat:
                    if (DeleteChatPackets.TryRead(body[reader.Position..], out var deleted) == DecodeStatus.Success)
                    {
                        // The instruction names a signed message, and nothing is signed on a server in offline mode, so
                        // there is nothing here it can match. Recorded rather than pretended: removing a line by
                        // signature needs the client to keep the signatures it was shown, which it does not yet.
                        if (_diagEnabled)
                        {
                            Console.WriteLine($"chat delete ignored: no signature tracking ({deleted.Length} bytes)");
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.SetTitleText:
                case PacketIds.PlayClientbound.SetSubtitleText:
                    if (ChatPackets.TryReadComponentRuns(body[reader.Position..], out var titleRuns, _language))
                    {
                        if (_diagEnabled)
                        {
                            Console.WriteLine($"title received [{(packetId == PacketIds.PlayClientbound.SetTitleText ? "title" : "subtitle")}]: {Plain(titleRuns)}");
                        }

                        if (packetId == PacketIds.PlayClientbound.SetTitleText)
                        {
                            _title.Show(titleRuns);
                        }
                        else
                        {
                            _title.ShowSubtitle(titleRuns);
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.ClearTitles:
                    _title.Clear();
                    break;

                case PacketIds.PlayClientbound.SetActionBarText:
                    // A component may be a bare string, so the same reader serves titles and the action bar.
                    if (ChatPackets.TryReadComponentRuns(body[reader.Position..], out var actionBarRuns, _language))
                    {
                        _actionBar.Set(actionBarRuns);

                        if (_diagEnabled)
                        {
                            Console.WriteLine($"action bar received: {Plain(actionBarRuns)}");
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.SystemChat:
                    // The style is read beside the runs because that is what makes a message clickable: the reference
                    // resolves the style under the pointer, so the click has to travel with the run that owns it.
                    if (ChatPackets.TryReadSystemChatStyled(
                            body[reader.Position..], out var chatRuns, out var chatClicks, out var chatHovers,
                            out var overlay, _language) == DecodeStatus.Success)
                    {

                        // The overlay flag means "show this above the hotbar rather than in the history".
                        if (overlay)
                        {
                            _actionBar.Set(chatRuns);
                        }
                        else
                        {
                            // A system line is what the reference tags with its pale indicator bar.
                            _chatLog.Add(
                                chatRuns,
                                _chatTicks,
                                Sharpcraft.Client.Ui.ChatLog.MessageTag.System,
                                chatClicks,
                                chatHovers);

                            if (_diagEnabled)
                            {
                                Console.WriteLine($"chat received [system]: {Plain(chatRuns)}");
                            }
                        }
                    }
                    else if (_diagEnabled)
                    {
                        Console.WriteLine($"system_chat parse failed: {ChatPackets.TryReadSystemChatStyled(body[reader.Position..], out _, out _, out _, out _, _language)}, hex={Convert.ToHexString(body.AsSpan(reader.Position, Math.Min(14, body.Length - reader.Position)))}");
                    }

                    break;

                case PacketIds.PlayClientbound.PlayerInfoUpdate:
                    if (PlayerListPackets.TryReadPlayerInfoUpdate(body[reader.Position..], out _, out var profileUpdates) ==
                        DecodeStatus.Success)
                    {
                        _playerList.Apply(profileUpdates);
                    }

                    break;

                case PacketIds.PlayClientbound.PlayerInfoRemove:
                    if (PlayerListPackets.TryReadPlayerInfoRemove(body[reader.Position..], out var removedProfiles) ==
                        DecodeStatus.Success)
                    {
                        _playerList.Remove(removedProfiles);
                    }

                    break;

                case PacketIds.PlayClientbound.AddEntity:
                    if (EntityPackets.TryReadAddEntity(body[reader.Position..], out var added) == DecodeStatus.Success &&
                        added is not null)
                    {
                        _entities.Add(added);
                    }

                    break;

                case PacketIds.PlayClientbound.RemoveEntities:
                    if (EntityPackets.TryReadRemoveEntities(body[reader.Position..], out var removedIds) == DecodeStatus.Success)
                    {
                        _entities.Remove(removedIds);
                    }

                    break;

                case PacketIds.PlayClientbound.MoveEntityPos:
                    if (EntityPackets.TryReadMoveEntity(body[reader.Position..], hasPosition: true, hasRotation: false, out var moved) == DecodeStatus.Success)
                    {
                        _entities.ApplyMove(moved);
                    }

                    break;

                case PacketIds.PlayClientbound.MoveEntityPosRot:
                    if (EntityPackets.TryReadMoveEntity(body[reader.Position..], hasPosition: true, hasRotation: true, out var movedRot) == DecodeStatus.Success)
                    {
                        _entities.ApplyMove(movedRot);
                    }

                    break;

                case PacketIds.PlayClientbound.MoveEntityRot:
                    if (EntityPackets.TryReadMoveEntity(body[reader.Position..], hasPosition: false, hasRotation: true, out var rotated) == DecodeStatus.Success)
                    {
                        _entities.ApplyMove(rotated);
                    }

                    break;

                case PacketIds.PlayClientbound.RotateHead:
                    if (EntityPackets.TryReadRotateHead(body[reader.Position..], out var headId, out var headYaw) == DecodeStatus.Success)
                    {
                        _entities.ApplyHeadYaw(headId, headYaw);
                    }

                    break;

                case PacketIds.PlayClientbound.SetEntityMotion:
                    if (EntityPackets.TryReadSetEntityMotion(body[reader.Position..], out var motion) == DecodeStatus.Success)
                    {
                        _entities.ApplyMotion(motion);
                    }

                    break;

                case PacketIds.PlayClientbound.TeleportEntity:
                    if (EntityPackets.TryReadTeleportEntity(body[reader.Position..], out var teleport) == DecodeStatus.Success)
                    {
                        _entities.ApplyTeleport(teleport);
                    }

                    break;

                case PacketIds.PlayClientbound.BlockUpdate:
                    if (_world is not null &&
                        BlockUpdatePackets.TryReadBlockUpdate(body[reader.Position..], out var block) == DecodeStatus.Success)
                    {
                        _world.SetBlockState(block.X, block.Y, block.Z, block.State);
                        _geometryDirty = true;
                    }

                    break;

                case PacketIds.PlayClientbound.SectionBlocksUpdate:
                    if (_world is not null &&
                        BlockUpdatePackets.TryReadSectionBlocksUpdate(body[reader.Position..], out var blocks) == DecodeStatus.Success)
                    {
                        foreach (var change in blocks)
                        {
                            _world.SetBlockState(change.X, change.Y, change.Z, change.State);
                        }

                        _geometryDirty = true;
                    }

                    break;

                case PacketIds.PlayClientbound.PlayerRotation:
                    // Server-driven turning, for example a forced facing: the facing is authoritative.
                    if (_sync is not null &&
                        PlayMovementPackets.TryReadPlayerRotation(body[reader.Position..], out var rotation) == DecodeStatus.Success &&
                        rotation is not null)
                    {
                        var (yaw, pitch) = _sync.ApplyServerRotation(rotation);
                        AdoptRotation(yaw, pitch);
                    }

                    break;

                case PacketIds.PlayClientbound.ChunkBatchFinished:
                    // The server waits for the acknowledgement before sending the next batch.
                    _live.Send(PlayPackets.BuildChunkBatchReceived(8f));
                    break;

                case PacketIds.PlayClientbound.Disconnect:
                    return;

                default:
                    // Temporary: an unhandled id is worth seeing when a behaviour is missing, rather than guessing
                    // which id the server used for it.
                    if (_diagEnabled)
                    {
                        Console.WriteLine($"unhandled packet id={packetId} bytes={body.Length}");
                    }
                    else if (Environment.GetEnvironmentVariable("MC_PACKET_TRACE") == "1")
                    {
                        Console.WriteLine($"unhandled packet id={packetId} bytes={body.Length}");
                    }

                    break;
            }
        }
    }
}
