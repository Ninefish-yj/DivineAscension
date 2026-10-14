// ============================================================

using System;
using Code.UI;
using Code.Core;
using Code.Data;
using UnityEngine;

namespace Code.Combat
{
    public static class CombatRank
    {
        /// <summary>
        /// 获取跨阶伤害衰减率（防御者阶位 vs 攻击者阶位）
        /// 返回0~1的衰减比例，1表示完全免疫
        /// </summary>
        public static float GetDamageReduction(int defenderTier, int attackerTier)
        {
            if (defenderTier <= 0 || attackerTier <= 0) return 0f;
            int diff = defenderTier - attackerTier;
            if (diff <= 0) return 0f;  // 同阶或低阶攻击高阶，无衰减

            // 每差1阶衰减15%，上限85%
            float reduction = diff * 0.15f;
            return Mathf.Clamp(reduction, 0f, 0.85f);
        }

        /// <summary>

        /// <summary>
        /// 年度结算中调用：回溯血量变化，应用跨阶伤害衰减
        /// 检测到异能者受到伤害且攻击者阶位较低时，回复衰减部分的血量
        /// </summary>
        public static void ApplyCombatRankProtection(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;
            int tier = CultivationData.GetRealmTier(actor);
            if (tier <= 0) return;

            // 获取当前血量
            float currentHp = GetActorHp(actor);
            float lastHp = CultivationData.GetLastHp(actor);

            // 首次记录
            if (lastHp < 0)
            {
                CultivationData.SetLastHp(actor, currentHp);
                return;
            }

            // 检测是否受到伤害（血量下降）
            float hpLoss = lastHp - currentHp;
            if (hpLoss <= 0)
            {
                // 没有受伤，更新记录
                CultivationData.SetLastHp(actor, currentHp);
                return;
            }

            // 检测攻击者：0.51.x 中 attackedBy 是 internal 字段（getHit 时赋值），外部程序集只能反射
            BaseSimObject attackerObj = null;
            try
            {
                var attackedByField = typeof(Actor).GetField("attackedBy",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (attackedByField != null) attackerObj = attackedByField.GetValue(actor) as BaseSimObject;
            }
            catch { }
            // 兼容其他版本：GetAttackedBy() 方法（存在时调用）
            if (attackerObj == null)
            {
                try
                {
                    var getAttackedBy = typeof(Actor).GetMethod("GetAttackedBy",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (getAttackedBy != null) attackerObj = getAttackedBy.Invoke(actor, null) as BaseSimObject;
                }
                catch { }
            }
            if (attackerObj == null)
            {
                CultivationData.SetLastHp(actor, currentHp);
                return;
            }

            // 直接访问a字段（源码模组模式，完全信任）
            Actor attacker = attackerObj.a as Actor;
            if (attacker == null)
            {
                CultivationData.SetLastHp(actor, currentHp);
                return;
            }

            int attackerTier = CultivationData.GetRealmTier(attacker);
            float reduction = GetDamageReduction(tier, attackerTier);

            if (reduction > 0f)
            {
                // 回复衰减部分的血量（模拟伤害被位格抵消）
                float recovered = hpLoss * reduction;
                HealActor(actor, recovered);
                CultivationData.SetDamageReduction(actor, reduction);

                if (AnnualTickManager.PerformanceDiagnosticsEnabled && reduction > 0.3f)
                {
                    DSDebug.Verbose(string.Format("[GeneCultivation] 位格防护: {0}({1}阶) 受到 {2}({3}阶) 攻击，衰减{4:F0}%，回复{5:F0}血量",
                        actor.getName(), tier, attacker.getName(), attackerTier, reduction * 100, recovered));
                }
            }

            // 更新血量记录
            CultivationData.SetLastHp(actor, GetActorHp(actor));
        }

        /// <summary>获取单位当前血量（直接调用公开API）</summary>
        private static float GetActorHp(Actor actor)
        {
            if (actor == null) return 0f;
            try { return actor.getHealth(); }
            catch { return 0f; }
        }

        /// <summary>治疗单位（直接调用公开API，回复血量不超上限）</summary>
        private static void HealActor(Actor actor, float amount)
        {
            if (actor == null || amount <= 0 || !actor.isAlive()) return;
            try
            {
                int current = actor.getHealth();
                int maxHp = actor.getMaxHealth();
                int newHp = Mathf.Min(current + (int)amount, maxHp);
                actor.setHealth(newHp);
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] HealActor 异常: " + dsEx.Message); }
        }

    }
}

