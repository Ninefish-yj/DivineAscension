// ============================================================

using System;
using UnityEngine;
using Code.Core;
using Code.Data;
using Code.Traits;
using Code.UI;

namespace Code.Realm
{
    /// <summary>
    /// 境界特色机制管理器
    /// 每个境界都有独特的机制，在年度Tick和战斗中生效
    /// </summary>
    public static class RealmFeatures
    {
        private static bool _initialized = false;

        /// <summary>初始化境界特色机制</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            DSDebug.Verbose("境界特色机制初始化完成");
        }

        // ============================================================
        //  境界特色机制描述（用于UI显示）
        // ============================================================

        /// <summary>获取境界特色机制描述（中文）</summary>
        public static string GetRealmFeatureDescriptionZh(int tier)
        {
            switch (tier)
            {
                case 1: return UILocalization.Get("feature_tier_1");
                case 2: return UILocalization.Get("feature_tier_2");
                case 3: return UILocalization.Get("feature_tier_3");
                case 4: return UILocalization.Get("feature_tier_4");
                case 5: return UILocalization.Get("feature_tier_5");
                case 6: return UILocalization.Get("feature_tier_6");
                case 7: return UILocalization.Get("feature_tier_7");
                case 8: return UILocalization.Get("feature_tier_8");
                case 9: return UILocalization.Get("feature_tier_9");
                case 10: return UILocalization.Get("feature_tier_10");
                case 11: return UILocalization.Get("feature_tier_11");
                case 12: return UILocalization.Get("feature_tier_12");
                case 13: return UILocalization.Get("feature_tier_13");
                default: return UILocalization.Get("feature_mortal");
            }
        }

        /// <summary>获取境界特色机制描述（英文）</summary>
        public static string GetRealmFeatureDescriptionEn(int tier)
        {
            switch (tier)
            {
                case 1: return "Gene Awakening: Ability gene first unlocked, can faintly sense and guide ability energy, randomly gains first base element";
                case 2: return "Element Resonance: Ability energy forms inner cycle auto-regeneration, can unlock more base elements, elements start producing weak combo effects";
                case 3: return "Body Tempering: Ability energy tempers body, ability cost reduced by 20%, body resistance increased, can unlock matter/life elements";
                case 4: return "Gene Transformation: Gene deep optimization, immune to disease and poison, lifespan greatly extended, element combo effects enhanced";
                case 5: return "Element Control: Fine control of ability energy, corruption growth halved, can unlock 3+ elements for stable combos [Element System Start]";
                case 6: return "Element Domain: Opens exclusive element domain, element combo effects boosted by 30% within range, suppresses enemies [Combat Watershed]";
                case 7: return "Element Materialization: Immune to low-level mental disturbance, element combos can condense into physical form for attack, can unlock mind/information elements";
                case 8: return "Origin Touch: Touches element origin level, attacks deal origin damage, element combos produce qualitative effects [Element Origin Awakening]";
                case 9: return "Origin Return: Returns to element origin, ability cost secondarily reduced, energy regeneration accelerated, can unlock ultimate elements";
                case 10: return "Dimension Break: Breaks dimension boundaries, resonates with environmental element energy at long range, attack range +50%, can sense elements across space";
                case 11: return "Element Sanctification: Corruption auto-dissipates (-5 per year), can condense element avatar for independent combat, element combos produce domain-level effects";
                case 12: return "Ascension Realm: Corruption stops growing, immortal body, personal dimensional space projection activated, element combos nearly infinite possibilities";
                case 13: return "True God: Solidifies eternal dimensional space, wields complete element origin, can birth divine subordinate beings, exists across save files";
                default: return "Mortal: No ability powers";
            }
        }

        /// <summary>获取当前语言的境界特色机制描述</summary>
        public static string GetRealmFeatureDescription(int tier)
        {
            return UILocalization.CurrentLanguage == "en" ? GetRealmFeatureDescriptionEn(tier) : GetRealmFeatureDescriptionZh(tier);
        }

        // ============================================================
        //  年度Tick境界特色效果
        // ============================================================

        /// <summary>
        /// 应用年度Tick境界特色效果
        /// 在年度结算时调用，每个境界有独特的年度效果
        /// 设计：每阶在保留基础数值被动的同时，叠加独特机制被动
        /// </summary>
        public static void ApplyAnnualRealmFeatures(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;

            int tier = CultivationData.GetRealmTier(actor);
            if (tier <= 0) return;

            try
            {
                ApplyRealmFeaturesForTier(actor, tier);
            }
            catch (Exception e)
            {
                DSDebug.Warning($"ApplyAnnualRealmFeatures异常: {actor.getName()}, {e.Message}");
            }
        }

        /// <summary>补全到目标阶位：从1阶依次执行到targetTier（突破跳阶时调用，补全中间所有阶位机制）</summary>
        public static void ApplyAllRealmFeaturesUpTo(Actor actor, int targetTier)
        {
            if (actor == null || !actor.isAlive()) return;
            if (targetTier <= 0) return;

            try
            {
                for (int t = 1; t <= targetTier; t++)
                {
                    try
                    {
                        ApplyRealmFeaturesForTier(actor, t);
                    }
                    catch (Exception inner)
                    {
                        DSDebug.Warning($"ApplyAllRealmFeaturesUpTo {t}阶异常: {actor.getName()}, {inner.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning($"ApplyAllRealmFeaturesUpTo异常: {actor.getName()}, {e.Message}");
            }
        }

        /// <summary>按单个阶位执行境界机制（每年结算只调当前阶位；突破跳阶时从1补跑到目标阶位）</summary>
        public static void ApplyRealmFeaturesForTier(Actor actor, int tier)
        {
            try
            {
                switch (tier)
                {
                    case 1:
                        // F级：基础自愈 + 基因共鸣（能量上限临时+20%）
                        ApplyBasicRegeneration(actor, 0.05f);
                        ApplyGeneResonance(actor);
                        break;

                    case 2:
                        // E级：异能能量自动恢复 + 基因感知（附近异能者影响修炼）
                        ApplyQiRegeneration(actor, 50f);
                        ApplyBasicRegeneration(actor, 0.08f);
                        ApplyElementPerception(actor);
                        break;

                    case 3:
                        // D级：能力消耗降低 + 肉身强化（维持strong/fast特质）
                        ApplyQiRegeneration(actor, 80f);
                        ApplyBasicRegeneration(actor, 0.1f);
                        ApplyBodyStrengthening(actor);
                        break;

                    case 4:
                        // C级：免疫疾病/中毒 + 基因稳定 + 黑色方碑效果
                        ApplyDiseaseImmunity(actor);
                        ApplyQiRegeneration(actor, 100f);
                        ApplyBasicRegeneration(actor, 0.15f);
                        ApplyGeneStability(actor);
                        Code.Realm.TranscendentSpeciesSystem.ApplyMonolithEvolution(actor);   // 黑色方碑效果（完整版）
                        break;

                    case 5:
                        // B级：失控增长减半 + 精细掌控（能量消耗-25%）+ 基因组合自动检测
                        ApplyQiRegeneration(actor, 150f);
                        ApplyBasicRegeneration(actor, 0.2f);
                        ApplyFineControl(actor);
                        break;

                    case 6:
                        // A级：场域雏形（释放技能时展开，战斗中体现）
                        ApplyQiRegeneration(actor, 200f);
                        ApplyBasicRegeneration(actor, 0.25f);
                        ApplyDomainPassive(actor);
                        break;

                    case 7:
                        // S级：免疫精神扰动 + 能量具象化被动（普攻概率触发能量刃）
                        ApplyMentalImmunity(actor);
                        ApplyQiRegeneration(actor, 250f);
                        ApplyBasicRegeneration(actor, 0.3f);
                        ApplyMaterializationPassive(actor);
                        break;

                    case 8:
                        // 称号级：规则压制（附近低阶敌人无法使用技能）+ 本源伤害
                        ApplyQiRegeneration(actor, 300f);
                        ApplyBasicRegeneration(actor, 0.35f);
                        ApplyRuleSuppressionPassive(actor);
                        break;

                    case 9:
                        // 天灾级：本源汲取（每年汲取周围敌人能量）+ 本源共鸣 + 能量恢复翻倍
                        ApplyQiRegeneration(actor, 1000f); // 翻倍：原500×2
                        ApplyBasicRegeneration(actor, 0.4f);
                        ApplyOriginDrainPassive(actor);
                        break;

                    case 10:
                        // 使徒级：独立种族 + 完美生命 + 基因效果翻倍 + 创教
                        ApplyQiRegeneration(actor, 800f);
                        ApplyBasicRegeneration(actor, 0.5f);
                        ApplySpatialPerception(actor);
                        ApplyGeneEffectDouble(actor);           // 所有基因效果翻倍
                        break;

                    case 11:
                        // 登神级：失控消解 + 登神被动 + 创教
                        ApplySanctuaryPurification(actor);
                        ApplyQiRegeneration(actor, 1000f);
                        ApplyBasicRegeneration(actor, 0.6f);
                        ApplySuperBodyPassive(actor);
                        VanillaIntegration.TryCreateReligion(actor);  // 登神级创教
                        break;

                    case 12:
                        // 真神级：不朽 + 奇点引力（周围敌人减速/能量伤害）+ 维度投影自动激活
                        ApplyDivineImmortality(actor);
                        ApplyQiRegeneration(actor, 2000f);
                        ApplyBasicRegeneration(actor, 0.8f);
                        ApplySingularityGravity(actor);
                        break;

                    case 13:
                        // 超神级：完整本源 + 全局影响（突破/灾变/能量恢复）
                        ApplyTrueGodPower(actor);
                        ApplyQiRegeneration(actor, 5000f);
                        ApplyBasicRegeneration(actor, 1.0f);
                        ApplyTrueGodGlobalPassive(actor);
                        break;
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning($"ApplyAnnualRealmFeatures异常: {actor.getName()}, {e.Message}");
            }
        }

        // ============================================================
        //  具体境界特色效果实现
        // ============================================================

        /// <summary>基础自愈：每年恢复最大血量的一定比例</summary>
private static void ApplyBasicRegeneration(Actor actor, float ratio)
        {
            try
            {
                // 通过反射获取血量
                var hpField = typeof(ActorData).GetField("health");
                if (hpField == null)
                    hpField = typeof(Actor).GetField("health");
                if (hpField != null)
                {
                    object target = hpField.DeclaringType == typeof(Actor) ? actor : (object)ActorDataAccessor.GetData(actor);
                    if (target == null) return;
                    float currentHp = Convert.ToSingle(hpField.GetValue(target));
                    float maxHp = GetMaxHealth(actor);
                    if (maxHp > 0 && currentHp < maxHp)
                    {
                        float healAmount = maxHp * ratio;
                        float newHp = Mathf.Min(currentHp + healAmount, maxHp);
                        hpField.SetValue(target, newHp);
                    }
                }
            }
            catch { /* 忽略自愈失败 */ }
        }

        /// <summary>异能能量自动恢复</summary>
        private static void ApplyQiRegeneration(Actor actor, float amount)
        {
            try
            {
                float currentQi = CultivationData.GetEnergy(actor);
                float maxQi = RealmJudge.GetQiThreshold(CultivationData.GetRealmTier(actor) + 1);
                if (maxQi <= 0) maxQi = currentQi + amount;
                float newQi = Mathf.Min(currentQi + amount, maxQi);
                CultivationData.SetEnergy(actor, newQi);
            }
            catch { /* 忽略能量恢复失败 */ }
        }

        /// <summary>突变者：免疫疾病和中毒</summary>
        private static void ApplyDiseaseImmunity(Actor actor)
        {
            try
            {
                // 授予免疫疾病特质（如果还没有）
                if (!actor.hasTrait("immune_to_disease"))
                {
                    actor.addTrait("immune_to_disease", false);
                }
                // 清除疾病状态
                if (actor.hasStatus("diseased"))
                {
                    actor.finishStatusEffect("diseased");
                }
                if (actor.hasStatus("plague"))
                {
                    actor.finishStatusEffect("plague");
                }
            }
            catch { /* 忽略免疫失败 */ }
        }

        /// <summary>具象者：免疫精神扰动</summary>
        private static void ApplyMentalImmunity(Actor actor)
        {
            try
            {
                // 清除恐惧、混乱等精神状态
                if (actor.hasStatus("fear"))
                {
                    actor.finishStatusEffect("fear");
                }
                if (actor.hasStatus("confused"))
                {
                    actor.finishStatusEffect("confused");
                }
            }
            catch { /* 忽略精神免疫失败 */ }
        }

        /// <summary>登神级：失控指数自动消解（每年-5）</summary>
        private static void ApplySanctuaryPurification(Actor actor)
        {
            try
            {
                float currentTurb = CultivationData.GetTurbulence(actor);
                if (currentTurb > 0f)
                {
                    float newTurb = Mathf.Max(0f, currentTurb - 5f);
                    CultivationData.SetTurbulence(actor, newTurb);

                    // 如果失控指数降到50以下，能力失控状态效果会自动过期
                }
            }
            catch { /* 忽略净化失败 */ }
        }

        /// <summary>真神级：不朽肉身，失控指数停止增长</summary>
        private static void ApplyDivineImmortality(Actor actor)
        {
            try
            {
                // 授予不朽特质（如果还没有）
                if (!actor.hasTrait("immortal"))
                {
                    actor.addTrait("immortal", false);
                }
                // 失控指数停止增长（保持当前值，不增加）
                // 这个在失控结算时体现，这里只确保不朽
            }
            catch { /* 忽略不朽失败 */ }
        }

        /// <summary>真神：完整本源力量</summary>
        private static void ApplyTrueGodPower(Actor actor)
        {
            try
            {
                // 真神：失控指数完全清零
                CultivationData.SetTurbulence(actor, 0f);
                // 真神：负面状态效果会自动过期，无需手动移除
            }
            catch { /* 忽略真神力量失败 */ }
        }

        // ============================================================
        //  第一段：1-3阶 独特机制
        // ============================================================

        /// <summary>
        /// 1阶 觉醒者：基因共鸣
        /// 觉醒时有概率触发基因共鸣能量上限临时+20%持续1年。
        /// 实现：每年10%概率触发，触发后能量恢复量额外+20%（等效上限提升）。
        /// </summary>
        private static void ApplyGeneResonance(Actor actor)
        {
            try
            {
                int resonanceCd = CultivationData.GetCooldown(actor, "gene_resonance");
                if (resonanceCd > 0)
                {
                    // 共鸣持续中：额外恢复20%能量
                    float currentQi = CultivationData.GetEnergy(actor);
                    float bonus = currentQi * 0.2f;
                    if (bonus > 1f)
                    {
                        CultivationData.SetEnergy(actor, currentQi + bonus);
                    }
                    CultivationData.DecrementCooldown(actor, "gene_resonance");
                    return;
                }
                // 10%概率触发基因共鸣，持续1年
                if (UnityEngine.Random.value <= 0.10f)
                {
                    CultivationData.SetCooldown(actor, "gene_resonance", 1);
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 触发基因共鸣！能量上限临时+20%（持续1年）");
                }
            }
            catch { /* 忽略基因共鸣失败 */ }
        }

        /// <summary>
        /// 2阶 共振者：基因感知
        /// 可感知附近10格内的其他异能者和基因浓度，附近异能者越多修炼效率越高。
        /// 实现：统计附近10格内异能者数量，每个提供+5%能量恢复（上限+30%）。
        /// </summary>
        private static void ApplyElementPerception(Actor actor)
        {
            try
            {
                int nearbyCount = CountNearbyCultivators(actor, 10f);
                if (nearbyCount > 0)
                {
                    float bonus = Mathf.Min(0.30f, nearbyCount * 0.05f);
                    float currentQi = CultivationData.GetEnergy(actor);
                    float extra = currentQi * bonus;
                    if (extra > 1f)
                    {
                        CultivationData.SetEnergy(actor, currentQi + extra);
                    }
                    DSDebug.Verbose($"[DivineAscension] {actor.getName()} 基因感知：附近{nearbyCount}个异能者，能量恢复+{bonus * 100:F0}%");
                }
            }
            catch { /* 忽略基因感知失败 */ }
        }

        /// <summary>
        /// 3阶 强化者：肉身强化
        /// 物理攻击附带异能能量伤害（+15%伤害），移动速度+10%。
        /// 实现：每年检查并维持strong/fast特质，战斗伤害加成在AbilityStatusPatch中体现。
        /// </summary>
        private static void ApplyBodyStrengthening(Actor actor)
        {
            try
            {
                // 维持strong特质（攻击加成）
                if (!actor.hasTrait("strong"))
                {
                    actor.addTrait("strong", false);
                }
                // 维持fast特质（速度加成）
                if (!actor.hasTrait("fast"))
                {
                    actor.addTrait("fast", false);
                }
            }
            catch { /* 忽略肉身强化失败 */ }
        }

        // ============================================================
        //  第二段：4-6阶 独特机制
        // ============================================================

        /// <summary>
        /// 4阶 突变者：基因稳定
        /// 失控指数上限降低20%（从100降到80），突破成功率+15%，寿命延长。
        /// 实现：失控指数超过80时强制压回80；授予long_living特质延长寿命。
        /// 突破成功率加成在RealmJudge中读取。
        /// </summary>
        private static void ApplyGeneStability(Actor actor)
        {
            try
            {
                // 失控上限降低到80
                float turb = CultivationData.GetTurbulence(actor);
                if (turb > 80f)
                {
                    CultivationData.SetTurbulence(actor, 80f);
                }
                // 寿命延长：授予long_living特质（如果存在）
                if (!actor.hasTrait("long_living"))
                {
                    try { actor.addTrait("long_living", false); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ApplyGeneStability 异常: " + dsEx.Message); }
                }
                // 标记基因稳定状态（供突破判定读取）
                CultivationData.SetCooldown(actor, "gene_stable", 1);
            }
            catch { /* 忽略基因稳定失败 */ }
        }

        /// <summary>
        /// 5阶 调控者：精细掌控
        /// 技能能量消耗-25%，同时自动激活基因组合检测。
        /// 实现：标记精细掌控状态（供技能消耗计算读取）；每年触发基因组合检测。
        /// </summary>
        private static void ApplyFineControl(Actor actor)
        {
            try
            {
                // 标记精细掌控（技能消耗时读取此标记减免25%）
                CultivationData.SetCooldown(actor, "fine_control", 1);
                // 自动激活基因组合检测（确保5阶时组合效果生效）
                GeneLinkageSystem.CheckAllLinkages(actor);
            }
            catch { /* 忽略精细掌控失败 */ }
        }

        /// <summary>
        /// 6阶 场域者：场域雏形
        /// 释放技能时自动展开半径8格个人场域，场内自身全属性+20%，敌人全属性-10%。
        /// 实现：年度维持场域标记；实际场域效果在RealmAbilities.UseAbility时触发。
        /// </summary>
        private static void ApplyDomainPassive(Actor actor)
        {
            try
            {
                // 标记场域就绪（技能释放时检测此标记触发展开场域）
                CultivationData.SetCooldown(actor, "domain_ready", 1);
                // 场域者自身持续获得strong特质（场域常驻增幅）
                if (!actor.hasTrait("strong"))
                {
                    actor.addTrait("strong", false);
                }
            }
            catch { /* 忽略场域被动失败 */ }
        }

        // ============================================================
        //  第三段：7-9阶 独特机制
        // ============================================================

        /// <summary>
        /// 7阶 具象者：构形被动
        /// 普通攻击有30%概率触发能量刃，造成额外50%伤害。
        /// 实现：标记构形状态（战斗Patch中读取，随机判定触发额外伤害）。
        /// </summary>
        private static void ApplyMaterializationPassive(Actor actor)
        {
            try
            {
                CultivationData.SetCooldown(actor, "energy_blade_ready", 1);
            }
            catch { /* 忽略构形被动失败 */ }
        }

        /// <summary>
        /// 8阶 干涉者：规则压制被动
        /// 周围12格内低于自身境界的敌人无法使用技能（沉默效果）。
        /// 攻击附带本源伤害（+30%）。
        /// 实现：标记规则压制状态；实际沉默检测在RealmAbilities.UseAbility中进行。
        /// </summary>
        private static void ApplyRuleSuppressionPassive(Actor actor)
        {
            try
            {
                CultivationData.SetCooldown(actor, "rule_suppression", 1);
            }
            catch { /* 忽略规则压制失败 */ }
        }

        /// <summary>
        /// 9阶 解析者：本源汲取被动 + 本源共鸣
        /// 每年自动汲取周围10格内敌人5%能量恢复自身。
        /// 与同境界以上单位接近时，双方能量恢复+50%。
        /// </summary>
        private static void ApplyOriginDrainPassive(Actor actor)
        {
            try
            {
                if (World.world == null || World.world.units == null) return;

                float totalDrained = 0f;
                int sameTierCount = 0;
                int actorTier = CultivationData.GetRealmTier(actor);

                foreach (var unit in World.world.units.units_only_alive)
                {
                    if (unit == null || !unit.isAlive() || unit == actor) continue;
                    float dist = Vector2.Distance(actor.current_position, unit.current_position);
                    if (dist > 10f) continue;

                    int unitTier = CultivationData.GetRealmTier(unit);

                    // 本源汲取：汲取敌人5%能量
                    if (unitTier > 0 && unitTier < actorTier)
                    {
                        float enemyEnergy = CultivationData.GetEnergy(unit);
                        float drain = enemyEnergy * 0.05f;
                        if (drain > 1f)
                        {
                            CultivationData.SetEnergy(unit, enemyEnergy - drain);
                            totalDrained += drain;
                        }
                    }

                    // 本源共鸣：同境界以上单位接近时计数
                    if (unitTier >= actorTier)
                    {
                        sameTierCount++;
                    }
                }

                // 应用汲取的能量
                if (totalDrained > 0f)
                {
                    float currentQi = CultivationData.GetEnergy(actor);
                    CultivationData.SetEnergy(actor, currentQi + totalDrained);
                }

                // 本源共鸣：每个同阶以上单位提供+50%能量恢复（上限+100%）
                if (sameTierCount > 0)
                {
                    float resonanceBonus = Mathf.Min(1.0f, sameTierCount * 0.5f);
                    float currentQi2 = CultivationData.GetEnergy(actor);
                    float bonus = currentQi2 * resonanceBonus * 0.1f; // 共鸣加成按当前能量10%×倍率
                    if (bonus > 1f)
                    {
                        CultivationData.SetEnergy(actor, currentQi2 + bonus);
                    }
                }
            }
            catch { /* 忽略本源汲取失败 */ }
        }

        // ============================================================
        //  第四段：10-12阶 独特机制
        // ============================================================

        /// <summary>
        /// 10阶 使徒级：空间感知 + 维度跳跃
        /// 可感知全图异能者位置（标记状态，UI可查询）。
        /// 15%概率闪避攻击（通过evasive特质体现）。
        /// </summary>
        private static void ApplySpatialPerception(Actor actor)
        {
            try
            {
                // 维度跳跃：维持evasive特质（闪避）
                if (!actor.hasTrait("evasive"))
                {
                    try { actor.addTrait("evasive", false); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ApplySpatialPerception 异常: " + dsEx.Message); }
                }
                // 标记空间感知（供UI/调试面板显示全图异能者位置）
                CultivationData.SetCooldown(actor, "spatial_awareness", 1);
            }
            catch { /* 忽略空间感知失败 */ }
        }

        /// <summary>
        /// 11阶 登神级：登神被动
        /// 全属性+50%（通过strong/fast/smart/genius等特质叠加体现）。
        /// 免疫所有负面状态（每年检查并清除weak/slow/fear/confused等）。
        /// </summary>
        private static void ApplySuperBodyPassive(Actor actor)
        {
            try
            {
                // 清除负面状态
                string[] negativeStatuses = { "weak", "slow", "fear", "confused", "diseased", "plague", "weakened", "poisoned" };
                foreach (string status in negativeStatuses)
                {
                    try
                    {
                        if (Code.Core.StatusHelper.HasStatus(actor, status))
                        {
                            actor.finishStatusEffect(status);
                        }
                    }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ApplySuperBodyPassive 异常: " + dsEx.Message); }
                }
                // 清除负面特质
                string[] negativeTraits = { "weak", "slow", "coward", "stupid", "dumb", "ugly", "sickly", "lazy", "clumsy" };
                foreach (string trait in negativeTraits)
                {
                    try
                    {
                        if (actor.hasTrait(trait))
                        {
                            actor.removeTrait(trait);
                        }
                    }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ApplySuperBodyPassive 异常: " + dsEx.Message); }
                }
                // 维持强力特质（全属性+50%的近似体现）
                if (!actor.hasTrait("strong")) actor.addTrait("strong", false);
                if (!actor.hasTrait("fast")) actor.addTrait("fast", false);
                if (!actor.hasTrait("genius")) try { actor.addTrait("genius", false); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ApplySuperBodyPassive 异常: " + dsEx.Message); }
                if (!actor.hasTrait("blessed")) actor.addTrait("blessed", false);
            }
            catch { /* 忽略登神被动失败 */ }
        }

        /// <summary>
        /// 12阶 真神级：奇点引力 + 维度投影自动激活
        /// 周围15格内敌人被缓慢吸引（移动速度-30%），每秒受到少量能量伤害。
        /// 维度投影自动激活（如果尚未激活）。
        /// 个人维度空间激活（标记状态）。
        /// </summary>
        private static void ApplySingularityGravity(Actor actor)
        {
            try
            {
                // 维度投影自动激活
                // 维度空间改为玩家手动进入，不再自动激活
                // if (!CultivationData.IsDimensionProjectionActive(actor))
                // {
                //     Code.Dimension.DimensionProjectionManager.ActivateProjection(actor);
                // }

                // 奇点引力：对周围15格内敌人施加减速和能量伤害
                if (World.world != null && World.world.units != null)
                {
                    foreach (var unit in World.world.units.units_only_alive)
                    {
                        if (unit == null || !unit.isAlive() || unit == actor) continue;
                        float dist = Vector2.Distance(actor.current_position, unit.current_position);
                        if (dist > 15f) continue;

                        int unitTier = CultivationData.GetRealmTier(unit);
                        // 只对低于12阶的单位产生引力效果
                        if (unitTier < 12)
                        {
                            // 减速：添加slow特质（如果没有）
                            if (!unit.hasTrait("slow"))
                            {
                                try { unit.addTrait("slow", false); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ApplySingularityGravity 异常: " + dsEx.Message); }
                            }
                            // 少量能量伤害（年度结算时一次性扣除，等效每秒微量伤害的年度累积）
                            float gravityDamage = 50f * (1f - dist / 15f);
                            if (gravityDamage > 5f)
                            {
                                unit.getHit(gravityDamage, false, AttackType.Other, null, false, false, true);
                            }
                        }
                    }
                }

                // 标记个人维度空间已激活
                CultivationData.SetCooldown(actor, "personal_dimension", 1);
            }
            catch { /* 忽略奇点引力失败 */ }
        }

        // ============================================================
        //  第五段：13阶 独特机制
        // ============================================================

        /// <summary>
        /// 13阶 超神级：全局影响被动
        /// 标记真神存在状态，供全局系统读取：
        /// - 全球异能者突破成功率+10%（RealmJudge读取）
        /// - 全球灾变发生概率-20%（DisasterManager读取）
        /// - 全球异能者能量恢复+20%（AnnualTickManager读取）
        /// </summary>
        private static void ApplyTrueGodGlobalPassive(Actor actor)
        {
            try
            {
                // 真神存在标记（全局系统通过HasTrueGodInWorld()检测）
                CultivationData.SetCooldown(actor, "true_god_present", 1);
                // 真神自身维持不朽和强力
                if (!actor.hasTrait("immortal")) actor.addTrait("immortal", false);
                if (!actor.hasTrait("strong")) actor.addTrait("strong", false);
                if (!actor.hasTrait("blessed")) actor.addTrait("blessed", false);
            }
            catch { /* 忽略真神全局被动失败 */ }
        }

        // ============================================================
        //  全局查询工具方法
        // ============================================================

        /// <summary>
        /// 检查当前世界是否存在12阶真神级级（供全局系统读取）。
        /// 遍历活跃异能者列表，不逐帧全图遍历。
        /// </summary>
        public static bool HasTrueGodInWorld()
        {
            try
            {
                var ids = AnnualTickManager.GetActiveCultivatorIds();
                foreach (long id in ids)
                {
                    Actor a = SystemManagerExtensions.FindActorById(id);
                    if (a != null && a.isAlive() && CultivationData.GetRealmTier(a) >= 12)
                    {
                        return true;
                    }
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] HasTrueGodInWorld 异常: " + dsEx.Message); }
            return false;
        }

        /// <summary>
        /// 检查当前世界是否存在13阶超神级（供全局系统读取）。
        /// </summary>
        public static bool HasTranscendentInWorld()
        {
            try
            {
                var ids = AnnualTickManager.GetActiveCultivatorIds();
                foreach (long id in ids)
                {
                    Actor a = SystemManagerExtensions.FindActorById(id);
                    if (a != null && a.isAlive() && CultivationData.GetRealmTier(a) >= 13)
                    {
                        return true;
                    }
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] HasTranscendentInWorld 异常: " + dsEx.Message); }
            return false;
        }

        /// <summary>
        /// 统计指定半径内的异能者数量（不含自身）。
        /// </summary>
        public static int CountNearbyCultivators(Actor actor, float radius)
        {
            int count = 0;
            try
            {
                if (World.world == null || World.world.units == null) return 0;
                foreach (var unit in World.world.units.units_only_alive)
                {
                    if (unit == null || !unit.isAlive() || unit == actor) continue;
                    if (CultivationData.GetRealmTier(unit) <= 0) continue;
                    float dist = Vector2.Distance(actor.current_position, unit.current_position);
                    if (dist <= radius) count++;
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] CountNearbyCultivators 异常: " + dsEx.Message); }
            return count;
        }

        /// <summary>
        /// 检查指定单位周围radius格内是否存在tier阶以上的干涉者（8阶+）。
        /// 用于规则压制沉默检测。
        /// </summary>
        public static bool HasRuleSuppressorNearby(Actor actor, float radius)
        {
            try
            {
                if (World.world == null || World.world.units == null) return false;
                int actorTier = CultivationData.GetRealmTier(actor);
                foreach (var unit in World.world.units.units_only_alive)
                {
                    if (unit == null || !unit.isAlive() || unit == actor) continue;
                    int unitTier = CultivationData.GetRealmTier(unit);
                    if (unitTier >= 8 && unitTier > actorTier)
                    {
                        float dist = Vector2.Distance(actor.current_position, unit.current_position);
                        if (dist <= radius) return true;
                    }
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] HasRuleSuppressorNearby 异常: " + dsEx.Message); }
            return false;
        }

        /// <summary>
        /// 获取境界技能能量消耗倍率（精细掌控等效果影响）。
        /// 5阶精细掌控：-25%；9阶本源回归：额外-20%；13阶至高：额外-30%。
        /// </summary>
        public static float GetAbilityCostMultiplier(Actor actor)
        {
            if (actor == null) return 1f;
            int tier = CultivationData.GetRealmTier(actor);
            float mult = 1f;
            if (tier >= 5) mult *= 0.75f;  // 精细掌控
            if (tier >= 9) mult *= 0.80f;  // 本源回归
            if (tier >= 13) mult *= 0.70f; // 至高权能
            return mult;
        }

        // ============================================================
        //  战斗中的境界特色效果
        // ============================================================

        /// <summary>
        /// 获取境界战斗伤害倍率
        /// 在战斗中调用，不同境界有不同的伤害加成
        /// </summary>
        public static float GetRealmCombatMultiplier(int tier)
        {
            switch (tier)
            {
                case 1: return 1.0f;    // 觉醒者：基础
                case 2: return 1.1f;    // E级：+10%
                case 3: return 1.25f;   // D级：+25%
                case 4: return 1.5f;    // C级：+50%
                case 5: return 1.8f;    // B级：+80%
                case 6: return 2.2f;    // A级：+120%【战力分水岭】
                case 7: return 2.7f;    // S级：+170%
                case 8: return 3.5f;    // 称号级：+250%【能量转规则】
                case 9: return 4.5f;    // 天灾级：+350%
                case 10: return 6.0f;   // 使徒级：+500%
                case 11: return 8.0f;   // 登神级：+700%
                case 12: return 12.0f;  // 真神级：+1100%
                case 13: return 20.0f;  // 超神级：+1900%
                default: return 1.0f;
            }
        }

        /// <summary>
        /// 获取境界防御倍率
        /// 在战斗中调用，不同境界有不同的防御加成
        /// </summary>
        public static float GetRealmDefenseMultiplier(int tier)
        {
            switch (tier)
            {
                case 1: return 1.0f;
                case 2: return 1.05f;
                case 3: return 1.15f;
                case 4: return 1.3f;    // C级：抗火抗毒
                case 5: return 1.45f;
                case 6: return 1.7f;    // A级：场域防护
                case 7: return 2.0f;    // S级：实体化防御
                case 8: return 2.5f;    // 称号级：规则防护
                case 9: return 3.0f;
                case 10: return 4.0f;
                case 11: return 5.5f;   // 登神级：圣化防御
                case 12: return 8.0f;   // 真神级：不朽肉身
                case 13: return 15.0f;  // 超神级：永恒维度
                default: return 1.0f;
            }
        }

        // ============================================================
                                // ============================================================


        // ============================================================
        //  工具方法
        // ============================================================

        /// <summary>获取单位最大血量（通过反射）</summary>
        private static float GetMaxHealth(Actor actor)
        {
            try
            {
                var maxHpField = typeof(ActorData).GetField("max_health");
                if (maxHpField == null)
                    maxHpField = typeof(Actor).GetField("max_health");
                if (maxHpField != null)
                {
                    object target = maxHpField.DeclaringType == typeof(Actor) ? actor : (object)ActorDataAccessor.GetData(actor);
                    if (target != null)
                    {
                        return Convert.ToSingle(maxHpField.GetValue(target));
                    }
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] GetMaxHealth 异常: " + dsEx.Message); }
            return 100f; // 默认值
        }

        /// <summary>获取境界修炼效率倍率（影响异能能量增长速度）</summary>
        public static float GetRealmCultivationEfficiency(int tier)
        {
            switch (tier)
            {
                case 1: return 1.0f;
                case 2: return 1.2f;
                case 3: return 1.5f;
                case 4: return 1.8f;
                case 5: return 2.2f;    // B级：精细控制
                case 6: return 2.7f;    // A级：场域增幅
                case 7: return 3.3f;
                case 8: return 4.0f;    // 称号级：规则加速
                case 9: return 5.0f;    // 天灾级：本源回归
                case 10: return 6.5f;
                case 11: return 8.5f;
                case 12: return 12.0f;  // 真神级：永续闭环
                case 13: return 20.0f;  // 超神级：完整本源
                default: return 1.0f;
            }
        }

                        

        /// <summary>基因进化：10阶突破时清空普通基因，只留神圣基因</summary>

        /// <summary>所有基因效果翻倍</summary>
        private static void ApplyGeneEffectDouble(Actor actor)
        {
            try
            {
                if (!actor.hasTrait("ds_gene_double"))
                {
                    actor.addTrait("ds_gene_double", false);
                }
                DSDebug.Verbose($"[DivineAscension] {actor.name} 所有基因效果翻倍");
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[DivineAscension] 基因效果翻倍失败: {e.Message}");
            }
        }

        /// <summary>黑色方碑效果：4阶突破时召唤黑色方碑，周围单位获得基因强化</summary>
        private static void ApplyBlackObeliskEffect(Actor actor)
        {
            try
            {
                if (!actor.hasTrait("ds_black_obelisk"))
                {
                    actor.addTrait("ds_black_obelisk", false);
                }
                DSDebug.Verbose($"[DivineAscension] {actor.name} 激活黑色方碑效果");
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[DivineAscension] 黑色方碑效果失败: {e.Message}");
            }
        }
    }
}










