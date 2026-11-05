<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# Play Clientbound 包覆盖审计

基线：Minecraft 26.3（protocol 777）。证据：`ref://mc-26.3-analysis/vectors/packet-tables.txt`（原版注册转储）与 `src/Sharpcraft.Launcher/InteractiveSession.cs` 的 `case PacketIds.PlayClientbound.*`。
生成方式：用注册转储列出全部 **144** 个 play clientbound 包，减去实际 `case` 处理的 **51** 个，得到**未处理 93** 个（下表）。仅类名/ID，无原版源码。

## 未处理清单（93）\n\n> 已补：`block_changed_ack(4)`、`set_experience(105)`、`set_held_slot(107)`（见 T-642），故从下表移除。

按 ID：0 bundle_delimiter, 2 animate(?), 3 award_stats, 4 block_changed_ack, 5 block_destruction, 7 block_event, 9 boss_event, 10 change_difficulty, 12 chunk_batch_start, 13 chunks_biomes, 15 command_suggestions, 16 commands, 17 container_close, 18 container_set_content, 19 container_set_data, 20 container_set_slot, 21 cookie_request, 22 cooldown, 23 custom_chat_completions, 24 custom_payload, 25 damage_event, 26–29 debug, 30 debug_sample, 36 explode, 37 add_transient_block, 38 forget_level_chunk, 40 game_rule_values, 41 game_test_highlight_pos, 42 mount_screen_open, 43 hurt_animation, 44 initialize_border, 48 level_particles, 49 light_update, 50 login, 51 low_disk_space_warning, 52 map_item_data, 53 merchant_offers, 56 move_minecart_along_track, 58 move_vehicle, 59 open_book, 60 open_screen, 61 open_sign_editor, 63 pong_response, 64 place_ghost_recipe, 65 player_abilities, 67 player_combat_end, 68 player_combat_enter, 72 player_look_at, 75/76/77 recipe_book_*, 79 remove_mob_effect, 80 reset_score, 81 resource_pack_pop, 82 resource_pack_push, 83 post_effects, 84 respawn, 87 select_advancements_tab, 88 server_data, 90–94 border, 95 set_camera, 96/97 chunk_cache, 98 set_cursor_item, 99 set_default_spawn_position, 100 set_display_objective, 105 set_experience, 107 set_held_slot, 108 set_objective, 110 set_player_inventory, 111 set_player_team, 112 set_score, 113 set_simulation_distance, 117 set_titles_animation, 118 sound_entity, 119 sound, 120 start_configuration, 121 stop_sound, 122 store_cookie, 125 tab_list, 126 tag_query, 127 take_item_entity, 129/130/131/132 test/ticking/transfer, 133 update_advancements, 135 update_mob_effect, 136 update_recipes, 137 update_tags, 138 projectile_power, 139/140/141 custom_report/server_links/waypoint, 142/143 clear/show_dialog。

## 分级（建议处理优先级）

### P0 世界/区块/光照正确性（必须尽快）
- `block_changed_ack(4)`、`block_destruction(5)`、`block_event(7)`、`add_transient_block(37)`、`level_particles(48)`、`light_update(49)`、`chunks_biomes(13)`、`chunk_batch_start(12)`、`forget_level_chunk(38)`、`set_chunk_cache_center/radius(96/97)`、`explode(36)`。
- 影响：光照/生物群系/方块回执缺失会导致区块显示错误或无法卸载；粒子/爆炸观感缺失。

### P1 玩家与实体状态（影响玩法/表现）
- `player_abilities(65)`、`set_experience(105)`、`set_held_slot(107)`、`respawn(84)`、`damage_event(25)`、`hurt_animation(43)`、`cooldown(22)`、`update_mob_effect(135)`、`remove_mob_effect(79)`、`player_combat_enter/end(68/67)`、`player_look_at(72)`、`move_vehicle(58)`、`move_minecart_along_track(56)`。

### P2 交互/容器/物品
- `container_close(17)`、`container_set_content/slot/data(18/20/19)`、`set_player_inventory(110)`、`set_cursor_item(98)`、`merchant_offers(53)`、`open_screen(60)`、`open_book(59)`、`open_sign_editor(61)`、`place_ghost_recipe(64)`、`mount_screen_open(42)`、`take_item_entity(127)`、`map_item_data(52)`。

### P3 音频/聊天/命令
- `sound(119)`、`sound_entity(118)`、`stop_sound(121)`、`commands(16)`、`command_suggestions(15)`、`custom_chat_completions(23)`、`custom_payload(24)`、`server_data(88)`。

### P4 UI/记分板/进度/资源包/对话框/调试（可后置）
- 其余（boss_event、tab_list、score/objective/border、advancements、resource packs、dialogs、waypoint、debug/test/ticking 等）。

## 说明
- 本表是"当前**未**处理"的全集；`case` 覆盖 `41`。部分包可能由别的入口间接处理（需逐条核对），但**绝大多数确为未实现**。
- 下一步：按 P0→P1 顺序补齐解码与分发，并为每个补上的包加 traceability 行与最小回归（防再出现 T-640 式"整包漏接"）。



