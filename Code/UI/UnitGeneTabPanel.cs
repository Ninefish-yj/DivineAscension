// ============================================================
// 单位窗·基因页签内容面板（v0.18.4 新增）——uGUI 渲染
//
//  数据全部来自只读缓存（与基因图谱窗口同源），页面是纯视图：
//    - 状态行：境界/能量/失控/掌握度
//    - 染色体亲和条：原版基因 → 8 染色体（桥A 生效缓存）
//    - 基因属性加成总览：五类来源实时汇总（与 GeneBoostSummary 同口径）
//    - 原版基因来源：当前染色体的支撑基因明细
//    - 入口按钮：打开基因图谱/异能树
//
//  只克隆 UI 对象，不碰原版基因/存档结构。
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Code.Core;
using Code.Data;
using Code.Realm.Prototype;

namespace Code.UI
{
    /// <summary>单位窗基因页签的内容渲染（uGUI 静态组件，无状态）</summary>
    public static class UnitGeneTabPanel
    {
        private const string ContainerName = "DS_GeneTabContent";

        private static RectTransform _container;
        private static Actor _boundActor;

        internal static void Ensure(UnitWindow window, WindowMetaTab tab, ScrollWindow scroll)
        {
            try
            {
                if (window == null || tab == null || scroll == null) return;

                RectTransform scrollContent = scroll.transform_content;
                if (scrollContent == null) return;

                Transform existing = scrollContent.Find(ContainerName);
                if (existing != null)
                {
                    _container = existing as RectTransform ?? existing.GetComponent<RectTransform>();
                }
                else
                {
                    _container = BuildContainer(scrollContent);
                }

                if (_container != null && !tab.tab_elements.Contains(_container.transform))
                {
                    tab.tab_elements.Add(_container.transform);
                }

                bool onTab = scroll.tabs != null && scroll.tabs.isActiveTab(tab);
                if (_container != null) _container.gameObject.SetActive(onTab);
            }
            catch (System.Exception ex)
            {
                Code.Core.DSDebug.Verbose("[GeneCultivation] UnitGeneTabPanel.Ensure 异常: " + ex.Message);
            }
        }

        internal static void ShowForActor(Actor actor)
        {
            _boundActor = actor;
            if (_container == null) return;
            try { Render(); }
            catch (System.Exception ex) { Code.Core.DSDebug.Verbose("[GeneCultivation] UnitGeneTabPanel.Render 异常: " + ex.Message); }
        }

        internal static void Hide()
        {
            _boundActor = null;
            if (_container != null) _container.gameObject.SetActive(false);
        }

        // ---------- 容器构建 ----------

        private static RectTransform BuildContainer(RectTransform scrollContent)
        {
            GameObject go = new GameObject(ContainerName, typeof(RectTransform));
            go.transform.SetParent(scrollContent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 3f;
            layout.padding = new RectOffset(6, 6, 4, 10);

            return rt;
        }

        // ---------- 渲染 ----------

        private static void Render()
        {
            Actor actor = _boundActor;
            if (actor == null) return;
            ClearChildren();
            bool en = UILocalization.CurrentLanguage == "en";

            int tier = CultivationData.GetRealmTier(actor);
            float energy = CultivationData.GetEnergy(actor);
            float turb = CultivationData.GetTurbulence(actor);
            float mastery = CultivationData.GetMastery(actor);

            // 标题
            MakeText("title", en ? "GENE PANEL" : UILocalization.Get("unit_gene_tab_title"),
                11, FontStyle.Bold, new Color(0.95f, 0.85f, 0.5f), TextAnchor.MiddleCenter);

            // 状态行
            string tierName = UnitWindowIntegration.GetTierName(tier);
            string status = string.Format("{0} {1} · {2} {3:F0} · {4} {5:F1}/100",
                en ? "Realm" : UILocalization.Get("unit_realm"), tierName,
                en ? "Energy" : UILocalization.Get("unit_qi"), energy,
                en ? "Turb" : UILocalization.Get("unit_turbulence"), turb);
            MakeText("status", status, 9, FontStyle.Normal, new Color(0.85f, 0.9f, 0.95f), TextAnchor.MiddleLeft);

            if (tier > 0 && tier < 13)
            {
                string masteryText = string.Format("{0} {1:F0}/100", en ? "Mastery" : UILocalization.Get("unit_mastery"), mastery);
                MakeText("mastery", masteryText, 9, FontStyle.Normal, new Color(0.75f, 0.85f, 0.8f), TextAnchor.MiddleLeft);
            }

            // 分隔
            MakeDivider(6f);

            // 染色体亲和（桥A）
            MakeText("aff_head", en ? "CHROMOSOME AFFINITY (vanilla gene -> 8)" : UILocalization.Get("bridge_section_title"),
                10, FontStyle.Bold, new Color(0.6f, 0.9f, 0.85f), TextAnchor.MiddleLeft);
            bool anyAff = false;
            for (int i = 0; i < 8; i++)
            {
                float aff = GeneAffinityBridge.GetChromosomeAffinity(actor, i);
                if (aff <= 0.01f) continue;
                anyAff = true;
                string name = i < GeneVanillaMapping.ChromosomeNames.Length ? GeneVanillaMapping.ChromosomeNames[i] : ("C" + i);
                MakeAffinityRow("aff_" + i, name, aff, 8f);
            }
            if (!anyAff)
            {
                MakeText("aff_none", en ? "No active affinity yet (awaken & cultivate)" : UILocalization.Get("bridge_no_genes"),
                    9, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleLeft);
            }

            MakeDivider(6f);

            // 基因属性加成总览
            MakeText("boost_head", en ? "GENE BOOST SUMMARY (live)" : UILocalization.Get("boost_title"),
                10, FontStyle.Bold, new Color(0.95f, 0.85f, 0.5f), TextAnchor.MiddleLeft);
            var boosts = GeneBoostSummary.Summarize(actor);
            if (boosts.Count == 0)
            {
                MakeText("boost_none", en ? "No active gene boosts (awaken first)" : UILocalization.Get("boost_none"),
                    9, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleLeft);
            }
            else
            {
                int show = Mathf.Min(14, boosts.Count);
                for (int i = 0; i < show; i++)
                {
                    MakeBoostRow("boost_" + i, boosts[i].Label, boosts[i].Value);
                }
                if (boosts.Count > show)
                {
                    MakeText("boost_more", "+" + (boosts.Count - show) + (en ? " more" : UILocalization.Get("boost_more")),
                        8, FontStyle.Normal, new Color(0.5f, 0.55f, 0.5f), TextAnchor.MiddleLeft);
                }
            }

            MakeDivider(6f);

            // 原版基因来源（当前染色体分组按亲和最强的分组聚合展示）
            var contributions = GeneContributionCollector.Collect(actor);
            if (contributions != null && contributions.Count > 0)
            {
                MakeText("src_head", en ? "VANILLA GENE SOURCES" : UILocalization.Get("proto_sources_line"),
                    10, FontStyle.Bold, new Color(0.75f, 0.8f, 0.9f), TextAnchor.MiddleLeft);
                int showSrc = Mathf.Min(8, contributions.Count);
                for (int i = 0; i < showSrc; i++)
                {
                    var c = contributions[i];
                    string tagText = "";
                    if (c.Tags != null && c.Tags.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (var t in c.Tags) names.Add(t.ToString());
                        tagText = " [" + string.Join("/", names.ToArray()) + "]";
                    }
                    MakeText("src_" + i, c.GeneId + "  Lv" + c.Level + "  +" + c.Weight.ToString("F1") + tagText,
                        9, FontStyle.Normal, new Color(0.65f, 0.9f, 0.7f), TextAnchor.MiddleLeft);
                }
                if (contributions.Count > showSrc)
                {
                    MakeText("src_more", "+" + (contributions.Count - showSrc) + (en ? " more" : UILocalization.Get("bridge_more")),
                        8, FontStyle.Normal, new Color(0.5f, 0.55f, 0.5f), TextAnchor.MiddleLeft);
                }
            }

            MakeDivider(8f);

            // 入口按钮：基因图谱 / 异能树
            Actor bound = actor;
            MakeButton("btn_gene", en ? "Open Gene Graph" : UILocalization.Get("unit_gene_detail"), () =>
            {
                if (bound != null && bound.isAlive()) GeneGraphWindow.Show(bound);
            });
            MakeButton("btn_proto", en ? "Open Ability Tree" : UILocalization.Get("unit_prototype"), () =>
            {
                if (bound != null && bound.isAlive()) PrototypeWindow.Show(bound);
            });
        }

        private static void ClearChildren()
        {
            if (_container == null) return;
            for (int i = _container.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_container.GetChild(i).gameObject);
            }
        }

        // ---------- uGUI 基础控件 ----------

        private static Text MakeText(string childName, string text, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(_container, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size + 8f;
            le.minHeight = size + 6f;

            Text t = go.AddComponent<Text>();
            t.font = LocalizedTextManager.current_font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        private static void MakeDivider(float height)
        {
            GameObject go = new GameObject("divider", typeof(RectTransform));
            go.transform.SetParent(_container, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            Image img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.12f);
            img.raycastTarget = false;
        }

        private static void MakeAffinityRow(string childName, string name, float value, float cap)
        {
            GameObject row = new GameObject(childName, typeof(RectTransform));
            row.transform.SetParent(_container, false);
            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 14f;
            rowLe.minHeight = 14f;

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 6f;
            hlg.padding = new RectOffset(2, 2, 1, 1);

            Text nameText = NewChildText(row.transform, "n", name, 9, TextAnchor.MiddleLeft, new Color(0.8f, 0.9f, 1f));
            LayoutElement nameLe = nameText.gameObject.AddComponent<LayoutElement>();
            nameLe.preferredWidth = 44f;
            nameLe.minWidth = 44f;

            // 进度条（底 + 填充）
            GameObject barGo = new GameObject("bar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(row.transform, false);
            Image barImg = barGo.GetComponent<Image>();
            barImg.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
            barImg.raycastTarget = false;
            LayoutElement barLe = barGo.AddComponent<LayoutElement>();
            barLe.flexibleWidth = 1f;
            barLe.preferredHeight = 10f;

            float pct = Mathf.Clamp01(value / cap);
            if (pct > 0.01f)
            {
                GameObject fillGo = new GameObject("fill", typeof(RectTransform), typeof(Image));
                fillGo.transform.SetParent(barGo.transform, false);
                RectTransform fillRt = fillGo.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = new Vector2(pct, 1f);
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
                Image fillImg = fillGo.GetComponent<Image>();
                fillImg.color = new Color(0.35f, 0.8f, 0.6f, 0.95f);
                fillImg.raycastTarget = false;
            }

            Text valueText = NewChildText(row.transform, "v", value.ToString("F1"), 9, TextAnchor.MiddleRight, new Color(0.85f, 0.9f, 0.95f));
            LayoutElement valueLe = valueText.gameObject.AddComponent<LayoutElement>();
            valueLe.preferredWidth = 34f;
            valueLe.minWidth = 34f;
        }

        private static void MakeBoostRow(string childName, string label, string value)
        {
            GameObject row = new GameObject(childName, typeof(RectTransform));
            row.transform.SetParent(_container, false);
            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 14f;
            rowLe.minHeight = 14f;

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 6f;
            hlg.padding = new RectOffset(2, 2, 1, 1);

            Text labelText = NewChildText(row.transform, "l", label, 9, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 0.9f));
            LayoutElement labelLe = labelText.gameObject.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1f;
            labelLe.minWidth = 60f;

            Text valueText = NewChildText(row.transform, "v", value, 9, TextAnchor.MiddleRight, new Color(0.9f, 0.75f, 0.4f));
            LayoutElement valueLe = valueText.gameObject.AddComponent<LayoutElement>();
            valueLe.preferredWidth = 70f;
            valueLe.minWidth = 60f;
        }

        private static void MakeButton(string childName, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(childName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_container, false);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.25f, 0.3f, 0.35f, 0.9f);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 24f;
            le.minHeight = 22f;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            Text t = NewChildText(go.transform, "label", label, 9, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.95f));
            RectTransform tRt = t.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
        }

        private static Text NewChildText(Transform parent, string childName, string text, int size, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = LocalizedTextManager.current_font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }
    }
}
