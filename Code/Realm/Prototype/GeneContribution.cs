// ============================================================
// 原版基因贡献明细（v0.18.2 新增）——三系统桥接的"显性效果层"
//
//  背景：桥A/桥C 的亲和度一直有隐性效果（战斗/觉醒），但玩家不可感知。
//  本层把"单位实际携带的原版基因 → 正在生效的染色体/标签亲和"采集成
//  可展示明细，供 UI 呈现（原版基因不再只是文案，看得见摸得着）。
//
//  只读采集：复用 GeneVanillaMapping 数据知识，绝不写回原版基因。
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Code.Realm.Prototype
{
    /// <summary>单条原版基因的生效贡献明细（供 UI 展示）</summary>
    public class VanillaGeneContribution
    {
        public string GeneId;               // 原版基因 id，如 damage_3
        public int Level;                   // 强度等级 1-5
        public string Keyword;              // 匹配关键词，如 damage（无匹配为 null）
        public float Weight;                // 亲和权重 = 1 + level*0.5
        public int Chromosome;              // 染色体分组 -1=无映射
        public List<PrototypeTag> Tags;     // 关联标签（无映射为 null）
    }

    /// <summary>
    /// 明细采集器：遍历单位原版基因一次，产出贡献明细 + 聚合亲和。
    /// 聚合口径与 GeneAffinityBridge.SyncAffinities 完全一致（同一公式），
    /// 便于 UI 同时展示"明细"与"生效缓存"而不会对不上。
    /// </summary>
    public static class GeneContributionCollector
    {
        /// <summary>采集明细（无基因返回空表）</summary>
        public static List<VanillaGeneContribution> Collect(Actor actor)
        {
            var result = new List<VanillaGeneContribution>();
            if (actor == null) return result;
            try
            {
                var ids = GeneVanillaMapping.CollectVanillaGenes(actor);
                foreach (string geneId in ids)
                {
                    if (string.IsNullOrEmpty(geneId)) continue;
                    var c = new VanillaGeneContribution
                    {
                        GeneId = geneId,
                        Level = GeneVanillaMapping.GetGeneLevel(geneId),
                        Keyword = GeneVanillaMapping.MatchKeyword(geneId),
                        Chromosome = -1,
                        Tags = null
                    };
                    c.Weight = 1f + c.Level * 0.5f;
                    if (c.Keyword != null)
                    {
                        c.Chromosome = GeneVanillaMapping.GetChromosomeOf(c.Keyword);
                        c.Tags = GeneVanillaMapping.GetTagsOf(c.Keyword);
                    }
                    result.Add(c);
                }
            }
            catch (System.Exception dsEx)
            {
                DSDebug.Verbose("[GeneCultivation] GeneContributionCollector 采集异常: " + dsEx.Message);
            }
            return result;
        }

        /// <summary>聚合染色体亲和（与桥A缓存同公式；cap 由调用方按需处理）</summary>
        public static float[] AggregateChromosome(List<VanillaGeneContribution> contributions)
        {
            float[] aff = new float[8];
            if (contributions == null) return aff;
            foreach (var c in contributions)
            {
                if (c.Chromosome >= 0 && c.Chromosome < aff.Length) aff[c.Chromosome] += c.Weight;
            }
            return aff;
        }

        /// <summary>聚合标签亲和（与桥C缓存同公式）</summary>
        public static float[] AggregateTags(List<VanillaGeneContribution> contributions)
        {
            int count = System.Enum.GetValues(typeof(PrototypeTag)).Length;
            float[] aff = new float[count];
            if (contributions == null) return aff;
            foreach (var c in contributions)
            {
                if (c.Tags == null) continue;
                foreach (var tag in c.Tags)
                {
                    int ti = (int)tag;
                    if (ti >= 0 && ti < aff.Length) aff[ti] += c.Weight;
                }
            }
            return aff;
        }

        /// <summary>展示用标签名（前3个最高亲和标签）</summary>
        public static string TopTagsSummary(List<VanillaGeneContribution> contributions)
        {
            if (contributions == null || contributions.Count == 0) return "";
            var aff = AggregateTags(contributions);
            int[] order = new int[aff.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => aff[b].CompareTo(aff[a]));
            var names = new List<string>();
            for (int i = 0; i < order.Length && i < 3; i++)
            {
                if (aff[order[i]] <= 0f) break;
                names.Add(((PrototypeTag)order[i]).ToString());
            }
            return string.Join("/", names);
        }
    }
}
