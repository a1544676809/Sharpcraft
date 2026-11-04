// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>ShulkerModel.setupAnim</c> plus the client-side peek clock from <c>Shulker.updatePeekAmount</c> /
/// <c>getClientPeekAmount</c>. The lid opens with the synced peek amount, yaws while opening, and the small head looks
/// along the render-state pitch relative to the body.
/// </summary>
/// <remarks>
/// <para>
/// The peek amount is a synced byte (<c>DATA_PEEK</c>, index 17, BYTE serializer 0) interpolated toward
/// <c>raw * 0.01</c> by 0.05 per tick. The <see cref="State"/> reproduces that clock, exposes the partial-tick lerp, and
/// takes the raw byte through <see cref="IEntitySyncedDataAnimationState"/>.
/// </para>
/// <para>
/// setupAnim: <c>bs = (0.5 + peek)·π</c>; <c>q = -1 + sin(bs)</c>; extra = <c>sin(age·0.1)·0.7</c> only when
/// <c>bs &gt; π</c>; <c>lid.y = 16 + sin(bs)·8 + extra</c>; <c>lid.yRot = q⁴·π·0.125</c> when peek &gt; 0.3 (else 0);
/// head xRot = pitch, yRot = <c>(HeadYaw − 180 − Yaw)</c>. The lid position is stored absolute in the reference; the
/// bind pivot is (0,24,0) in a 24-tall y-down model (stored y-up 0), so the additive Position.Y offset is
/// <c>8 − sin(bs)·8 − extra</c>. Rotations keep reference signs.
/// </para>
/// <para>
/// <c>attachFace</c> orientation is a renderer transform (<c>ShulkerRenderer.setupRotations</c>): it is applied by
/// <c>EntityModelTransform</c> from the synced attach-face direction, not by this provider.
/// </para>
/// </remarks>
public sealed class ShulkerAnimationProvider(int typeId) : IEntityAnimationProvider
{
    /// <summary>Synced data index of the reference <c>DATA_PEEK_ID</c> byte.</summary>
    public const int PeekDataIndex = 17;

    /// <summary>Entity data serializer id for BYTE.</summary>
    public const int ByteSerializerId = 0;

    private static readonly string[] NominalClips = ["closed", "open"];

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference model height (the lid/base pivot sits at 24 in y-down space); y-up bind is 0.</summary>
    private const float ModelHeight = 24f;

    /// <summary>Reference lid base height in y-down space (16).</summary>
    private const float LidBaseY = 16f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var peek = (state as State)?.PeekAmount(frame.PartialTick) ?? 0f;
        ApplyPose(peek, frame, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var peek = string.Equals(clip, "open", StringComparison.Ordinal) ? 1f : 0f;
        ApplyPose(peek, frame, pose);
    }

    private static void ApplyPose(float peekAmount, in EntityAnimationFrame frame, EntityPose pose)
    {
        var bs = (0.5f + peekAmount) * MathF.PI;
        var sinBs = MathF.Sin(bs);
        var q = -1f + sinBs;
        var extra = bs > MathF.PI ? MathF.Sin(frame.AgeInTicks * 0.1f) * 0.7f : 0f;

        // lid.setPos(0, 16 + sin(bs)*8 + extra, 0) in y-down; stored y-up target = ModelHeight - refY, bind 0.
        var lidY = (ModelHeight - (LidBaseY + (sinBs * 8f) + extra)) - 0f;
        var lidYRot = peekAmount > 0.3f ? q * q * q * q * MathF.PI * 0.125f : 0f;
        pose.Add("lid", new PoseOffset(new Vector3(0f, lidY, 0f), new Vector3(0f, lidYRot, 0f), Vector3.Zero));

        // head.xRot = pitch; head.yRot = (HeadYaw - 180 - Yaw). Bind rotations 0.
        var headPitch = frame.Pitch * DegreesToRadians;
        var headYaw = (frame.HeadYaw - 180f - frame.Yaw) * DegreesToRadians;
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, headYaw, 0f), Vector3.Zero));
    }

    private static float Lerp(float delta, float start, float end) => start + (end - start) * delta;

    /// <summary>
    /// Per-entity peek clock: the synced byte target plus the reference <c>updatePeekAmount</c> lerp toward
    /// <c>raw * 0.01</c> by 0.05/tick, clamped.
    /// </summary>
    private sealed class State : IEntityAnimationState, IEntitySyncedDataAnimationState, IEntityTickAnimationState
    {
        private int _rawPeek;
        private float _current;
        private float _currentO;

        public float PeekAmount(float partialTick) => Lerp(partialTick, _currentO, _current);

        public void OnSyncedData(int index, int serializerId, int value)
        {
            if (index == PeekDataIndex && serializerId == ByteSerializerId)
            {
                _rawPeek = value;
            }
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
            _currentO = _current;
            var target = _rawPeek * 0.01f;
            if (_current == target)
            {
                return;
            }

            if (_current > target)
            {
                _current = Math.Clamp(_current - 0.05f, target, 1f);
            }
            else
            {
                _current = Math.Clamp(_current + 0.05f, 0f, target);
            }
        }
    }
}
