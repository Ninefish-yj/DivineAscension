// ============================================================
// 全屏异能树窗口 · 树图渲染引擎（v0.18.0 重写）
// 大众认知异能 = 异能体系 → 12 大类 → 小类 → 异能（196+ 异能叶子）
// 引擎借鉴诸天神座道途树（DaoTuDiagram）：
//   · 平移（拖动）+ 滚轮缩放（0.08x~2x，以鼠标为中心）
//   · 滚动网格背景 + 缩放分级渲染（远处只画色块边框 → 拉近显名 → 更近显标签徽章）
//   · 当前单位异能：金色高亮（节点 + 父链连线）
//   · 点击节点 → 右下详情卡（异能：主/副标签/效果/亲和条；大类/小类：概览）
//   · 工具栏：聚焦当前异能 / 重置视图
// 纯展示层，不写任何业务数据。数据与布局由 PrototypeTreeData.cs 提供。
// 接口兼容：Show/Hide/Toggle/OnGUI 由 AbilityCodexPanel 全局驱动。
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
        private const float TOOLBAR_H = 44f;
        private const float ZOOM_MIN = 0.08f;
        private const float ZOOM_MAX = 2f;
        private const float LINE_THICK = 2f;
        #endregion

        #region 实例变量
        private Rect _fullScreenRect;
        private bool _visible;
        private Actor _selectedActor;
        private ProtoTreeLayout _layout;
        private Vector2 _pan;          // 屏幕平移（世界坐标 → 屏幕：w*zoom + pan）
        private float _zoom = 1f;
        private bool _draging;
        private Vector2 _dragStart;
        private Vector2 _panStart;
        private string _focusNodeId;   // 点击选中的节点（详情卡；默认=当前异能）
        private Texture2D _bgTex;
        private Texture2D _titleBgTex;
        private Texture2D _cardBgTex;
        private Texture2D _nodeBgTex;
        private Texture2D _nodeActiveBgTex;
        private GUIStyle _titleStyle;
        private GUIStyle _nodeStyle;
        private GUIStyle _subStyle;
        private GUIStyle _leafStyle;
        private GUIStyle _tagStyle;
        private GUIStyle _descStyle;
        private GUIStyle _infoStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _closeBtnStyle;
        private GUIStyle _detailNameStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle[] _badgeStyles;   // 8~12 号五档缓存（禁止 OnGUI 内 new GUIStyle）
        private bool _wasPaused;         // 打开前的暂停态（关闭时精确恢复）
        private bool _pauseCaptured;
        #endregion

        #region 静态方法（接口兼容 v0.17.x）
        public static void Ensure()
        {
            if (_instance == null) _instance = new PrototypeWindow();
        }

        public static void Show(Actor actor)
        {
            bool wasVisible = Instance._visible;
            Instance._selectedActor = actor;
            Instance._visible = true;
            Instance._layout = ProtoTreeLayoutBuilder.Build();
            Instance._focusNodeId = null;
            if (!wasVisible)
            {
                MouseOverlayBlocker.RequestBlock();
                // 大窗规范：记录原暂停态并强制暂停，关闭时精确恢复
                try
                {
                    Instance._wasPaused = Config.paused;
                    Instance._pauseCaptured = true;
                    Config.paused = true;
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 异能树暂停捕获失败: " + dsEx.Message); }
                Instance.ResetView();
            }
            Instance.InitTextures();
            // 样式惰性初始化必须在 OnGUI 内（new GUIStyle 读 GUI.skin）
        }

        public static void Hide()
        {
            if (Instance._visible)
            {
                MouseOverlayBlocker.ReleaseBlock();
                if (Instance._pauseCaptured)
                {
                    try { Config.paused = Instance._wasPaused; } catch { }
                    Instance._pauseCaptured = false;
                }
            }
            Instance._visible = false;
            Instance._selectedActor = null;   // 关窗不持有 Actor 引用（指南 7.2）
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
            if (_bgTex == null) _bgTex = MakeSolidTexture(2, 2, new Color(0.06f, 0.07f, 0.10f, 0.95f));
            if (_titleBgTex == null) _titleBgTex = MakeSolidTexture(2, 2, new Color(0.12f, 0.13f, 0.17f, 0.98f));
            if (_cardBgTex == null) _cardBgTex = MakeSolidTexture(2, 2, new Color(0.10f, 0.11f, 0.14f, 0.96f));
            if (_nodeBgTex == null) _nodeBgTex = MakeSolidTexture(2, 2, new Color(0.15f, 0.16f, 0.21f, 0.95f));
            if (_nodeActiveBgTex == null) _nodeActiveBgTex = MakeSolidTexture(2, 2, new Color(0.36f, 0.32f, 0.18f, 0.98f));
        }

        private void InitStyles()
        {
            if (_titleStyle == null) _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            if (_nodeStyle == null) _nodeStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (_subStyle == null) _subStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (_leafStyle == null) _leafStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (_tagStyle == null) _tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            if (_descStyle == null) _descStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            if (_infoStyle == null) _infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            if (_buttonStyle == null) _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            if (_closeBtnStyle == null) _closeBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            if (_detailNameStyle == null) _detailNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                _badgeStyles = new GUIStyle[5];
                for (int bi = 0; bi < _badgeStyles.Length; bi++)
                {
                    _badgeStyles[bi] = new GUIStyle(_badgeStyle) { fontSize = 8 + bi };
                }
            }
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

        #region 视图控制
        /// <summary>重置视图：Fit 全树</summary>
        private void ResetView()
        {
            _zoom = 1f;
            _pan = Vector2.zero;
            if (_layout != null) FitToRect(_layout.Bounds);
        }

        /// <summary>把世界矩形缩放到可视区域（自适应缩放上限 1x）</summary>
        private void FitToRect(Rect worldRect)
        {
            float areaW = Screen.width - 24f;
            float areaH = Screen.height - TITLE_HEIGHT - TOOLBAR_H - 80f;
            if (worldRect.width < 1f || worldRect.height < 1f || areaW < 10f || areaH < 10f) return;
            float zx = areaW / worldRect.width;
            float zy = areaH / worldRect.height;
            _zoom = Mathf.Clamp(Mathf.Min(zx, zy), ZOOM_MIN, 1f);
            float cx = worldRect.center.x * _zoom;
            float cy = worldRect.center.y * _zoom;
            _pan.x = Screen.width / 2f - cx;
            _pan.y = (TITLE_HEIGHT + TOOLBAR_H + Screen.height) / 2f - cy;
        }

        /// <summary>聚焦某节点（该节点居中 + 放大到可读）</summary>
        private void FocusOn(string nodeId)
        {
            var node = _layout != null ? _layout.GetNode(nodeId) : null;
            if (node == null) return;
            float targetZoom = Mathf.Max(_zoom, 0.85f);
            _zoom = Mathf.Clamp(targetZoom, ZOOM_MIN, ZOOM_MAX);
            float cx = (node.X + node.W / 2f) * _zoom;
            float cy = (node.Y + node.H / 2f) * _zoom;
            _pan.x = Screen.width / 2f - cx;
            _pan.y = (TITLE_HEIGHT + TOOLBAR_H + Screen.height) / 2f - cy;
        }
        #endregion

        #region OnGUI
        public void OnGUI()
        {
            if (!_visible) return;
            // 整窗异常隔离（指南 3.3/7.3）：绘制异常 → 自动关窗 + 撤遮罩 + 顶部提示，绝不每帧刷屏
            try
            {
                DoOnGUI();
            }
            catch (System.Exception dsEx)
            {
                Code.Core.DSDebug.Error("[GeneCultivation] 异能树绘制异常，自动关闭: " + dsEx.Message);
                SafeCloseWithTip();
            }
        }

        /// <summary>异常安全关窗：撤遮罩 + 恢复暂停 + 顶部提示</summary>
        private void SafeCloseWithTip()
        {
            try
            {
                if (_visible) MouseOverlayBlocker.ReleaseBlock();
                if (_pauseCaptured) { Config.paused = _wasPaused; _pauseCaptured = false; }
                _visible = false;
                _selectedActor = null;
                try { WorldTip.showNow("异能树绘制异常，已自动关闭。", false, "top", 5f); } catch { }
            }
            catch { }
        }

        private void DoOnGUI()
        {
            if (_selectedActor == null || !_selectedActor.isAlive())
            {
                SafeCloseWithTip();
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
                if (_pauseCaptured) { Config.paused = _wasPaused; _pauseCaptured = false; }
                _visible = false;
                _selectedActor = null;
                e.Use();
                return;
            }

            // 全屏背景
            GUI.DrawTexture(_fullScreenRect, _bgTex);

            // 标题条
            Rect titleRect = new Rect(0f, 0f, Screen.width, TITLE_HEIGHT);
            GUI.DrawTexture(titleRect, _titleBgTex);
            GUI.color = DSUITheme.TitleText;
            GUI.Label(new Rect(16f, 12f, Screen.width - 160f, 32f), GetTitleText(), _titleStyle);
            GUI.color = Color.white;

            // 关闭按钮
            Rect closeRect = new Rect(Screen.width - 48f, 12f, 32f, 32f);
            GUI.color = new Color(0.9f, 0.3f, 0.3f);
            if (GUI.Button(closeRect, "×", _closeBtnStyle))
            {
                MouseOverlayBlocker.ReleaseBlock();
                if (_pauseCaptured) { Config.paused = _wasPaused; _pauseCaptured = false; }
                _visible = false;
                _selectedActor = null;
                return;
            }
            GUI.color = Color.white;

            // 工具栏（聚焦当前 / 重置视图）
            DrawToolbar();

            // 树区（网格 + 连线 + 节点 + 交互）
            Rect treeArea = new Rect(0f, TITLE_HEIGHT, Screen.width, Screen.height - TITLE_HEIGHT);
            DrawTreeArea(e, treeArea);

            // 右下详情卡
            DrawDetailCard();

            // 底部状态栏
            GUI.color = DSUITheme.SecondaryText;
            GUI.Label(new Rect(16f, Screen.height - 30f, Screen.width - 32f, 24f),
                GetBottomStatus(), _infoStyle);
            GUI.color = Color.white;

            // 全屏遮挡
            MouseOverlayBlocker.UpdateBlockArea(_fullScreenRect, true);
            InputBlocker.ReportWindowRect(_fullScreenRect);
        }

        private void DrawToolbar()
        {
            float x = 16f;
            float y = TITLE_HEIGHT + 6f;
            float bw = 130f;
            float bh = TOOLBAR_H - 12f;
            Rect fitBtn = new Rect(x, y, bw, bh);
            if (GUI.Button(fitBtn, GetText("proto_focus_btn", "聚焦当前异能"), _buttonStyle))
            {
                var proto = PrototypeRegistry.GetForActor(_selectedActor);
                if (proto != null) FocusOn(proto.Id);
                else ResetView();
            }
            x += bw + 8f;
            Rect resetBtn = new Rect(x, y, bw, bh);
            if (GUI.Button(resetBtn, GetText("proto_reset_btn", "重置视图"), _buttonStyle)) ResetView();
            x += bw + 8f;
            Rect zoomHint = new Rect(x + 6f, y + 4f, Screen.width - x - 100f, bh);
            GUI.color = DSUITheme.SecondaryText;
            GUI.Label(zoomHint, GetText("proto_zoom_hint", "拖动平移 · 滚轮缩放 · 点击节点看详情"), _infoStyle);
            GUI.color = Color.white;
        }

        private string GetTitleText()
        {
            string baseTitle = GetText("proto_tree_title", "异能体系");
            return _selectedActor != null ? _selectedActor.getName() + " · " + baseTitle : baseTitle;
        }

        private string GetBottomStatus()
        {
            var proto = PrototypeRegistry.GetForActor(_selectedActor);
            if (proto == null)
                return GetText("proto_no_prototype", "尚未觉醒异能。觉醒时将根据原版基因定相异能。");
            return _selectedActor.getName() + " · " + proto.NameZh +
                   "（" + PrototypeTags.GetName(proto.MainTag) + " / " + PrototypeTags.GetName(proto.SubTag) + "） · " +
                   GetText("proto_total", "共") + " " + (_layout != null ? _layout.Nodes.Count.ToString() : "0") + " " + GetText("proto_nodes", "节点");
        }

        private static string GetText(string key, string fallbackZh)
        {
            try
            {
                if (UILocalization.CurrentLanguage == "en") return fallbackZh;
                string v = UILocalization.Get(key);
                return string.IsNullOrEmpty(v) ? fallbackZh : v;
            }
            catch { return fallbackZh; }
        }
        #endregion

        #region 树区渲染
        private void DrawTreeArea(Event e, Rect area)
        {
            if (_layout == null) return;

            // ---- 输入：滚轮缩放 / 拖动平移 / 点击选中 ----
            HandleTreeInput(e, area);

            // ---- 网格背景（随 pan 滚动）----
            DrawGrid(area);

            // ---- 连线（父 → 子；当前异能父链金色）----
            DrawEdges(area);

            // ---- 节点（缩放分级渲染）----
            DrawNodes(area);

            // 未觉醒引导（底部）
            if (PrototypeRegistry.GetForActor(_selectedActor) == null)
            {
                GUI.color = DSUITheme.SecondaryText;
                GUI.Label(new Rect(16f, Screen.height - 60f, Screen.width - 32f, 24f),
                    GetText("proto_awaken_hint", "尚未觉醒异能，觉醒时将根据原版基因定相异能。"), _infoStyle);
                GUI.color = Color.white;
            }
        }

        private void HandleTreeInput(Event e, Rect area)
        {
            if (!area.Contains(e.mousePosition)) return;

            // 滚轮缩放（以鼠标位置为中心）
            if (e.type == EventType.ScrollWheel)
            {
                float factor = e.delta.y > 0f ? 1.15f : 1f / 1.15f;
                float before = _zoom;
                _zoom = Mathf.Clamp(_zoom * factor, ZOOM_MIN, ZOOM_MAX);
                if (Mathf.Abs(_zoom - before) > 0.0001f)
                {
                    Vector2 mouse = e.mousePosition;
                    _pan = mouse + (_pan - mouse) * (_zoom / before);
                }
                e.Use();
                return;
            }

            // 拖动平移（左键在空白处 / 中键 / 右键）
            if (e.type == EventType.MouseDown && (e.button == 2 || e.button == 1 || (e.button == 0 && HitTestNode(e.mousePosition) == null)))
            {
                _draging = true;
                _dragStart = e.mousePosition;
                _panStart = _pan;
                e.Use();
                return;
            }
            if (e.type == EventType.MouseDrag && _draging)
            {
                Vector2 delta = e.mousePosition - _dragStart;
                _pan = _panStart + delta;
                e.Use();
                return;
            }
            if (e.type == EventType.MouseUp && _draging)
            {
                _draging = false;
                e.Use();
                return;
            }

            // 左键点击节点 → 选中详情
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var hit = HitTestNode(e.mousePosition);
                if (hit != null)
                {
                    _focusNodeId = hit.Id;
                    e.Use();
                }
            }
        }

        private ProtoTreeNode HitTestNode(Vector2 screenPos)
        {
            if (_layout == null) return null;
            foreach (var node in _layout.Nodes)
            {
                Rect r = ScreenRect(node);
                if (r.Contains(screenPos)) return node;
            }
            return null;
        }

        /// <summary>世界坐标 → 屏幕矩形</summary>
        private Rect ScreenRect(ProtoTreeNode node)
        {
            return new Rect(node.X * _zoom + _pan.x, node.Y * _zoom + _pan.y,
                node.W * _zoom, node.H * _zoom);
        }

        private void DrawGrid(Rect area)
        {
            float pitch = 40f * Mathf.Max(0.6f, _zoom);
            if (pitch < 6f) return;
            GUI.color = new Color(0.10f, 0.14f, 0.20f, 0.35f);
            for (float x = _pan.x % pitch; x < area.width; x += pitch)
                GUI.DrawTexture(new Rect(x, area.y, 1f, area.height), Texture2D.whiteTexture);
            for (float y = _pan.y % pitch; y < area.y + area.height; y += pitch)
                GUI.DrawTexture(new Rect(0f, y, area.width, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawEdges(Rect area)
        {
            var proto = PrototypeRegistry.GetForActor(_selectedActor);
            string activeId = proto != null ? proto.Id : null;

            foreach (var node in _layout.Nodes)
            {
                if (node.Level == 0) continue;
                var parent = _layout.GetNode(node.ParentId);
                if (parent == null) continue;
                Rect pr = ScreenRect(parent);
                Rect nr = ScreenRect(node);
                Vector2 from = new Vector2(pr.center.x, pr.yMax);
                Vector2 to = new Vector2(nr.center.x, nr.yMin);
                // 父链高亮：节点自身或其父链上的节点是当前异能
                bool activeEdge = IsOnActiveChain(node, activeId);
                DrawLine(from, to, activeEdge ? DSUITheme.AccentGold : DSUITheme.BorderDark,
                    activeEdge ? 3f : LINE_THICK);
            }
        }

        private bool IsOnActiveChain(ProtoTreeNode node, string activeId)
        {
            if (string.IsNullOrEmpty(activeId)) return false;
            var cur = node;
            while (cur != null)
            {
                if (cur.Id == activeId) return true;
                cur = _layout.GetNode(cur.ParentId);
            }
            return false;
        }

        private void DrawNodes(Rect area)
        {
            var proto = PrototypeRegistry.GetForActor(_selectedActor);
            string activeId = proto != null ? proto.Id : null;

            foreach (var node in _layout.Nodes)
            {
                Rect r = ScreenRect(node);
                if (r.xMax < -20f || r.yMax < -20f || r.x > area.width + 20f || r.y > area.height + 20f) continue; // 视口裁剪

                bool isActive = node.Id == activeId;
                Color mainColor = isActive ? DSUITheme.AccentGold : node.Color;

                // 底色
                GUI.DrawTexture(r, isActive ? _nodeActiveBgTex : _nodeBgTex);
                // 左侧色条（大类色/金色）
                GUI.color = mainColor;
                GUI.DrawTexture(new Rect(r.x, r.y, Mathf.Max(3f, 4f * _zoom), r.height), Texture2D.whiteTexture);
                // 边框
                DrawBorder(r, isActive ? DSUITheme.AccentGold : node.Color * new Color(0.7f, 0.7f, 0.7f, 1f));

                // 缩放分级：远处只画色块；>=0.3 显名；>=0.65 异能显标签徽章
                if (_zoom >= 0.28f)
                {
                    GUIStyle nameStyle = node.Level == 3 ? _leafStyle : (node.Level == 2 ? _subStyle : _nodeStyle);
                    nameStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt((node.Level == 3 ? 12 : node.Level == 2 ? 13 : 15) * _zoom), 9, 22);
                    GUI.color = isActive ? DSUITheme.AccentGold : DSUITheme.BodyText;
                    Rect nameRect = new Rect(r.x + 6f, r.y + 4f, r.width - 12f, r.height - 8f);
                    GUI.Label(nameRect, node.Name, nameStyle);
                    GUI.color = Color.white;

                    // 异能叶子：标签徽章
                    if (node.Level == 3 && node.Def != null && _zoom >= 0.65f)
                    {
                        float badgeH = Mathf.Max(12f, 17f * _zoom);
                        float badgeW = (r.width - 10f) / 2f;
                        float by = r.yMax - badgeH - 3f;
                        DrawTagBadge(new Rect(r.x + 5f, by, badgeW, badgeH), node.Def.MainTag, _zoom);
                        DrawTagBadge(new Rect(r.x + 5f + badgeW + 4f, by, badgeW, badgeH), node.Def.SubTag, _zoom);
                    }
                }

                // 当前异能：右上角金色角标
                if (isActive)
                {
                    float dot = Mathf.Max(5f, 7f * _zoom);
                    GUI.color = new Color(1f, 0.85f, 0.35f, 1f);
                    GUI.DrawTexture(new Rect(r.xMax - dot - 2f, r.y + 3f, dot, dot), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }
        }
        #endregion

        #region 详情卡
        private void DrawDetailCard()
        {
            float W = Screen.width;
            float H = Screen.height;
            float cardW = Mathf.Min(430f, W * 0.34f);
            float cardH = Mathf.Min(310f, H * 0.36f);
            Rect card = new Rect(W - cardW - 16f, H - cardH - 40f, cardW, cardH);
            GUI.DrawTexture(card, _cardBgTex);
            DrawBorder(card, DSUITheme.BorderLight);

            // 展示对象：点击选中的节点（无选中 → 当前异能）
            ProtoTreeNode shown = null;
            if (!string.IsNullOrEmpty(_focusNodeId)) shown = _layout != null ? _layout.GetNode(_focusNodeId) : null;
            if (shown == null || shown.Def == null)
            {
                var proto = PrototypeRegistry.GetForActor(_selectedActor);
                shown = proto != null && _layout != null ? _layout.GetNode(proto.Id) : null;
            }

            if (shown == null)
            {
                GUI.color = DSUITheme.SecondaryText;
                GUI.Label(new Rect(card.x + 14f, card.y + 14f, card.width - 28f, 100f),
                    GetText("proto_no_prototype", "尚未觉醒异能。觉醒时将根据原版基因定相异能。"), _descStyle);
                GUI.color = Color.white;
                return;
            }

            float x = card.x + 14f;
            float y = card.y + 12f;
            float w = card.width - 28f;

            if (shown.Def != null)
            {
                // ---- 异能详情 ----
                var def = shown.Def;
                GUI.color = DSUITheme.AccentGold;
                GUI.Label(new Rect(x, y, w - 150f, 26f), def.NameZh, _detailNameStyle);
                GUI.color = Color.white;
                DrawTagBadge(new Rect(x + w - 150f, y, 66f, 22f), def.MainTag, 1f);
                DrawTagBadge(new Rect(x + w - 78f, y, 66f, 22f), def.SubTag, 1f);

                y += 30f;
                GUI.color = DSUITheme.BodyText;
                GUI.Label(new Rect(x, y, w, 48f), def.DescZh, _descStyle);
                GUI.color = Color.white;

                y += 56f;
                GUI.color = DSUITheme.SecondaryText;
                GUI.Label(new Rect(x, y, w, 22f),
                    GetText("proto_category_line", "分类：") + def.Category + " → " + def.SubCategory, _infoStyle);
                GUI.color = Color.white;

                // 亲和数值（三桥联动证据：原版基因 → 染色体/标签亲和）
                y += 26f;
                float mainAff = GeneAffinityBridge.GetTagAffinity(_selectedActor, def.MainTag);
                float subAff = GeneAffinityBridge.GetTagAffinity(_selectedActor, def.SubTag);
                float chromoAff = GeneAffinityBridge.GetChromosomeAffinity(_selectedActor, def.ChromosomePreference);
                GUI.color = DSUITheme.SecondaryText;
                GUI.Label(new Rect(x, y, w, 22f),
                    GetText("proto_affinity_line",
                        "标签亲和  {0} {1:F1} / {2} {3:F1}   染色体 {4} {5:F1}")
                    .Replace("{0}", PrototypeTags.GetName(def.MainTag)).Replace("{1}", mainAff.ToString("F1"))
                    .Replace("{2}", PrototypeTags.GetName(def.SubTag)).Replace("{3}", subAff.ToString("F1"))
                    .Replace("{4}", GetChromosomeName(def.ChromosomePreference)).Replace("{5}", chromoAff.ToString("F1")),
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
                    GetChromosomeName(def.ChromosomePreference) + GetText("proto_chromo_line", "染色体亲和 ") + chromoAff.ToString("F1") + "/8",
                    _tagStyle);
                GUI.color = Color.white;

                // 原版基因支撑来源（三系统桥接显性层：原版基因 → 标签 → 本异能）
                y += 36f;
                var feedingKeywords = new System.Collections.Generic.List<string>();
                var mainGenes = GeneVanillaMapping.GetGenesForTag(def.MainTag);
                var subGenes = GeneVanillaMapping.GetGenesForTag(def.SubTag);
                if (mainGenes != null) foreach (var g in mainGenes) if (!feedingKeywords.Contains(g)) feedingKeywords.Add(g);
                if (subGenes != null) foreach (var g in subGenes) if (!feedingKeywords.Contains(g)) feedingKeywords.Add(g);
                if (feedingKeywords.Count > 0)
                {
                    string sourceText = GetText("proto_sources_line", "原版基因支撑: ") + string.Join(" / ", feedingKeywords.ToArray());
                    GUI.color = new Color(0.6f, 0.9f, 0.75f);
                    GUI.Label(new Rect(x, y, w, 18f), sourceText, _tagStyle);
                    GUI.color = Color.white;
                }
            }
            else
            {
                // ---- 大类/小类/根 概览 ----
                GUI.color = DSUITheme.AccentCyan;
                GUI.Label(new Rect(x, y, w, 26f), shown.Name, _detailNameStyle);
                GUI.color = Color.white;
                y += 32f;
                int count = CountDescendants(shown);
                GUI.color = DSUITheme.BodyText;
                GUI.Label(new Rect(x, y, w, 48f),
                    shown.Level == 1
                        ? GetText("proto_cat_desc", "大类：") + shown.Name + "，含 " + count + " 个异能。觉醒时可能定相于此系异能。"
                        : shown.Level == 2
                            ? GetText("proto_sub_desc", "小类：") + shown.Name + "，含 " + count + " 个异能。"
                            : GetText("proto_root_desc", "异能体系：12 大类 × 小类 × 数百异能。每个异能由 30 种效果标签组合驱动。"),
                    _descStyle);
                GUI.color = Color.white;
            }
        }

        private int CountDescendants(ProtoTreeNode node)
        {
            if (_layout == null) return 0;
            int count = 0;
            foreach (var n in _layout.Nodes)
            {
                if (n.Level != 3) continue;
                var cur = n;
                while (cur != null && cur.Level > node.Level)
                {
                    if (cur.ParentId == node.Id) { count++; break; }
                    cur = _layout.GetNode(cur.ParentId);
                }
            }
            return count;
        }
        #endregion

        #region 绘制工具
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

        private void DrawTagBadge(Rect rect, PrototypeTag tag, float zoom)
        {
            if (rect.width < 8f || rect.height < 6f) return;
            GUI.color = GetTagColor(tag) * new Color(0.55f, 0.55f, 0.55f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = DSUITheme.TitleText;
            int fs = Mathf.Clamp(Mathf.RoundToInt(10 * Mathf.Max(0.8f, zoom)), 8, 12);
            GUIStyle st = _badgeStyles != null ? _badgeStyles[fs - 8] : _badgeStyle;
            GUI.Label(rect, PrototypeTags.GetName(tag), st);
            GUI.color = Color.white;
        }

        /// <summary>30 标签配色（按组：灼烧/减速/眩晕/毒蚀/吸血/暴击/治疗/溅射/狂暴/护盾/闪避/反击/回复）</summary>
        private static Color GetTagColor(PrototypeTag tag)
        {
            switch (tag)
            {
                // 灼烧组
                case PrototypeTag.Burn: return new Color(0.85f, 0.45f, 0.35f, 1f);
                case PrototypeTag.Plasma: return new Color(0.95f, 0.55f, 0.30f, 1f);
                case PrototypeTag.Frost: return new Color(0.55f, 0.75f, 0.90f, 1f);
                // 减速组
                case PrototypeTag.Slow: return new Color(0.45f, 0.70f, 0.85f, 1f);
                case PrototypeTag.Frostbite: return new Color(0.55f, 0.85f, 0.90f, 1f);
                case PrototypeTag.TimeSlow: return new Color(0.60f, 0.70f, 0.95f, 1f);
                // 眩晕组
                case PrototypeTag.Stun: return new Color(0.88f, 0.75f, 0.40f, 1f);
                case PrototypeTag.Knockback: return new Color(0.80f, 0.60f, 0.35f, 1f);
                case PrototypeTag.Gravity: return new Color(0.70f, 0.55f, 0.85f, 1f);
                case PrototypeTag.Fear: return new Color(0.80f, 0.50f, 0.65f, 1f);
                // 毒蚀组
                case PrototypeTag.Poison: return new Color(0.45f, 0.75f, 0.45f, 1f);
                case PrototypeTag.Curse: return new Color(0.55f, 0.45f, 0.65f, 1f);
                // 吸血组
                case PrototypeTag.Lifesteal: return new Color(0.75f, 0.45f, 0.70f, 1f);
                case PrototypeTag.Drain: return new Color(0.65f, 0.50f, 0.75f, 1f);
                case PrototypeTag.Blood: return new Color(0.85f, 0.35f, 0.45f, 1f);
                // 暴击组
                case PrototypeTag.Crit: return new Color(0.90f, 0.80f, 0.50f, 1f);
                case PrototypeTag.Telekinesis: return new Color(0.65f, 0.60f, 0.90f, 1f);
                // 治疗组
                case PrototypeTag.Heal: return new Color(0.55f, 0.80f, 0.55f, 1f);
                case PrototypeTag.Soul: return new Color(0.50f, 0.70f, 0.90f, 1f);
                // 溅射/狂暴
                case PrototypeTag.Chain: return new Color(0.65f, 0.55f, 0.85f, 1f);
                case PrototypeTag.Berserk: return new Color(0.90f, 0.45f, 0.35f, 1f);
                // 护盾
                case PrototypeTag.Shield: return new Color(0.45f, 0.60f, 0.85f, 1f);
                // 闪避组
                case PrototypeTag.Dodge: return new Color(0.40f, 0.75f, 0.75f, 1f);
                case PrototypeTag.Illusion: return new Color(0.60f, 0.70f, 0.85f, 1f);
                case PrototypeTag.Invisibility: return new Color(0.55f, 0.65f, 0.80f, 1f);
                // 防守反击组
                case PrototypeTag.Magnetism: return new Color(0.55f, 0.70f, 0.90f, 1f);
                case PrototypeTag.Vibration: return new Color(0.75f, 0.65f, 0.55f, 1f);
                case PrototypeTag.Sound: return new Color(0.70f, 0.70f, 0.55f, 1f);
                // 防守回复组
                case PrototypeTag.Blessing: return new Color(0.85f, 0.75f, 0.55f, 1f);
                case PrototypeTag.Regeneration: return new Color(0.60f, 0.85f, 0.60f, 1f);
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
