// ============================================================
// 全屏异能树窗口（v0.17.3）——三系统联动的异能可视化展示层
// 大众认知异能 = 大类（元素系）→ 小类（火/水/风…）→ 异能（火焰操控…）
// 全屏树形布局：根「异能体系」→ 大类「元素系」→ 10 小类 → 10 异能叶子
//   · 层级连线（根→大类→小类→异能）
//   · 当前单位异能：金色高亮（节点+连线）
//   · 右下详情卡：异能名/主副标签/效果/亲和数值（三桥联动证据）
// 纯展示层，不写任何业务数据（UI 与业务逻辑分离）。
// 接口兼容 v0.17.2：Show/Hide/Toggle/OnGUI 由 AbilityCodexPanel 全局驱动。
// ============================================================

using System;
using UnityEngine;
using Code.Core;
using Code.Realm.Prototype;

namespace Code.UI
{
    public class PrototypeWindow
    {
        #region 静态实例
        private static PrototypeWindow _instance;
        public static PrototypeWindow Instance => _instance ?? (_instance = new PrototypeWindow());
        #endregion

        #region 常量
        private const float TITLE_HEIGHT = 56f;
        private const float NODE_W = 120f;
        private const float NODE_H = 42f;
        private const float CAT_W = 150f;
        private const float CAT_H = 40f;
        private const float SUB_W = 92f;
        private const float SUB_H = 38f;
        private const float LEAF_W = 150f;
        private const float LEAF_H = 56f;
        private const float LINE_THICK = 2f;
        #endregion

        #region 实例变量
        private Rect _fullScreenRect;
        private bool _visible;
        private Actor _selectedActor;
        private Texture2D _bgTex;
        private Texture2D _titleBgTex;
        private Texture2D _cardBgTex;
        private Texture2D _nodeBgTex;
        private Texture2D _nodeActiveBgTex;
        private Texture2D _rootBgTex;
        private GUIStyle _titleStyle;
        private GUIStyle _nodeStyle;
        private GUIStyle _leafStyle;
        private GUIStyle _tagStyle;
        private GUIStyle _descStyle;
        private GUIStyle _infoStyle;
        #endregion

        #region 静态方法
        public static void Ensure()
        {
            if (_instance == null) _instance = new PrototypeWindow();
        }

        public static void Show(Actor actor)
        {
            bool wasVisible = Instance._visible;
            Instance._selectedActor = actor;
            Instance._visible = true;
            if (!wasVisible) MouseOverlayBlocker.RequestBlock();
            Instance.InitTextures();
            // 注意：不在此处调用 InitStyles()——new GUIStyle 读取 GUI.skin，
            // 只能在 OnGUI 上下文内执行（按钮回调等非 OnGUI 场景会抛
            // "You can only call GUI functions from inside OnGUI"）。
            // 样式在 OnGUI 每帧惰性初始化（InitStyles 内部判空）。
        }

        public static void Hide()
        {
            if (Instance._visible) MouseOverlayBlocker.ReleaseBlock();
            Instance._visible = false;
        }

        public static void Toggle(Actor actor)
        {
            if (Instance._visible) Hide();
            else Show(actor);
        }

        public static bool IsVisible()
        {
            return Instance._visible;
        }
        #endregion

        #region 初始化
        private void InitTextures()
        {
            if (_bgTex == null) _bgTex = MakeSolidTexture(2, 2, new Color(0.07f, 0.08f, 0.11f, 0.94f));
            if (_titleBgTex == null) _titleBgTex = MakeSolidTexture(2, 2, new Color(0.13f, 0.14f, 0.18f, 0.98f));
            if (_cardBgTex == null) _cardBgTex = MakeSolidTexture(2, 2, new Color(0.11f, 0.12f, 0.15f, 0.95f));
            if (_nodeBgTex == null) _nodeBgTex = MakeSolidTexture(2, 2, new Color(0.16f, 0.17f, 0.22f, 0.95f));
            if (_nodeActiveBgTex == null) _nodeActiveBgTex = MakeSolidTexture(2, 2, new Color(0.36f, 0.32f, 0.20f, 0.98f));
            if (_rootBgTex == null) _rootBgTex = MakeSolidTexture(2, 2, new Color(0.22f, 0.23f, 0.30f, 0.98f));
        }

        private void InitStyles()
        {
            if (_titleStyle == null) _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            if (_nodeStyle == null) _nodeStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (_leafStyle == null) _leafStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (_tagStyle == null) _tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            if (_descStyle == null) _descStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            if (_infoStyle == null) _infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        }

        private static Texture2D MakeSolidTexture(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
        #endregion

        #region OnGUI
        public void OnGUI()
        {
            if (!_visible) return;
            if (_selectedActor == null || !_selectedActor.isAlive())
            {
                if (_visible) MouseOverlayBlocker.ReleaseBlock();
                _visible = false;
                return;
            }

            InitTextures();
            InitStyles();
            _fullScreenRect = new Rect(0f, 0f, Screen.width, Screen.height);

            Event e = Event.current;
            // ESC 关闭
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                MouseOverlayBlocker.ReleaseBlock();
                _visible = false;
                e.Use();
                return;
            }

            // 全屏背景 + 标题条
            GUI.DrawTexture(_fullScreenRect, _bgTex);
            Rect titleRect = new Rect(0f, 0f, Screen.width, TITLE_HEIGHT);
            GUI.DrawTexture(titleRect, _titleBgTex);
            GUI.color = DSUITheme.TitleText;
            GUI.Label(new Rect(16f, 12f, Screen.width - 120f, 32f), GetTitleText(), _titleStyle);
            GUI.color = Color.white;

            // 关闭按钮（右上角）
            Rect closeRect = new Rect(Screen.width - 48f, 12f, 32f, 32f);
            GUI.color = new Color(0.9f, 0.3f, 0.3f);
            if (GUI.Button(closeRect, "×", new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold }))
            {
                MouseOverlayBlocker.ReleaseBlock();
                _visible = false;
                return;
            }
            GUI.color = Color.white;

            // 树区
            DrawTree(e);

            // 右下详情卡
            DrawDetailCard();

            // 底部状态栏
            GUI.color = DSUITheme.SecondaryText;
            GUI.Label(new Rect(16f, Screen.height - 36f, Screen.width - 32f, 24f),
                GetBottomStatus(), _infoStyle);
            GUI.color = Color.white;

            // 全屏遮挡（阻挡 UGUI/点击/缩放）
            MouseOverlayBlocker.UpdateBlockArea(_fullScreenRect, true);
            InputBlocker.ReportWindowRect(_fullScreenRect);
        }

        private string GetTitleText()
        {
            string baseTitle = UILocalization.CurrentLanguage == "en" ? "Prototype System" : UILocalization.Get("proto_tree_title");
            return _selectedActor != null ? _selectedActor.getName() + " · " + baseTitle : baseTitle;
        }

        private string GetBottomStatus()
        {
            var proto = PrototypeRegistry.GetForActor(_selectedActor);
            if (proto == null)
            {
                return UILocalization.CurrentLanguage == "en"
                    ? "Not awakened yet. Awakening determines a prototype."
                    : UILocalization.Get("proto_no_prototype");
            }
            return _selectedActor.getName() + " · " + proto.NameZh +
                   "（" + PrototypeTags.GetName(proto.MainTag) + " / " + PrototypeTags.GetName(proto.SubTag) + "）";
        }
        #endregion

        #region 树形绘制
        private void DrawTree(Event e)
        {
            float W = Screen.width;
            float H = Screen.height;
            var proto = PrototypeRegistry.GetForActor(_selectedActor);

            // 层 Y 坐标（相对屏幕高度，自适应任意分辨率）
            float rootY = H * 0.10f;
            float catY = H * 0.26f;
            float subY = H * 0.42f;
            float leafY = H * 0.58f;

            // ---- 1. 根节点「异能体系」（顶部居中）----
            Vector2 rootCenter = new Vector2(W / 2f, rootY);
            Rect rootRect = CenterRect(rootCenter, NODE_W, NODE_H);
            GUI.DrawTexture(rootRect, _rootBgTex);
            DrawBorder(rootRect, DSUITheme.AccentCyan);
            GUI.color = DSUITheme.AccentCyan;
            GUI.Label(rootRect, UILocalization.CurrentLanguage == "en" ? "Prototype System" : "异能体系", _nodeStyle);
            GUI.color = Color.white;

            // ---- 2. 大类节点「元素系」----
            Vector2 catCenter = new Vector2(W / 2f, catY);
            Rect catRect = CenterRect(catCenter, CAT_W, CAT_H);
            GUI.DrawTexture(catRect, _nodeBgTex);
            DrawBorder(catRect, DSUITheme.AccentBlue);
            GUI.color = DSUITheme.AccentBlue;
            GUI.Label(catRect, UILocalization.CurrentLanguage == "en" ? "Elemental" : "元素系", _nodeStyle);
            GUI.color = Color.white;

            // 根 → 大类 连线
            DrawLine(new Vector2(rootCenter.x, rootRect.yMax), new Vector2(catCenter.x, catRect.yMin),
                DSUITheme.AccentCyan, LINE_THICK);

            // ---- 3. 小类节点（10 元素，横排均分）+ 4. 异能叶子 ----
            var list = PrototypeRegistry.Elements;
            int count = list.Count;
            float slotW = (W - 80f) / count;

            for (int i = 0; i < count; i++)
            {
                var def = list[i];
                if (def == null) continue;
                float slotCenterX = 40f + slotW * i + slotW / 2f;
                bool isActive = proto != null && proto.Id == def.Id;

                // 小类节点（宽度自适应槽宽）
                Vector2 subCenter = new Vector2(slotCenterX, subY);
                float subW = Mathf.Min(SUB_W, slotW - 10f);
                Rect subRect = CenterRect(subCenter, subW, SUB_H);
                GUI.DrawTexture(subRect, isActive ? _nodeActiveBgTex : _nodeBgTex);
                DrawBorder(subRect, isActive ? DSUITheme.AccentGold : DSUITheme.BorderLight);
                GUI.color = isActive ? DSUITheme.AccentGold : DSUITheme.BodyText;
                GUI.Label(subRect, GetSubName(def), _nodeStyle);
                GUI.color = Color.white;

                // 异能叶子（宽度自适应槽宽，防窄屏溢出）
                Vector2 leafCenter = new Vector2(slotCenterX, leafY);
                float leafW = Mathf.Min(LEAF_W, slotW - 12f);
                Rect leafRect = CenterRect(leafCenter, leafW, LEAF_H);
                GUI.DrawTexture(leafRect, isActive ? _nodeActiveBgTex : _nodeBgTex);
                DrawBorder(leafRect, isActive ? DSUITheme.AccentGold : DSUITheme.BorderLight);
                GUI.color = isActive ? DSUITheme.AccentGold : DSUITheme.BodyText;
                GUI.Label(new Rect(leafRect.x, leafRect.y + 5f, leafRect.width, 22f), def.NameZh, _leafStyle);
                GUI.color = Color.white;
                // 叶子第二行：主/副标签徽章（随叶子宽度自适应）
                float badgeW = (leafRect.width - 16f) / 2f;
                DrawTagBadge(new Rect(leafRect.x + 6f, leafRect.y + 30f, badgeW, 18f), def.MainTag);
                DrawTagBadge(new Rect(leafRect.x + 6f + badgeW + 4f, leafRect.y + 30f, badgeW, 18f), def.SubTag);

                // 连线：大类 → 小类（斜线），小类 → 异能（竖线）
                DrawLine(new Vector2(catRect.xMax, catCenter.y), new Vector2(subRect.x + subRect.width / 2f, subRect.yMin),
                    isActive ? DSUITheme.AccentGold : DSUITheme.BorderDark, isActive ? 3f : LINE_THICK);
                DrawLine(new Vector2(subCenter.x, subRect.yMax), new Vector2(subCenter.x, leafRect.yMin),
                    isActive ? DSUITheme.AccentGold : DSUITheme.BorderDark, isActive ? 3f : LINE_THICK);
            }
        }

        /// <summary>小类显示名：取异能名去掉"操控/之力/之光/暗影/疾风"等尾缀后的元素名</summary>
        private static string GetSubName(PrototypeDef def)
        {
            string name = def.NameZh;
            string[] suffixes = { "操控", "之力", "掌控", "圣光", "暗影", "疾风" };
            foreach (string s in suffixes)
            {
                if (name.EndsWith(s) && name.Length > s.Length)
                    return name.Substring(0, name.Length - s.Length);
            }
            return name;
        }

        private void DrawDetailCard()
        {
            var proto = PrototypeRegistry.GetForActor(_selectedActor);
            float W = Screen.width;
            float H = Screen.height;
            float cardW = Mathf.Min(430f, W * 0.35f);
            float cardH = Mathf.Min(300f, H * 0.34f);
            Rect card = new Rect(W - cardW - 20f, H - cardH - 44f, cardW, cardH);
            GUI.DrawTexture(card, _cardBgTex);
            DrawBorder(card, DSUITheme.BorderLight);

            if (proto == null)
            {
                GUI.color = DSUITheme.SecondaryText;
                GUI.Label(new Rect(card.x + 14f, card.y + 14f, card.width - 28f, 80f),
                    UILocalization.CurrentLanguage == "en"
                        ? "Not awakened yet. Awakening determines prototype."
                        : UILocalization.Get("proto_no_prototype"),
                    _descStyle);
                GUI.color = Color.white;
                return;
            }

            float x = card.x + 14f;
            float y = card.y + 12f;
            float w = card.width - 28f;

            // 异能名
            GUI.color = DSUITheme.AccentGold;
            GUI.Label(new Rect(x, y, w - 150f, 26f), proto.NameZh,
                new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            GUI.color = Color.white;
            DrawTagBadge(new Rect(x + w - 150f, y, 66f, 22f), proto.MainTag);
            DrawTagBadge(new Rect(x + w - 78f, y, 66f, 22f), proto.SubTag);

            // 描述
            y += 30f;
            GUI.color = DSUITheme.BodyText;
            GUI.Label(new Rect(x, y, w, 64f), proto.DescZh, _descStyle);
            GUI.color = Color.white;

            // 亲和数值（三桥联动证据：原版基因 → 染色体/标签亲和）
            y += 72f;
            GUI.color = DSUITheme.SecondaryText;
            float mainAff = GeneAffinityBridge.GetTagAffinity(_selectedActor, proto.MainTag);
            float subAff = GeneAffinityBridge.GetTagAffinity(_selectedActor, proto.SubTag);
            float chromoAff = GeneAffinityBridge.GetChromosomeAffinity(_selectedActor, proto.ChromosomePreference);
            GUI.Label(new Rect(x, y, w, 22f),
                UILocalization.CurrentLanguage == "en"
                    ? string.Format("Tag Affinity  {0} {1:F1} / {2} {3:F1}   Chromosome {4} {5:F1}",
                        PrototypeTags.GetName(proto.MainTag), mainAff,
                        PrototypeTags.GetName(proto.SubTag), subAff,
                        GetChromosomeName(proto.ChromosomePreference), chromoAff)
                    : string.Format(UILocalization.Get("proto_affinity_line"),
                        PrototypeTags.GetName(proto.MainTag), mainAff,
                        PrototypeTags.GetName(proto.SubTag), subAff,
                        GetChromosomeName(proto.ChromosomePreference), chromoAff),
                _infoStyle);
            GUI.color = Color.white;

            // 染色体亲和条（0-8 标尺）
            y += 30f;
            float barW = w;
            float barH = 10f;
            GUI.color = DSUITheme.BorderDark;
            GUI.DrawTexture(new Rect(x, y, barW, barH), Texture2D.whiteTexture);
            GUI.color = DSUITheme.AccentCyan;
            float fill = Mathf.Clamp01(chromoAff / 8f) * barW;
            GUI.DrawTexture(new Rect(x, y, fill, barH), Texture2D.whiteTexture);
            GUI.color = DSUITheme.SecondaryText;
            GUI.Label(new Rect(x, y + 14f, w, 18f),
                UILocalization.CurrentLanguage == "en"
                    ? "Affinity bar: chromosome " + GetChromosomeName(proto.ChromosomePreference) + " (" + chromoAff.ToString("F1") + "/8)"
                    : GetChromosomeName(proto.ChromosomePreference) + "染色体亲和 " + chromoAff.ToString("F1") + "/8",
                _tagStyle);
            GUI.color = Color.white;
        }
        #endregion

        #region 绘制工具
        /// <summary>以 center 为中心返回 w×h 矩形</summary>
        private static Rect CenterRect(Vector2 center, float w, float h)
        {
            return new Rect(center.x - w / 2f, center.y - h / 2f, w, h);
        }

        /// <summary>画矩形描边（1px）</summary>
        private static void DrawBorder(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>画任意角度线段（旋转 GUI.matrix，画完恢复）</summary>
        private static void DrawLine(Vector2 a, Vector2 b, Color color, float thickness)
        {
            Vector2 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.01f) return;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUI.color = color;
            Matrix4x4 prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness / 2f, len, thickness), Texture2D.whiteTexture);
            GUI.matrix = prev;
            GUI.color = Color.white;
        }

        private void DrawTagBadge(Rect rect, PrototypeTag tag)
        {
            GUI.color = GetTagColor(tag) * new Color(0.55f, 0.55f, 0.55f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = DSUITheme.TitleText;
            GUI.Label(rect, PrototypeTags.GetName(tag), _tagStyle);
            GUI.color = Color.white;
        }

        private static Color GetTagColor(PrototypeTag tag)
        {
            switch (tag)
            {
                case PrototypeTag.Burn: return new Color(0.85f, 0.45f, 0.35f, 1f);
                case PrototypeTag.Slow: return new Color(0.45f, 0.70f, 0.85f, 1f);
                case PrototypeTag.Stun: return new Color(0.88f, 0.75f, 0.40f, 1f);
                case PrototypeTag.Poison: return new Color(0.45f, 0.75f, 0.45f, 1f);
                case PrototypeTag.Shield: return new Color(0.45f, 0.60f, 0.85f, 1f);
                case PrototypeTag.Dodge: return new Color(0.40f, 0.75f, 0.75f, 1f);
                case PrototypeTag.Lifesteal: return new Color(0.75f, 0.45f, 0.70f, 1f);
                case PrototypeTag.Crit: return new Color(0.90f, 0.80f, 0.50f, 1f);
                case PrototypeTag.Heal: return new Color(0.55f, 0.80f, 0.55f, 1f);
                case PrototypeTag.Chain: return new Color(0.65f, 0.55f, 0.85f, 1f);
                default: return new Color(0.6f, 0.6f, 0.6f, 1f);
            }
        }

        private static string GetChromosomeName(int index)
        {
            switch (index)
            {
                case 0: return "力量";
                case 1: return "敏捷";
                case 2: return "体质";
                case 3: return "智力";
                case 4: return "感知";
                case 5: return "意志";
                case 6: return "异能";
                case 7: return "潜能";
                default: return index.ToString();
            }
        }
        #endregion
    }
}
