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

        // 原版基因 id → 染色体/标签映射已抽至 GeneVanillaMapping（数据知识单一职责）

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

                var genes = GeneVanillaMapping.CollectVanillaGenes(actor);
                foreach (string geneId in genes)
                {
                    if (string.IsNullOrEmpty(geneId)) continue;
                    int level = GeneVanillaMapping.GetGeneLevel(geneId);
                    float weight = 1f + level * 0.5f;   // 等级越高亲和越强（_1=1.5，_5=3.5）

                    // 桥A：染色体亲和
                    string kw = GeneVanillaMapping.MatchKeyword(geneId);
                    int chr = GeneVanillaMapping.GetChromosomeOf(kw);
                    if (chr >= 0 && chr < chromoAff.Length) chromoAff[chr] += weight;

                    // 桥C：标签亲和
                    var tags = GeneVanillaMapping.GetTagsOf(kw);
                    if (tags != null)
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
                    // 稀有度加权（v0.18.22）：品质越高越难天生觉醒（概念级约 1.5%）
                    w *= PrototypeEvolution.GetRarityWeight(PrototypeEvolution.GetTier(def));
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
                        PrototypeEvolution.RecordAwakened(actor);   // 记录来源=天生觉醒 + 觉醒年份（God 视角区分进阶）
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
