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
using System.Collections.Generic;
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
        private bool _pathMode = true; // 仅路径模式：只显示当前异能所在链路（异能叶→小类→系→根）
        // 仅路径模式：链上节点紧凑重排（根在下→叶在上，居中堆叠，连线自然变短）
        private string _chainCacheKey;                               // 缓存对应的 activeId
        private readonly Dictionary<string, Vector2> _chainPos = new Dictionary<string, Vector2>();
        private float _chainMaxW, _chainMaxH;
        private float _prevPinchDist;  // 双指捏合上次距离（0=未在捏合）
        private Texture2D _bgTex;
        private Texture2D _titleBgTex;
        private Texture2D _cardBgTex;
        private Texture2D _nodeBgTex;
        private Texture2D _nodeActiveBgTex;
        private Texture2D _nodeRoundedTex;    // 圆角节点底（白色，DrawNodes 染色）
        private Texture2D _badgeRoundedTex;   // 圆角标签徽章底（白色，染色）
        private Texture2D _circleTex;         // 圆形系徽章（白色，染色）
        private GUIStyle _titleStyle;
        private GUIStyle _rootStyle;
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
                // 默认仅路径模式：直接聚焦当前异能的链路；未觉醒则 Fit 全树
                var proto = PrototypeRegistry.GetForActor(Instance._selectedActor);
                Instance._pathMode = true;
                if (proto != null) Instance.FocusChain(proto.Id);
                else Instance.ResetView();
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
            if (_nodeRoundedTex == null) _nodeRoundedTex = MakeRoundedRectTexture(96, 40, 0.24f, Color.white);
            if (_badgeRoundedTex == null) _badgeRoundedTex = MakeRoundedRectTexture(48, 24, 0.30f, Color.white);
            if (_circleTex == null) _circleTex = MakeCircleTexture(64, Color.white);
            // 兼容保留（绘制处以圆角纹理为主）
            if (_nodeBgTex == null) _nodeBgTex = MakeSolidTexture(2, 2, new Color(0.15f, 0.16f, 0.21f, 0.95f));
            if (_nodeActiveBgTex == null) _nodeActiveBgTex = MakeSolidTexture(2, 2, new Color(0.36f, 0.32f, 0.18f, 0.98f));
        }

        private void InitStyles()
        {
            if (_titleStyle == null) _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            if (_rootStyle == null) _rootStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
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
            _zoom = Mathf.Clamp(Mathf.Min(zx, zy), 0.02f, 1f);   // Fit 场景放宽下限（274 节点大树可完整显示）
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
            // 仅路径模式：当前异能节点按紧凑链坐标居中（避免聚焦到原布局的空白远处）
            string activeId = GetActiveProtoId();
            Vector2 pos = (_pathMode && !string.IsNullOrEmpty(activeId) && IsOnActiveChain(node, activeId))
                ? GetChainPos(node, activeId) : new Vector2(node.X, node.Y);
            float cx = (pos.x + node.W / 2f) * _zoom;
            float cy = (pos.y + node.H / 2f) * _zoom;
            _pan.x = Screen.width / 2f - cx;
            _pan.y = (TITLE_HEIGHT + TOOLBAR_H + Screen.height) / 2f - cy;
        }

        /// <summary>链上节点的紧凑坐标（惰性构建缓存）</summary>
        private Vector2 GetChainPos(ProtoTreeNode node, string activeId)
        {
            EnsureChainLayout(activeId);
            if (_chainPos.TryGetValue(node.Id, out Vector2 p)) return p;
            return new Vector2(node.X, node.Y);
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
            // 仅路径 / 显示全部 切换
            var activeProto = PrototypeRegistry.GetForActor(_selectedActor);
            Rect pathBtn = new Rect(x, y, bw, bh);
            if (GUI.Button(pathBtn, _pathMode ? GetText("proto_show_all_btn", "显示全部") : GetText("proto_path_btn", "仅显路径"), _buttonStyle))
            {
                _pathMode = !_pathMode;
                if (_pathMode)
                {
                    if (activeProto != null) FocusChain(activeProto.Id);
                    else ResetView();
                }
                else ResetView();
            }
            x += bw + 8f;
            Rect zoomHint = new Rect(x + 6f, y + 4f, Screen.width - x - 100f, bh);
            GUI.color = DSUITheme.SecondaryText;
            GUI.Label(zoomHint, GetText("proto_zoom_hint", "拖动平移 · 滚轮/双指缩放 · 点击节点看详情"), _infoStyle);
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
            string chainInfo = "";
            if (_pathMode && _layout != null)
            {
                int onChain = 0;
                foreach (var n in _layout.Nodes)
                    if (IsOnActiveChain(n, proto.Id)) onChain++;
                chainInfo = " · " + GetText("proto_path_only", "仅路径") + " " + onChain + " " + GetText("proto_nodes", "节点");
            }
            // 来源（God 视角：天生觉醒 vs 后天进阶）
            string originInfo = PrototypeEvolution.IsEvolved(_selectedActor)
                ? " · " + GetText("proto_origin_evolved", "进阶×{0}").Replace("{0}", PrototypeEvolution.GetEvolveCount(_selectedActor).ToString())
                : " · " + GetText("proto_origin_awakened", "觉醒");
            return _selectedActor.getName() + " · " + proto.NameZh +
                   "（" + PrototypeTags.GetName(proto.MainTag) + " / " + PrototypeTags.GetName(proto.SubTag) + "）" + originInfo + " · " +
                   GetText("proto_total", "共") + " " + (_layout != null ? _layout.Nodes.Count.ToString() : "0") + " " + GetText("proto_nodes", "节点") + chainInfo;
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
            // ---- 每帧轮询输入（统一在 Repaint 帧执行一次，避免 Layout/事件帧重复应用）----
            if (e.type == EventType.Repaint)
            {
                HandlePolledInput(area);
            }
            if (Input.touchCount > 0) return;   // 触摸期间 IMGUI 鼠标事件不处理

            if (!area.Contains(e.mousePosition)) return;

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

        /// <summary>轮询输入：PC 滚轮缩放 + 手机双指捏合缩放 + 单指拖动平移。
        /// 说明：IMGUI 的 ScrollWheel 事件只在存在滚动容器时才派发，全屏自绘树不可靠，
        /// 故改用 Input.mouseScrollDelta 轮询；手机双指缩放此前完全缺失，一并补上。</summary>
        private void HandlePolledInput(Rect area)
        {
            // ---- PC 滚轮缩放（以鼠标为中心）----
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y); // → GUI 坐标
                if (area.Contains(mouse))
                {
                    ApplyZoomAt(mouse, wheel > 0f ? 1.15f : 1f / 1.15f);
                }
            }

            // ---- 手机触摸 ----
            if (Input.touchCount >= 2)
            {
                // 双指捏合缩放（以两指中点为中心）
                Touch t0 = Input.GetTouch(0), t1 = Input.GetTouch(1);
                Vector2 mid = (t0.position + t1.position) * 0.5f;
                mid.y = Screen.height - mid.y; // → GUI 坐标
                if (area.Contains(mid))
                {
                    float d = Vector2.Distance(t0.position, t1.position);
                    if (_prevPinchDist > 0f && Mathf.Abs(d - _prevPinchDist) > 1f)
                    {
                        ApplyZoomAt(mid, d / _prevPinchDist);
                    }
                    _prevPinchDist = d;
                }
                else _prevPinchDist = 0f;
            }
            else if (Input.touchCount == 1)
            {
                // 单指拖动平移；Began 时若命中节点则选中详情
                _prevPinchDist = 0f;
                Touch t0 = Input.GetTouch(0);
                Vector2 p = t0.position;
                p.y = Screen.height - p.y; // → GUI 坐标
                if (!area.Contains(p)) return;
                if (t0.phase == TouchPhase.Began)
                {
                    var hit = HitTestNode(p);
                    if (hit != null) { _focusNodeId = hit.Id; _draging = false; }
                    else { _draging = true; _dragStart = p; _panStart = _pan; }
                }
                else if (t0.phase == TouchPhase.Moved && _draging)
                {
                    _pan = _panStart + (p - _dragStart);
                }
                else if (t0.phase == TouchPhase.Ended || t0.phase == TouchPhase.Canceled)
                {
                    _draging = false;
                }
            }
            else _prevPinchDist = 0f;
        }

        /// <summary>以 anchor 为锚点缩放（anchor 为 GUI 屏幕坐标）</summary>
        private void ApplyZoomAt(Vector2 anchor, float factor)
        {
            float before = _zoom;
            _zoom = Mathf.Clamp(_zoom * factor, ZOOM_MIN, ZOOM_MAX);
            if (Mathf.Abs(_zoom - before) > 0.0001f)
            {
                _pan = anchor + (_pan - anchor) * (_zoom / before);
            }
        }

        private ProtoTreeNode HitTestNode(Vector2 screenPos)
        {
            if (_layout == null) return null;
            string activeId = GetActiveProtoId();
            foreach (var node in _layout.Nodes)
            {
                Rect r = NodeScreenRect(node, activeId);
                if (r.Contains(screenPos)) return node;
            }
            return null;
        }

        /// <summary>当前选中单位已觉醒的异能 Id（无则 null）</summary>
        private string GetActiveProtoId()
        {
            var proto = PrototypeRegistry.GetForActor(_selectedActor);
            return proto != null ? proto.Id : null;
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
                if (string.IsNullOrEmpty(node.ParentId)) continue;   // 根节点无父，不画入线
                if (_pathMode && !string.IsNullOrEmpty(activeId) && !IsOnActiveChain(node, activeId)) continue;   // 仅路径模式：只画链上入边
                var parent = _layout.GetNode(node.ParentId);
                if (parent == null) continue;
                Rect pr = NodeScreenRect(parent, activeId);
                Rect nr = NodeScreenRect(node, activeId);
                Vector2 from = new Vector2(pr.center.x, pr.yMax);
                Vector2 to = new Vector2(nr.center.x, nr.yMin);
                // 贝塞尔曲线（父底 → 子顶平滑下弧），当前异能所在路径高亮发光（双层：晕+实线）
                bool activeEdge = IsOnActiveChain(node, activeId);
                Color c = activeEdge ? DSUITheme.AccentGold : DSUITheme.BorderDark;
                float t = activeEdge ? 3f : LINE_THICK;
                if (activeEdge)
                {
                    DrawCurvedEdge(from, to, new Color(c.r, c.g, c.b, 0.26f), t + 4f);   // 外层发光晕
                    DrawCurvedEdge(from, to, c, t);                                       // 内层实线
                }
                else
                {
                    DrawCurvedEdge(from, to, c, t);
                }
            }
        }

        /// <summary>三次贝塞尔曲线（垂直切线控制点：父底中点 → 子顶中点平滑下弧）</summary>
        private static void DrawCurvedEdge(Vector2 from, Vector2 to, Color color, float thickness)
        {
            float dy = Mathf.Max(Mathf.Abs(to.y - from.y), 8f);
            Vector2 c1 = new Vector2(from.x, from.y + dy * 0.45f);
            Vector2 c2 = new Vector2(to.x, to.y - dy * 0.45f);
            const int SEGS = 14;
            Vector2 prev = from;
            for (int i = 1; i <= SEGS; i++)
            {
                float t = i / (float)SEGS;
                float u = 1f - t;
                Vector2 p = u * u * u * from + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * to;
                DrawLine(prev, p, color, thickness);
                prev = p;
            }
        }

        private bool IsOnActiveChain(ProtoTreeNode node, string activeId)
        {
            if (string.IsNullOrEmpty(activeId)) return false;
            // 从当前异能沿父链向上爬：node 是 activeId 的祖先或自己 → 在链路上
            var cur = _layout.GetNode(activeId);
            while (cur != null)
            {
                if (cur.Id == node.Id) return true;
                cur = _layout.GetNode(cur.ParentId);
            }
            return false;
        }

        /// <summary>仅路径模式：链上节点紧凑重排（根在下→叶在上，同 x 居中堆叠），连线自然变短</summary>
        private void EnsureChainLayout(string activeId)
        {
            if (_chainCacheKey == activeId && _chainPos.Count > 0) return;
            _chainCacheKey = activeId;
            _chainPos.Clear();
            _chainMaxW = 0f; _chainMaxH = 0f;
            if (_layout == null || string.IsNullOrEmpty(activeId)) return;

            // 收集链上节点，按 Level 升序（根 Level0 在底部 → 异能叶 Level2 在顶部）
            var nodes = new List<ProtoTreeNode>();
            foreach (var n in _layout.Nodes)
                if (IsOnActiveChain(n, activeId)) nodes.Add(n);
            nodes.Sort((a, b) => a.Level.CompareTo(b.Level));

            float maxW = 0f;
            foreach (var n in nodes) maxW = Mathf.Max(maxW, n.W);
            const float SPACING = 26f;   // 世界单位纵向间隙（缩放后仍紧凑）
            float curY = 0f;
            foreach (var n in nodes)
            {
                float x = (maxW - n.W) * 0.5f;
                _chainPos[n.Id] = new Vector2(x, curY);
                curY += n.H + SPACING;
            }
            _chainMaxW = maxW;
            _chainMaxH = curY - SPACING;   // 去掉末尾间隙 = 链总高
        }

        /// <summary>节点屏幕矩形：仅路径模式链上节点用紧凑坐标，其余用原布局坐标</summary>
        private Rect NodeScreenRect(ProtoTreeNode node, string activeId)
        {
            if (_pathMode && !string.IsNullOrEmpty(activeId) && IsOnActiveChain(node, activeId))
            {
                EnsureChainLayout(activeId);
                if (_chainPos.TryGetValue(node.Id, out Vector2 p))
                    return new Rect(p.x * _zoom + _pan.x, p.y * _zoom + _pan.y, node.W * _zoom, node.H * _zoom);
            }
            return ScreenRect(node);
        }

        /// <summary>聚焦当前异能链路（含根，Fit 到链路包围盒）</summary>
        private void FocusChain(string activeId)
        {
            if (_layout == null || string.IsNullOrEmpty(activeId)) return;
            Rect b = GetChainBounds(activeId);
            FitToRect(b);
        }

        /// <summary>当前异能链路的节点包围盒（异能叶→小类→系→根，含边距）；仅路径模式=紧凑链包围盒</summary>
        private Rect GetChainBounds(string activeId)
        {
            if (_pathMode && !string.IsNullOrEmpty(activeId))
            {
                EnsureChainLayout(activeId);
                if (_chainPos.Count > 0)
                    return new Rect(-60f, -60f, _chainMaxW + 120f, _chainMaxH + 120f);
            }
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var n in _layout.Nodes)
            {
                if (!IsOnActiveChain(n, activeId)) continue;
                x0 = Mathf.Min(x0, n.X); y0 = Mathf.Min(y0, n.Y);
                x1 = Mathf.Max(x1, n.X + n.W); y1 = Mathf.Max(y1, n.Y + n.H);
            }
            if (x0 > 1e9f) return _layout.Bounds;
            return Rect.MinMaxRect(x0 - 60f, y0 - 60f, x1 + 60f, y1 + 60f);
        }

        private void DrawNodes(Rect area)
        {
            var proto = PrototypeRegistry.GetForActor(_selectedActor);
            string activeId = proto != null ? proto.Id : null;

            foreach (var node in _layout.Nodes)
            {
                if (_pathMode && !string.IsNullOrEmpty(activeId) && !IsOnActiveChain(node, activeId)) continue;   // 仅路径模式：只画链上节点
                Rect r = NodeScreenRect(node, activeId);
                if (r.xMax < -20f || r.yMax < -20f || r.x > area.width + 20f || r.y > area.height + 20f) continue; // 视口裁剪

                bool isActive = node.Id == activeId;
                bool onActiveChain = !isActive && node.Level != -1 && IsOnActiveChain(node, activeId);
                Color mainColor = isActive ? DSUITheme.AccentGold : node.Color;

                // 圆角底色（染色）
                GUI.color = isActive ? new Color(0.36f, 0.32f, 0.18f, 0.98f) : new Color(0.15f, 0.16f, 0.21f, 0.95f);
                GUI.DrawTexture(r, _nodeRoundedTex);
                GUI.color = Color.white;
                // 左侧色条（大类色/金色）
                GUI.color = mainColor;
                GUI.DrawTexture(new Rect(r.x, r.y, Mathf.Max(3f, 4f * _zoom), r.height), Texture2D.whiteTexture);
                // 路径发光边框（当前异能所在链路的中间节点：半透明金外扩框）
                if (onActiveChain)
                    DrawBorder(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), new Color(0.95f, 0.85f, 0.55f, 0.35f));
                // 边框
                DrawBorder(r, isActive ? DSUITheme.AccentGold : node.Color * new Color(0.7f, 0.7f, 0.7f, 1f));

                // 系徽章圆形图标（左上角，系色）
                float iconSize = Mathf.Max(10f, 20f * _zoom);
                bool showIcon = node.Level != 2 || _zoom >= 0.28f;
                float iconPad = 0f;
                if (showIcon)
                {
                    iconPad = iconSize + 7f;
                    GUI.color = mainColor;
                    GUI.DrawTexture(new Rect(r.x + 5f, r.y + 5f, iconSize, iconSize), _circleTex);
                    GUI.color = Color.white;
                }

                // 缩放分级：远处只画色块；根/系/子类 >=0.10 显名；异能叶 >=0.28 显名；>=0.65 异能显标签徽章
                bool showName = node.Level == 2 ? _zoom >= 0.28f : _zoom >= 0.10f;
                if (showName)
                {
                    GUIStyle nameStyle = node.Level == 2 ? _leafStyle : (node.Level == 1 ? _subStyle : (node.Level == 0 ? _nodeStyle : _rootStyle));
                    nameStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt((node.Level == 2 ? 12 : node.Level == 1 ? 13 : node.Level == 0 ? 15 : 20) * _zoom), 9, 24);
                    GUI.color = isActive ? DSUITheme.AccentGold : DSUITheme.BodyText;
                    Rect nameRect = new Rect(r.x + 6f + iconPad, r.y + 4f, r.width - 12f - iconPad, r.height - 8f);
                    GUI.Label(nameRect, node.Name, nameStyle);
                    GUI.color = Color.white;

                    // 异能叶子：标签徽章
                    if (node.Level == 2 && node.Def != null && _zoom >= 0.65f)
                    {
                        float badgeH = Mathf.Max(12f, 17f * _zoom);
                        float badgeW = (r.width - 10f) / 2f;
                        float by = r.yMax - badgeH - 3f;
                        DrawTagBadge(new Rect(r.x + 5f, by, badgeW, badgeH), node.Def.MainTag, _zoom);
                        DrawTagBadge(new Rect(r.x + 5f + badgeW + 4f, by, badgeW, badgeH), node.Def.SubTag, _zoom);
                    }
                }

                // 当前异能：右上角金色角标；非激活概念级异能：金色小星角标
                if (isActive)
                {
                    float dot = Mathf.Max(5f, 7f * _zoom);
                    GUI.color = new Color(1f, 0.85f, 0.35f, 1f);
                    GUI.DrawTexture(new Rect(r.xMax - dot - 2f, r.y + 3f, dot, dot), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                else if (node.Level == 2 && node.Def != null && PrototypeEvolution.GetTier(node.Def) >= 5 && _zoom >= 0.28f)
                {
                    float dot = Mathf.Max(5f, 7f * _zoom);
                    GUI.color = new Color(1f, 0.85f, 0.35f, 0.75f);
                    GUI.DrawTexture(new Rect(r.xMax - dot - 2f, r.y + 3f, dot, dot), _circleTex);
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
                GUI.Label(new Rect(x, y, w - 210f, 26f), def.NameZh, _detailNameStyle);
                GUI.color = Color.white;
                // 品质徽章（异能等级 1凡~5概念级）
                int tier = PrototypeEvolution.GetTier(def);
                Color tierColor = tier >= 5 ? new Color(1f, 0.82f, 0.35f, 1f)
                    : tier == 4 ? new Color(0.95f, 0.62f, 0.35f, 1f)
                    : tier == 3 ? new Color(0.68f, 0.50f, 0.90f, 1f)
                    : tier == 2 ? new Color(0.50f, 0.75f, 0.90f, 1f)
                    : new Color(0.55f, 0.55f, 0.60f, 1f);
                GUI.color = tierColor;
                GUI.DrawTexture(new Rect(x + w - 210f, y, 56f, 22f), _badgeRoundedTex != null ? _badgeRoundedTex : Texture2D.whiteTexture);
                GUI.color = DSUITheme.TitleText;
                GUI.Label(new Rect(x + w - 210f, y, 56f, 22f), TierNameText(tier), _badgeStyle);
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

                // 来源 · 进化链（God 视角：天生觉醒 vs 后天进阶；进化目标=该异能家族内上级）
                y += 24f;
                var ownProto = PrototypeRegistry.GetForActor(_selectedActor);
                bool isOwn = ownProto != null && ownProto.Id == def.Id;
                if (isOwn)
                {
                    bool evolved = PrototypeEvolution.IsEvolved(_selectedActor);
                    string originText = evolved
                        ? GetText("proto_origin_evolved", "进阶×{0}").Replace("{0}", PrototypeEvolution.GetEvolveCount(_selectedActor).ToString())
                        : GetText("proto_origin_awakened", "觉醒");
                    GUI.color = evolved ? new Color(0.72f, 0.60f, 1f, 1f) : new Color(0.55f, 0.80f, 1f, 1f);
                    GUI.Label(new Rect(x, y, 100f, 22f), GetText("proto_origin_label", "来源: ") + originText, _infoStyle);
                    GUI.color = Color.white;
                }
                var evolveTarget = PrototypeEvolution.GetEvolveTarget(def);
                string evoText;
                if (evolveTarget != null)
                    evoText = GetText("proto_evolve_hint", "可进化→{0}（{1}）")
                        .Replace("{0}", evolveTarget.NameZh).Replace("{1}", TierNameText(PrototypeEvolution.GetTier(evolveTarget)));
                else
                    evoText = GetText("proto_evolve_peak", "已达{0}顶点").Replace("{0}", TierNameText(tier));
                GUI.color = new Color(0.95f, 0.70f, 0.45f, 1f);
                GUI.Label(new Rect(x + (isOwn ? 102f : 0f), y, w - (isOwn ? 102f : 0f), 22f), evoText, _infoStyle);
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
                    .Replace("{0}", PrototypeTags.GetName(def.MainTag)).Replace("{1:F1}", mainAff.ToString("F1"))
                    .Replace("{2}", PrototypeTags.GetName(def.SubTag)).Replace("{3:F1}", subAff.ToString("F1"))
                    .Replace("{4}", GetChromosomeName(def.ChromosomePreference)).Replace("{5:F1}", chromoAff.ToString("F1")),
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
                    shown.Level == 0
                        ? GetText("proto_cat_desc", "大类：") + shown.Name + "，含 " + count + " 个异能。觉醒时可能定相于此系异能。"
                        : shown.Level == 1
                            ? GetText("proto_sub_desc", "小类：") + shown.Name + "，含 " + count + " 个异能。"
                            : GetText("proto_root_desc", "异能体系：13 大类 × 小类 × 数百异能。每个异能由 30 种效果标签组合驱动。"),
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
                if (n.Level != 2) continue;   // 异能叶子（Level==2）
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
            GUI.DrawTexture(rect, _badgeRoundedTex != null ? _badgeRoundedTex : Texture2D.whiteTexture);
            GUI.color = DSUITheme.TitleText;
            int fs = Mathf.Clamp(Mathf.RoundToInt(10 * Mathf.Max(0.8f, zoom)), 8, 12);
            GUIStyle st = _badgeStyles != null ? _badgeStyles[fs - 8] : _badgeStyle;
            GUI.Label(rect, PrototypeTags.GetName(tag), st);
            GUI.color = Color.white;
        }

        /// <summary>圆角矩形纹理（白色，GUI.color 染色；1px 抗锯齿圆角）</summary>
        private static Texture2D MakeRoundedRectTexture(int w, int h, float radiusFrac, Color color)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float radius = Mathf.Max(1f, Mathf.Min(w, h) * radiusFrac);
            Color[] px = new Color[w * h];
            float innerW = w / 2f - radius, innerH = h / 2f - radius;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f - w / 2f;
                    float cy = y + 0.5f - h / 2f;
                    float dx = Mathf.Max(Mathf.Abs(cx) - innerW, 0f);
                    float dy = Mathf.Max(Mathf.Abs(cy) - innerH, 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    px[y * w + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>实心圆形纹理（白色，GUI.color 染色；1px 抗锯齿边缘）</summary>
        private static Texture2D MakeCircleTexture(int size, Color color)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    float alpha = Mathf.Clamp01(r - d + 0.5f);
                    px[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
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

        /// <summary>异能品质名（汉化优先，兜底业务层默认名）</summary>
        private static string TierNameText(int tier)
        {
            switch (tier)
            {
                case 1: return GetText("proto_tier_1", "凡阶");
                case 2: return GetText("proto_tier_2", "玄阶");
                case 3: return GetText("proto_tier_3", "灵阶");
                case 4: return GetText("proto_tier_4", "圣阶");
                case 5: return GetText("proto_tier_5", "神阶");
                default: return "";
            }
        }
        #endregion
    }
}
