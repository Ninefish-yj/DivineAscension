// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Combat
{
    /// <summary>
    /// 地形破坏类型枚举
    /// </summary>
    public enum TerrainDestructionType
    {
        /// <summary>能量风暴型：烧焦+点火+加热+移除龙卷风</summary>
        EnergyStorm = 0,
        /// <summary>维度打击型：降低高度+移除液体+生成坑洞</summary>
        DimensionalStrike = 1,
        /// <summary>神罚型：全屏烧焦+点火+屏幕震动+加热</summary>
        DivinePunishment = 2,
        /// <summary>通用型：烧焦+少量加热</summary>
        Generic = 3
    }

    /// <summary>
    /// 地形破坏系统（重制版）
    /// 使用原版游戏的 TerraformOptions 和 MapAction API
    /// </summary>
    public static class DSTerrainDestruction
    {
        // 地形破坏的冷却时间（避免频繁破坏同一区域）
        private static HashSet<string> _recentlyDestroyed = new HashSet<string>();
        private static float _lastCleanupTime = 0f;

        // ============================================================
        // 公共接口
        // ============================================================

        /// <summary>
        /// 在指定位置周围破坏地形
        /// </summary>
        /// <param name="center">中心位置</param>
        /// <param name="radius">破坏半径（格）</param>
        /// <param name="destructionChance">每个地块的破坏概率（0-1）</param>
        /// <param name="destructionType">破坏类型</param>
        /// <param name="caster">施法者（用于敌友判断和盖亚契约）</param>
        /// <returns>破坏的地块数量</returns>
        public static int DestroyTerrain(
            Vector2 center,
            float radius,
            float destructionChance,
            TerrainDestructionType destructionType = TerrainDestructionType.Generic,
            Actor caster = null)
        {
            if (World.world == null || radius <= 0f || destructionChance <= 0f) return 0;

            // 清理过期的冷却记录（每60秒清理一次）
            if (Time.time - _lastCleanupTime > 60f)
            {
                _recentlyDestroyed.Clear();
                _lastCleanupTime = Time.time;
            }

            int destroyedCount = 0;
            System.Random random = new System.Random();

            try
            {
                // 获取中心地块
                WorldTile centerTile = World.world.GetTileSimple((int)center.x, (int)center.y);
                if (centerTile == null) return 0;

                // 构建地形改造配置
                TerraformOptions options = BuildTerraformOptions(destructionType);

                // 遍历范围内的地块
                int minX = Mathf.Max(0, (int)(center.x - radius));
                int maxX = Mathf.Min(MapBox.width - 1, (int)(center.x + radius));
                int minY = Mathf.Max(0, (int)(center.y - radius));
                int maxY = Mathf.Min(MapBox.height - 1, (int)(center.y + radius));

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        // 距离检查（圆形范围）
                        float dist = Vector2.Distance(center, new Vector2(x, y));
                        if (dist > radius) continue;

                        // 概率检查（中心概率更高，边缘概率更低）
                        float adjustedChance = destructionChance * (1f - dist / radius * 0.5f);
                        if (random.NextDouble() > adjustedChance) continue;

                        // 冷却检查
                        string tileKey = $"{x}_{y}";
                        if (_recentlyDestroyed.Contains(tileKey)) continue;

                        // 获取地块
                        WorldTile tile = World.world.GetTileSimple(x, y);
                        if (tile == null) continue;

                        // 应用地形破坏
                        if (ApplyDestructionToTile(tile, destructionType, options, caster))
                        {
                            _recentlyDestroyed.Add(tileKey);
                            destroyedCount++;
                        }
                    }
                }

                // 重置重绘计时器（让地形变化立即显示）
                if (destroyedCount > 0)
                {
                    try { World.world.resetRedrawTimer(); } catch (Exception ex) { Code.Core.DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
                }
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose($"[地形破坏] DestroyTerrain 异常: {e.Message}");
            }

            return destroyedCount;
        }

        /// <summary>
        /// 在施法者周围破坏地形（便捷方法）
        /// </summary>
        public static int DestroyTerrainAtCaster(
            Actor caster,
            float radius,
            float destructionChance,
            TerrainDestructionType destructionType = TerrainDestructionType.Generic)
        {
            if (caster == null) return 0;
            return DestroyTerrain(caster.current_position, radius, destructionChance, destructionType, caster);
        }

        // ============================================================
        // 内部方法
        // ============================================================

        /// <summary>
        /// 构建地形改造配置
        /// </summary>
        private static TerraformOptions BuildTerraformOptions(TerrainDestructionType type)
        {
            TerraformOptions options = new TerraformOptions();

            switch (type)
            {
                case TerrainDestructionType.EnergyStorm:
                    // 能量风暴：烧焦+点火+加热+移除龙卷风
                    options.add_burned = true;
                    options.set_fire = true;
                    options.add_heat = 15;
                    options.remove_tornado = true;
                    options.lightning_effect = false;
                    options.damage = 0;
                    break;

                case TerrainDestructionType.DimensionalStrike:
                    // 维度打击：烧焦+少量加热（高度变化在ApplyDestructionToTile中处理）
                    options.add_burned = true;
                    options.add_heat = 5;
                    options.damage = 0;
                    break;

                case TerrainDestructionType.DivinePunishment:
                    // 神罚：烧焦+点火+加热+屏幕震动
                    options.add_burned = true;
                    options.set_fire = true;
                    options.add_heat = 25;
                    options.shake = true;
                    options.shake_duration = 0.5f;
                    options.shake_interval = 0.05f;
                    options.shake_intensity = 0.3f;
                    options.damage = 0;
                    break;

                case TerrainDestructionType.Generic:
                default:
                    // 通用型：烧焦+少量加热
                    options.add_burned = true;
                    options.add_heat = 5;
                    options.damage = 0;
                    break;
            }

            return options;
        }

        /// <summary>
        /// 对单个地块应用破坏效果
        /// </summary>
        private static bool ApplyDestructionToTile(
            WorldTile tile,
            TerrainDestructionType destructionType,
            TerraformOptions options,
            Actor caster)
        {
            if (tile == null) return false;

            try
            {
                // 盖亚契约检查（保护某些地块不被破坏）
                try
                {
                    if (!MapAction.checkTileDamageGaiaCovenant(tile, true))
                    {
                        return false;
                    }
                }
                catch (Exception ex) { Code.Core.DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }

                // 跳过液体地块（海洋等）
                if (tile.Type != null && tile.Type.liquid)
                {
                    // 维度打击可以移除液体
                    if (destructionType == TerrainDestructionType.DimensionalStrike)
                    {
                        try { MapAction.removeLiquid(tile); } catch (Exception ex) { Code.Core.DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
                    }
                    else
                    {
                        return false;
                    }
                }

                // 应用通用地形改造效果（烧焦、点火、加热等）
                ApplyCommonTerraformEffects(tile, options, caster);

                // 强制点火（参考西幻世界：fire_timestamp + setFireData）
                if (options.set_fire && !tile.Type.liquid)
                {
                    try
                    {
                        tile.data.fire_timestamp = World.world.getCurWorldTime();
                        tile.setFireData(true);
                    }
                    catch { }
                }

                // 应用特定类型的额外效果
                switch (destructionType)
                {
                    case TerrainDestructionType.DimensionalStrike:
                        // 维度打击：降低地块高度
                        try { MapAction.decreaseTile(tile, true, "flash"); } catch (Exception ex) { Code.Core.DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
                        break;

                    case TerrainDestructionType.DivinePunishment:
                        // 神罚：额外加热
                        try { World.world.heat.addTile(tile, 10); } catch (Exception ex) { Code.Core.DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
                        break;
                }

                return true;
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose($"[地形破坏] ApplyDestructionToTile 异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 应用通用地形改造效果
        /// </summary>
        private static void ApplyCommonTerraformEffects(WorldTile tile, TerraformOptions options, Actor caster)
        {
            if (tile == null || options == null) return;

            try
            {
                // 烧焦
                if (options.add_burned && tile.Type != null && !tile.Type.liquid)
                {
                    try { tile.setBurned(-1); } catch (Exception ex) { Code.Core.DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
                }

                // 点火（烧焦的地块在原版游戏中可能会自动着火，这里不强制点火）
                // 如需强制点火，可使用 TerraformOptions.set_fire + ApplyTerraformArea

                // 加热
                if (options.add_heat != 0)
                {
                    try { World.world.heat.addTile(tile, options.add_heat); } catch (Exception ex) { Code.Core.DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
                }

                // 移除龙卷风（已恢复——Harmony patch修复了TornadoEffect的null key bug）
                if (options.remove_tornado)
                {
                    try { MapAction.tryRemoveTornadoFromTile(tile); } catch (Exception ex) { Code.Core.DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
                }

                // 屏幕震动（只在中心地块触发一次，避免重复）
                if (options.shake)
                {
                    // 屏幕震动在DestroyTerrain中统一处理，这里不重复触发
                }

                // 物理力
                if (options.apply_force)
                {
                    try
                    {
                        World.world.applyForceOnTile(
                            tile, 1, options.force_power, true, 
                            options.damage, options.ignore_kingdoms, caster, options, false);
                    }
                    catch (Exception ex) { Code.Core.DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
                }

                // 破坏建筑（如果有建筑且是敌方城市）
                if (tile.hasBuilding() && tile.building.hasCity())
                {
                    try
                    {
                        if (caster != null && caster.kingdom != null && 
                            tile.building.city.kingdom != null &&
                            caster.kingdom.isEnemy(tile.building.city.kingdom))
                        {
                            if (tile.building.asset.spawn_drops)
                            {
                                tile.building.spawnBurstSpecial(10);
                            }
                            tile.building.startDestroyBuilding();
                        }
                    }
                    catch (Exception ex) { Code.Core.DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
                }
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose($"[地形破坏] ApplyCommonTerraformEffects 异常: {e.Message}");
            }
        }
    }
}
