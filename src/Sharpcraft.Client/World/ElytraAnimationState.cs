// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Port of the reference <c>ElytraAnimationState</c> (Minecraft 26.3). Holds smoothed wing rotations for the elytra
/// model; ticked once per client tick and sampled with the partial tick for rendering.
/// </summary>
/// <remarks>
/// Reference targets: standing (pi/12, 0, -pi/12); crouching (2pi/9, 0.0873, -pi/4); fall-flying interpolates
/// (pi/12,-pi/12)->(pi/9,-pi/2) by <c>ratio = 1 - (-normalizedVelocity.y)^1.5</c>. Each tick moves 30% toward target;
/// Old values drive the partial-tick lerp. Values are in the reference (y-down) convention; the model layer converts
/// axis signs when applying them.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.entity.ElytraAnimationState", "ref://mc-26.3-analysis/src/net/minecraft/world/entity/ElytraAnimationState.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public sealed class ElytraAnimationState
{
    private const float DefaultXRot = MathF.PI / 12f;
    private const float DefaultZRot = -MathF.PI / 12f;

    private float _rotX;
    private float _rotY;
    private float _rotZ;
    private float _rotXOld;
    private float _rotYOld;
    private float _rotZOld;

    /// <summary>Advances the smoothed rotations one tick toward the pose-dependent target.</summary>
    public void Tick(bool fallFlying, bool crouching, float velocityX, float velocityY, float velocityZ)
    {
        _rotXOld = _rotX;
        _rotYOld = _rotY;
        _rotZOld = _rotZ;

        float targetXRot;
        float targetZRot;
        float targetYRot;
        if (fallFlying)
        {
            var ratio = 1f;
            if (velocityY < 0f)
            {
                var length = MathF.Sqrt((velocityX * velocityX) + (velocityY * velocityY) + (velocityZ * velocityZ));
                if (length > 1e-6f)
                {
                    var normalizedY = velocityY / length;
                    ratio = 1f - (float)Math.Pow(-normalizedY, 1.5);
                }
            }

            targetXRot = Lerp(ratio, MathF.PI / 12f, MathF.PI / 9f);
            targetZRot = Lerp(ratio, -MathF.PI / 12f, -MathF.PI / 2f);
            targetYRot = 0f;
        }
        else if (crouching)
        {
            targetXRot = MathF.PI * 2f / 9f;
            targetZRot = -MathF.PI / 4f;
            targetYRot = 0.08726646f;
        }
        else
        {
            targetXRot = DefaultXRot;
            targetZRot = DefaultZRot;
            targetYRot = 0f;
        }

        _rotX += (targetXRot - _rotX) * 0.3f;
        _rotY += (targetYRot - _rotY) * 0.3f;
        _rotZ += (targetZRot - _rotZ) * 0.3f;
    }

    public float GetRotX(float partialTick) => _rotXOld + ((_rotX - _rotXOld) * partialTick);

    public float GetRotY(float partialTick) => _rotYOld + ((_rotY - _rotYOld) * partialTick);

    public float GetRotZ(float partialTick) => _rotZOld + ((_rotZ - _rotZOld) * partialTick);

    private static float Lerp(float t, float a, float b) => a + ((b - a) * t);
}
