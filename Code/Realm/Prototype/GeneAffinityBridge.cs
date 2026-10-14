// ============================================================
// 基因亲和桥（v0.17.0 新增系统）——三系统联动的桥接层
//
//  原版基因 ──桥A──▶ 我的基因（觉醒定相：染色体亲和决定初始基因构成）
//  原版基因 ──桥C──▶ 我的异能（天生亲和：标签亲和决定异能权重与触发强度）
//
// 设计约束（用户确认）：
//  1. 现有三系统内部零改动，本文件是唯一新增桥接层
//  2. 原版基因只读不写（规避"原版基因强兼崩溃"教训——仅遍历 nucleus.chromosomes 读 GeneAsset.id）
//  3. 亲和度缓存到 custom_data_float（ds_affinity_* / ds_tagaff_*），与现有 ds_* 模式一致
//  4. 单一职责：读原版基因 → 算亲和 → 写缓存；消费方（觉醒/异能引擎）自行读取
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Code.Core;
using Code.Data;

namespace Code.Realm.Prototype
{
    public static class GeneAffinityBridge
    {
        // custom_data 键
        public const string KEY_PROTOTYPE = "ds_prototype";          // 异能ID（string）
        public const string KEY_AFFINITY_PREFIX = "ds_affinity_";    // 染色体亲和（float，0-7）
        public const string KEY_TAGAFF_PREFIX = "ds_tagaff_";        // 标签亲和（float，按标签名）

        // 原版基因 id → 染色体分组（真实基因 id，见反编译 GeneLibrary.cs）
        // 力量：damage_* / scale_plus
        // 敏捷：speed_* / attack_speed
        // 体质：armor_* / health_* / stamina_*
        // 智力：intelligence_*
        // 感知：diplomacy_*
        // 意志：warfare_*
        // 潜能：stewardship_*
        private static readonly Dictionary<string, int> _vanillaToChromosome = new Dictionary<string, int>
        {
            // damage → 力量(0)
            { "damage", 0 },
            { "scale", 0 },
            // speed/attack_speed → 敏捷(1)
            { "speed", 1 },
            { "attack_speed", 1 },
            // armor/health/stamina → 体质(2)
            { "armor", 2 },
            { "health", 2 },
            { "stamina", 2 },
            // intelligence → 智力(3)
            { "intelligence", 3 },
            // diplomacy → 感知(4)
            { "diplomacy", 4 },
            // warfare → 意志(5)
            { "warfare", 5 },
            // stewardship → 潜能(7)
            { "stewardship", 7 }
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
        /// 与 TranscendentSpeciesSystem.ScanAndApplyGeneticBoost 同一数据源（actor.subspecies.nucleus.chromosomes）。
        /// 只读，绝不写回原版数据结构。
        /// </summary>
        private static List<string> CollectVanillaGenes(Actor actor)
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
                DSDebug.Verbose("[GeneCultivation] GeneAffinityBridge 采集原版基因异常: " + dsEx.Message);
            }
            return result;
        }

        /// <summary>
        /// 从原版基因 id 提取强度等级（damage_3 → 3；无数字 → 1）。
        /// </summary>
        private static int GetGeneLevel(string geneId)
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
        /// 匹配原版基因关键词（damage_1 → "damage"）。
        /// </summary>
        private static string MatchKeyword(string geneId)
        {
            foreach (var kv in _vanillaToChromosome)
            {
                if (geneId.StartsWith(kv.Key, System.StringComparison.Ordinal)) return kv.Key;
            }
            return null;
        }

        /// <summary>
        /// 桥A + 桥C 核心：同步亲和度。
        /// 遍历原版基因一次，同时计算：
        ///   - 染色体亲和（ds_affinity_{0-7}）：觉醒时决定初始解锁基因的染色体倾向
        ///   - 标签亲和（ds_tagaff_{tag}）：异能触发概率/强度的天生加成
        /// 仅在觉醒（或显式调用）时执行，亲和度缓存持久化于 custom_data。
        /// </summary>
        public static void SyncAffinities(Actor actor)
        {
            if (actor == null) return;
            var data = ActorDataAccessor.GetData(actor);
            if (data == null) return;
            CultivationData.EnsureCustomDataInitialized(actor);
            if (data.custom_data_float == null) return;

            try
            {
                // 染色体亲和（8 条）
                float[] chromoAff = new float[8];
                // 标签亲和（枚举到 float）
                float[] tagAff = new float[System.Enum.GetValues(typeof(PrototypeTag)).Length];

                var genes = CollectVanillaGenes(actor);
                foreach (string geneId in genes)
                {
                    if (string.IsNullOrEmpty(geneId)) continue;
                    int level = GetGeneLevel(geneId);
                    float weight = 1f + level * 0.5f;   // 等级越高亲和越强（_1=1.5，_5=3.5）

                    // 桥A：染色体亲和
                    string kw = MatchKeyword(geneId);
                    if (kw != null && _vanillaToChromosome.TryGetValue(kw, out int chr))
                    {
                        if (chr >= 0 && chr < chromoAff.Length) chromoAff[chr] += weight;
                    }

                    // 桥C：标签亲和
                    if (kw != null && _vanillaToTags.TryGetValue(kw, out var tags))
                    {
                        foreach (var tag in tags)
                        {
                            int ti = (int)tag;
                            if (ti >= 0 && ti < tagAff.Length) tagAff[ti] += weight;
                        }
                    }
                }

                // 写缓存（归一化：染色体亲和上限 8，标签亲和上限 5，防止堆叠爆炸）
                for (int i = 0; i < 8; i++)
                {
                    data.custom_data_float[KEY_AFFINITY_PREFIX + i] = Mathf.Min(8f, chromoAff[i]);
                }
                for (int i = 0; i < tagAff.Length; i++)
                {
                    string tagName = ((PrototypeTag)i).ToString();
                    data.custom_data_float[KEY_TAGAFF_PREFIX + tagName] = Mathf.Min(5f, tagAff[i]);
                }

                DSDebug.Verbose("[GeneCultivation] GeneAffinityBridge 亲和同步完成: " + actor.getName() +
                    " 染色体亲和=" + string.Join(",", chromoAff) + " 标签亲和=" + string.Join(",", tagAff));
            }
            catch (System.Exception dsEx)
            {
                DSDebug.Warning("[GeneCultivation] GeneAffinityBridge 亲和同步异常: " + dsEx.Message);
            }
        }

        /// <summary>读染色体亲和（未同步返回 0）</summary>
        public static float GetChromosomeAffinity(Actor actor, int chromosome)
        {
            if (actor == null || chromosome < 0 || chromosome > 7) return 0f;
            var data = ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_float == null) return 0f;
            data.custom_data_float.TryGetValue(KEY_AFFINITY_PREFIX + chromosome, out float v);
            return v;
        }

        /// <summary>读标签亲和（未同步返回 0）</summary>
        public static float GetTagAffinity(Actor actor, PrototypeTag tag)
        {
            if (actor == null) return 0f;
            var data = ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_float == null) return 0f;
            data.custom_data_float.TryGetValue(KEY_TAGAFF_PREFIX + tag.ToString(), out float v);
            return v;
        }

        /// <summary>
        /// 觉醒定相（桥A 消费方）：按亲和度加权分配元素系异能。
        /// 权重 = 基础1 + 染色体亲和[异能偏好]×1 + 标签亲和(主标签+副标签)×0.5
        /// 已有异能不覆盖（终身锁定）。
        /// </summary>
        public static string RollPrototype(Actor actor)
        {
            if (actor == null) return null;
            var data = ActorDataAccessor.GetData(actor);
            if (data == null) return null;
            CultivationData.EnsureCustomDataInitialized(actor);

            // 已有异能：终身锁定不更换
            if (data.custom_data_string != null &&
                data.custom_data_string.TryGetValue(KEY_PROTOTYPE, out string existing) &&
                !string.IsNullOrEmpty(existing))
            {
                return existing;
            }

            try
            {
                var candidates = PrototypeRegistry.Elements;
                if (candidates == null || candidates.Count == 0) return null;

                float totalWeight = 0f;
                float[] weights = new float[candidates.Count];
                for (int i = 0; i < candidates.Count; i++)
                {
                    var def = candidates[i];
                    if (def == null) continue;
                    float w = 1f;
                    w += GetChromosomeAffinity(actor, def.ChromosomePreference) * 1f;
                    w += GetTagAffinity(actor, def.MainTag) * 0.5f;
                    w += GetTagAffinity(actor, def.SubTag) * 0.25f;
                    weights[i] = w;
                    totalWeight += w;
                }

                if (totalWeight <= 0f) return null;

                float roll = Random.value * totalWeight;
                float acc = 0f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    acc += weights[i];
                    if (roll <= acc)
                    {
                        string chosenId = candidates[i].Id;
                        data.custom_data_string[KEY_PROTOTYPE] = chosenId;
                        DSDebug.Verbose("[GeneCultivation] 觉醒定相: " + actor.getName() + " 觉醒异能=" + candidates[i].NameZh + "(" + chosenId + ")");
                        return chosenId;
                    }
                }
            }
            catch (System.Exception dsEx)
            {
                DSDebug.Warning("[GeneCultivation] GeneAffinityBridge 异能分配异常: " + dsEx.Message);
            }
            return null;
        }
    }
}
