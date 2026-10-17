// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Core;

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

                        // 概率检查（v0.18.46：中心地块必破，边缘线性递减至 chance×0.5——修复旧版中心也只有50%概率导致"破坏太弱"）
                        float adjustedChance = Mathf.Lerp(1f, destructionChance * 0.5f, dist / radius);
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
                    try { World.world.resetRedrawTimer(); } catch (Exception ex) { Code.Core.DSDebug.Warning("[GeneCultivation] 操作失败: " + ex.Message); }
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
                // ============================================================
        // 内部方法
        // ============================================================

        /// <summary>
        /// 构建地形改造配置
        /// v0.18.77：爆炸化——explode_tile 炸树/炸坑、damage_buildings 炸全部建筑（原版 applyTileDamage 处理），
        /// damage>0 对单位造成爆炸伤害。旧版只烧焦+加热(damage=0)，树/非城市建筑/坑都不破坏，配不上炫特效。
        /// </summary>
        private static TerraformOptions BuildTerraformOptions(TerrainDestructionType type)
        {
            TerraformOptions options = new TerraformOptions();
            options.explode_tile = true;        // 炸树/炸可爆地块/炸坑
            options.damage_buildings = true;    // 炸建筑（含非城市建筑）
            options.add_burned = true;          // 烧焦
            options.shake = true;               // 屏幕震动（视觉匹配）
            options.shake_duration = 0.5f;
            options.shake_interval = 0.04f;
            options.shake_intensity = 0.35f;    // v0.18.88 上调（大破坏配大震动）

            switch (type)
            {
                case TerrainDestructionType.EnergyStorm:
                    // 能量风暴：烧焦+点火+加热+移除龙卷风+爆炸伤害（v0.18.88 伤害/加热上调）
                    options.set_fire = true;
                    options.add_heat = 25;
                    options.remove_tornado = true;
                    options.lightning_effect = false;
                    options.damage = 50;
                    break;

                case TerrainDestructionType.DimensionalStrike:
                    // 维度打击：烧焦+少量加热+爆炸伤害（高度变化在ApplyDestructionToTile中处理；v0.18.88 上调）
                    options.add_heat = 10;
                    options.damage = 90;
                    break;

                case TerrainDestructionType.DivinePunishment:
                    // 神罚：烧焦+点火+强加热+高爆炸伤害（v0.18.88 大幅上调，匹配超神级特效）
                    options.set_fire = true;
                    options.add_heat = 45;
                    options.damage = 160;
                    break;

                case TerrainDestructionType.Generic:
                default:
                    // 通用型：烧焦+少量加热+基础爆炸伤害（v0.18.88 上调）
                    options.add_heat = 10;
                    options.damage = 20;
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
                catch (Exception ex) { Code.Core.DSDebug.Warning("[GeneCultivation] 操作失败: " + ex.Message); }

                // 跳过液体地块（海洋等）
                if (tile.Type != null && tile.Type.liquid)
                {
                    // 维度打击可以移除液体
                    if (destructionType == TerrainDestructionType.DimensionalStrike)
                    {
                        try { MapAction.removeLiquid(tile); } catch (Exception ex) { Code.Core.DSDebug.Warning("[GeneCultivation] 操作失败: " + ex.Message); }
                    }
                    else
                    {
                        return false;
                    }
                }

                // 应用地形破坏（v0.18.77：原版 explodeTile 中心格全强度——
                // 单位爆炸伤害 + 炸树(explodable) + 炸坑(decreaseTile) + 烧焦 + 随机火）
                MapAction.explodeTile(tile, 0f, 1f, tile, options);

                // 建筑伤害（explodeTile 只炸液体建筑；applyTileDamage 的 damage_buildings 逻辑手动补，含非城市建筑）
                if (options.damage_buildings && tile.hasBuilding() && tile.building.isAlive())
                {
                    try { tile.building.getHit(options.damage); } catch (Exception ex) { Code.Core.DSDebug.Warning("[GeneCultivation] 操作失败: " + ex.Message); }
                }

                // 固定点火（参考西幻世界：fire_timestamp + setFireData）
                if (options.set_fire && !tile.Type.liquid)
                {
                    try { tile.data.fire_timestamp = World.world.getCurWorldTime(); tile.setFireData(true); } catch { }
                }

                // 加热
                if (options.add_heat != 0)
                {
                    try { World.world.heat.addTile(tile, options.add_heat); } catch (Exception ex) { Code.Core.DSDebug.Warning("[GeneCultivation] 操作失败: " + ex.Message); }
                }

                // 移除龙卷风
                if (options.remove_tornado)
                {
                    try { MapAction.tryRemoveTornadoFromTile(tile); } catch (Exception ex) { Code.Core.DSDebug.Warning("[GeneCultivation] 操作失败: " + ex.Message); }
                }

                // 应用特定类型的额外效果
                switch (destructionType)
                {
                    case TerrainDestructionType.DimensionalStrike:
                        // 维度打击：降低地块高度
                        try { MapAction.decreaseTile(tile, true, "flash"); } catch (Exception ex) { Code.Core.DSDebug.Warning("[GeneCultivation] 操作失败: " + ex.Message); }
                        break;

                    case TerrainDestructionType.DivinePunishment:
                        // 神罚：额外加热
                        try { World.world.heat.addTile(tile, 10); } catch (Exception ex) { Code.Core.DSDebug.Warning("[GeneCultivation] 操作失败: " + ex.Message); }
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
    }
}
