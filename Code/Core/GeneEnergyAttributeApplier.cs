
using System;
using System.Reflection;
using Code.Data;
using Code.Realm;
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

        // 登神协调效应（基因翻倍）作用的属性 key——须与原版 stats 合成 key 一致（反编译 Actor.cs 核实）
        private static readonly string[] STAT_BOOST_KEYS = new string[]
        {
            "health", "damage", "speed", "mana", "stamina", "intelligence", "armor", "warfare"
        };

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

                // 登神协调效应（基因序列完美配对，基因属性翻倍）：
                // ScanAndApplyGeneticBoost 在登神时把翻倍数值写入 custom_data_float["ds_boost_{key}"]，
                // 这里真正加到合成后的 stats——此前只有 UI 文案（ds_genetic_boost 字符串），属性从未生效。
                if (actor.hasTrait("ds_species_transcendent"))
                {
                    try
                    {
                        ActorData data = ActorDataAccessor.GetData(actor);
                        if (data != null && data.custom_data_float != null)
                        {
                            foreach (string bk in STAT_BOOST_KEYS)
                            {
                                if (data.custom_data_float.TryGetValue("ds_boost_" + bk, out float bv) && bv > 0f)
                                {
                                    stats[bk] += bv;
                                }
                            }
                        }
                    }
                    catch (System.Exception dsEx) { DSDebug.Verbose("[基因能量属性加成器] 协调效应应用失败: " + dsEx.Message); }
                }

                // 异能共鸣（基因连锁，v0.15.0 落地）：
                // GeneLinkageSystem.ApplyLinkageBonuses 把连锁百分比加成写入 custom_data_float["ds_link_{key}"]，
                // 这里按 (1+百分比) 乘到合成后的 stats（damage/health/speed/armor/mana）。
                // 替代已废弃的 12 阶"基因共鸣"数值特质——异能共鸣从此由真实连锁机制驱动。
                try
                {
                    ActorData data = ActorDataAccessor.GetData(actor);
                    if (data != null && data.custom_data_float != null)
                    {
                        // 全属性连锁（ds_link_all）叠加到各主属性
                        float allBonus = 0f;
                        data.custom_data_float.TryGetValue("ds_link_all", out allBonus);

                        string[] linkKeys = { "damage", "health", "speed", "armor", "mana" };
                        foreach (string lk in linkKeys)
                        {
                            float linkBonus = 0f;
                            data.custom_data_float.TryGetValue("ds_link_" + lk, out linkBonus);
                            float total = linkBonus + allBonus;
                            if (total > 0f)
                            {
                                stats[lk] = stats[lk] * (1f + total);
                            }
                        }
                    }
                }
                catch (System.Exception dsEx) { DSDebug.Verbose("[基因能量属性加成器] 异能共鸣应用失败: " + dsEx.Message); }

                // 基因个体效果（v0.16.0）：56 个基因各自的 EffectValue × 表达倍率聚合后应用。
                // AttackBonus→伤害、DefenseBonus→护甲、SpeedBonus→速度、HealthBonus→生命、EnergyMaxBonus→能量上限（各限+50%）。
                // 此前这些数值只出现在图谱描述里，从未真正改变属性。
                try
                {
                    float geneAtk = GeneExpressionSystem.GetIndividualEffectSum(actor, GeneEffectType.AttackBonus);
                    if (geneAtk > 0f) stats["damage"] = stats["damage"] * (1f + geneAtk);
                    float geneDef = GeneExpressionSystem.GetIndividualEffectSum(actor, GeneEffectType.DefenseBonus);
                    if (geneDef > 0f) stats["armor"] = stats["armor"] * (1f + geneDef);
                    float geneSpd = GeneExpressionSystem.GetIndividualEffectSum(actor, GeneEffectType.SpeedBonus);
                    if (geneSpd > 0f) stats["speed"] = stats["speed"] * (1f + geneSpd);
                    float geneHp = GeneExpressionSystem.GetIndividualEffectSum(actor, GeneEffectType.HealthBonus);
                    if (geneHp > 0f) stats["health"] = stats["health"] * (1f + geneHp);
                    float geneEn = GeneExpressionSystem.GetIndividualEffectSum(actor, GeneEffectType.EnergyMaxBonus);
                    if (geneEn > 0f) stats["mana"] = stats["mana"] * (1f + geneEn);
                }
                catch (System.Exception dsEx) { DSDebug.Verbose("[基因能量属性加成器] 基因个体效果应用失败: " + dsEx.Message); }
            }
            catch (System.Exception e)
            {
                DSDebug.Verbose("[基因能量属性加成器] 应用属性加成失败: " + e.Message);
            }
        }
    }
}
