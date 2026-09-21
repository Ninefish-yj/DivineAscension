
using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Core;

namespace Code.Combat
{
    /// <summary>
    /// 统一AOE伤害管理器
    /// 提供范围伤害的统一接口，修复了原实现的性能和敌友判断问题
    /// </summary>
    public static class DSAoEDamageManager
    {
        // ============================================================
        // 核心AOE伤害方法
        // ============================================================

        /// <summary>
        /// 对指定位置周围的敌人造成范围伤害
        /// </summary>
        /// <param name="attacker">施法者（用于敌友判断和伤害加成）</param>
        /// <param name="centerPosition">AOE中心位置</param>
        /// <param name="radius">伤害半径（世界坐标单位）</param>
        /// <param name="baseDamage">基础伤害</param>
        /// <param name="damageType">伤害类型（0=物理，1=魔法，2=真实）</param>
        /// <param name="distanceDecay">是否启用距离衰减（中心伤害最高，边缘为0）</param>
        /// <param name="hitAllies">是否伤害友军（默认false，只伤害敌人）</param>
        /// <returns>命中的敌人数量</returns>
        public static int ApplyAreaDamage(
            Actor attacker,
            Vector2 centerPosition,
            float radius,
            float baseDamage,
            int damageType = 0,
            bool distanceDecay = true,
            bool hitAllies = false)
        {
            if (attacker == null || World.world == null) return 0;
            if (radius <= 0f || baseDamage <= 0f) return 0;

            try
            {
                // 获取中心瓦片
                WorldTile centerTile = World.world.GetTileSimple((int)centerPosition.x, (int)centerPosition.y);
                if (centerTile == null) return 0;

                // 使用空间分区查询附近单位（性能优化，替代遍历所有单位）
                // 参数: 中心瓦片, chunk搜索范围, 搜索半径, 是否包含死亡单位
                var nearbyUnits = Finder.getUnitsFromChunk(centerTile, 16, radius + 5f, false);
                if (nearbyUnits == null) return 0;

                int hitCount = 0;

                foreach (Actor target in nearbyUnits)
                {
                    if (target == null || !target.isAlive() || Code.Data.ActorDataAccessor.GetData(target) == null) continue;
                    if (target == attacker) continue;

                    // 敌友判断
                    if (!hitAllies && !IsEnemy(attacker, target)) continue;

                    // 距离检查
                    float dist = Vector2.Distance(centerPosition, target.current_position);
                    if (dist > radius) continue;

                    // 计算最终伤害
                    float finalDamage = baseDamage;
                    if (distanceDecay && radius > 0f)
                    {
                        // 线性距离衰减：中心100%，边缘0%
                        finalDamage *= Mathf.Max(0f, 1f - dist / radius);
                    }

                    if (finalDamage <= 0f) continue;

                    // 造成伤害（使用原版getHit，经过抗性和防御计算）
                    ApplyDamageToTarget(attacker, target, finalDamage, damageType);
                    hitCount++;
                }

                return hitCount;
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[AOE伤害] ApplyAreaDamage 异常: {e.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 对施法者周围的敌人造成范围伤害
        /// </summary>
        public static int ApplyAreaDamageAtCaster(
            Actor attacker,
            float radius,
            float baseDamage,
            int damageType = 0,
            bool distanceDecay = true,
            bool hitAllies = false)
        {
            if (attacker == null) return 0;
            return ApplyAreaDamage(attacker, attacker.current_position, radius, baseDamage,
                damageType, distanceDecay, hitAllies);
        }

        /// <summary>
        /// 对目标单位周围的敌人造成范围伤害
        /// </summary>
        public static int ApplyAreaDamageAtTarget(
            Actor attacker,
            Actor target,
            float radius,
            float baseDamage,
            int damageType = 0,
            bool distanceDecay = true,
            bool hitAllies = false)
        {
            if (target == null) return 0;
            return ApplyAreaDamage(attacker, target.current_position, radius, baseDamage,
                damageType, distanceDecay, hitAllies);
        }

        // ============================================================
        // 范围治疗方法
        // ============================================================

        /// <summary>
        /// 对指定位置周围的友军进行范围治疗
        /// </summary>
        /// <param name="healer">治疗者</param>
        /// <param name="centerPosition">治疗中心位置</param>
        /// <param name="radius">治疗半径</param>
        /// <param name="healAmount">治疗量</param>
        /// <param name="includeSelf">是否包含自己</param>
        /// <returns>治疗的友军数量</returns>
        public static int ApplyAreaHeal(
            Actor healer,
            Vector2 centerPosition,
            float radius,
            float healAmount,
            bool includeSelf = true)
        {
            if (healer == null || World.world == null) return 0;
            if (radius <= 0f || healAmount <= 0f) return 0;

            try
            {
                WorldTile centerTile = World.world.GetTileSimple((int)centerPosition.x, (int)centerPosition.y);
                if (centerTile == null) return 0;

                var nearbyUnits = Finder.getUnitsFromChunk(centerTile, 16, radius + 5f, false);
                if (nearbyUnits == null) return 0;

                int healedCount = 0;

                foreach (Actor target in nearbyUnits)
                {
                    if (target == null || !target.isAlive() || Code.Data.ActorDataAccessor.GetData(target) == null) continue;
                    if (!includeSelf && target == healer) continue;

                    // 只治疗友军
                    if (!IsAlly(healer, target)) continue;

                    float dist = Vector2.Distance(centerPosition, target.current_position);
                    if (dist > radius) continue;

                    // 恢复生命值（用ActorDataAccessor访问data.health，避免FieldAccessException）
                    var targetData = Code.Data.ActorDataAccessor.GetData(target);
                    if (targetData != null)
                    {
                        float newHp = Mathf.Min(target.getHealth() + healAmount, target.getMaxHealth());
                        targetData.health = (int)newHp;
                    }
                    healedCount++;
                }

                return healedCount;
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[AOE治疗] ApplyAreaHeal 异常: {e.Message}");
                return 0;
            }
        }

        // ============================================================
        // 辅助方法
        // ============================================================

        /// <summary>
        /// 判断两个单位是否为敌人
        /// </summary>
        private static bool IsEnemy(Actor a, Actor b)
        {
            if (a == null || b == null) return false;

            // 都有王国时，判断王国是否敌对
            if (a.kingdom != null && b.kingdom != null)
            {
                return a.kingdom.isEnemy(b.kingdom);
            }

            // 一方无王国时，默认视为敌人（野生生物/中立单位）
            // 但如果是同一种族的野生单位，视为中立
            if (a.kingdom == null && b.kingdom == null)
            {
                // 双方都是野生单位，不互相伤害
                return false;
            }

            // 一方有王国一方没有，视为敌人（野生单位攻击城市等）
            return true;
        }

        /// <summary>
        /// 判断两个单位是否为友军
        /// </summary>
        private static bool IsAlly(Actor a, Actor b)
        {
            if (a == null || b == null) return false;
            if (a == b) return true;

            // 同一王国视为友军
            if (a.kingdom != null && b.kingdom != null)
            {
                return a.kingdom == b.kingdom || !a.kingdom.isEnemy(b.kingdom);
            }

            // 都无王国时，视为中立（不互相治疗）
            return false;
        }

        /// <summary>
        /// 对目标造成伤害（使用原版getHit，经过抗性和防御计算）
        /// </summary>
        private static void ApplyDamageToTarget(Actor attacker, Actor target, float damage, int damageType)
        {
            if (target == null || !target.isAlive() || damage <= 0f) return;

            try
            {
                // 使用原版getHit方法，经过完整的抗性和防御计算
                // 参数: 伤害, 是否暴击, 攻击类型, 攻击者, 是否显示伤害数字, 是否击退, 是否无视无敌
                // 注意：原版AttackType只有Weapon和Other，没有Magic，统一用Other
                target.getHit(damage, false, AttackType.Other, attacker, true, true, false);
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[AOE伤害] ApplyDamageToTarget 异常: {e.Message}");
            }
        }
    }
}
