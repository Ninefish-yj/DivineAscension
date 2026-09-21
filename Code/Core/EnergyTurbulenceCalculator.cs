// ============================================================

using System;
using System.Collections.Generic;
using Code.Data;
using Code.Realm;
using UnityEngine;

namespace Code.Core
{
    /// <summary>全局可调配置（对应文档19.2节）
    /// 注意：这些属性直接引用DengShenConfig（NML原生配置系统）的值，
    /// 用户在游戏设置界面修改配置会实时影响游戏逻辑。
    /// </summary>
    public static class CultivationConfig
    {
        public static float CultivationRateMultiplier {
            get => DengShenConfig.CultivationRateMultiplier;
            set => DengShenConfig.CultivationRateMultiplier = value;
        }
        public static float TurbulenceDecayRate {
            get => DengShenConfig.TurbulenceDecayRate;
            set => DengShenConfig.TurbulenceDecayRate = value;
        }
        public static float DomainUpkeepMultiplier = 1.0f;
        public static float MortalAwakenChance {
            get => DengShenConfig.MortalAwakenChance;
            set => DengShenConfig.MortalAwakenChance = value;
        }
        public static float EnlightenmentGainBase = 50f;
        public static float HighTurbulenceThreshold = 80f;
        public const float BreakthroughTurbulenceLimit = 70f;
    }

    public static class EnergyTurbulenceCalculator
    {
        // ============================================================
        //  各阶基础修炼速度（异能能量/年）
        // ============================================================
        private static readonly float[] _baseCultivationSpeed =
        {
            // 修炼效率（单位：能级/年）
            // 重新设计：从200~100亿压缩到15~80万，数值更合理
            // 基础突破时间约6.7年/阶，从1阶到13阶基础约80年
            // 高境界有难度但不是不可能：突破成功率阶梯式下降
            0f, 20f, 50f, 100f, 200f, 400f, 800f,
            1500f, 3000f, 6000f, 12000f, 25000f,
            50000f, 100000f
        };

        // ============================================================
        //  年度异能能量结算
        // ============================================================

        /// <summary>
        /// 对单个异能者执行年度异能能量结算。
        /// 获取：基础修炼×倍率×地块加成×静坐加成
        /// 消耗：场域维持、真神结界维持
        /// 禁令2：绝不从击杀/破坏中获取异能能量
        /// </summary>
        public static void SettleAnnualQi(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;
            int tier = CultivationData.GetRealmTier(actor);
            if (tier <= 0) return;

            // ---- 异能能量获取 ----
            float gain = _baseCultivationSpeed[tier] * CultivationConfig.CultivationRateMultiplier;
            {
                gain *= 1.2f;
            }

            //  原版深度交互：修炼速度受原版属性影响（smart/genius/blessed修炼更快）
            gain *= VanillaInteractionManager.GetCultivationSpeedMultiplier(actor);

            //  深度觉醒状态：修炼速度+100%（持续3年）
            if (CultivationData.IsEnlightenmentActive(actor))
            {
                gain *= 2.0f;
            }

            //  动物修炼速度惩罚：非智慧生物在获得高级脑功能区（文明化特质）之前，修炼速度更慢
            // 现代异能体系特色：动物可以觉醒异能，但没有智慧，修炼效率低
            bool isIntelligent = AnnualTickManager.IsHumanoidCivilized(actor); // 智慧生物 = 种族有脑属性或拥有文明化特质
            if (!isIntelligent)
            {
                gain *= 0.25f; // 动物修炼速度为智慧生物的25%（再减半）
            }

            // 地块异能能量浓度加成（简化：±20%随机波动）
            float tileBonus = 1f + UnityEngine.Random.Range(-0.2f, 0.3f);
            gain *= tileBonus;

            // 静坐修炼加成（玩家引导，有冷却）
            int medCd = CultivationData.GetCooldown(actor, "meditation");
            if (medCd > 0)
            {
                gain *= 1.3f;
                CultivationData.DecrementCooldown(actor, "meditation");
            }

            // ============================================================
            //  前四阶修炼快速机制
            //  设计：低境界基因潜能尚未完全开发，修炼速度更快
            //  1阶×5.0 / 2阶×4.0 / 3阶×3.0 / 4阶×2.5 / 5阶+×1.0（百年内可达4阶）
            // ============================================================
            float[] earlyTierBonus = { 1.0f, 5.0f, 4.0f, 3.0f, 2.5f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f };
            if (tier >= 1 && tier <= 13)
            {
                gain *= earlyTierBonus[tier];
            }

            // 本源阶(9)：抑制异能能量外泄，等效获取提升
            if (tier >= 9) gain *= 1.2f;

            //  境界修炼效率：境界越高，对能量的利用越高效（RealmFeatures 境界特色）
            float realmCult = Realm.RealmFeatures.GetRealmCultivationEfficiency(tier);
            if (realmCult > 1f)
            {
                gain *= realmCult;
            }

            //  门徒修炼加速：真神基因改造的门徒，修炼速度+50%（基因层面的优化）
            if (CultivationData.GetCooldown(actor, "ds_disciple") > 0)
            {
                gain *= 1.5f;
            }

            //  建筑修炼加速：境界建筑（聚灵阵/洞天福地）提供的修炼速度加成
//             float buildingBoost = CultivationData.GetBuildingCultivationBoost(actor);
//             if (buildingBoost > 0f)
//             {
//                 gain *= (1f + buildingBoost);
//             }

            //  组织职位修炼加速：加入异能组织并获得职位身份的单位，修炼速度小幅提升
            //  纵向等级（取最高职位）+ 横向荣誉身份（可叠加），数值温和（2%~10%）
            float sectBoost = GetSectCultivationBoost(actor);
            if (sectBoost > 0f)
            {
                gain *= (1f + sectBoost);
            }

            //  真神纪元效果：纪元主宰的基因染色体决定全局能量获取和修炼速度加成
            try
            {
                var eraEffect = Realm.RealmJudge.GetCurrentEraEffect();
                if (eraEffect != null)
                {
                    // EnergyGainBonus：能量获取加成（如能量纪元+20%）
                    if (eraEffect.EnergyGainBonus > 0f)
                    {
                        gain *= (1f + eraEffect.EnergyGainBonus);
                    }
                    // CultivationBonus：修炼速度加成（如精神纪元+20%，时空纪元+15%）
                    if (eraEffect.CultivationBonus > 0f)
                    {
                        gain *= (1f + eraEffect.CultivationBonus);
                    }
                }
            } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleAnnualQi 异常: " + dsEx.Message); }

            //  神创基因修炼效率加成：独立百分比乘（每年异能能量获取额外加成）
            try
            {
                float divineCult = Code.Core.GeneEnergyCalculator.GetDivineCultBonus(actor);
                if (divineCult > 0f) gain *= (1f + divineCult);
                //  神创基因能量恢复加成：独立百分比乘（与修炼效率叠加）
                float divineEnRec = Code.Core.GeneEnergyCalculator.GetDivineEnRecBonus(actor);
                if (divineEnRec > 0f) gain *= (1f + divineEnRec);
            } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleAnnualQi 异常: " + dsEx.Message); }

            // ---- 异能能量消耗 ----
            float cost = 0f;

            if (CultivationData.IsDomainActive(actor) && tier >= 6)
            {
                cost += _baseCultivationSpeed[tier] * 0.1f * CultivationConfig.DomainUpkeepMultiplier;
            }

            if (CultivationData.IsSanctuaryActive(actor) && tier >= 11)
            {
                cost += _baseCultivationSpeed[tier] * 0.15f * CultivationConfig.DomainUpkeepMultiplier;
            }

            // 维度滞留：本体弱化，获取降低消耗也降低
            if (CultivationData.IsInDivineRealm(actor) && tier >= 12)
            {
                gain *= 0.5f;
                cost *= 0.3f;
            }

            // ---- 结算 ----
            float newQi = Mathf.Max(0f, CultivationData.GetEnergy(actor) + gain - cost);

            // 异能能量上限：当前阶门槛的10倍（奇点+无上限）
            if (tier < 12)
            {
                float qiCap = RealmJudge.GetQiThreshold(tier) * 10f;
                // 神创基因能量上限加成：独立百分比乘
                float divineMaxEn = Code.Core.GeneEnergyCalculator.GetDivineMaxEnBonus(actor);
                if (divineMaxEn > 0f) qiCap *= (1f + divineMaxEn);
                newQi = Mathf.Min(newQi, qiCap);
            }

            CultivationData.SetEnergy(actor, newQi);

            //  统计钩子：累计获得能量（仅记录正收益部分）
            try { CultivationData.AddTotalEnergyGained(actor, gain); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleAnnualQi 异常: " + dsEx.Message); }
        }

        /// <summary>
        /// 组织职位修炼加成（文档：异能组织机制）。
        /// 纵向等级取最高职位（2%~10%），横向荣誉身份可叠加（3%~8%）。
        /// 数值温和，避免组织加成导致修炼过快。
        /// </summary>
        public static float GetSectCultivationBoost(Actor actor)
        {
            float boost = 0f;
            try
            {
                // 纵向等级身份（互斥，取最高职位）
                if (actor.hasTrait(Code.Traits.TraitManager.SECT_LEADER)) boost += 0.10f;
                else if (actor.hasTrait(Code.Traits.TraitManager.SECT_DEPUTY)) boost += 0.08f;
                else if (actor.hasTrait(Code.Traits.TraitManager.SECT_ELDER)) boost += 0.06f;
                else if (actor.hasTrait(Code.Traits.TraitManager.SECT_CADRE)) boost += 0.05f;
                else if (actor.hasTrait(Code.Traits.TraitManager.SECT_ELITE)) boost += 0.04f;
                else if (actor.hasTrait(Code.Traits.TraitManager.SECT_MEMBER)) boost += 0.03f;
                else if (actor.hasTrait(Code.Traits.TraitManager.SECT_PROBATION)) boost += 0.02f;

                // 横向荣誉身份（可叠加）
                if (actor.hasTrait(Code.Traits.TraitManager.SECT_FOUNDER)) boost += 0.08f;
                if (actor.hasTrait(Code.Traits.TraitManager.SECT_HERITAGE)) boost += 0.05f;
                if (actor.hasTrait(Code.Traits.TraitManager.SECT_GUARDIAN)) boost += 0.03f;
                if (actor.hasTrait(Code.Traits.TraitManager.SECT_GUEST)) boost += 0.03f;
            }
            catch (System.Exception ex) { DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
            return boost;
        }

        // ============================================================
        //  年度失控指数结算
        // ============================================================

        /// <summary>
        /// 对单个异能者执行年度失控指数结算。
        /// 增长：外部事件写入（屠戮/破坏/战乱）
        /// 衰减：世界年度统一批量自然衰减
        /// 压制链条：衡元减缓增速  真神自主消解  奇点停止增长
        /// </summary>
        public static void SettleAnnualTurbulence(Actor actor, float externalTurbulenceGain)
        {
            if (actor == null || !actor.isAlive()) return;
            int tier = CultivationData.GetRealmTier(actor);
            if (tier <= 0) return;

            float gain = externalTurbulenceGain;
            {
                gain *= 1.5f;
            }

            // ---- 压制链条（文档9.2节） ----

            // 衡元阶(5)：失控指数增长减半
            if (tier >= 5 && tier < 11)
            {
                gain *= 0.5f;
            }

            // 奇点阶(12)：失控指数停止自然增长
            if (tier >= 12)
            {
                gain = 0f;
            }

            //  神创基因失控减少：独立百分比降低失控增长（在压制链条之后、衰减之前）
            try
            {
                float divineStab = Code.Core.GeneEnergyCalculator.GetDivineStabBonus(actor);
                if (divineStab > 0f) gain *= Mathf.Max(0f, 1f - divineStab);
            } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SettleAnnualTurbulence 异常: " + dsEx.Message); }

            // ---- 衰减 ----
            float decay = CultivationConfig.TurbulenceDecayRate;

            // 真神阶(11)：自主消解自身失控指数
            if (tier >= 11 && tier < 12)
            {
                decay += 8f;
            }

            // 维度无失控指数：在维度时快速衰减
            if (CultivationData.IsInDivineRealm(actor))
            {
                decay += 15f;
            }

            // ---- 结算 ----
            float current = CultivationData.GetTurbulence(actor);
            CultivationData.SetTurbulence(actor, current + gain - decay);
        }

        // ============================================================
        //  失控惩罚应用
        // ============================================================

        /// <summary>
        /// 应用失控惩罚debuff。高失控指数（>80）定期施加虚弱。
        /// 仅新增debuff，不屏蔽原版任何机制（禁令9）。
        /// </summary>
        public static void ApplyTurbulencePenalties(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;

            float turb = CultivationData.GetTurbulence(actor);

            // 失控指数 > 80：30%概率施加虚弱debuff
            if (turb > CultivationConfig.HighTurbulenceThreshold)
            {
                if (UnityEngine.Random.value < 0.3f)
                {
                    // 通过原生status_effect系统施加虚弱
                    try
                    {
                        actor.addStatusEffect("weakened", 365f, true);
                    }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ApplyTurbulencePenalties 异常: " + dsEx.Message); }
                }
            }

            // 失控指数 = 100：50%概率触发能力失控状态
            if (turb >= 100f)
            {
                if (UnityEngine.Random.value < 0.5f)
                {
                    DSStatusManager.AddOutOfControl(actor);
                }
            }
        }

        // ============================================================
        //  深度觉醒经验积累（带概率阈值：越到高阶越难增长）
        // ============================================================

        /// <summary>
        /// 年度深度觉醒经验积累（带概率阈值）
        /// 经验越低越容易增长，经验越高越难增长：
        ///   0-1000: 100%增长（入门期）
        ///   1000-5000: 80%（成长期）
        ///   5000-10000: 60%（深入期）
        ///   10000-20000: 40%（瓶颈期）
        ///   20000+: 20%（超越期）
        /// 只是一个随机数判断，不影响性能。
        /// </summary>
        public static void SettleEnlightenment(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;
            int tier = CultivationData.GetRealmTier(actor);
            if (tier <= 0 || tier >= 13) return;

            float currentExp = CultivationData.GetEnlightenmentExp(actor);

            // 概率阈值：本年度是否增长
            float growthChance = GetEnlightenmentGrowthChance(currentExp);
            if (UnityEngine.Random.value > growthChance) return; // 本年度不增长

            // 深度觉醒经验持续积累
            float gain = CultivationConfig.EnlightenmentGainBase * tier;
            if (tier >= 7) gain *= 1.3f; // 高阶深度觉醒效率提升
            float newExp = currentExp + gain;
            CultivationData.SetEnlightenmentExp(actor, newExp);

            // 深度觉醒触发：经验跨过阶段门槛时进入深度觉醒状态（顿悟）
            // 门槛：入门1000 / 成长5000 / 深入10000 / 瓶颈20000
            if (!CultivationData.IsEnlightenmentActive(actor))
            {
                float[] thresholds = { 1000f, 5000f, 10000f, 20000f };
                foreach (float t in thresholds)
                {
                    if (currentExp < t && newExp >= t)
                    {
                        Code.Core.DSStatusManager.AddDeepAwakening(actor);
                        break;
                    }
                }
            }
        }

        /// <summary>深度觉醒经验增长概率（越到高阶越难增长）</summary>
        public static float GetEnlightenmentGrowthChance(float currentExp)
        {
            if (currentExp < 1000f) return 1.0f;   // 入门期：100%增长
            if (currentExp < 5000f) return 0.8f;    // 成长期：80%
            if (currentExp < 10000f) return 0.6f;   // 深入期：60%
            if (currentExp < 20000f) return 0.4f;   // 瓶颈期：40%
            return 0.2f;                               // 超越期：20%
        }

        // ============================================================
        //  外部失控指数增量追踪
        // ============================================================

        /// <summary>
        /// 记录单位本年度因外部行为产生的失控指数增量。
        /// 由事件系统（击杀回调、地形破坏回调、战争状态）调用写入。
        /// 注意：这是失控指数增长，不是异能能量增长杀戮绝不产生异能能量（禁令2）。
        /// </summary>
        public static void AddExternalTurbulence(Actor actor, float amount)
        {
            if (actor == null || amount <= 0 || !actor.isAlive()) return;
            int current = CultivationData.GetCooldown(actor, "annual_turb_gain");
            CultivationData.SetCooldown(actor, "annual_turb_gain", current + (int)amount);
        }

        /// <summary>读取并清零本年度外部失控指数增量</summary>
        public static float ConsumeAnnualTurbulenceGain(Actor actor)
        {
            if (actor == null) return 0f;
            int gain = CultivationData.GetCooldown(actor, "annual_turb_gain");
            CultivationData.SetCooldown(actor, "annual_turb_gain", 0);
            return gain;
        }
    }
}







