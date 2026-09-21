// ============================================================

using System;
using System.Collections.Generic;
using Code.UI;
using UnityEngine;
using Code.Data;
using Code.Core;
using Code.Dimension;

namespace Code.UI
{
    /// <summary>
    /// 宏观维度空间全屏UI
    /// 背景是像素宇宙空间，单位在空间中显示为像素小人
    /// </summary>
    public class DimensionRealmWindow
    {
        #region 静态实例
        private static DimensionRealmWindow _instance;
        public static DimensionRealmWindow Instance => _instance ?? (_instance = new DimensionRealmWindow());
        #endregion

        #region 常量
        private const float LEFT_PANEL_WIDTH = 280f;
        private const float RIGHT_PANEL_WIDTH = 280f;
        private const float BOTTOM_PANEL_HEIGHT = 60f;
        private const float BUTTON_HEIGHT = 36f;
        private const float BUTTON_SPACING = 8f;
        #endregion

        #region 实例变量
        private bool _visible;
        private Actor _selectedActor;
        private long _selectedInhabitantId = -1;
        private Vector2 _leftScrollPosition;
        private Vector2 _rightScrollPosition;

        // 像素宇宙背景
        private Texture2D _cosmosBgTex;
        private float _starAnimationTime = 0f;

        // UI纹理
        private Texture2D _panelBgTex;
        private Texture2D _titleBgTex;
        private Texture2D _buttonBgTex;
        private Texture2D _buttonHoverTex;
        private Texture2D _activeButtonTex;
        private Texture2D _inhabitantBgTex;
        private Texture2D _selectedInhabitantBgTex;
        private Texture2D _starTex;
        #endregion

        #region 静态方法
        public static void Ensure()
        {
            if (_instance == null) _instance = new DimensionRealmWindow();
        }

        public static void Show()
        {
            Show(null);
        }

        public static void Show(Actor actor)
        {
            bool wasVisible = Instance._visible;
            Instance._selectedActor = actor;
            Instance._visible = true;
            if (!wasVisible) MouseOverlayBlocker.RequestBlock();
            Instance._leftScrollPosition = Vector2.zero;
            Instance._rightScrollPosition = Vector2.zero;
            Instance.InitTextures();
            Instance.GenerateCosmosBackground();
        }

        public static void Hide()
        {
            if (Instance._visible) MouseOverlayBlocker.ReleaseBlock();
            Instance._visible = false;
        }

        public static void Toggle()
        {
            if (Instance._visible) Hide();
            else Show();
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

        /// <summary>检测快捷键 Shift+B</summary>
        public static void CheckHotkey()
        {
            bool shiftPressed = Input.GetKey((KeyCode)304) || Input.GetKey((KeyCode)303);
            if (shiftPressed && Input.GetKeyDown((KeyCode)98))
            {
                // 不需要选中单位，直接打开维度空间
                // 如果有选中单位，则自动选中该单位
                Actor selectedActor = GetSelectedActor();
                if (selectedActor != null)
                {
                    Toggle(selectedActor);
                }
                else
                {
                    Toggle();
                }
            }
        }

        /// <summary>获取当前选中的单位</summary>
        private static Actor GetSelectedActor()
        {
            try
            {
                var unitWindowType = typeof(UnitWindow);
                var instanceField = unitWindowType.GetField("instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceField != null)
                {
                    object unitWindow = instanceField.GetValue(null);
                    if (unitWindow != null)
                    {
                        var actorField = unitWindowType.GetField("actor",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (actorField != null)
                        {
                            Actor actor = actorField.GetValue(unitWindow) as Actor;
                            if (actor != null && actor.isAlive()) return actor;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        #endregion

        #region 初始化
        private static Texture2D CreateSolidTex(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void InitTextures()
        {
            if (_panelBgTex != null) return;
            _panelBgTex = CreateSolidTex(new Color(0.08f, 0.10f, 0.18f, 0.92f));
            _titleBgTex = CreateSolidTex(new Color(0.15f, 0.20f, 0.35f, 0.95f));
            _buttonBgTex = CreateSolidTex(new Color(0.20f, 0.28f, 0.45f, 0.9f));
            _buttonHoverTex = CreateSolidTex(new Color(0.30f, 0.42f, 0.65f, 0.95f));
            _activeButtonTex = CreateSolidTex(new Color(0.25f, 0.55f, 0.85f, 0.95f));
            _inhabitantBgTex = CreateSolidTex(new Color(0.12f, 0.16f, 0.28f, 0.85f));
            _selectedInhabitantBgTex = CreateSolidTex(new Color(0.25f, 0.45f, 0.75f, 0.9f));
        }

        /// <summary>生成像素宇宙空间背景</summary>
        private void GenerateCosmosBackground()
        {
            if (_cosmosBgTex != null) return;

            int width = 512;
            int height = 512;
            _cosmosBgTex = new Texture2D(width, height);
            _cosmosBgTex.filterMode = FilterMode.Point;  // 像素风格

            // 深空背景渐变（深蓝色到黑色）
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float t = (float)y / height;
                    float r = Mathf.Lerp(0.02f, 0.08f, t);
                    float g = Mathf.Lerp(0.02f, 0.06f, t);
                    float b = Mathf.Lerp(0.08f, 0.15f, t);
                    _cosmosBgTex.SetPixel(x, y, new Color(r, g, b, 1f));
                }
            }

            // 添加像素星星（不同大小和亮度）
            System.Random rand = new System.Random(42);
            int starCount = 300;
            for (int i = 0; i < starCount; i++)
            {
                int x = rand.Next(0, width);
                int y = rand.Next(0, height);
                float brightness = (float)rand.NextDouble() * 0.8f + 0.2f;
                int size = rand.Next(1, 3);

                Color starColor = new Color(brightness, brightness, Mathf.Min(1f, brightness + 0.2f), 1f);

                // 绘制像素星星
                for (int dy = 0; dy < size; dy++)
                {
                    for (int dx = 0; dx < size; dx++)
                    {
                        int px = x + dx;
                        int py = y + dy;
                        if (px >= 0 && px < width && py >= 0 && py < height)
                        {
                            _cosmosBgTex.SetPixel(px, py, starColor);
                        }
                    }
                }
            }

            // 添加像素星云（不同颜色的像素云）
            int nebulaCount = 8;
            Color[] nebulaColors = new Color[]
            {
                new Color(0.3f, 0.1f, 0.5f, 0.15f),  // 紫色
                new Color(0.1f, 0.3f, 0.5f, 0.15f),  // 蓝色
                new Color(0.5f, 0.2f, 0.3f, 0.12f),  // 粉色
                new Color(0.2f, 0.4f, 0.3f, 0.12f),  // 青色
            };

            for (int i = 0; i < nebulaCount; i++)
            {
                int cx = rand.Next(50, width - 50);
                int cy = rand.Next(50, height - 50);
                int radius = rand.Next(30, 80);
                Color nebulaColor = nebulaColors[i % nebulaColors.Length];

                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        float dist = Mathf.Sqrt(x * x + y * y);
                        if (dist < radius)
                        {
                            float alpha = (1f - dist / radius) * nebulaColor.a;
                            int px = cx + x;
                            int py = cy + y;
                            if (px >= 0 && px < width && py >= 0 && py < height)
                            {
                                Color existing = _cosmosBgTex.GetPixel(px, py);
                                Color blended = Color.Lerp(existing, nebulaColor, alpha);
                                _cosmosBgTex.SetPixel(px, py, blended);
                            }
                        }
                    }
                }
            }

            _cosmosBgTex.Apply();
            DSDebug.Verbose("[维度空间] 像素宇宙背景生成完成");
        }
        #endregion

        #region 主绘制
        public void OnGUI()
        {
            if (!_visible) return;

            try
            {
                // 更新星星动画时间
                _starAnimationTime += Time.deltaTime;

                // 全屏背景：像素宇宙空间
                DrawCosmosBackground();

                // 左侧面板：空间概览 + 单位列表
                DrawLeftPanel();

                // 右侧面板：空间功能
                DrawRightPanel();

                // 底部面板：进入/退出按钮
                DrawBottomPanel();

                // 中间区域：空间内单位显示
                DrawInhabitantsInSpace();

                // 上报全屏窗口矩形给InputBlocker（拦截点选/滚轮/拖动/右键穿透）
                InputBlocker.ReportWindowRect(new Rect(0f, 0f, Screen.width, Screen.height));
            }
            catch (System.Exception e)
            {
                Code.Core.DSDebug.Warning("[维度空间] OnGUI异常: " + e.Message);
            }
        }

        /// <summary>绘制像素宇宙背景</summary>
        private void DrawCosmosBackground()
        {
            if (_cosmosBgTex == null) return;

            // 平铺背景
            int tileX = Mathf.CeilToInt(Screen.width / 512f);
            int tileY = Mathf.CeilToInt(Screen.height / 512f);
            for (int y = 0; y < tileY; y++)
            {
                for (int x = 0; x < tileX; x++)
                {
                    GUI.DrawTexture(new Rect(x * 512, y * 512, 512, 512), _cosmosBgTex);
                }
            }

            // 绘制闪烁的星星（动画效果）
            DrawTwinklingStars();
        }

        /// <summary>绘制闪烁的星星（优化：单个白色Texture2D + GUI.color控制颜色，避免每帧创建50个Texture2D）</summary>
        private void DrawTwinklingStars()
        {
            if (_starTex == null) _starTex = CreateSolidTex(Color.white);

            System.Random rand = new System.Random(123);
            int starCount = 50;
            Color originalColor = GUI.color;
            for (int i = 0; i < starCount; i++)
            {
                float x = rand.Next(0, Screen.width);
                float y = rand.Next(0, Screen.height);
                float phase = (float)rand.NextDouble() * Mathf.PI * 2;
                float brightness = 0.5f + 0.5f * Mathf.Sin(_starAnimationTime * 2f + phase);
                int size = rand.Next(2, 5);

                GUI.color = new Color(brightness, brightness, Mathf.Min(1f, brightness + 0.3f), brightness * 0.8f);
                GUI.DrawTexture(new Rect(x, y, size, size), _starTex);
            }
            GUI.color = originalColor;
        }
        #endregion

        #region 左侧面板
        private void DrawLeftPanel()
        {
            float x = 10f;
            float y = 10f;
            float width = LEFT_PANEL_WIDTH;
            float height = Screen.height - BOTTOM_PANEL_HEIGHT - 30f;

            // 面板背景
            GUI.DrawTexture(new Rect(x, y, width, height), _panelBgTex);

            // 标题
            GUI.DrawTexture(new Rect(x, y, width, 40f), _titleBgTex);
            GUI.Label(new Rect(x + 15, y, width - 30, 40f), UILocalization.Get("dimension_overview"),
                new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.7f, 0.85f, 1f) } });

            float contentY = y + 55f;
            float contentX = x + 15f;
            float contentWidth = width - 30f;

            // 空间信息
            GUI.Label(new Rect(contentX, contentY, contentWidth, 22f),
                string.Format(UILocalization.Get("dim_window_level"), DimensionRealmManager.GetRealmLevel()),
                new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } });
            contentY += 26f;

            GUI.Label(new Rect(contentX, contentY, contentWidth, 22f),
                string.Format(UILocalization.Get("dim_window_energy"), DimensionRealmManager.GetRealmEnergy()),
                new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.7f, 0.9f, 1f) } });
            contentY += 26f;

            GUI.Label(new Rect(contentX, contentY, contentWidth, 22f),
                string.Format(UILocalization.Get("dim_window_size"), DimensionRealmManager.GetRealmSize()),
                new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } });
            contentY += 26f;

            string ownerName = DimensionRealmManager.GetOwnerName();
            GUI.Label(new Rect(contentX, contentY, contentWidth, 22f),
                string.Format(UILocalization.Get("dim_window_owner"), (string.IsNullOrEmpty(ownerName) ? UILocalization.Get("dim_window_owner_none") : ownerName)),
                new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(1f, 0.85f, 0.5f) } });
            contentY += 30f;

            // 单位列表标题
            GUI.Label(new Rect(contentX, contentY, contentWidth, 22f),
                string.Format(UILocalization.Get("dim_window_inhabitants"), DimensionRealmManager.GetInhabitantCount()),
                new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.8f, 0.9f, 1f) } });
            contentY += 28f;

            // 滚动区域：可进入单位 + 空间内单位
            Rect scrollRect = new Rect(contentX, contentY, contentWidth, height - (contentY - y) - 15f);
            var eligibleActors = DimensionRealmManager.GetEligibleActors();
            float listHeight = (eligibleActors.Count > 0 ? 30f + eligibleActors.Count * 58f : 0f)
                             + (DimensionRealmManager.GetInhabitantCount() > 0 ? 30f + DimensionRealmManager.GetInhabitantCount() * 58f : 0f)
                             + 20f;
            _leftScrollPosition = GUI.BeginScrollView(scrollRect, _leftScrollPosition,
                new Rect(0, 0, contentWidth - 20f, listHeight));

            float listY = 0f;

            // 可进入单位列表
            if (eligibleActors.Count > 0)
            {
                GUI.Label(new Rect(0f, listY, contentWidth - 20f, 22f),
                    string.Format(UILocalization.Get("dim_window_eligible"), eligibleActors.Count),
                    new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.6f, 0.9f, 0.7f) } });
                listY += 28f;

                foreach (var actor in eligibleActors)
                {
                    bool isSelected = (_selectedActor != null && _selectedActor.getID() == actor.getID());
                    Texture2D bgTex = isSelected ? _selectedInhabitantBgTex : _inhabitantBgTex;

                    GUI.DrawTexture(new Rect(0, listY, contentWidth - 20f, 52f), bgTex);

                    string nameText = actor.getName();
                    int tier = CultivationData.GetRealmTier(actor);
                    GUI.Label(new Rect(10f, listY + 5f, contentWidth - 40f, 20f), nameText,
                        new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = isSelected ? Color.white : new Color(0.85f, 0.9f, 1f) } });

                    GUI.Label(new Rect(10f, listY + 26f, contentWidth - 40f, 18f),
                        string.Format(UILocalization.Get("dim_window_eligible_info"), tier),
                        new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.6f, 0.85f, 0.7f) } });

                    if (GUI.Button(new Rect(0, listY, contentWidth - 20f, 52f), "", GUIStyle.none))
                    {
                        _selectedActor = actor;
                        _selectedInhabitantId = -1;
                    }

                    listY += 58f;
                }
            }

            // 空间内单位列表
            if (DimensionRealmManager.GetInhabitantCount() > 0)
            {
                GUI.Label(new Rect(0f, listY, contentWidth - 20f, 22f),
                    UILocalization.Get("dim_window_in_space_label"),
                    new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.7f, 0.85f, 1f) } });
                listY += 28f;

                foreach (var inhabitant in DimensionRealmManager.GetAllInhabitants())
                {
                    bool isSelected = (inhabitant.ActorId == _selectedInhabitantId);
                    Texture2D bgTex = isSelected ? _selectedInhabitantBgTex : _inhabitantBgTex;

                    GUI.DrawTexture(new Rect(0, listY, contentWidth - 20f, 52f), bgTex);

                    string nameText = inhabitant.ActorName;
                    if (inhabitant.IsOwner) nameText += " ";
                    GUI.Label(new Rect(10f, listY + 5f, contentWidth - 40f, 20f), nameText,
                        new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = isSelected ? Color.white : new Color(0.85f, 0.9f, 1f) } });

                    GUI.Label(new Rect(10f, listY + 26f, contentWidth - 40f, 18f),
                        string.Format(UILocalization.Get("dim_window_inhabitant_info"), inhabitant.RealmTier, inhabitant.Energy),
                        new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.7f, 0.75f, 0.85f) } });

                    if (GUI.Button(new Rect(0, listY, contentWidth - 20f, 52f), "", GUIStyle.none))
                    {
                        _selectedInhabitantId = inhabitant.ActorId;
                        _selectedActor = SystemManagerExtensions.FindActorById(inhabitant.ActorId);
                    }

                    listY += 58f;
                }
            }

            GUI.EndScrollView();
        }
        #endregion

        #region 右侧面板
        private void DrawRightPanel()
        {
            float x = Screen.width - RIGHT_PANEL_WIDTH - 10f;
            float y = 10f;
            float width = RIGHT_PANEL_WIDTH;
            float height = Screen.height - BOTTOM_PANEL_HEIGHT - 30f;

            // 面板背景
            GUI.DrawTexture(new Rect(x, y, width, height), _panelBgTex);

            // 标题
            GUI.DrawTexture(new Rect(x, y, width, 40f), _titleBgTex);
            GUI.Label(new Rect(x + 15, y, width - 30, 40f), UILocalization.Get("dimension_functions"),
                new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.7f, 0.85f, 1f) } });

            float contentY = y + 55f;
            float contentX = x + 15f;
            float contentWidth = width - 30f;

            // 滚动区域
            Rect scrollRect = new Rect(contentX, contentY, contentWidth, height - (contentY - y) - 15f);
            _rightScrollPosition = GUI.BeginScrollView(scrollRect, _rightScrollPosition,
                new Rect(0, 0, contentWidth - 20f, 600f));

            float funcY = 0f;

            // 功能1：修炼室
            funcY = DrawFunctionCard(0f, funcY, contentWidth - 20f,
                UILocalization.Get("dimension_cultivation_room"),
                UILocalization.Get("dim_window_cultivation_buff"),
                _selectedActor != null && DimensionRealmManager.IsInDimension(_selectedActor),
                null);

            // 功能2：失控消解场
            funcY = DrawFunctionCard(0f, funcY, contentWidth - 20f,
                UILocalization.Get("dim_window_turbulence_field"),
                UILocalization.Get("dim_window_turbulence_desc"),
                _selectedActor != null && DimensionRealmManager.IsInDimension(_selectedActor),
                null);

            // 功能3：神识扫描
            funcY = DrawFunctionCard(0f, funcY, contentWidth - 20f,
                UILocalization.Get("dim_window_divine_scan"),
                UILocalization.Get("dim_window_divine_scan_desc"),
                true,
                null);

            // 功能4：空间升级
            funcY = DrawFunctionCard(0f, funcY, contentWidth - 20f,
                UILocalization.Get("dim_window_upgrade"),
                string.Format(UILocalization.Get("dim_window_upgrade_desc"), DimensionRealmManager.GetRealmLevel()),
                false,
                () => { if (_selectedActor != null) DimensionRealmManager.UpgradeRealm(_selectedActor); });

            // 功能5：能量充能
            funcY = DrawFunctionCard(0f, funcY, contentWidth - 20f,
                UILocalization.Get("dim_window_charge"),
                string.Format(UILocalization.Get("dim_window_charge_desc"), (_selectedActor != null ? CultivationData.GetEnergy(_selectedActor).ToString("F0") : "0")),
                false,
                () => { if (_selectedActor != null) DimensionRealmManager.ChargeEnergy(_selectedActor); });

            // 功能6：创造能量生命体（仅13阶超神级）
            int tier = _selectedActor != null ? CultivationData.GetRealmTier(_selectedActor) : 0;
            if (tier >= 13)
            {
                funcY = DrawFunctionCard(0f, funcY, contentWidth - 20f,
                    UILocalization.Get("dim_window_create_life"),
                    UILocalization.Get("dim_window_create_life_desc"),
                    false,
                    () => { if (_selectedActor != null) DimensionRealmManager.CreateEnergyLifeform(_selectedActor); });
            }

            GUI.EndScrollView();
        }

        private float DrawFunctionCard(float x, float y, float width, string title, string desc, bool isActive, Action onClick)
        {
            float cardHeight = 85f;
            float spacing = 10f;

            // 卡片背景
            Texture2D cardBg = isActive ? _activeButtonTex : _inhabitantBgTex;
            GUI.DrawTexture(new Rect(x, y, width, cardHeight), cardBg);

            // 标题
            GUI.Label(new Rect(x + 10f, y + 8f, width - 20f, 22f), title,
                new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = isActive ? new Color(0.7f, 1f, 0.8f) : new Color(0.8f, 0.9f, 1f) } });

            // 描述
            GUI.Label(new Rect(x + 10f, y + 30f, width - 120f, 50f), desc,
                new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.7f, 0.75f, 0.85f) }, wordWrap = true });

            // 按钮（如果有点击事件）
            if (onClick != null)
            {
                if (GUI.Button(new Rect(x + width - 100f, y + 28f, 90f, 30f), UILocalization.Get("dim_window_execute"),
                    new GUIStyle(GUI.skin.button) { fontSize = 12, normal = { textColor = Color.white, background = _buttonBgTex }, hover = { background = _buttonHoverTex } }))
                {
                    onClick?.Invoke();
                }
            }
            else if (isActive)
            {
                GUI.Label(new Rect(x + width - 100f, y + 28f, 90f, 30f), UILocalization.Get("dim_window_activated"),
                    new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.6f, 1f, 0.7f) } });
            }

            return y + cardHeight + spacing;
        }
        #endregion

        #region 底部面板
        private void DrawBottomPanel()
        {
            float x = 10f;
            float y = Screen.height - BOTTOM_PANEL_HEIGHT - 10f;
            float width = Screen.width - 20f;
            float height = BOTTOM_PANEL_HEIGHT;

            // 面板背景
            GUI.DrawTexture(new Rect(x, y, width, height), _panelBgTex);

            // 当前选中单位信息
            if (_selectedActor != null)
            {
                GUI.Label(new Rect(x + 20f, y + 10f, 300f, 20f),
                    string.Format(UILocalization.Get("dim_window_current_unit"), _selectedActor.getName(), CultivationData.GetRealmTier(_selectedActor)),
                    new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } });

                bool inSpace = DimensionRealmManager.IsInDimension(_selectedActor);
                GUI.Label(new Rect(x + 20f, y + 32f, 300f, 18f),
                    inSpace ? UILocalization.Get("dim_window_status_in_space") : UILocalization.Get("dim_window_status_in_world"),
                    new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = inSpace ? new Color(0.6f, 1f, 0.7f) : new Color(0.8f, 0.8f, 0.8f) } });
            }
            else
            {
                GUI.Label(new Rect(x + 20f, y + 10f, 400f, 20f),
                    UILocalization.Get("dim_window_no_selection"),
                    new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.8f, 0.85f, 0.95f) } });
                GUI.Label(new Rect(x + 20f, y + 32f, 400f, 18f),
                    UILocalization.Get("dim_window_hint"),
                    new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.6f, 0.65f, 0.75f) } });
            }

            // 进入/退出按钮
            float btnWidth = 200f;
            float btnX = Screen.width - btnWidth - 30f;
            float btnY = y + (height - BUTTON_HEIGHT) / 2f;

            bool hasSelection = _selectedActor != null;
            bool inDimension = hasSelection && DimensionRealmManager.IsInDimension(_selectedActor);
            string btnText = !hasSelection ? UILocalization.Get("dim_window_please_select") : (inDimension ? UILocalization.Get("dim_window_exit_space") : UILocalization.Get("dim_window_enter_space"));
            Texture2D btnTex = inDimension ? _activeButtonTex : _buttonBgTex;

            if (hasSelection && GUI.Button(new Rect(btnX, btnY, btnWidth, BUTTON_HEIGHT), btnText,
                new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white, background = btnTex }, hover = { background = _buttonHoverTex } }))
            {
                DimensionRealmManager.ToggleDimension(_selectedActor);
            }
            else if (!hasSelection)
            {
                // 禁用状态显示
                GUI.DrawTexture(new Rect(btnX, btnY, btnWidth, BUTTON_HEIGHT), _buttonBgTex);
                GUI.Label(new Rect(btnX, btnY, btnWidth, BUTTON_HEIGHT), btnText,
                    new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.5f, 0.55f, 0.65f) } });
            }

            // 关闭按钮
            if (GUI.Button(new Rect(Screen.width - 50f, y + 10f, 35f, 35f), "×",
                new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white, background = _buttonBgTex }, hover = { background = _buttonHoverTex } }))
            {
                Hide();
            }
        }
        #endregion

        #region 中间区域：空间内单位显示
        private void DrawInhabitantsInSpace()
        {
            // 中间区域范围
            float centerX = LEFT_PANEL_WIDTH + 30f;
            float centerY = 60f;
            float centerWidth = Screen.width - LEFT_PANEL_WIDTH - RIGHT_PANEL_WIDTH - 60f;
            float centerHeight = Screen.height - BOTTOM_PANEL_HEIGHT - 100f;

            // 绘制空间边界
            GUI.Box(new Rect(centerX, centerY, centerWidth, centerHeight), "",
                new GUIStyle(GUI.skin.box) { normal = { background = null } });

            // 绘制空间内的单位（像素小人）
            foreach (var inhabitant in DimensionRealmManager.GetAllInhabitants())
            {
                // 将空间坐标映射到屏幕坐标
                float screenX = centerX + (inhabitant.X / DimensionRealmManager.GetRealmSize()) * centerWidth;
                float screenY = centerY + (inhabitant.Y / DimensionRealmManager.GetRealmSize()) * centerHeight;

                // 单位大小根据境界
                float unitSize = 20f + inhabitant.RealmTier * 1.5f;

                // 单位颜色根据境界
                Color unitColor = GetTierColor(inhabitant.RealmTier);
                if (inhabitant.IsOwner) unitColor = new Color(1f, 0.85f, 0.3f);  // 主人金色

                // 绘制像素小人（简单的像素风格）
                DrawPixelUnit(screenX, screenY, unitSize, unitColor, inhabitant.IsOwner);

                // 单位名字
                GUI.Label(new Rect(screenX - 50f, screenY + unitSize + 2f, 100f, 18f), inhabitant.ActorName,
                    new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });

                // 点击选中
                if (GUI.Button(new Rect(screenX - unitSize / 2f, screenY - unitSize / 2f, unitSize, unitSize + 20f), "", GUIStyle.none))
                {
                    _selectedInhabitantId = inhabitant.ActorId;
                    _selectedActor = SystemManagerExtensions.FindActorById(inhabitant.ActorId);
                }
            }

            // 如果空间内没有单位，显示提示
            if (DimensionRealmManager.GetInhabitantCount() == 0)
            {
                GUI.Label(new Rect(centerX, centerY + centerHeight / 2f - 20f, centerWidth, 40f),
                    UILocalization.Get("dim_window_no_units"),
                    new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.7f, 0.8f, 0.9f) } });
            }
        }

        /// <summary>绘制像素小人</summary>
        private void DrawPixelUnit(float x, float y, float size, Color color, bool isOwner)
        {
            // 简单的像素风格小人：头部 + 身体
            float headSize = size * 0.4f;
            float bodyWidth = size * 0.6f;
            float bodyHeight = size * 0.5f;

            // 头部
            Texture2D headTex = CreateSolidTex(color);
            GUI.DrawTexture(new Rect(x - headSize / 2f, y - size / 2f, headSize, headSize), headTex);

            // 身体
            Texture2D bodyTex = CreateSolidTex(new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 1f));
            GUI.DrawTexture(new Rect(x - bodyWidth / 2f, y - size / 2f + headSize, bodyWidth, bodyHeight), bodyTex);

            // 如果是主人，绘制光环
            if (isOwner)
            {
                Texture2D haloTex = CreateSolidTex(new Color(1f, 0.85f, 0.3f, 0.5f));
                GUI.DrawTexture(new Rect(x - size / 2f - 4f, y - size / 2f - 4f, size + 8f, size + 8f), haloTex);
            }
        }

        /// <summary>根据境界获取颜色</summary>
        private Color GetTierColor(int tier)
        {
            if (tier >= 13) return new Color(1f, 0.5f, 1f);      // 真神 - 紫色
            if (tier >= 12) return new Color(1f, 0.7f, 0.3f);      // 真神级 - 橙色
            if (tier >= 10) return new Color(0.8f, 0.3f, 0.3f);    // 登神级 - 红色
            if (tier >= 7) return new Color(0.3f, 0.6f, 1f);       // 具象者 - 蓝色
            if (tier >= 4) return new Color(0.3f, 1f, 0.6f);       // 突变者 - 绿色
            return new Color(0.7f, 0.7f, 0.7f);                      // 低阶 - 灰色
        }
        #endregion
    }
}



