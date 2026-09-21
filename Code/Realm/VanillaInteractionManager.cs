// ============================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Code.Data;
using Code.Core;

namespace Code.Realm
{
    /// <summary>
    /// 原版游戏深度交互管理器
    /// 
    /// 修炼成长不仅是自定义数值，还要影响原版角色的属性和特质。
    /// </summary>
    public static class VanillaInteractionManager
    {
        private static bool _initialized = false;

        // ============================================================
        //  原版特质ID常量（WorldBox原生特质）
        // ============================================================
        public const string V_STRONG = "strong";
        public const string V_FAST = "fast";
        public const string V_SMART = "smart";
        public const string V_BLESSED = "blessed";
        public const string V_REGENERATING = "regenerating";
        public const string V_IMMUNE_DISEASE = "immune_to_disease";
        public const string V_GENIUS = "genius";
        public const string V_LUCKY = "lucky";
        public const string V_IMMORTAL = "immortal";
        public const string V_TOURIST = "tourist";
        public const string V_PACIFIST = "pacifist";
        public const string V_WARRIOR = "warrior";
        public const string V_HUNTER = "hunter";
        public const string V_GATHERER = "gatherer";
        public const string V_FARMER = "farmer";
        public const string V_MINER = "miner";
        public const string V_BLACKSMITH = "blacksmith";
        public const string V_PHYSICIAN = "physician";
        public const string V_SCHOLAR = "scholar";
        public const string V_MAGE = "mage";
        public const string V_RANGER = "ranger";
        public const string V_ASSASSIN = "assassin";
        public const string V_BERSERKER = "berserker";
        public const string V_PALADIN = "paladin";
        public const string V_DRUID = "druid";
        public const string V_NECROMANCER = "necromancer";

        // ============================================================
        //  境界原版特质映射表
        //  突破到对应境界时自动授予累计的原版特质
        //  每阶境界对应更强的原版属性
        // ============================================================
        private static readonly Dictionary<int, string[]> _realmVanillaTraits = new Dictionary<int, string[]>
        {
            // 1-4阶：基础属性提升
            { 1, new string[] { } },                                    // 觉醒者：无原版特质（刚入门）
            { 2, new string[] { V_FAST } },                             // 共振者：攻速提升
            { 3, new string[] { V_FAST, V_SMART } },                   // 强化者：攻速+聪明
            { 4, new string[] { V_FAST, V_SMART, V_STRONG } },         // 突变者：攻速+聪明+强壮
            // 5-9阶：全面提升+特殊能力
            { 5, new string[] { V_FAST, V_SMART, V_STRONG, V_REGENERATING } }, // 调控者：+再生
            { 6, new string[] { V_FAST, V_SMART, V_STRONG, V_REGENERATING, V_BLESSED } }, // 场域者：+祝福
            { 7, new string[] { V_FAST, V_SMART, V_STRONG, V_REGENERATING, V_BLESSED, V_IMMUNE_DISEASE } }, // 具象者：+免疫疾病
            { 8, new string[] { V_FAST, V_SMART, V_STRONG, V_REGENERATING, V_BLESSED, V_IMMUNE_DISEASE, V_GENIUS } }, // 干涉者：+天才
            { 9, new string[] { V_FAST, V_SMART, V_STRONG, V_REGENERATING, V_BLESSED, V_IMMUNE_DISEASE, V_GENIUS, V_LUCKY } }, // 解析者：+幸运
            // 10-11阶：真神纯化，全属性极致
            { 10, new string[] { V_FAST, V_SMART, V_STRONG, V_REGENERATING, V_BLESSED, V_IMMUNE_DISEASE, V_GENIUS, V_LUCKY, V_WARRIOR } }, // 使徒级：+战士（空间撕裂强化近战）
            { 11, new string[] { V_FAST, V_SMART, V_STRONG, V_REGENERATING, V_BLESSED, V_IMMUNE_DISEASE, V_GENIUS, V_LUCKY, V_WARRIOR, V_RANGER } }, // 登神级：+游侠（真神纯化全属性极致）
            // 12-13阶：不朽，神性
            { 12, new string[] { V_FAST, V_SMART, V_STRONG, V_REGENERATING, V_BLESSED, V_IMMUNE_DISEASE, V_GENIUS, V_LUCKY, V_WARRIOR, V_RANGER, V_PALADIN, V_IMMORTAL } }, // 真神级：+圣骑士+不朽
            { 13, new string[] { V_FAST, V_SMART, V_STRONG, V_REGENERATING, V_BLESSED, V_IMMUNE_DISEASE, V_GENIUS, V_LUCKY, V_WARRIOR, V_RANGER, V_PALADIN, V_DRUID, V_IMMORTAL } }, // 真神：+德鲁伊（神性自然）
        };

        // ============================================================
        //  初始化
        // ============================================================
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            DSDebug.Verbose("原版交互管理器初始化完成：境界原版特质映射已加载（13阶境界）");
        }

        // ============================================================
        //  突破时授予原版特质
        // ============================================================

        /// <summary>
        /// 单位突破到新境界时，授予该境界对应的全部原版特质（累计）。
        /// 调用时机：RealmJudge.TryBreakthrough成功后。
        /// </summary>
        public static void GrantVanillaTraitsForRealm(Actor actor, int newTier)
        {
            if (actor == null || newTier < 1 || newTier > 13) return;

            if (!_realmVanillaTraits.TryGetValue(newTier, out var traits)) return;

            int granted = 0;
            foreach (string traitId in traits)
            {
                if (!actor.hasTrait(traitId))
                {
                    actor.addTrait(traitId, false);
                    granted++;
                }
            }

            if (granted > 0)
            {
                DSDebug.Verbose($"[DivineAscension] {actor.getName()} 突破到{newTier}阶，授予{granted}个原版特质");
            }
        }

        // ============================================================
        //  修炼速度受原版属性影响
        // ============================================================

        /// <summary>
        /// 根据单位的原版属性计算修炼速度倍率。
        /// smart/genius/blessed特质会提升修炼速度。
        /// 原版属性影响自定义成长。
        /// </summary>
        /// <summary>获取单位的智力属性（通过反射）</summary>
        public static float GetIntelligence(Actor actor)
        {
            if (actor == null) return 0f;
            try
            {
                // 尝试通过ActorData获取属性
                var data = ActorDataAccessor.GetData(actor);
                if (data != null)
                {
                    var prop = data.GetType().GetProperty("intelligence");
                    if (prop == null) prop = data.GetType().GetProperty("Intelligence");
                    if (prop != null)
                    {
                        object val = prop.GetValue(data, null);
                        if (val != null) return System.Convert.ToSingle(val);
                    }
                    var field = data.GetType().GetField("intelligence");
                    if (field != null)
                    {
                        object val = field.GetValue(data);
                        if (val != null) return System.Convert.ToSingle(val);
                    }
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] GetIntelligence 异常: " + dsEx.Message); }
            // 回退：通过特质判断
            float intel = 1f; // 基础智力1
            if (actor.hasTrait(V_SMART)) intel += 2f;
            if (actor.hasTrait(V_GENIUS)) intel += 5f;
            if (actor.hasTrait(V_SCHOLAR)) intel += 3f;
            if (actor.hasTrait(V_MAGE)) intel += 4f;
            return intel;
        }

        public static float GetCultivationSpeedMultiplier(Actor actor)
        {
            if (actor == null) return 1.0f;

            float mult = 1.0f;

            //  核心：修炼速度直接和智力属性挂钩
            // 每点智力+3%修炼速度，上限+150%（智力50点达到上限）
            float intelligence = GetIntelligence(actor);
            float intBonus = Mathf.Min(1.5f, intelligence * 0.03f);
            mult += intBonus;

            //  初始基因（细胞分裂）的修炼效果
            // 细胞分裂是一切异能的根源，提供基础修炼速度+10%
            if (CultivationData.IsElementUnlocked(actor, "gene_cell_division"))
            {
                mult += 0.10f;
            }

            //  主要基因染色体的修炼速度影响
            // 不同基因染色体有不同的修炼速度加成
            var unlockedForCult = CultivationData.GetUnlockedElements(actor);
            if (unlockedForCult != null && unlockedForCult.Count > 0)
            {
                int[] groupCountForCult = new int[8];
                foreach (string eid in unlockedForCult)
                {
                    var elem = ElementDef.GetById(eid);
                    if (elem != null) groupCountForCult[elem.Group]++;
                }
                int dominantGroupForCult = 0, maxCountForCult = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (groupCountForCult[i] > maxCountForCult) { maxCountForCult = groupCountForCult[i]; dominantGroupForCult = i; }
                }
                // 按主要基因染色体（Group）决定修炼速度加成
                switch (dominantGroupForCult)
                {
                    case 0: mult += 0.05f; break;  // 力量染色体：稳定但慢+5%
                    case 1: mult += 0.10f; break;  // 敏捷染色体：基因亲和+10%
                    case 2: mult += 0.15f; break;  // 体质染色体：能量亲和+15%
                    case 3: mult += 0.05f; break;  // 智力染色体：稳定但慢+5%
                    case 4: mult += 0.00f; break;  // 感知染色体：平衡+0%
                    case 5: mult += 0.20f; break;  // 意志染色体：智力加成+20%
                    case 6: mult += 0.10f; break;  // 异能染色体：学习快+10%
                    case 7: mult -= 0.10f; break;  // 潜能染色体：难以理解-10%
                }
            }

            // 狂战士：修炼速度-10%（倾向于战斗而非修炼）
            if (actor.hasTrait(V_BERSERKER)) mult -= 0.10f;

            //  深度觉醒加成（核心机制，保留）
            // 在EnergyTurbulenceCalculator中已经处理了×1.5

            //  纪元主宰效果：修炼速度加成
            try
            {
                var eraEffect = Realm.RealmJudge.GetCurrentEraEffect();
                if (eraEffect != null)
                {
                    mult += eraEffect.CultivationBonus;
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] GetCultivationSpeedMultiplier 异常: " + dsEx.Message); }

            //  专精加成：鼓励只掌握2个基因（初始基因+觉醒第二基因）走专精路线
            // 此时修炼速度+30%，突破成功率+10%
            // 掌握3-5个基因时，修炼速度+15%
            // 掌握6个以上基因时，无专精加成（博而不精）
            var unlockedElements = CultivationData.GetUnlockedElements(actor);
            if (unlockedElements != null)
            {
                int elemCount = unlockedElements.Count;
                if (elemCount <= 2) mult += 0.30f;      // 专精：初始基因+觉醒第二基因
                else if (elemCount <= 5) mult += 0.15f; // 小成：少量基因
                // 6个以上：无加成（博而不精）
            }

            return Mathf.Max(0.1f, mult);
        }

        /// <summary>获取突破成功率加成（专精加成）</summary>

        // ============================================================
        //  突破成功率受原版特质影响
        // ============================================================

        // ============================================================
        //  寿命受境界影响
        // ============================================================

        /// <summary>
        /// 根据境界计算寿命加成（年）。
        /// 高境界异能者寿命更长，通过修改原版年龄上限实现。
        /// 注意：不直接改写health，而是通过immortal特质和年龄机制间接影响。
        /// </summary>
        public static float GetLifespanBonus(int tier)
        {
            if (tier < 1) return 0f;

            // 每阶境界增加寿命（年）
            // 参考设计文档：突变者延缓衰老，真神级不朽
            float[] lifespanBonus = {
                0,      // 0阶：凡人
                5,      // 1阶：觉醒者 +5年
                10,     // 2阶：共振者 +10年
                20,     // 3阶：强化者 +20年
                50,     // 4阶：突变者 +50年（延缓衰老）
                100,    // 5阶：调控者 +100年
                200,    // 6阶：场域者 +200年
                500,    // 7阶：具象者 +500年
                1000,   // 8阶：干涉者 +1000年
                2000,   // 9阶：解析者 +2000年
                5000,   // 10阶：使徒级 +5000年
                10000,  // 11阶：登神级 +10000年
                -1,     // 12阶：真神级 不朽（-1表示无限）
                -1,     // 13阶：超神级 不朽
            };

            if (tier >= 12) return -1f; // 不朽
            return lifespanBonus[tier];
        }

        /// <summary>
        /// 应用寿命加成到单位（突破成功后调用）。
        /// 通过反射修改Actor的年龄上限字段，实现高境界异能者寿命更长。
        /// 12阶以上授予immortal特质（不朽），不需要修改年龄上限。
        /// </summary>
        public static void ApplyLifespanBonus(Actor actor, int tier)
        {
            if (actor == null || tier < 1) return;
            if (tier >= 12) return; // 12阶以上已有immortal特质

            float bonusYears = GetLifespanBonus(tier);
            if (bonusYears <= 0f) return;

            try
            {
                // 尝试通过反射查找并修改年龄上限字段
                string[] ageFieldNames = { "max_age", "age_max", "lifespan", "max_lifespan", "life_expectancy", "kill_age", "maxAge", "age", "years_lived", "death_age", "age_death" };
                bool applied = false;

                // 调试：打印所有float/int字段名，找年龄相关的
                if (!applied)
                {
                    var fields = actor.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    DSDebug.Verbose($"[DivineAscension] Actor所有字段:");
                    foreach (var field in fields)
                    {
                        if (field.FieldType == typeof(float) || field.FieldType == typeof(int))
                        {
                            object val = field.GetValue(actor);
                            DSDebug.Verbose($"  {field.Name} ({field.FieldType.Name}) = {val}");
                        }
                    }
                }

                foreach (string fieldName in ageFieldNames)
                {
                    try
                    {
                        var field = actor.GetType().GetField(fieldName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            object currentValue = field.GetValue(actor);
                            if (currentValue is float)
                            {
                                field.SetValue(actor, (float)currentValue + bonusYears);
                                applied = true;
                                DSDebug.Verbose("[DivineAscension] " + actor.getName() + " 寿命加成 +" + bonusYears.ToString("F0") + "年（字段:" + fieldName + "）");
                                break;
                            }
                            else if (currentValue is int)
                            {
                                field.SetValue(actor, (int)currentValue + (int)bonusYears);
                                applied = true;
                                DSDebug.Verbose("[DivineAscension] " + actor.getName() + " 寿命加成 +" + bonusYears.ToString("F0") + "年（字段:" + fieldName + "）");
                                break;
                            }
                        }
                    }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] ApplyLifespanBonus 异常: " + dsEx.Message); }
                }

                if (!applied)
                {
                    DSDebug.Verbose("[DivineAscension] " + actor.getName() + " 寿命加成未找到年龄字段，跳过（" + bonusYears.ToString("F0") + "年）");
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[DivineAscension] 应用寿命加成失败: " + e.Message);
            }
        }

        // ============================================================
        //  原版事件影响异能者失控指数
        // ============================================================

        /// <summary>
        /// 异能者击杀敌人时，降低自身失控指数（宣泄戾气，以战养战）。
        /// 注意：遵守禁令2，杀戮只产生失控指数，绝不产生异能能量。
        /// 特殊能力是通过战斗宣泄戾气，降低能量失控风险。
        /// 调用时机：EventHooks.OnDeath中。
        /// </summary>
    }
}





