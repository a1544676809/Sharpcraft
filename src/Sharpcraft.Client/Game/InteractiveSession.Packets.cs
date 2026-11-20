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
    private void DrainIncoming()
    {
        if (_live is null)
        {
            return;
        }

        while (_live.TryDequeue(out var body))
        {
            _bundleAssembler.Process(body, DispatchPacket);
        }
    }

    private void DispatchPacket(byte[] body)
    {
        if (_live is null)
        {
            return;
        }

        var reader = new PayloadReader(body);
        if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success)
        {
            return;
        }

            // Host-only join packet capture (MC_PACKET_LOG=1): id/length/prefix of the first packets, so the clock source
            // can be found against the reference (principle 13: parsed here, in the host; no LINQ on this hot path).
            if (_packetLog && _packetLogCount < PacketLogLimit)
            {
                _packetLogCount++;
                var prefix = body[..Math.Min(24, body.Length)];
                Console.WriteLine($"pkt#{_packetLogCount} id={packetId} len={body.Length} hex={Convert.ToHexString(prefix)}");
            }

            if (packetId == PacketIds.PlayClientbound.ChunkBatchFinished)
            {
                var batchSize = 0;
                reader.TryReadVarInt(out batchSize);
                _chunkBatchCalculator.OnBatchFinished(batchSize);
                _live.Send(PlayPackets.BuildChunkBatchReceived(_chunkBatchCalculator.DesiredChunksPerTick));
                return;
            }

            // Transport-level replies (keep-alive, ping, chunk batch) share one implementation with the warm-up
            // session, so the reply encoding lives in exactly one place (AGENTS.md principle 14).
            var transportKeepAlives = 0;
            var transportPings = 0;
            var transportBatches = 0;
            if (PlaySession.TryHandleTransport(
                    packetId,
                    ref reader,
                    pkt => { _live.Send(pkt); },
                    ref transportKeepAlives,
                    ref transportPings,
                    ref transportBatches,
                    _chunkBatchCalculator.DesiredChunksPerTick))
            {
                return;
            }

            switch (packetId)
            {
                case PacketIds.PlayClientbound.PlayerPosition:
                    if (_sync is not null &&
                        PlayMovementPackets.TryReadPlayerPosition(body[reader.Position..], out var correction) == DecodeStatus.Success &&
                        correction is not null)
                    {
                        var outcome = _sync.ApplyServerPosition(_player, correction, isTeleport: true);
                        _hasServerPosition = true;
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

                            // The server ignores movement until the client says the world is loaded, so this goes out once
                            // the join teleport has been accepted, exactly like the reference client.
                            if (!_playerLoadedSent)
                            {
                                _live.Send(PlayPackets.BuildPlayerLoaded());
                                _playerLoadedSent = true;
                            }
                        }
                    }

                    break;

                // Death notice. The reference opens the death screen and respawns when the player clicks; this client has
                // no death screen yet, so it auto-respawns (temporary deviation recorded in docs/STATE.md).
                case PacketIds.PlayClientbound.PlayerCombatKill:
                    if (!_respawnRequested)
                    {
                        _live.Send(PlayPackets.BuildClientCommand(0));
                        _respawnRequested = true;
                    }

                    break;

                // Local health. A player who joins while dead (health 0) receives no chunks until they respawn, so
                // request it once; a positive health clears the guard after the respawn lands.
                case PacketIds.PlayClientbound.SetHealth:
                    if (PlayPackets.TryReadSetHealth(body, out var localHealth) == DecodeStatus.Success)
                    {
                        if (localHealth > 0f)
                        {
                            _respawnRequested = false;
                        }
                        else if (!_respawnRequested)
                        {
                            _live.Send(PlayPackets.BuildClientCommand(0));
                            _respawnRequested = true;
                        }
                    }

                    break;

                // Block entity updates carry the position, the type and a fresh NBT tag. Decoding them keeps the packet
                // stream honest; nothing consumes the tag until block entity rendering exists.
                case PacketIds.PlayClientbound.BlockEntityData:
                    BlockEntityPackets.TryReadBlockEntityData(body, out _);
                    break;

                // Synced entity data: the variant index is what decides an entity's appearance
                // (docs/analysis/render/entity-variants.md 2).
                case PacketIds.PlayClientbound.SetEntityData:
                    {
                        Span<EntityDataItem> items = stackalloc EntityDataItem[EntityDataPackets.MaxItems];
                        if (EntityDataPackets.TryRead(body, out var dataEntityId, items, out var itemCount, out _) == DecodeStatus.Success)
                        {
                            _entities.ApplyEntityData(dataEntityId, items[..itemCount]);
                        }

                        break;
                    }

                // Equipment slots: identity only in this stage; see docs/modules/entity-equipment.md E1.
                case PacketIds.PlayClientbound.SetEquipment:
                    {
                        var equipment = new EquipmentItem[SetEquipmentPackets.MaxSlots];
                        if (SetEquipmentPackets.TryRead(body, out var equipmentEntityId, equipment, out var equipmentCount, out _) == DecodeStatus.Success)
                        {
                            _entities.ApplyEquipment(equipmentEntityId, equipment[..equipmentCount]);
                        }

                        break;
                    }

                // Entity events start/stop animation state clocks (attack, sonic charge, tendril shiver, ...).
                case PacketIds.PlayClientbound.EntityEvent:
                    if (EntityEventPackets.TryRead(body, out var eventEntityId, out var eventId) == DecodeStatus.Success)
                    {
                        _entities.ApplyEntityEvent(eventEntityId, eventId);
                    }

                    break;

                // Passenger graph: needed by the presentation layer (isRiding/isVehicle) and the flap clock.
                case PacketIds.PlayClientbound.SetPassengers:
                    {
                        Span<int> passengers = stackalloc int[PassengerPackets.MaxPassengers];
                        if (PassengerPackets.TryRead(body, out var vehicleId, passengers, out var passengerCount) == DecodeStatus.Success)
                        {
                            _entities.ApplyPassengers(vehicleId, passengers[..passengerCount]);
                        }

                        break;
                    }

                // Jukebox level events drive parrot party state.
                case PacketIds.PlayClientbound.LevelEvent:
                    if (LevelEventPackets.TryRead(body, out var levelEventType, out var levelEventPos, out var levelEventData, out _) == DecodeStatus.Success)
                    {
                        _entities.ApplyLevelEvent(levelEventType, levelEventPos, levelEventData);
                    }

                    break;

                // Leash link: the Rabbit idle gate needs the client-side leashed flag.
                case PacketIds.PlayClientbound.SetEntityLink:
                    if (EntityLinkPackets.TryRead(body, out var linkSource, out var linkDestination) == DecodeStatus.Success)
                    {
                        _entities.ApplyEntityLink(linkSource, linkDestination);
                    }

                    break;

                // SCALE attribute (and any other attributes): update the entity's presentation attribute map.
                case PacketIds.PlayClientbound.UpdateAttributes:
                    {
                        Span<AttributeValue> attributes = stackalloc AttributeValue[128];
                        if (UpdateAttributesPackets.TryRead(body, attributes, out var attributeCount, out var attributeEntityId) == DecodeStatus.Success)
                        {
                            _entities.ApplyAttributes(attributeEntityId, attributes[..attributeCount]);
                        }

                        break;
                    }

                // Arm-swing animation: drives the presentation layer's swing clock for armed mobs.
                case PacketIds.PlayClientbound.SwingAnimation:
                    if (SwingAnimationPackets.TryRead(body, out var swingEntityId, out var swingHand, out var swingType, out var swingDuration) == DecodeStatus.Success)
                    {
                        _entities.ApplySwingAnimation(swingEntityId, swingHand, swingType, swingDuration);
                    }

                    break;

                case PacketIds.PlayClientbound.SetTime:
                    if (_world is not null)
                    {
                        var setTimeReader = new PayloadReader(body);
                        if (SetTimePacketCodec.TryRead(ref setTimeReader, out var setTime) == DecodeStatus.Success)
                        {
                            _world.ApplyTime(setTime);
                            if (GameOptions.Get("MC_DIAG") == "1")
                            {
                                var clocks = new System.Text.StringBuilder();
                                foreach (var kv in setTime.Clocks)
                                {
                                    if (clocks.Length > 0)
                                    {
                                        clocks.Append(',');
                                    }

                                    clocks.Append(kv.Key).Append(":t=").Append(kv.Value.TotalTicks).Append(",r=").Append(kv.Value.Rate);
                                }

                                var head = Convert.ToHexString(body[..Math.Min(40, body.Length)]);
                                Console.WriteLine($"set_time: len={body.Length} gameTime={setTime.GameTime} day={_world.DayTime} clocks=[{clocks}] hex={head}");
                            }
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.GameEvent:
                    if (_world is not null)
                    {
                        var gameEventReader = new PayloadReader(body);
                        if (GameEventPacketCodec.TryRead(ref gameEventReader, out var gameEvent) == DecodeStatus.Success)
                        {
                            _world.ApplyWeather(gameEvent);
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
                        InstallChunk(parsed);
                    }

                    break;


                case PacketIds.PlayClientbound.PlayerChat:
                    if (PlayerChatPackets.TryReadPlayerChatStyled(
                            body[reader.Position..], out var chatSender, out var chatBody, out var chatTarget,
                            out var chatSigned, out var chatSignature, out var chatTypeId, _language) == DecodeStatus.Success)
                    {
                        // The shape of a player message comes from the chat type registry, never from an assumption:
                        // the same packet renders as <name> text, name >> text or [name -> target] text depending on
                        // the entry the server named.
                        var decoratedValues = DecorateLineStyled(chatTypeId, chatSender, chatBody, chatTarget);

                        if (decoratedValues is not { } decoratedLine)
                        {
                            break;
                        }

                        // A message without a signature comes from a server that does not sign chat; the reference
                        // marks those as not secure, which shows the same pale bar a system line gets.
                        _chatLog.Add(
                            decoratedLine.Runs,
                            _chatTicks,
                            chatSigned ? Sharpcraft.Client.Ui.ChatLog.MessageTag.None : Sharpcraft.Client.Ui.ChatLog.MessageTag.NotSecure,
                            decoratedLine.Clicks,
                            decoratedLine.Hovers,
                            chatSignature);

                        if (_diagEnabled)
                        {
                            Console.WriteLine($"chat received [type {chatTypeId}]: {Plain(decoratedLine.Runs)}");
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
                    var playerInfoStatus = PlayerListPackets.TryReadPlayerInfoUpdate(
                        body[reader.Position..], out var playerInfoActions, out var profileUpdates);
                    if (playerInfoStatus == DecodeStatus.Success)
                    {
                        _playerList.Apply(profileUpdates);
                        if (_packetLog)
                        {
                            Console.WriteLine($"playerinfo: actions={playerInfoActions} entries={profileUpdates.Length} roster={_playerList.Count}");
                        }
                    }
                    else if (_packetLog)
                    {
                        Console.WriteLine($"playerinfo: parse failed status={playerInfoStatus} len={body.Length}");
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

                case PacketIds.PlayClientbound.SetCamera:
                    if (PlayPackets.TryReadSetCamera(body[reader.Position..], out var cameraId) == DecodeStatus.Success)
                    {
                        _cameraEntityId = cameraId;
                    }

                    break;

                case PacketIds.PlayClientbound.TakeItemEntity:
                    if (PlayPackets.TryReadTakeItemEntity(body[reader.Position..], out var takeItem, out var collector, out var takeAmount) == DecodeStatus.Success)
                    {
                        _lastTakeItemId = takeItem;
                        _lastCollectorId = collector;
                        _lastTakeAmount = takeAmount;
                    }

                    break;

                case PacketIds.PlayClientbound.ProjectilePower:
                    if (PlayPackets.TryReadProjectilePower(body[reader.Position..], out var projectileEntity, out var projectilePower) == DecodeStatus.Success)
                    {
                        _lastProjectileEntity = projectileEntity;
                        _lastProjectilePower = projectilePower;
                    }

                    break;

                case PacketIds.PlayClientbound.BlockDestruction:
                    if (PlayPackets.TryReadBlockDestruction(body[reader.Position..], out _, out var bdX, out var bdY, out var bdZ, out var bdProgress) == DecodeStatus.Success)
                    {
                        var key = Sharpcraft.Common.Network.BlockPositionCodec.PackBlockPosition(bdX, bdY, bdZ);
                        if (bdProgress == 0)
                        {
                            _blockDestruction.Remove(key);
                        }
                        else
                        {
                            _blockDestruction[key] = bdProgress;
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.BlockEvent:
                    if (PlayPackets.TryReadBlockEvent(body[reader.Position..], out var evX, out var evY, out var evZ, out var evB0, out var evB1, out var evBlock) == DecodeStatus.Success)
                    {
                        _lastBlockEventX = evX;
                        _lastBlockEventY = evY;
                        _lastBlockEventZ = evZ;
                        _lastBlockEventB0 = evB0;
                        _lastBlockEventB1 = evB1;
                        _lastBlockEventBlock = evBlock;
                        _world?.BlockEntityAnimations.Apply(evX, evY, evZ, evB0, evB1);
                    }

                    break;

                case PacketIds.PlayClientbound.ChunkBatchStart:
                    _chunkBatchCalculator.OnBatchStart();
                    break;

                case PacketIds.PlayClientbound.ForgetLevelChunk:
                    if (PlayPackets.TryReadForgetLevelChunk(body[reader.Position..], out var forgetX, out var forgetZ) == DecodeStatus.Success)
                    {
                        _world?.RemoveChunk(forgetX, forgetZ);
                    }

                    break;

                case PacketIds.PlayClientbound.SetChunkCacheCenter:
                    if (PlayPackets.TryReadSetChunkCacheCenter(body[reader.Position..], out var cacheX, out var cacheZ) == DecodeStatus.Success)
                    {
                        _chunkCacheCenterX = cacheX;
                        _chunkCacheCenterZ = cacheZ;
                    }

                    break;

                case PacketIds.PlayClientbound.SetChunkCacheRadius:
                    if (PlayPackets.TryReadSetChunkCacheRadius(body[reader.Position..], out var cacheRadius) == DecodeStatus.Success)
                    {
                        _chunkCacheRadius = cacheRadius;
                    }

                    break;

                case PacketIds.PlayClientbound.DamageEvent:
                    if (PlayPackets.TryReadDamageEvent(body[reader.Position..], out var dmgEntity, out _, out _, out _, out _, out _, out _, out _) == DecodeStatus.Success)
                    {
                        _lastDamageEntityId = dmgEntity;
                    }

                    break;

                case PacketIds.PlayClientbound.HurtAnimation:
                    if (PlayPackets.TryReadHurtAnimation(body[reader.Position..], out var hurtEntity, out var hurtYaw) == DecodeStatus.Success)
                    {
                        _lastHurtEntityId = hurtEntity;
                        _lastHurtYaw = hurtYaw;
                    }

                    break;

                case PacketIds.PlayClientbound.UpdateMobEffect:
                    if (PlayPackets.TryReadUpdateMobEffect(body[reader.Position..], out var effEntity, out var effId, out _, out _, out _) == DecodeStatus.Success)
                    {
                        if (!_mobEffects.TryGetValue(effEntity, out var set))
                        {
                            _mobEffects[effEntity] = set = new System.Collections.Generic.HashSet<int>();
                        }

                        set.Add(effId);
                    }

                    break;

                case PacketIds.PlayClientbound.RemoveMobEffect:
                    if (PlayPackets.TryReadRemoveMobEffect(body[reader.Position..], out var rmEntity, out var rmEffect) == DecodeStatus.Success
                        && _mobEffects.TryGetValue(rmEntity, out var rmSet))
                    {
                        rmSet.Remove(rmEffect);
                    }

                    break;

                case PacketIds.PlayClientbound.PlayerAbilities:
                    if (PlayPackets.TryReadPlayerAbilities(body[reader.Position..], out var abilityFlags, out var flySpeed, out var walkSpeed) == DecodeStatus.Success)
                    {
                        _invulnerable = (abilityFlags & 1) != 0;
                        _flying = (abilityFlags & 2) != 0;
                        _canFly = (abilityFlags & 4) != 0;
                        _instabuild = (abilityFlags & 8) != 0;
                        _flyingSpeed = flySpeed;
                        _walkingSpeed = walkSpeed;
                    }

                    break;

                case PacketIds.PlayClientbound.SetSimulationDistance:
                    if (PlayPackets.TryReadSetSimulationDistance(body[reader.Position..], out var simDistance) == DecodeStatus.Success)
                    {
                        _simulationDistance = simDistance;
                    }

                    break;

                case PacketIds.PlayClientbound.Cooldown:
                    if (PlayPackets.TryReadCooldown(body[reader.Position..], out var cooldownGroup, out var cooldownDuration) == DecodeStatus.Success)
                    {
                        _cooldowns[cooldownGroup] = cooldownDuration;
                    }

                    break;

                case PacketIds.PlayClientbound.BlockChangedAck:
                    if (PlayPackets.TryReadBlockChangedAck(body[reader.Position..], out _) == DecodeStatus.Success)
                    {
                        // Prediction ack: no client-side prediction sequence is tracked yet, so nothing to apply.
                    }

                    break;

                case PacketIds.PlayClientbound.SetHeldSlot:
                    if (PlayPackets.TryReadSetHeldSlot(body[reader.Position..], out var heldSlot) == DecodeStatus.Success)
                    {
                        _selectedHotbarSlot = heldSlot;
                    }

                    break;

                case PacketIds.PlayClientbound.SetExperience:
                    if (PlayPackets.TryReadSetExperience(body[reader.Position..], out var xpProgress, out var xpTotal, out var xpLevel) == DecodeStatus.Success)
                    {
                        _experienceProgress = xpProgress;
                        _totalExperience = xpTotal;
                        _experienceLevel = xpLevel;
                    }

                    break;

                case PacketIds.PlayClientbound.EntityPositionSync:
                    if (EntityPackets.TryReadEntityPositionSync(body[reader.Position..], out var posSync) == DecodeStatus.Success)
                    {
                        _entities.ApplyPositionSync(posSync);
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

                case PacketIds.PlayClientbound.Disconnect:
                    return;

                default:
                    // Temporary: an unhandled id is worth seeing when a behaviour is missing, rather than guessing
                    // which id the server used for it.
                    if (_diagEnabled)
                    {
                        Console.WriteLine($"unhandled packet id={packetId} bytes={body.Length}");
                    }
                    else if (GameOptions.Get("MC_PACKET_TRACE") == "1")
                    {
                        Console.WriteLine($"unhandled packet id={packetId} bytes={body.Length}");
                    }

                    break;
            }
    }
}
