// ============================================================
// 原版基因映射知识库（v0.18.2 新增，从 GeneAffinityBridge 抽出）
//
//  单一职责：原版基因 id 的知识（关键词→染色体分组、关键词→异能标签、
//  等级提取、单位原版基因采集）。无副作用、无缓存写入。
//
//  三系统桥接共用此知识：
//    GeneAffinityBridge       → 亲和计算 + 缓存（写 ds_affinity_* / ds_tagaff_*）
//    GeneContributionCollector → 显性明细采集（供 UI 展示"原版基因生效加成"）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Code.Core;

namespace Code.Realm.Prototype
{
    /// <summary>
    /// 原版基因 id → 染色体分组（真实基因 id，见反编译 GeneLibrary.cs）。
    /// 力量：damage_* / scale_plus；敏捷：speed_* / attack_speed；体质：armor_* / health_* / stamina_*；
    /// 智力：intelligence_*；感知：diplomacy_*；意志：warfare_*；潜能：stewardship_*
    /// </summary>
    public static class GeneVanillaMapping
    {
        // 染色体分组常量（与 GeneGraphWindow.GetGroupName 的 8 色数组一致）
        public const int CHR_STRENGTH = 0;    // 力量
        public const int CHR_AGILITY = 1;     // 敏捷
        public const int CHR_CONSTITUTION = 2;// 体质
        public const int CHR_INTELLECT = 3;   // 智力
        public const int CHR_PERCEPTION = 4;  // 感知
        public const int CHR_WILL = 5;        // 意志
        public const int CHR_ABILITY = 6;     // 异能（原版无直接映射，保留槽位）
        public const int CHR_POTENTIAL = 7;   // 潜能

        public static readonly string[] ChromosomeNames = { "力量", "敏捷", "体质", "智力", "感知", "意志", "异能", "潜能" };

        private static readonly Dictionary<string, int> _vanillaToChromosome = new Dictionary<string, int>
        {
            // damage → 力量(0)
            { "damage", CHR_STRENGTH },
            { "scale", CHR_STRENGTH },
            // speed/attack_speed → 敏捷(1)
            { "speed", CHR_AGILITY },
            { "attack_speed", CHR_AGILITY },
            // armor/health/stamina → 体质(2)
            { "armor", CHR_CONSTITUTION },
            { "health", CHR_CONSTITUTION },
            { "stamina", CHR_CONSTITUTION },
            // intelligence → 智力(3)
            { "intelligence", CHR_INTELLECT },
            // diplomacy → 感知(4)
            { "diplomacy", CHR_PERCEPTION },
            // warfare → 意志(5)
            { "warfare", CHR_WILL },
            // stewardship → 潜能(7)
            { "stewardship", CHR_POTENTIAL }
        };

        // 原版基因关键词 → 标签亲和（桥C：天生异能亲和）
        private static readonly Dictionary<string, List<PrototypeTag>> _vanillaToTags = new Dictionary<string, List<PrototypeTag>>
        {
            { "damage",  new List<PrototypeTag> { PrototypeTag.Burn, PrototypeTag.Crit, PrototypeTag.Chain } },
            { "armor",   new List<PrototypeTag> { PrototypeTag.Shield, PrototypeTag.Heal } },
            { "health",  new List<PrototypeTag> { PrototypeTag.Shield, PrototypeTag.Heal } },
            { "stamina", new List<PrototypeTag> { PrototypeTag.Shield, PrototypeTag.Heal } },
            { "speed",   new List<PrototypeTag> { PrototypeTag.Dodge, PrototypeTag.Slow } },
            { "attack_speed", new List<PrototypeTag> { PrototypeTag.Dodge, PrototypeTag.Chain } },
            { "intelligence", new List<PrototypeTag> { PrototypeTag.Heal, PrototypeTag.Stun } },
            { "warfare", new List<PrototypeTag> { PrototypeTag.Chain, PrototypeTag.Crit } },
            { "diplomacy", new List<PrototypeTag> { PrototypeTag.Heal, PrototypeTag.Shield } },
            { "stewardship", new List<PrototypeTag> { PrototypeTag.Heal, PrototypeTag.Shield } }
        };

        /// <summary>
        /// 遍历单位原版基因（亚种 nucleus 染色体），安全采集基因 id 列表。
        /// 只读，绝不写回原版数据结构（规避"原版基因强兼崩溃"教训）。
        /// </summary>
        public static List<string> CollectVanillaGenes(Actor actor)
        {
            var result = new List<string>();
            if (actor == null) return result;
            try
            {
                var nucleus = actor.subspecies?.nucleus;
                if (nucleus == null || nucleus.chromosomes == null) return result;
                foreach (var chr in nucleus.chromosomes)
                {
                    if (chr == null || chr.genes == null) continue;
                    foreach (var g in chr.genes)
                    {
                        GeneAsset gene = g as GeneAsset;
                        if (gene == null || string.IsNullOrEmpty(gene.id)) continue;
                        result.Add(gene.id);
                    }
                }
            }
            catch (System.Exception dsEx)
            {
                DSDebug.Verbose("[GeneCultivation] GeneVanillaMapping 采集原版基因异常: " + dsEx.Message);
            }
            return result;
        }

        /// <summary>
        /// 从原版基因 id 提取强度等级（damage_3 → 3；无数字 → 1）。
        /// </summary>
        public static int GetGeneLevel(string geneId)
        {
            try
            {
                int idx = geneId.LastIndexOf('_');
                if (idx >= 0 && idx < geneId.Length - 1)
                {
                    string suffix = geneId.Substring(idx + 1);
                    if (int.TryParse(suffix, out int level)) return Mathf.Clamp(level, 1, 5);
                }
            }
            catch { }
            return 1;
        }

        /// <summary>
        /// 匹配原版基因关键词（damage_1 → "damage"）。无匹配返回 null。
        /// </summary>
        public static string MatchKeyword(string geneId)
        {
            if (string.IsNullOrEmpty(geneId)) return null;
            foreach (var kv in _vanillaToChromosome)
            {
                if (geneId.StartsWith(kv.Key, System.StringComparison.Ordinal)) return kv.Key;
            }
            return null;
        }

        /// <summary>关键词 → 染色体分组（无映射返回 -1）</summary>
        public static int GetChromosomeOf(string keyword)
        {
            if (keyword != null && _vanillaToChromosome.TryGetValue(keyword, out int chr)) return chr;
            return -1;
        }

        /// <summary>关键词 → 标签列表（无映射返回 null）</summary>
        public static List<PrototypeTag> GetTagsOf(string keyword)
        {
            if (keyword != null && _vanillaToTags.TryGetValue(keyword, out var tags)) return tags;
            return null;
        }

        /// <summary>标签 → 支撑该标签的原版基因关键词列表（反向映射，供异能详情展示"来源"）</summary>
        public static List<string> GetGenesForTag(PrototypeTag tag)
        {
            var result = new List<string>();
            foreach (var kv in _vanillaToTags)
            {
                if (kv.Value != null && kv.Value.Contains(tag)) result.Add(kv.Key);
            }
            return result;
        }
    }
}
