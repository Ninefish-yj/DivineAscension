// ============================================================
// 异能树窗口（v0.17.2 新增 UI）——三系统联动的可视化展示层
// 大众认知异能 = 大类（元素系）→ 小类（火/水/风…）→ 异能（火焰操控…）
// 本窗口：
//   1) 上段：当前单位异能详情卡（异能名 + 主/副标签 + 效果 + 亲和）
//   2) 主体：异能树浏览（元素系 10 异能，当前异能高亮）
// 纯展示层，不写任何业务数据（UI 与业务逻辑分离）。
// 仿 GeneGraphWindow 骨架：Rect 窗口 + 拖拽 + 关闭 + InputBlocker 遮挡。
// ============================================================

using System;
using UnityEngine;
using Code.Core;
using Code.UI;
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
        private const float WINDOW_WIDTH = 760f;
        private const float WINDOW_HEIGHT = 640f;
        private const float TITLE_HEIGHT = 36f;
        private const float DETAIL_HEIGHT = 150f;
        private const float ENTRY_HEIGHT = 54f;
        #endregion

        #region 实例变量
        private Rect _windowRect;
        private bool _visible;
        private Actor _selectedActor;
        private Vector2 _scrollPosition;
        private bool _isDragging;
        private Vector2 _dragStartMouse;
        private Vector2 _dragStartWindowPos;
        private Texture2D _bgTex;
        private Texture2D _titleBgTex;
        private Texture2D _detailBgTex;
        private Texture2D _categoryBgTex;
        private Texture2D _entryBgTex;
        private Texture2D _entryActiveBgTex;
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
            Instance._scrollPosition = Vector2.zero;
            Instance.InitTextures();
            Instance.CenterWindow();
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
            if (_bgTex == null) _bgTex = MakeSolidTexture(2, 2, new Color(0.11f, 0.12f, 0.15f, 0.96f));
            if (_titleBgTex == null) _titleBgTex = MakeSolidTexture(2, 2, new Color(0.16f, 0.17f, 0.21f, 0.98f));
            if (_detailBgTex == null) _detailBgTex = MakeSolidTexture(2, 2, new Color(0.13f, 0.14f, 0.17f, 0.95f));
            if (_categoryBgTex == null) _categoryBgTex = MakeSolidTexture(2, 2, new Color(0.20f, 0.21f, 0.26f, 0.95f));
            if (_entryBgTex == null) _entryBgTex = MakeSolidTexture(2, 2, new Color(0.15f, 0.16f, 0.20f, 0.9f));
            if (_entryActiveBgTex == null) _entryActiveBgTex = MakeSolidTexture(2, 2, new Color(0.31f, 0.29f, 0.23f, 0.95f));
        }

        private void CenterWindow()
        {
            _windowRect = new Rect(
                (Screen.width - WINDOW_WIDTH) / 2f,
                (Screen.height - WINDOW_HEIGHT) / 2f,
                WINDOW_WIDTH,
                WINDOW_HEIGHT
            );
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

            // 标题栏拖拽（排除关闭按钮）
            Rect titleRect = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, TITLE_HEIGHT);
            Rect closeRect = new Rect(_windowRect.xMax - 32, _windowRect.y + 4, 28, 28);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 &&
                titleRect.Contains(e.mousePosition) && !closeRect.Contains(e.mousePosition))
            {
                _isDragging = true;
                _dragStartMouse = e.mousePosition;
                _dragStartWindowPos = _windowRect.position;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDragging)
            {
                _windowRect.position = _dragStartWindowPos + (e.mousePosition - _dragStartMouse);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _isDragging)
            {
                _isDragging = false;
                e.Use();
            }

            // 背景 + 标题
            GUI.DrawTexture(_windowRect, _bgTex);
            GUI.DrawTexture(titleRect, _titleBgTex);
            GUI.color = DSUITheme.TitleText;
            GUI.Label(new Rect(titleRect.x + 12, titleRect.y + 8, titleRect.width - 60, 24),
                GetTitleText(), new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold });
            GUI.color = Color.white;
            GUI.color = new Color(0.9f, 0.3f, 0.3f);
            if (GUI.Button(closeRect, "×", new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold }))
            {
                MouseOverlayBlocker.ReleaseBlock();
                _visible = false;
                return;
            }
            GUI.color = Color.white;

            // 上段：当前异能详情卡
            float contentY = _windowRect.y + TITLE_HEIGHT + 6;
            Rect detailArea = new Rect(_windowRect.x + 6, contentY, _windowRect.width - 12, DETAIL_HEIGHT - 6f);
            GUI.DrawTexture(detailArea, _detailBgTex);
            DrawDetailCard(detailArea);

            // 主体：异能树滚动列表
            float listY = contentY + DETAIL_HEIGHT + 2f;
            float listHeight = _windowRect.height - TITLE_HEIGHT - DETAIL_HEIGHT - 14f;
            Rect listArea = new Rect(_windowRect.x + 6, listY, _windowRect.width - 12, listHeight);
            DrawPrototypeTree(listArea);

            // 遮挡处理（鼠标在窗口内时阻挡 UGUI/点击/缩放）
            MouseOverlayBlocker.UpdateBlockArea(_windowRect, _windowRect.Contains(e.mousePosition));
            InputBlocker.ReportWindowRect(_windowRect);
        }

        private string GetTitleText()
        {
            string baseTitle = UILocalization.CurrentLanguage == "en" ? "Prototype Tree" : UILocalization.Get("proto_tree_title");
            return _selectedActor != null ? _selectedActor.getName() + " · " + baseTitle : baseTitle;
        }
        #endregion

        #region 详情卡
        private void DrawDetailCard(Rect area)
        {
            var proto = PrototypeRegistry.GetForActor(_selectedActor);
            float y = area.y + 8f;
            float x = area.x + 12f;
            float w = area.width - 24f;

            if (proto == null)
            {
                GUI.color = DSUITheme.SecondaryText;
                GUI.Label(new Rect(x, y, w, 60),
                    UILocalization.CurrentLanguage == "en"
                        ? "Not awakened yet. Awakening determines prototype."
                        : UILocalization.Get("proto_no_prototype"),
                    new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true });
                GUI.color = Color.white;
                return;
            }

            // 异能名
            GUI.color = DSUITheme.AccentGold;
            GUI.Label(new Rect(x, y, w - 160f, 26), proto.NameZh,
                new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold });
            GUI.color = Color.white;

            // 标签徽章（主/副）
            float badgeX = x + w - 150f;
            DrawTagBadge(new Rect(badgeX, y, 70f, 22f), proto.MainTag);
            DrawTagBadge(new Rect(badgeX + 76f, y, 70f, 22f), proto.SubTag);

            // 描述
            y += 30f;
            GUI.color = DSUITheme.BodyText;
            GUI.Label(new Rect(x, y, w, 44f), proto.DescZh,
                new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true });
            GUI.color = Color.white;

            // 亲和数据（主/副标签亲和 + 染色体偏好）
            y += 48f;
            GUI.color = DSUITheme.SecondaryText;
            float mainAff = GeneAffinityBridge.GetTagAffinity(_selectedActor, proto.MainTag);
            float subAff = GeneAffinityBridge.GetTagAffinity(_selectedActor, proto.SubTag);
            float chromoAff = GeneAffinityBridge.GetChromosomeAffinity(_selectedActor, proto.ChromosomePreference);
            GUI.Label(new Rect(x, y, w, 22f),
                UILocalization.CurrentLanguage == "en"
                    ? string.Format("Tag Affinity: {0} ({1:F1}) / {2} ({3:F1})   Chromosome {4}: {5:F1}",
                        PrototypeTags.GetName(proto.MainTag), mainAff, PrototypeTags.GetName(proto.SubTag), subAff,
                        GetChromosomeName(proto.ChromosomePreference), chromoAff)
                    : string.Format(UILocalization.Get("proto_affinity_line"),
                        PrototypeTags.GetName(proto.MainTag), mainAff, PrototypeTags.GetName(proto.SubTag), subAff,
                        GetChromosomeName(proto.ChromosomePreference), chromoAff),
                new GUIStyle(GUI.skin.label) { fontSize = 12 });
            GUI.color = Color.white;
        }

        private void DrawTagBadge(Rect rect, PrototypeTag tag)
        {
            // 用内置白纹理着色画色块（避免每帧 new Texture2D）
            GUI.color = GetTagColor(tag) * new Color(0.55f, 0.55f, 0.55f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = DSUITheme.TitleText;
            GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, 18f), PrototypeTags.GetName(tag),
                new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
        }

        private static Color GetTagColor(PrototypeTag tag)
        {
            switch (tag)
            {
                case PrototypeTag.Burn: return new Color(0.85f, 0.45f, 0.35f, 1f);      // 火红
                case PrototypeTag.Slow: return new Color(0.45f, 0.70f, 0.85f, 1f);      // 冰蓝
                case PrototypeTag.Stun: return new Color(0.88f, 0.75f, 0.40f, 1f);      // 金黄
                case PrototypeTag.Poison: return new Color(0.45f, 0.75f, 0.45f, 1f);    // 毒绿
                case PrototypeTag.Shield: return new Color(0.45f, 0.60f, 0.85f, 1f);    // 蓝盾
                case PrototypeTag.Dodge: return new Color(0.40f, 0.75f, 0.75f, 1f);     // 青
                case PrototypeTag.Lifesteal: return new Color(0.75f, 0.45f, 0.70f, 1f); // 紫红
                case PrototypeTag.Crit: return new Color(0.90f, 0.80f, 0.50f, 1f);      // 金
                case PrototypeTag.Heal: return new Color(0.55f, 0.80f, 0.55f, 1f);      // 绿
                case PrototypeTag.Chain: return new Color(0.65f, 0.55f, 0.85f, 1f);     // 紫
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

        #region 异能树列表
        private void DrawPrototypeTree(Rect area)
        {
            using (new GUILayout.AreaScope(area))
            {
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, Array.Empty<GUILayoutOption>());

                // 大类头：元素系
                DrawCategoryHeader("元素系 · Elemental");

                // 10 异能条目
                var list = PrototypeRegistry.Elements;
                var current = PrototypeRegistry.GetForActor(_selectedActor);
                foreach (var def in list)
                {
                    if (def == null) continue;
                    bool isActive = current != null && current.Id == def.Id;
                    DrawPrototypeEntry(def, isActive);
                }

                GUILayout.Space(8f);
                GUILayout.EndScrollView();
            }
        }

        private void DrawCategoryHeader(string title)
        {
            Rect rect = GUILayoutUtility.GetRect(area_width(), ENTRY_HEIGHT * 0.45f);
            GUI.DrawTexture(rect, _categoryBgTex);
            GUI.color = DSUITheme.AccentCyan;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 20f), title,
                new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold });
            GUI.color = Color.white;
        }

        private void DrawPrototypeEntry(PrototypeDef def, bool isActive)
        {
            Rect rect = GUILayoutUtility.GetRect(area_width(), ENTRY_HEIGHT);
            GUI.DrawTexture(rect, isActive ? _entryActiveBgTex : _entryBgTex);

            // 异能名（当前高亮金色）
            GUI.color = isActive ? DSUITheme.AccentGold : DSUITheme.BodyText;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 150f, 22f), def.NameZh,
                new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUI.color = Color.white;

            // 主/副标签徽章
            DrawTagBadge(new Rect(rect.x + rect.width - 150f, rect.y + 7f, 66f, 20f), def.MainTag);
            DrawTagBadge(new Rect(rect.x + rect.width - 78f, rect.y + 7f, 66f, 20f), def.SubTag);

            // 描述
            GUI.color = DSUITheme.SecondaryText;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 28f, rect.width - 20f, 22f), def.DescZh,
                new GUIStyle(GUI.skin.label) { fontSize = 11 });
            GUI.color = Color.white;
        }

        private float area_width()
        {
            return _windowRect.width - 12f - 16f; // 减滚动条余量
        }
        #endregion
    }
}
