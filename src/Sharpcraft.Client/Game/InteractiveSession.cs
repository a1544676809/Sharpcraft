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

/// <summary>
/// Glue between the window, the input mapper, the player entity and the camera. Kept here rather than in
/// the client so the client stays free of window and renderer types
/// (docs/modules/input.md 3: platform capabilities live in the engine layer).
/// </summary>
internal sealed partial class InteractiveSession
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
    private Sharpcraft.Common.Localization.LanguageTable _language;

    /// <summary>
    /// The message table. Settable because a resource reload replaces it: chat already on screen keeps the text it was
    /// drawn with, and messages that arrive afterwards use the new table, which is what a reload means for text.
    /// </summary>
    public Sharpcraft.Common.Localization.LanguageTable Language
    {
        get => _language;
        set => _language = value ?? Sharpcraft.Common.Localization.LanguageTable.Empty;
    }
    private long _chatTicks;
    private long _chatOpenedAtMs;
    private readonly Sharpcraft.Client.Ui.ActionBar _actionBar = new();
    private readonly Sharpcraft.Client.Ui.Title _title = new();
    private bool _diagnosticChatSent;
    private bool _playerLoadedSent;
    private bool _respawnRequested;

    // True once the server has placed us. Until then the client must not simulate: with no position it would fall
    // through an empty world while the server is still generating, which reads as the player dropping into the void.
    private bool _hasServerPosition;
    private readonly System.Diagnostics.Stopwatch _installClock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>Chunks installed into the world so far, i.e. data that has arrived from the server.</summary>
    public int ChunksInstalled { get; private set; }

    /// <summary>Seconds since the session started when the last chunk was installed.</summary>
    public double LastInstallSeconds { get; private set; }

    /// <summary>Seconds since the session started when the first chunk was installed.</summary>
    public double FirstInstallSeconds { get; private set; }

    /// <summary>Number of ticks this session has run, used by the host speed readout.</summary>
    public long TickIndex { get; private set; }
    private readonly bool _diagEnabled = GameOptions.Get("MC_DIAG") == "1";
    private int _selectedHotbarSlot;
    private float _experienceProgress;
    private int _totalExperience;
    private int _experienceLevel;
    private bool _canFly, _flying, _invulnerable, _instabuild;
    private float _flyingSpeed = 0.05f, _walkingSpeed = 0.1f;
    private int _simulationDistance;
    private readonly System.Collections.Generic.Dictionary<string, int> _cooldowns = new(StringComparer.Ordinal);
    private int _lastDamageEntityId;
    private int _lastHurtEntityId;
    private float _lastHurtYaw;
    private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>> _mobEffects = new();
    private int _chunkCacheCenterX;
    private int _chunkCacheCenterZ;
    private int _chunkCacheRadius;
    private readonly System.Collections.Generic.Dictionary<long, int> _blockDestruction = new();
    private int _lastBlockEventX, _lastBlockEventY, _lastBlockEventZ, _lastBlockEventB0, _lastBlockEventB1, _lastBlockEventBlock;
    private int _cameraEntityId;
    private int _lastTakeItemId, _lastCollectorId, _lastTakeAmount;
    private int _lastProjectileEntity;
    private double _lastProjectilePower;
    private int _diagFrames;
    private readonly bool _packetLog = GameOptions.Get("MC_PACKET_LOG") == "1";
    private int _packetLogCount;
    private const int PacketLogLimit = 400;
    private readonly Sharpcraft.Client.Render.BlockLightFlicker _blockLightFlicker = new();
    private Sharpcraft.Common.World.BiomeRegistry? _biomes;
    private Sharpcraft.Client.Assets.CloudTexture? _cloudTexture;
    private readonly Sharpcraft.Client.Render.CloudDrawCache _cloudDrawCache = new();
    private bool _cloudsEnabled = true;
    private bool _cloudsFancy = true;
    private int _cloudRangeChunks = 128;
    private bool _cloudUseOit = true;
    private bool _cloudAdaptive;
    private int _adaptiveCloudRange = Sharpcraft.Client.Render.CloudRangePolicy.Maximum;
    private double _frameMsAccumulator;
    private int _frameSamples;
    private int _framesSinceRangeChange;

    /// <summary>
    /// Requested render distance in chunks. Host configuration (MC_VIEW_DISTANCE), default 12; the server may cap it and
    /// the effective radius arrives in JoinGame. Kept in the host so game assemblies read no environment (principle 13).
    /// </summary>
    public static int ViewDistanceChunks { get; } = ReadViewDistanceChunks();

    private static int ReadViewDistanceChunks()
    {
        var raw = GameOptions.Get("MC_VIEW_DISTANCE");
        return int.TryParse(raw, out var parsed) ? Math.Clamp(parsed, 2, 32) : 12;
    }

    /// <summary>Chunks a square view of <see cref="ViewDistanceChunks"/> can hold.</summary>
    public static int ExpectedChunkCount()
    {
        var side = (2 * ViewDistanceChunks) + 1;
        return side * side;
    }

    /// <summary>
    /// The lightmap uniform for the current frame: the world's day/night parameters with the block factor carrying the
    /// torches' flicker (docs/analysis/render/lighting-color-space.md #8). Rewritten every frame so day/night and flicker
    /// need no re-mesh.
    /// </summary>
    internal LightmapUniform CurrentLightmap()
    {
        if (_world is null)
        {
            return LightmapUniform.Day;
        }

        var parameters = FrameCommand.EffectiveLighting(_world) with { BlockFactor = _blockLightFlicker.BlockFactor };
        return FrameCommand.ToLightmapUniform(parameters);
    }

    /// <summary>
    /// The fog colour for the current frame: the base overworld fog multiplied by the FOG_COLOR timeline and weather, so
    /// distant terrain and water darken at night instead of staying day-blue (docs/modules/weather-and-sky-color.md 6.3).
    /// </summary>

    /// <summary>
    /// The atmospheric fog shell for this frame: where it starts and where it reaches full strength.
    /// </summary>
    /// <remarks>
    /// The reference pulls this shell in as rain rises, by 160 blocks at the start and 256 at the end, so
    /// rain visibly closes the view in rather than only tinting it. Two factors in the reference are not
    /// modelled here and are recorded in known-gaps: the sky light at the camera, which suppresses the
    /// effect underground, and the biome precipitation flag, which halves it in dry biomes.
    /// </remarks>
    internal (float Start, float End) CurrentFogEnvironmentalRange()
    {
        const float ClearStart = 0f;
        const float ClearEnd = 1024f;

        if (_world is null)
        {
            return (ClearStart, ClearEnd);
        }

        var partialTick = FrameCommand.EffectiveDayTimePartial(_world);
        var rain = FrameCommand.EffectiveRainLevel(_world, partialTick);
        var start = ClearStart - (160f * rain);
        var end = ClearEnd - (256f * rain);

        // The reference clamps the rainy end so it never collapses below ninety six blocks.
        var minRainEnd = MathF.Min(96f, ClearEnd);
        return (start, MathF.Max(minRainEnd, end));
    }

    internal Vector4 CurrentFogColour()
    {
        if (_world is not null && _biomes is not null)
        {
            // AtmosphericFogEnvironment.getBaseColor: fog base colour mixed toward the weather-darkened
            // SKY_COLOR, by a factor derived from how far the sky reaches against how far the world is drawn.
            // Distances here are in blocks, the same unit SKY_FOG_END_DISTANCE uses, and clampedLerp takes the
            // already divided value: at any normal render distance it saturates, so the fog simply becomes the
            // sky and the horizon has nothing to show a seam against (T-835).
            var fogBase = FrameCommand.EffectiveFogColour(_world);
            var sky = CurrentSkyColour();
            var renderDistanceBlocks = ViewDistanceChunks * 16f;
            var skyFogEnd = MathF.Min(512f, renderDistanceBlocks);
            var mixed01 = Math.Clamp(skyFogEnd / 32f, 0f, 1f);
            var clamped = 0.25f + (0.75f * mixed01);
            var mix = 1f - MathF.Pow(clamped, 0.25f);
            var mixed = Vector3.Lerp(new Vector3(fogBase.X, fogBase.Y, fogBase.Z), sky, mix);
            return new Vector4(mixed, 1f);
        }

        var baseFog = new Vector3(0.75f, 0.85f, 1.0f);
        if (_world is null)
        {
            return new Vector4(baseFog, 1f);
        }

        var partialTick = FrameCommand.EffectiveDayTimePartial(_world);
        var factor = Sharpcraft.Common.World.SkyAttributeTracks.FogColorFactorAt(FrameCommand.EffectiveDayTime(_world), partialTick);
        var rain = FrameCommand.EffectiveRainLevel(_world, partialTick);
        var thunder = FrameCommand.EffectiveThunderLevel(_world, partialTick);
        var fog = Sharpcraft.Common.World.WeatherBlend.ApplyFog(baseFog * factor, rain, thunder);
        return new Vector4(fog, 1f);
    }

    /// <summary>The SKY_COLOR at the camera for this frame (biome sky colour × day/night × weather).</summary>
    internal Vector3 CurrentSkyColour()
    {
        if (_world is null || _biomes is null)
        {
            return new Vector3(0.55f, 0.72f, 0.95f);
        }

        var eye = _player.Position;
        return FrameCommand.EffectiveSkyColour(
            _world, _biomes, (int)MathF.Floor(eye.X), (int)MathF.Floor(eye.Y + 1.6f), (int)MathF.Floor(eye.Z));
    }

    /// <summary>Provides the biome registry the live session uses for sky/fog colours.</summary>
    internal void SetBiomes(Sharpcraft.Common.World.BiomeRegistry? biomes) => _biomes = biomes;

    /// <summary>Provides the cloud texture the live session draws each frame.</summary>
    internal void SetCloudTexture(Sharpcraft.Client.Assets.CloudTexture? texture) => _cloudTexture = texture;

    /// <summary>Cloud options from the host: status/range mirror the reference CloudStatus and cloudRange settings.</summary>
    internal void SetCloudOptions(bool enabled, bool fancy, int rangeChunks, bool useOit, bool adaptive = false)
    {
        _cloudsEnabled = enabled;
        _cloudsFancy = fancy;
        _cloudRangeChunks = Math.Max(2, rangeChunks);
        _cloudUseOit = useOit;
        _cloudAdaptive = adaptive;
        _adaptiveCloudRange = _cloudRangeChunks;
    }

    /// <summary>Whether fancy clouds use the reference OIT path.</summary>
    internal bool CloudUseOit => _cloudUseOit;

    /// <summary>Feeds one presented-frame duration into the opt-in adaptive cloud range.</summary>
    internal void ObserveFrame(double frameMilliseconds)
    {
        if (!_cloudAdaptive || _cloudsFancy == false)
        {
            return;
        }

        _frameMsAccumulator += frameMilliseconds;
        _frameSamples++;
        _framesSinceRangeChange++;
        if (_frameSamples < 60 || _framesSinceRangeChange < 120)
        {
            return;
        }

        var average = _frameMsAccumulator / _frameSamples;
        _frameMsAccumulator = 0;
        _frameSamples = 0;
        var next = Sharpcraft.Client.Render.CloudRangePolicy.Next(_adaptiveCloudRange, average);
        if (next != _adaptiveCloudRange)
        {
            _adaptiveCloudRange = next;
            _framesSinceRangeChange = 0;
        }
    }

    private int EffectiveCloudRange => _cloudAdaptive ? _adaptiveCloudRange : _cloudRangeChunks;

    /// <summary>The per-frame cloud position-colour draw around the camera, or null when clouds/texture are unavailable.</summary>
    internal Sharpcraft.Framework.Rendering.CelestialColorDraw? CurrentCloudDraw()
    {
        if (_world is null || _cloudTexture is null || !_cloudsEnabled)
        {
            return null;
        }

        var eye = _player.Position + new Vector3(0f, 1.62f, 0f);
        return FrameCommand.BuildCloudDraw(_world, _cloudTexture, eye, _cloudDrawCache, EffectiveCloudRange, _cloudsFancy, _loop.Alpha);
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
        _entities.World = world;
        _interpolator = new PlayerInterpolator(player, _camera.Yaw);
        _live = live;
        Sharpcraft.Common.Registries.TrimRegistryTable.Configure(live?.Registries);
        Sharpcraft.Common.Registries.BannerPatternTable.Configure(live?.Registries);
        Sharpcraft.Client.Render.PotionColorTable.Configure(live?.Registries);
        _sync = live is null ? null : new PlayerSync();
        // Animation state is created per tracked entity and carried through the render state; the registry lives in the
        // rendering layer so the world model itself stays free of rendering types.
        _entities.AnimationStateFactory = Sharpcraft.Client.Render.EntityAnimationRegistry.CreateState;
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

    /// <summary>Player-skin lookup forwarded to the entity tracker; set by the host after the skin cache exists.</summary>
    public Func<Guid, PlayerSkinTextures?>? PlayerSkinLookup
    {
        get => _entities.PlayerSkinLookup;
        set => _entities.PlayerSkinLookup = value;
    }

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

        if (click.Action == Sharpcraft.Common.Network.ChatClickAction.ChangePage)
        {
            // An explicit page jump, independent of shift.
            if (int.TryParse(click.Value, out var page))
            {
                _chatLog.ScrollToPage(page);
            }

            return true;
        }

        if (click.Action == Sharpcraft.Common.Network.ChatClickAction.OpenFile)
        {
            OpenLinkedFile(click.Value);
            return true;
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

        var tagTip = ChatTagTipAt(x, y);
        Console.WriteLine(tagTip is null
            ? "chat probe: no tag tip"
            : $"chat probe: tag tip='{Plain(tagTip)}'");

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

        if (click is not null && GameOptions.Get("MC_CHAT_PROBE_CLICK") == "1")
        {
            var shift = GameOptions.Get("MC_CHAT_PROBE_SHIFT") == "1";
            var handled = TryClickChat(x, y, shift);
            Console.WriteLine($"chat probe: shift={shift} acted={handled} prompt='{_chat.Text}' open={_chat.IsOpen}");
        }
    }

    /// <summary>
    /// Tooltip for the indicator bar under a point, or null when the pointer is not over a bar.
    /// </summary>
    /// <param name="x">Pointer x in canvas pixels.</param>
    /// <param name="y">Pointer y in canvas pixels.</param>
    /// <remarks>
    /// The bar explains its own meaning when hovered, which is how the reference tells a player why a line is tinted.
    /// The texts come from the language file under the reference's own keys.
    /// </remarks>
    public Sharpcraft.Common.Network.TextRun[]? ChatTagTipAt(float x, float y)
    {
        if (CanvasHeight <= 0f || _chatLog.Count == 0)
        {
            return null;
        }

        var bottom = CanvasHeight - Sharpcraft.Client.Ui.ChatLog.BottomMargin;
        var entry = Sharpcraft.Client.Ui.ChatHitTest.EntryAt(
            x, y, 0f, Sharpcraft.Client.Ui.ChatLog.TagBarWidth, bottom,
            Sharpcraft.Client.Ui.ChatLog.EntryHeight, Sharpcraft.Client.Ui.ChatLog.LinesPerPage,
            _chatLog.Count, _chatLog.Scroll.Offset);

        if (entry < 0)
        {
            return null;
        }

        var key = _chatLog.TagFor(_chatLog.Count - 1 - entry) switch
        {
            Sharpcraft.Client.Ui.ChatLog.MessageTag.System => "chat.tag.system",
            Sharpcraft.Client.Ui.ChatLog.MessageTag.NotSecure => "chat.tag.not_secure",
            Sharpcraft.Client.Ui.ChatLog.MessageTag.Modified => "chat.tag.modified",
            Sharpcraft.Client.Ui.ChatLog.MessageTag.Error => "chat.tag.error",
            _ => null
        };

        if (key is null || !_language.TryGet(key, out var text))
        {
            return null;
        }

        return [new Sharpcraft.Common.Network.TextRun(text, Sharpcraft.Common.Network.ComponentRuns.DefaultColour, false)];
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
        var text = GameOptions.Get("MC_CHAT_DEBUG_TEXT");

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

    /// <summary>Resolves a player entity's display name through the player list UUID mapping.</summary>
    public string? EntityDisplayName(int entityId)
    {
        if (!_entities.TryGetUuid(entityId, out var uuid))
        {
            return null;
        }

        foreach (var entry in _playerList.Entries)
        {
            if (entry.Id == uuid && entry.Name.Length > 0)
            {
                return entry.Name;
            }
        }

        return null;
    }

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
        if (GameOptions.Get("MC_WINDOW_AUTOWALK") == "1")
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

        // Streaming telemetry, only under the existing diagnostic switch: the equivalence matrix reads these lines to prove
        // that chunks keep arriving as the player moves (docs/specs/quality/vanilla-equivalence-matrix.md).
        if (_diagEnabled && (_diagFrames % 60) == 0 && _world is not null)
        {
            Console.WriteLine(
                $"stream: loaded={_world.LoadedChunkCount} expected={ExpectedChunkCount()} " +
                $"pendingParsed={_pool?.ParsedReady ?? 0} pendingMesh={_pool?.MeshesReady ?? 0}");
        }

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

                // Advance the world clocks (day/night, docs/modules/time.md) and the block-light flicker once per tick,
                // like the reference client tick (LightmapRenderStateExtractor.tick).
                _world?.AdvanceTick();
                _blockLightFlicker.Tick();
        _world?.BlockEntityAnimations.Tick();

                // Verification hook: print the tracked entities so their positions can be compared against the
                // other client's own report of where it is (docs/specs/network/entity-sync.md 7).
                if (GameOptions.Get("MC_ENTITY_LOG") == "1" && _tickCounter % 20 == 0)
                {
                    var states = _entities.Interpolate(PartialTick);
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
                if (GameOptions.Get("MC_DIAG") == "1" && _tickCounter % 40 == 0)
                {
                    var (pressed, accepted, inAir) = JumpStats;
                    Console.WriteLine(
                        $"diag: tick={_tickCounter} fwd={frame.Forward:F0} sprint={frame.Sprint} sneak={frame.Sneak} "
                        + $"jump presses={pressed} accepted={accepted} inAir={inAir} ground={_player.OnGround} "
                        + $"y={_player.Position.Y:F2} vy={_player.DeltaMovement.Y:F3} day={_world?.DayTime}");
                    var diagLightmap = CurrentLightmap();
                    Console.WriteLine(
                        $"diag lightmap: skyFactor={diagLightmap.SkyFactor:F3} blockFactor={diagLightmap.BlockFactor:F3} " +
                        $"skyCol=({diagLightmap.SkyLightColor.X:F3},{diagLightmap.SkyLightColor.Y:F3},{diagLightmap.SkyLightColor.Z:F3}) " +
                        $"blockTint=({diagLightmap.BlockLightTint.X:F3},{diagLightmap.BlockLightTint.Y:F3},{diagLightmap.BlockLightTint.Z:F3})");
                }

                // Diagnostic hook: sweep the pitch so the head direction can be checked from a second client
                // without driving two windows at once.
                if (GameOptions.Get("MC_CAMERA_SWEEP") == "1")
                {
                    var sweep = MathF.Sin(_tickCounter * 0.05f * 0.9f) * (80f * (MathF.PI / 180f));
                    _camera.SetLook(_camera.Yaw, sweep);
                }

                if (_hasServerPosition)
                {
                    _player.Tick(frame.Forward, frame.Strafe, frame.Jump, frame.Sprint, _camera.Yaw, frame.Sneak);
                }

            TickIndex++;

                // Remote entities approach the position the server reported over a few ticks, so the tracker
                // has to advance once per tick (docs/specs/network/entity-sync.md 9.4).
                _entities.GameTime = _world?.GameTime ?? 0;
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

        // The title carries the live position and key count, but rebuilding that string and pushing it through
        // SetWindowText every single frame is measurable work for something nobody can read at 300 Hz. It is throttled
        // to 10 Hz, or immediately when the player moved more than 10 cm or left the ground (T-816).
        var position = _player.Position;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsedMs = (now - _titleUpdatedAt) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        var moved = Vector3.DistanceSquared(position, _titlePosition) > 0.01f;
        var stateChanged = _player.OnGround != _titleOnGround;
        if (_titleUpdatedAt != 0 && !moved && !stateChanged && elapsedMs < TitleUpdateIntervalMs)
        {
            return;
        }

        _titleUpdatedAt = now;
        _titlePosition = position;
        _titleOnGround = _player.OnGround;

        // The process id is in the title so that, when several copies are running, it is obvious which
        // window is which.
        window.Title =
            $"Sharpcraft #{Environment.ProcessId} | {position.X:F1} {position.Y:F1} {position.Z:F1} | " +
            $"{(_player.OnGround ? "ground" : "air")} | {window.Input.PressedKeys.Count} keys";
    }

    /// <summary>How often the window title is refreshed at most, in milliseconds (T-816).</summary>
    private const double TitleUpdateIntervalMs = 100d;

    private long _titleUpdatedAt;
    private Vector3 _titlePosition;
    private bool _titleOnGround;

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
            viewDistance: (byte)ViewDistanceChunks,
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
            InstallChunk(parsed);
        }
    }

    /// <summary>
    /// Installs a chunk into the world and marks the geometry stale.
    /// </summary>
    private void InstallChunk(Sharpcraft.Common.Network.ParsedChunk chunk)
    {
        _world!.AddChunk(chunk);
        _geometryDirty = true;

        // Arrival bookkeeping: the server's sending time must be excluded when judging how long the client takes to draw.
        ChunksInstalled++;
        LastInstallSeconds = _installClock.Elapsed.TotalSeconds;

        if (ChunksInstalled == 1)
        {
            FirstInstallSeconds = LastInstallSeconds;
        }
    }

}
