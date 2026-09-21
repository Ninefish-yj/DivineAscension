
using System;
using System.Reflection;
using Code.Data;
using HarmonyLib;
using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// 基因能量属性加成应用器
    /// 在 Actor.updateStats（stats 重建）后动态应用攻速、射程、速度加成
    /// </summary>
    [HarmonyPatch(typeof(Actor), "updateStats")]
    public static class GeneEnergyAttributeApplier
    {
        // 攻速最终上限：40（=0.025 秒攻击冷却），防止高阶叠状态后攻速失控
        public const float MAX_ATTACK_SPEED = 40f;

        // 缓存 stats 字段信息（BaseSimObject.stats 是 internal readonly BaseStats），避免重复反射
        private static FieldInfo _statsField;
        private static bool _fieldsInitialized;

        [HarmonyPostfix]
        public static void Postfix(Actor __instance)
        {
            if (__instance == null) return;
            try { ApplyAttributes(__instance); }
            catch (System.Exception dsEx) { DSDebug.Verbose("[基因能量属性加成器] updateStats 后应用失败: " + dsEx.Message); }
        }

        /// <summary>
        /// 初始化 stats 字段信息（只执行一次）
        /// </summary>
        private static void EnsureStatsField()
        {
            if (_fieldsInitialized) return;
            try
            {
                // stats 定义在 BaseSimObject（Actor 的基类）：internal readonly BaseStats stats
                _statsField = typeof(BaseSimObject).GetField("stats",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _fieldsInitialized = true;
                DSDebug.Verbose("[基因能量属性加成器] stats 字段初始化完成: " + (_statsField != null));
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("[基因能量属性加成器] stats 字段初始化失败: " + e.Message);
                _fieldsInitialized = true; // 标记已初始化，避免重复尝试
            }
        }

        /// <summary>
        /// 应用基因能量属性加成（在 Actor.updateStats Postfix 中调用）
        /// stats 此时已合成完成（亚种/特质/状态/装备等全部合并），直接乘 GE 加成
        /// </summary>
        public static void ApplyAttributes(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;
            if (!CultivationData.IsAscended(actor)) return;

            try
            {
                EnsureStatsField();
                if (_statsField == null) return;
                BaseStats stats = (BaseStats)_statsField.GetValue(actor);
                if (stats == null) return;

                // 攻速加成：基因能量100=+10%，上限+50%；最终攻速上限 40（防止高阶叠状态后失控）
                float attackSpeedBonus = GeneEnergyCalculator.GetAttackSpeedBonus(actor);
                if (attackSpeedBonus > 0f)
                {
                    float newAttackSpeed = stats["attack_speed"] * (1f + Mathf.Min(0.5f, attackSpeedBonus * 0.1f));
                    if (newAttackSpeed > MAX_ATTACK_SPEED) newAttackSpeed = MAX_ATTACK_SPEED;
                    stats["attack_speed"] = newAttackSpeed;
                }
                // 神创基因攻速加成：独立百分比乘（不受×0.1稀释），仍受40上限约束
                float divineSpd = GeneEnergyCalculator.GetDivineSpeedBonus(actor);
                if (divineSpd > 0f)
                {
                    float newAttackSpeed = stats["attack_speed"] * (1f + divineSpd);
                    if (newAttackSpeed > MAX_ATTACK_SPEED) newAttackSpeed = MAX_ATTACK_SPEED;
                    stats["attack_speed"] = newAttackSpeed;
                }

                // 神创基因生命加成：独立百分比乘最大生命（写 stats["health"]，
                // 原版 getMaxHealth() 直接返回 stats["health"]，无需再同步任何字段）
                float divineHp = GeneEnergyCalculator.GetDivineHpBonus(actor);
                if (divineHp > 0f)
                {
                    stats["health"] = stats["health"] * (1f + divineHp);
                }

                // 神创基因能量上限加成：独立百分比乘原版 mana（异能能量的容量标识）
                float divineMaxEn = GeneEnergyCalculator.GetDivineMaxEnBonus(actor);
                if (divineMaxEn > 0f)
                {
                    stats["mana"] = stats["mana"] * (1f + divineMaxEn);
                }

                // 射程加成：基因能量100=+5%，上限+30%
                float rangeBonus = GeneEnergyCalculator.GetRangeBonus(actor);
                if (rangeBonus > 0f)
                {
                    stats["range"] = stats["range"] * (1f + Mathf.Min(0.3f, rangeBonus * 0.05f));
                }

                // 速度加成：基因能量100=+5%，上限+30%
                float speedBonus = GeneEnergyCalculator.GetSpeedBonus(actor);
                if (speedBonus > 0f)
                {
                    stats["speed"] = stats["speed"] * (1f + Mathf.Min(0.3f, speedBonus * 0.05f));
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Verbose("[基因能量属性加成器] 应用属性加成失败: " + e.Message);
            }
        }
    }
}
