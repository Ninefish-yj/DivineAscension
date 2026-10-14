// ============================================================
// 原版基因·桥接来源面板（v0.18.2 新增）——三系统桥接的显性展示层
//
//  让玩家直接看到：单位携带的原版基因 → 正在支撑的染色体/标签亲和，
//  不再只是隐性数值（原版基因系统"看得见摸得着"）。
//
//  纯展示：数据来自 GeneContributionCollector / GeneAffinityBridge 缓存，
//  全部只读；样式走 DSGUIStylePool + GUI.color（性能规范）。
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Code.Realm.Prototype;

namespace Code.UI
{
    /// <summary>原版基因桥接来源的 UI 组件（静态绘制，无状态）</summary>
    public static class VanillaGenePanel
    {
        /// <summary>
        /// 左栏紧凑入口：原版基因总数 + 前3支撑标签（无基因显示占位提示）。
        /// 返回是否绘制了实际内容。
        /// </summary>
        public static bool DrawSourceLine(Actor actor)
        {
            var contributions = GeneContributionCollector.Collect(actor);
            bool en = UILocalization.CurrentLanguage == "en";
            if (contributions == null || contributions.Count == 0)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                GUILayout.Label(en ? "No vanilla genes on subspecies" : UILocalization.Get("bridge_no_genes"), DSGUIStylePool.Label(9, FontStyle.Normal, true));
                GUI.color = Color.white;
                return false;
            }

            string topTags = GeneContributionCollector.TopTagsSummary(contributions);
            GUI.color = new Color(0.6f, 0.85f, 0.75f);
            GUILayout.Label(en
                ? "Vanilla genes: " + contributions.Count + "  ->  " + topTags
                : string.Format(UILocalization.Get("bridge_source_line"), contributions.Count, topTags),
                DSGUIStylePool.Label(9, FontStyle.Normal, true));
            GUI.color = Color.white;
            return true;
        }

        /// <summary>
        /// 中栏详情区：8 条染色体亲和进度 + 当前分组(染色体)的来源基因明细。
        /// 亲和读生效缓存（与觉醒定相/异能权重同源）。
        /// </summary>
        public static void DrawRealmBridgeSection(Actor actor, int group)
        {
            if (actor == null) return;
            bool en = UILocalization.CurrentLanguage == "en";

            // 分隔
            Rect sepRect = GUILayoutUtility.GetRect(1f, 1f, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
            GUI.DrawTexture(sepRect, Texture2D.whiteTexture);
            GUILayout.Space(6f);

            GUI.color = new Color(0.6f, 0.9f, 0.85f);
            GUILayout.Label(en ? "VANILLA GENE -> CHROMOSOME AFFINITY (bridge)" : UILocalization.Get("bridge_section_title"), DSGUIStylePool.Label(10, FontStyle.Bold));
            GUI.color = Color.white;

            // 8 条染色体亲和微条（cap 8，与桥A缓存上限一致）
            float cap = 8f;
            for (int i = 0; i < 8; i++)
            {
                float aff = GeneAffinityBridge.GetChromosomeAffinity(actor, i);
                if (aff <= 0.01f) continue;
                string name = i < GeneVanillaMapping.ChromosomeNames.Length ? GeneVanillaMapping.ChromosomeNames[i] : ("C" + i);
                DrawAffinityRow(name, aff, cap, en ? "8" : "8");
            }

            // 当前元素所属染色体的来源基因明细
            var contributions = GeneContributionCollector.Collect(actor);
            var sources = new List<VanillaGeneContribution>();
            if (contributions != null)
            {
                foreach (var c in contributions)
                {
                    if (c.Chromosome == group) sources.Add(c);
                }
            }

            GUILayout.Space(4f);
            if (sources.Count == 0)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                GUILayout.Label(en ? "No vanilla genes feed this chromosome" : UILocalization.Get("bridge_chr_none"), DSGUIStylePool.Label(9));
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(0.75f, 0.8f, 0.9f);
                GUILayout.Label(en
                    ? "Feeding genes (" + sources.Count + "):"
                    : string.Format(UILocalization.Get("bridge_chr_feeders"), sources.Count),
                    DSGUIStylePool.Label(9, FontStyle.Bold));
                GUI.color = Color.white;
                // 最多展示 6 条，避免中栏过长
                int show = Mathf.Min(6, sources.Count);
                for (int i = 0; i < show; i++)
                {
                    var c = sources[i];
                    string tagText = "";
                    if (c.Tags != null && c.Tags.Count > 0)
                    {
                        var tnames = new List<string>();
                        foreach (var t in c.Tags) tnames.Add(t.ToString());
                        tagText = " [" + string.Join("/", tnames) + "]";
                    }
                    GUI.color = new Color(0.65f, 0.9f, 0.7f);
                    GUILayout.Label(c.GeneId + "  Lv" + c.Level + "  +" + c.Weight.ToString("F1") + tagText, DSGUIStylePool.Label(9));
                    GUI.color = Color.white;
                }
                if (sources.Count > show)
                {
                    GUI.color = new Color(0.5f, 0.6f, 0.55f);
                    GUILayout.Label(en ? "+" + (sources.Count - show) + " more" : "+" + (sources.Count - show) + UILocalization.Get("bridge_more"), DSGUIStylePool.Label(8));
                    GUI.color = Color.white;
                }
            }
        }

        /// <summary>单条亲和进度条（纯 GUI 绘制，无纹理分配）</summary>
        private static void DrawAffinityRow(string name, float value, float cap, string capText)
        {
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUI.color = new Color(0.8f, 0.9f, 1f);
            GUILayout.Label(name, DSGUIStylePool.Label(9), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(40f) });
            GUI.color = Color.white;

            float pct = Mathf.Clamp01(value / cap);
            Rect barRect = GUILayoutUtility.GetRect(1f, 8f, (GUILayoutOption[])(object)new GUILayoutOption[2] { GUILayout.ExpandWidth(true), GUILayout.MinWidth(40f) });
            // 底
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            // 填充
            if (pct > 0.01f)
            {
                Rect fill = new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height);
                GUI.color = new Color(0.35f, 0.8f, 0.6f, 0.95f);
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            GUI.color = new Color(0.85f, 0.9f, 0.95f);
            GUILayout.Label(value.ToString("F1"), DSGUIStylePool.Label(9), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(36f) });
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }
    }
}
