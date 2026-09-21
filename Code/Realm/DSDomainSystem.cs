using System;
using System.Collections.Generic;
using Code.Data;
using UnityEngine;

namespace Code.Realm
{
    /// <summary>
    /// 异能领域系统：参考天人武道领域对抗设计，改成现代基因异能题材
    /// </summary>
    public static class DSDomainSystem
    {
        public const float PulseIntervalSeconds = 1f;
        public const int BaseRadiusTiles = 20;

        private static readonly HashSet<long> s_seen = new HashSet<long>();
        private static Action<Actor> s_pulseCb;
        private static Actor s_pulseCaster;
        private static float s_pulseDamageMul = 1f;

        public static void ClearForWorldReset()
        {
            s_seen.Clear();
        }

        /// <summary>
        /// 检查领域是否被压制（同阶或更高阶的单位进入领域时，领域效果减半）
        /// </summary>
        public static bool IsSuppressed(Actor caster)
        {
            if (caster == null || World.world == null) return false;
            
            int casterTier = CultivationData.GetRealmTier(caster);
            if (casterTier < 6) return true; // 6阶以下没有领域

            // 检查范围内有没有同阶或更高阶的敌人
            int radius = GetRadiusTiles(caster);
            WorldTile center = World.world.GetTileSimple((int)caster.current_position.x, (int)caster.current_position.y);
            if (center == null) return false;

            int chunkScan = (radius + 15) / 16 + 1;
            var list = Finder.getUnitsFromChunk(center, chunkScan, radius, false);
            if (list == null) return false;

            long casterId = caster.getID();
            s_seen.Clear();
            
            foreach (Actor u in list)
            {
                if (u == null || !u.isAlive()) continue;
                long uid = u.getID();
                if (uid == casterId) continue;
                if (!s_seen.Add(uid)) continue;
                
                // 只检查敌对单位
                try
                {
                    if (!(caster.kingdom?.isEnemy(u.kingdom) ?? true)) continue;
                }
                catch { continue; }
                
                int uTier = CultivationData.GetRealmTier(u);
                if (uTier >= casterTier)
                {
                    return true; // 有同阶或更高阶的敌人，领域被压制
                }
            }
            return false;
        }

        /// <summary>
        /// 获取领域半径
        /// </summary>
        public static int GetRadiusTiles(Actor caster)
        {
            if (caster == null) return BaseRadiusTiles;
            int tier = CultivationData.GetRealmTier(caster);
            int radius = BaseRadiusTiles;
            
            // 高阶增加半径
            if (tier >= 9) radius += 5;
            if (tier >= 11) radius += 5;
            if (tier >= 12) radius += 5;
            if (tier >= 13) radius += 10;
            
            return radius;
        }

        /// <summary>
        /// 领域脉冲：对范围内所有敌人造成效果
        /// </summary>
        public static void Pulse(Actor caster)
        {
            if (caster == null || !caster.isAlive() || World.world == null) return;
            int tier = CultivationData.GetRealmTier(caster);
            if (tier < 6) return; // 6阶以下没有领域
            
            if (IsSuppressed(caster))
            {
                s_pulseDamageMul = 0.5f; // 被压制时伤害减半
            }
            else
            {
                s_pulseDamageMul = 1f;
            }

            s_pulseCaster = caster;
            s_pulseCb = OnPulseEnemy;
            
            int radius = GetRadiusTiles(caster);
            WorldTile center = World.world.GetTileSimple((int)caster.current_position.x, (int)caster.current_position.y);
            if (center == null) return;

            int chunkScan = (radius + 15) / 16 + 1;
            s_seen.Clear();
            long casterId = caster.getID();
            
            var list = Finder.getUnitsFromChunk(center, chunkScan, radius, false);
            if (list == null) return;

            foreach (Actor u in list)
            {
                if (u == null || !u.isAlive() || u.data == null) continue;
                long uid = u.getID();
                if (uid == casterId) continue;
                if (!s_seen.Add(uid)) continue;
                
                // 只处理敌对单位
                try
                {
                    if (!(caster.kingdom?.isEnemy(u.kingdom) ?? true)) continue;
                }
                catch { continue; }

                s_pulseCb(u);
            }

            s_pulseCaster = null;
            s_pulseCb = null;
        }

        /// <summary>
        /// 对单个敌人施加领域效果
        /// </summary>
        private static void OnPulseEnemy(Actor u)
        {
            Actor caster = s_pulseCaster;
            if (caster == null || u == null) return;

            int tier = CultivationData.GetRealmTier(caster);
            float atk = 0f;
            try { atk = caster.stats["damage"]; } catch { }
            
            if (atk <= 0f) return;
            float damage = atk * 0.1f * s_pulseDamageMul; // 每秒造成10%攻击力的伤害

            // 不同阶位的额外效果
            if (tier >= 9)
            {
                // 天灾领域：额外造成能量伤害
                damage *= 1.5f;
            }
            if (tier >= 11)
            {
                // 圣域：额外降低敌人属性
                try { u.stats["damage"] = Mathf.Max(0f, u.stats["damage"] * 0.95f); } catch { }
            }
            if (tier >= 12)
            {
                // 神域：全属性降低
                try { u.stats["strength"] = Mathf.Max(0f, u.stats["strength"] * 0.95f); } catch { }
                try { u.stats["intelligence"] = Mathf.Max(0f, u.stats["intelligence"] * 0.95f); } catch { }
            }
            if (tier >= 13)
            {
                // 超神领域：法则伤害，无视防御
                damage *= 2f;
            }

            // 造成伤害
            try
            {
                u.getHit(damage, false, AttackType.Other, caster, false, false, true);
            }
            catch { }
        }
    }
}
