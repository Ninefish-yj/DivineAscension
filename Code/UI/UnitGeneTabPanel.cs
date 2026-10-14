// ============================================================
// 单位窗·基因页签内容面板（v0.18.5 重构）——uGUI 渲染
//
//  数据全部来自只读缓存（与基因图谱窗口同源），页面是纯视图：
//    - 状态行：境界/能量/失控/掌握度
//    - 染色体亲和条：原版基因 → 8 染色体（桥A 生效缓存）
//    - 基因属性加成总览：五类来源实时汇总（与 GeneBoostSummary 同口径）
//    - 原版基因来源：当前染色体的支撑基因明细
//    - 入口按钮：打开基因图谱/异能树
//
//  软工检查后重构：
//    - AddElement/CreateText 工厂消除 6 处创建样板（单一职责）
//    - UnitGeneTabLayout 常量类集中魔法数（布局与业务分离）
//    - 分隔条 raycastTarget=false（修复点击射线拦截）
//    - ShowForActor(actor, render)：懒渲染，仅激活页签时重建内容
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
    /// <summary>基因页签布局常量（集中管理，避免魔法数散落）</summary>
    internal static class UnitGeneTabLayout
    {
        public const float GroupSpacing = 3f;
        public const int GroupPaddingL = 6;
        public const int GroupPaddingR = 6;
        public const int GroupPaddingT = 4;
        public const int GroupPaddingB = 10;

        public const int TitleFont = 11;
        public const int HeadFont = 10;
        public const int BodyFont = 9;
        public const int HintFont = 8;

        public const float RowHeight = 14f;
        public const float ButtonHeight = 24f;
        public const float ButtonHeightMin = 22f;
        public const float DividerHeight = 6f;
        public const float AffinityCap = 8f;
        public const int AffinityLabelWidth = 44;
        public const int AffinityValueWidth = 34;
        public const int BoostValueWidth = 70;
        public const int BoostValueWidthMin = 60;
        public const int BoostMaxRows = 14;
        public const int SourceMaxRows = 8;

        public static readonly Color TitleColor = new Color(0.95f, 0.85f, 0.5f);
        public static readonly Color BodyColor = new Color(0.85f, 0.9f, 0.95f);
        public static readonly Color MasteryColor = new Color(0.75f, 0.85f, 0.8f);
        public static readonly Color HeadColor = new Color(0.6f, 0.9f, 0.85f);
        public static readonly Color AffinityNameColor = new Color(0.8f, 0.9f, 1f);
        public static readonly Color SourceHeadColor = new Color(0.75f, 0.8f, 0.9f);
        public static readonly Color SourceColor = new Color(0.65f, 0.9f, 0.7f);
        public static readonly Color BoostLabelColor = new Color(0.8f, 0.85f, 0.9f);
        public static readonly Color BoostValueColor = new Color(0.9f, 0.75f, 0.4f);
        public static readonly int BoostLabelMinWidth = 60;
        public static readonly Color HintColor = new Color(0.5f, 0.5f, 0.5f);
        public static readonly Color HintAltColor = new Color(0.5f, 0.55f, 0.5f);
        public static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color BarBackColor = new Color(0.2f, 0.2f, 0.25f, 0.9f);
        public static readonly Color BarFillColor = new Color(0.35f, 0.8f, 0.6f, 0.95f);
        public static readonly Color ButtonColor = new Color(0.25f, 0.3f, 0.35f, 0.9f);
        public static readonly Color ButtonTextColor = new Color(1f, 1f, 1f, 0.95f);
    }

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

                // 激活态由原版 startTabAction 接管（tab_elements 统一显隐）；
                // 此处只对齐初始状态，避免容器残留显示在默认页签里。
                bool onTab = scroll.tabs != null && scroll.tabs.isActiveTab(tab);
                if (_container != null) _container.gameObject.SetActive(onTab);
            }
            catch (System.Exception ex)
            {
                Code.Core.DSDebug.Verbose("[GeneCultivation] UnitGeneTabPanel.Ensure 异常: " + ex.Message);
            }
        }

        /// <summary>记录目标单位；仅在 render=true（页签激活）时重建内容——懒渲染</summary>
        internal static void ShowForActor(Actor actor, bool render)
        {
            _boundActor = actor;
            if (_container == null) return;
            if (!render) return;
            try { Render(); }
            catch (System.Exception ex) { Code.Core.DSDebug.Warning("[GeneCultivation] UnitGeneTabPanel.Render 异常: " + ex.Message); }
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
            layout.spacing = UnitGeneTabLayout.GroupSpacing;
            layout.padding = new RectOffset(
                UnitGeneTabLayout.GroupPaddingL, UnitGeneTabLayout.GroupPaddingR,
                UnitGeneTabLayout.GroupPaddingT, UnitGeneTabLayout.GroupPaddingB);

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
                UnitGeneTabLayout.TitleFont, FontStyle.Bold, UnitGeneTabLayout.TitleColor, TextAnchor.MiddleCenter);

            // 状态行
            string tierName = UnitWindowIntegration.GetTierName(tier);
            MakeText("status", string.Format("{0} {1} · {2} {3:F0} · {4} {5:F1}/100",
                en ? "Realm" : UILocalization.Get("unit_realm"), tierName,
                en ? "Energy" : UILocalization.Get("unit_qi"), energy,
                en ? "Turb" : UILocalization.Get("unit_turbulence"), turb),
                UnitGeneTabLayout.BodyFont, FontStyle.Normal, UnitGeneTabLayout.BodyColor, TextAnchor.MiddleLeft);

            if (tier > 0 && tier < 13)
            {
                MakeText("mastery", string.Format("{0} {1:F0}/100",
                    en ? "Mastery" : UILocalization.Get("unit_mastery"), mastery),
                    UnitGeneTabLayout.BodyFont, FontStyle.Normal, UnitGeneTabLayout.MasteryColor, TextAnchor.MiddleLeft);
            }

            MakeDivider(UnitGeneTabLayout.DividerHeight);

            // 染色体亲和（桥A）
            MakeText("aff_head", en ? "CHROMOSOME AFFINITY (vanilla gene -> 8)" : UILocalization.Get("bridge_section_title"),
                UnitGeneTabLayout.HeadFont, FontStyle.Bold, UnitGeneTabLayout.HeadColor, TextAnchor.MiddleLeft);
            bool anyAff = false;
            for (int i = 0; i < 8; i++)
            {
                float aff = GeneAffinityBridge.GetChromosomeAffinity(actor, i);
                if (aff <= 0.01f) continue;
                anyAff = true;
                string name = i < GeneVanillaMapping.ChromosomeNames.Length ? GeneVanillaMapping.ChromosomeNames[i] : ("C" + i);
                MakeAffinityRow("aff_" + i, name, aff, UnitGeneTabLayout.AffinityCap);
            }
            if (!anyAff)
            {
                MakeText("aff_none", en ? "No active affinity yet (awaken & cultivate)" : UILocalization.Get("bridge_no_genes"),
                    UnitGeneTabLayout.BodyFont, FontStyle.Normal, UnitGeneTabLayout.HintColor, TextAnchor.MiddleLeft);
            }

            MakeDivider(UnitGeneTabLayout.DividerHeight);

            // 基因属性加成总览
            MakeText("boost_head", en ? "GENE BOOST SUMMARY (live)" : UILocalization.Get("boost_title"),
                UnitGeneTabLayout.HeadFont, FontStyle.Bold, UnitGeneTabLayout.TitleColor, TextAnchor.MiddleLeft);
            var boosts = GeneBoostSummary.Summarize(actor);
            if (boosts.Count == 0)
            {
                MakeText("boost_none", en ? "No active gene boosts (awaken first)" : UILocalization.Get("boost_none"),
                    UnitGeneTabLayout.BodyFont, FontStyle.Normal, UnitGeneTabLayout.HintColor, TextAnchor.MiddleLeft);
            }
            else
            {
                int show = Mathf.Min(UnitGeneTabLayout.BoostMaxRows, boosts.Count);
                for (int i = 0; i < show; i++)
                {
                    MakeBoostRow("boost_" + i, boosts[i].Label, boosts[i].Value);
                }
                if (boosts.Count > show)
                {
                    MakeText("boost_more", "+" + (boosts.Count - show) + (en ? " more" : UILocalization.Get("boost_more")),
                        UnitGeneTabLayout.HintFont, FontStyle.Normal, UnitGeneTabLayout.HintAltColor, TextAnchor.MiddleLeft);
                }
            }

            MakeDivider(UnitGeneTabLayout.DividerHeight);

            // 原版基因来源
            var contributions = GeneContributionCollector.Collect(actor);
            if (contributions != null && contributions.Count > 0)
            {
                MakeText("src_head", en ? "VANILLA GENE SOURCES" : UILocalization.Get("proto_sources_line"),
                    UnitGeneTabLayout.HeadFont, FontStyle.Bold, UnitGeneTabLayout.SourceHeadColor, TextAnchor.MiddleLeft);
                int showSrc = Mathf.Min(UnitGeneTabLayout.SourceMaxRows, contributions.Count);
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
                        UnitGeneTabLayout.BodyFont, FontStyle.Normal, UnitGeneTabLayout.SourceColor, TextAnchor.MiddleLeft);
                }
                if (contributions.Count > showSrc)
                {
                    MakeText("src_more", "+" + (contributions.Count - showSrc) + (en ? " more" : UILocalization.Get("bridge_more")),
                        UnitGeneTabLayout.HintFont, FontStyle.Normal, UnitGeneTabLayout.HintAltColor, TextAnchor.MiddleLeft);
                }
            }

            MakeDivider(UnitGeneTabLayout.DividerHeight + 2f);

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

        // ---------- 元素工厂（消除创建样板） ----------

        /// <summary>在容器下新建元素：GameObject + RectTransform + LayoutElement + 目标组件</summary>
        private static T AddElement<T>(string childName, float preferredHeight, float minHeight) where T : Component
        {
            GameObject go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(_container, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.minHeight = minHeight;
            return go.AddComponent<T>();
        }

        /// <summary>创建文本（挂指定父级，无 LayoutElement）——供行内文本复用</summary>
        private static Text CreateText(Transform parent, string childName, string text, int size, TextAnchor anchor, Color color)
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

        private static Text MakeText(string childName, string text, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            Text t = AddElement<Text>(childName, size + 8f, size + 6f);
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
            Image img = AddElement<Image>("divider", height, height);
            img.color = UnitGeneTabLayout.DividerColor;
            img.raycastTarget = false; // P0修复：分隔条不得拦截点击射线
        }

        private static void MakeAffinityRow(string childName, string name, float value, float cap)
        {
            RectTransform row = AddElement<RectTransform>(childName, UnitGeneTabLayout.RowHeight, UnitGeneTabLayout.RowHeight);

            HorizontalLayoutGroup hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 6f;
            hlg.padding = new RectOffset(2, 2, 1, 1);

            Text nameText = CreateText(row, "n", name, UnitGeneTabLayout.BodyFont, TextAnchor.MiddleLeft, UnitGeneTabLayout.AffinityNameColor);
            LayoutElement nameLe = nameText.gameObject.AddComponent<LayoutElement>();
            nameLe.preferredWidth = UnitGeneTabLayout.AffinityLabelWidth;
            nameLe.minWidth = UnitGeneTabLayout.AffinityLabelWidth;

            // 进度条（底 + 填充）
            GameObject barGo = new GameObject("bar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(row, false);
            Image barImg = barGo.GetComponent<Image>();
            barImg.color = UnitGeneTabLayout.BarBackColor;
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
                fillImg.color = UnitGeneTabLayout.BarFillColor;
                fillImg.raycastTarget = false;
            }

            Text valueText = CreateText(row, "v", value.ToString("F1"), UnitGeneTabLayout.BodyFont, TextAnchor.MiddleRight, UnitGeneTabLayout.BodyColor);
            LayoutElement valueLe = valueText.gameObject.AddComponent<LayoutElement>();
            valueLe.preferredWidth = UnitGeneTabLayout.AffinityValueWidth;
            valueLe.minWidth = UnitGeneTabLayout.AffinityValueWidth;
        }

        private static void MakeBoostRow(string childName, string label, string value)
        {
            RectTransform row = AddElement<RectTransform>(childName, UnitGeneTabLayout.RowHeight, UnitGeneTabLayout.RowHeight);

            HorizontalLayoutGroup hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 6f;
            hlg.padding = new RectOffset(2, 2, 1, 1);

            Text labelText = CreateText(row, "l", label, UnitGeneTabLayout.BodyFont, TextAnchor.MiddleLeft, UnitGeneTabLayout.BoostLabelColor);
            LayoutElement labelLe = labelText.gameObject.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1f;
            labelLe.minWidth = UnitGeneTabLayout.BoostLabelMinWidth;

            Text valueText = CreateText(row, "v", value, UnitGeneTabLayout.BodyFont, TextAnchor.MiddleRight, UnitGeneTabLayout.BoostValueColor);
            LayoutElement valueLe = valueText.gameObject.AddComponent<LayoutElement>();
            valueLe.preferredWidth = UnitGeneTabLayout.BoostValueWidth;
            valueLe.minWidth = UnitGeneTabLayout.BoostValueWidthMin;
        }

        private static void MakeButton(string childName, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = AddElement<RectTransform>(childName, UnitGeneTabLayout.ButtonHeight, UnitGeneTabLayout.ButtonHeightMin).gameObject;
            Image img = go.AddComponent<Image>();
            img.color = UnitGeneTabLayout.ButtonColor;
            img.raycastTarget = true;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            Text t = CreateText(go.transform, "label", label, UnitGeneTabLayout.BodyFont, TextAnchor.MiddleCenter, UnitGeneTabLayout.ButtonTextColor);
            RectTransform tRt = t.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
        }
    }
}
