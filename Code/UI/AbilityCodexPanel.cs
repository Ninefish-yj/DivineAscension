using System;
using System.Collections.Generic;
using Code.UI;
using System.Reflection;
using Code.Combat;
using Code.Core;
using Code.Data;
using Code.Disaster;
using Code.Dimension;
using Code.Realm;
using Code.Sect;
using Code.Traits;
using UnityEngine;

namespace Code.UI;

public class AbilityCodexPanel : MonoBehaviour
{
	private enum Tab
	{
		RealmCodex,
		AbilityCodex,
		Cultivators,
		Stats,
		Sect,
		Disaster,
		Divine
	}

	private bool _visible;

	private bool _wasVisible;

	private Rect _windowRect;

	private bool _isResizing;
		private Vector2 _resizeStartMouse;
		private Vector2 _resizeStartSize;

	private const float MIN_WINDOW_W = 600f;

	private const float MIN_WINDOW_H = 500f;

	private const float MAX_WINDOW_W = 1600f;

	private const float MAX_WINDOW_H = 1200f;

	private const float DEFAULT_W = 850f;

	private const float DEFAULT_H = 700f;

	private static float _savedWidth = 850f;

	private static float _savedHeight = 700f;

	private static bool _sizeLoaded = false;


	private const float DOUBLE_CLICK_INTERVAL = 0.3f;

	private Tab _currentTab;

	private Vector2 _listScroll;

	private Vector2 _detailScroll;

	private long _selectedActorId;

	private Vector2 _genericScroll;

	private int _selectedRealmTier;


	private string _newSectName;

	private string _selectedSectId;  // 当前选中展开详情的组织ID

	private string _statusMessage;

	private float _statusTimer;

	private float _lastRescanTime;



	private int _currentFontSize;

	// 自绘窗口拖动状态（基因图谱同款）
	private bool _isDragging;

	private Vector2 _dragStartMouse;

	private Vector2 _dragStartWindowPos;

	private static readonly string[] TabNames = new string[7]
	{
		UILocalization.Get("tab_realm_codex"),
		UILocalization.Get("tab_ability_codex"),
		UILocalization.Get("tab_cultivators"),
		UILocalization.Get("tab_stats"),
		UILocalization.Get("tab_sect"),
		UILocalization.Get("tab_disaster"),
		UILocalization.Get("tab_dimension")
	};

	private static readonly string[] TabKeys = new string[7] { "tab_realm_codex", "tab_ability_codex", "tab_cultivators", "tab_stats", "tab_sect", "tab_disaster", "tab_dimension" };

	private float ContentHeight => Mathf.Max(200f, (_windowRect).height - 130f);

	private float SpacingSmall => Mathf.Max(6f, (float)_currentFontSize * 0.4f);

	private float SpacingMedium => Mathf.Max(10f, (float)_currentFontSize * 0.7f);

	private float SpacingLarge => Mathf.Max(14f, (float)_currentFontSize);

	private void Awake()
	{
		if (_sizeLoaded)
		{
			(_windowRect).width = Mathf.Max(_savedWidth, 600f);
			(_windowRect).height = Mathf.Max(_savedHeight, 500f);
		}
	}

	private void Update()
	{
		if ((Input.GetKey((KeyCode)304) || Input.GetKey((KeyCode)303)) && Input.GetKeyDown((KeyCode)108))
		{
			_visible = !_visible;
		}
		// 维度空间快捷键检测（Shift+B，原在ModClass.Update中但BasicMod非MonoBehaviour不执行）
		try { Code.UI.DimensionRealmWindow.CheckHotkey(); } catch { }

		// 建筑放置模式处理
		try
		{
// 			Code.UI.BuildPlacementMode.HandleKey();
// 			if (Code.UI.BuildPlacementMode.IsActive && Input.GetMouseButtonDown(0))
			{
				Vector2 mousePos = World.world.getMousePos();
// 				Code.UI.BuildPlacementMode.HandleMouseClick(new Vector3(mousePos.x, 0, mousePos.y));
			}
		}
		catch { }

		// 旧境界建筑王国一次性迁移（读档后首次tick执行，消除小地图图标渲染空引用）
// 		try { Code.Realm.RealmBuildings.MigrateLegacyBuildingKingdoms(); } catch { }
		if (_statusTimer > 0f)
		{
			_statusTimer -= Time.deltaTime;
			if (_statusTimer <= 0f)
			{
				_statusMessage = "";
			}
		}
	}

	private void OnGUI()
	{
		// 建筑放置模式OnGUI
// 		try { Code.UI.BuildPlacementMode.OnGUI(); } catch { }

		if (_visible != _wasVisible)
		{
			if (_visible)
			{
				MouseOverlayBlocker.RequestBlock();
			}
			else
			{
				MouseOverlayBlocker.ReleaseBlock();
			}
			_wasVisible = _visible;
		}
		// 如果维度空间窗口可见，跳过异能图鉴面板的绘制，避免上下两层重叠
		if (Code.UI.DimensionRealmWindow.IsVisible())
		{
			// 仍然绘制基因图谱窗口和维度空间窗口
			GeneGraphWindow.Instance.OnGUI();
			try { Code.UI.DimensionRealmWindow.Instance.OnGUI(); } catch { }
			return;
		}

		if (_visible)
		{
			Event current = Event.current;
			bool flag = current != null && (_windowRect).Contains(current.mousePosition);
			MouseOverlayBlocker.UpdateBlockArea(_windowRect, flag); // 仅鼠标在窗口内时阻挡 UGUI（离开窗口立即放行地图操作）
			// === 图谱式自绘窗口（布局仍为流式）===
			Rect titleRect = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, 30f);
			Rect closeRect = new Rect(_windowRect.xMax - 32f, _windowRect.y + 4f, 28f, 28f);
			// 拖动：MouseDrag/MouseUp 在绘制前处理（不影响控件）
			if (current != null && current.type == EventType.MouseDrag && _isDragging)
			{
				_windowRect.position = _dragStartWindowPos + (current.mousePosition - _dragStartMouse);
				current.Use();
			}
			else if (current != null && current.type == EventType.MouseUp && _isDragging)
			{
				_isDragging = false;
				current.Use();
			}
			GUI.DrawTexture(_windowRect, DSUITheme.WindowBgTexture);
			GUI.DrawTexture(titleRect, DSUITheme.TitleBarBgTexture);
			GUI.color = DSUITheme.TitleText;
			GUI.Label(new Rect(titleRect.x + 12f, titleRect.y + 7f, titleRect.width - 60f, 22f), UILocalization.Get("panel_title"), new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
			GUI.color = Color.white;
			GUI.color = new Color(0.9f, 0.3f, 0.3f);
			if (GUI.Button(closeRect, "×", new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold }))
			{
				_visible = false;
				Event.current.Use();
			}
			GUI.color = Color.white;
			Rect contentRect = new Rect(_windowRect.x, _windowRect.y + 30f, _windowRect.width, _windowRect.height - 30f);
			GUILayout.BeginArea(contentRect);
			DrawWindowContent();
			GUILayout.EndArea();
			// 拖动启动检测：放在所有控件绘制之后，控件优先处理点击；
			// 若事件未被控件消费（仍为MouseDown）且鼠标在窗口空白区域（非关闭按钮/非调整手柄），则启动拖动
			Rect resizeHandle = new Rect(_windowRect.xMax - 22f, _windowRect.yMax - 22f, 20f, 20f);
			if (current != null && current.type == EventType.MouseDown && current.button == 0
			    && _windowRect.Contains(current.mousePosition)
			    && !closeRect.Contains(current.mousePosition)
			    && !resizeHandle.Contains(current.mousePosition))
			{
				_isDragging = true;
				_dragStartMouse = current.mousePosition;
				_dragStartWindowPos = _windowRect.position;
				current.Use();
			}
			// tooltip（屏幕坐标，Area 外绘制）
			if (!string.IsNullOrEmpty(GUI.tooltip))
			{
				Vector2 mousePosition = Event.current.mousePosition;
				string tooltip = GUI.tooltip;
				GUIContent val = new GUIContent(tooltip);
				Vector2 val2 = GUI.skin.label.CalcSize(val);
				float num3 = Mathf.Min(val2.x + 16f, (_windowRect).width - 20f);
				float num4 = val2.y + 8f;
				float num5 = mousePosition.x + 12f;
				float num6 = mousePosition.y + 12f;
				if (num5 + num3 > (_windowRect).width - 5f)
				{
					num5 = mousePosition.x - num3 - 12f;
				}
				if (num6 + num4 > (_windowRect).height - 5f)
				{
					num6 = mousePosition.y - num4 - 12f;
				}
				GUI.color = DSUITheme.PanelBg;
				GUI.DrawTexture(new Rect(num5, num6, num3, num4), (Texture)(object)Texture2D.whiteTexture);
				GUI.color = DSUITheme.BorderLight;
				GUI.DrawTexture(new Rect(num5, num6, num3, 1f), (Texture)(object)Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(num5, num6 + num4 - 1f, num3, 1f), (Texture)(object)Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(num5, num6, 1f, num4), (Texture)(object)Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(num5 + num3 - 1f, num6, 1f, num4), (Texture)(object)Texture2D.whiteTexture);
				GUI.color = DSUITheme.BodyText;
				GUI.Label(new Rect(num5 + 8f, num6 + 4f, num3 - 16f, num4 - 8f), tooltip);
				GUI.color = Color.white;
			}
			DrawResizeHandle();
			InputBlocker.ReportWindowRect(_windowRect);
			current = Event.current;
			if (current != null && (_windowRect).Contains(current.mousePosition) && ((int)current.type == 0 || (int)current.type == 1 || (int)current.type == 3 || (int)current.type == 6))
			{
				current.Use();
			}
		}

		// 绘制基因图谱窗口（独立于异能图鉴面板）
		GeneGraphWindow.Instance.OnGUI();
	}

	private void DrawWindowContent()
	{
		if (Time.realtimeSinceStartup - _lastRescanTime > 1f)
		{
			_lastRescanTime = Time.realtimeSinceStartup;
			AnnualTickManager.RebuildActiveList();
		}
		int num = Mathf.Clamp(Mathf.FloorToInt((_windowRect).width / 60f), 10, 24);
		int num2 = Mathf.Clamp(Mathf.FloorToInt((_windowRect).height / 50f), 10, 24);
		_currentFontSize = Mathf.Min(num, num2);
		using (DSGUISkinInjector.Inject(_currentFontSize))
		{
			GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
			DrawTabBar();
			GUILayout.Space(SpacingSmall);
			if (_currentTab != Tab.AbilityCodex)
			{
				// 图谱式：左侧信息栏 + 右侧内容区（谱系表需要全宽，不走此分支）
				GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
				DrawLeftInfoPanel();
				GUILayout.Space(SpacingSmall);
				GUILayout.BeginVertical((GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
				DrawActiveTabContent();
				GUILayout.EndVertical();
				GUILayout.EndHorizontal();
			}
			else
			{
				DrawActiveTabContent();
			}
			GUILayout.EndVertical();
		}

	}

	private void DrawResizeHandle()
	{
			Rect val = new Rect((_windowRect).x + (_windowRect).width - 22f, (_windowRect).y + (_windowRect).height - 22f, 20f, 20f); // 屏幕坐标（Area 外调用）
		Color color = GUI.color;
		GUI.color = new Color(0.3f, 0.4f, 0.6f, 0.3f);
		GUI.DrawTexture(new Rect((val).x, (val).y, (val).width, (val).height), (Texture)(object)Texture2D.whiteTexture);
		GUI.color = new Color(0.6f, 0.75f, 1f, 0.9f);
		for (int i = 0; i < 3; i++)
		{
			GUI.DrawTexture(new Rect((val).x + 4f + (float)(i * 4), (val).yMax - 6f - (float)(i * 4), (float)(14 - i * 4), 2f), (Texture)(object)Texture2D.whiteTexture);
		}
		GUI.color = color;
		Event current = Event.current;
		if ((int)current.type == 0 && (val).Contains(current.mousePosition))
		{
			_isResizing = true;
			_resizeStartMouse = current.mousePosition;
			_resizeStartSize = new Vector2((_windowRect).width, (_windowRect).height);
			current.Use();
		}
		else if ((int)current.type == 3 && _isResizing)
		{
			// 修复：窗口宽高 = 起点宽高 + 鼠标相对增量（原实现把鼠标绝对坐标当宽高  窗口被拉至鼠标位置，巨大窗口盖住地图并被InputBlocker拦截）
			float maxW = Mathf.Max(600f, Screen.width - (_windowRect).x - 20f);
			float maxH = Mathf.Max(500f, Screen.height - (_windowRect).y - 20f);
			(_windowRect).width = Mathf.Clamp(_resizeStartSize.x + (current.mousePosition.x - _resizeStartMouse.x), 600f, maxW);
			(_windowRect).height = Mathf.Clamp(_resizeStartSize.y + (current.mousePosition.y - _resizeStartMouse.y), 500f, maxH);
			current.Use();
		}
		else if ((int)current.type == 1 && _isResizing)
		{
			_isResizing = false;
			_savedWidth = (_windowRect).width;
			_savedHeight = (_windowRect).height;
			_sizeLoaded = true;
			current.Use();
		}
	}

	private void DrawTabBar()
	{
		// 页签栏：实心分段卡（基因图谱风格） 选中=金褐提亮+金色描边，未选中=深蓝灰卡
		int tabCount = TabNames.Length;
		int tabHeight = _currentFontSize + 14;
		float space = 2f;
		float totalW = _windowRect.width - 68f; // 预留语言切换按钮(50+2)
		float tabW = (totalW - (tabCount - 1) * space) / tabCount;

		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		for (int i = 0; i < tabCount; i++)
		{
			bool isActive = (_currentTab == (Tab)i);
			Rect tabRect = GUILayoutUtility.GetRect(tabW, tabHeight);

			// 背景：实心色块（与谱系表格子同款技术）
			Color tabBg = isActive ? new Color(0.31f, 0.29f, 0.23f, 1f) : new Color(0.15f, 0.16f, 0.20f, 1f);
			GUI.color = tabBg;
			GUI.DrawTexture(tabRect, Texture2D.whiteTexture);
			GUI.color = Color.white;

			// 选中：金色描边（顶部+底部细线，同谱系表选中金框风格）
			if (isActive)
			{
				GUI.color = DSUITheme.AccentGold;
				GUI.DrawTexture(new Rect(tabRect.x, tabRect.y, tabRect.width, 2f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(tabRect.x, tabRect.yMax - 2f, tabRect.width, 2f), Texture2D.whiteTexture);
				GUI.color = Color.white;
			}

			// 文字
			GUIStyle ts = new GUIStyle(GUI.skin.label);
			ts.fontSize = _currentFontSize;
			ts.alignment = TextAnchor.MiddleCenter;
			ts.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
			ts.normal.textColor = isActive ? DSUITheme.TitleText : DSUITheme.BodyText;
			GUI.Label(tabRect, UILocalization.Get(TabKeys[i]), ts);

			// 点击检测（左键按下即切换）
			if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && tabRect.Contains(Event.current.mousePosition))
			{
				_currentTab = (Tab)i;
				_genericScroll = Vector2.zero;
				Event.current.Use();
			}

			if (i < tabCount - 1) GUILayout.Space(space);
		}

		GUILayout.EndHorizontal();
	}
	private void DrawActiveTabContent()
	{
		switch (_currentTab)
		{
		case Tab.RealmCodex:
			DrawTabRealmCodex();
			break;
		case Tab.AbilityCodex:
			DrawTabAbilityCodex();
			break;
		case Tab.Cultivators:
			DrawTabCultivators();
			break;
		case Tab.Stats:
			DrawTabStats();
			break;
		case Tab.Sect:
			DrawTabSect();
			break;
		case Tab.Disaster:
			DrawTabDisaster();
			break;
		case Tab.Divine:
			DrawTabDivine();
			break;
		}
		if (!string.IsNullOrEmpty(_statusMessage))
		{
			GUILayout.Space(SpacingSmall);
			DSGUIHelper.ColoredLabel(_statusMessage, DSUITheme.SecondaryText, _currentFontSize - 2, (FontStyle)0);
		}
	}

	private void DrawLeftInfoPanel()
	{
		GUILayout.BeginVertical((GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(246f) });
		GUILayout.BeginVertical("box", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(246f) });
		DSGUIHelper.Title(UILocalization.Get(TabKeys[(int)_currentTab]), _currentFontSize + 1);
		DSGUIHelper.Separator();
		switch (_currentTab)
		{
		case Tab.RealmCodex:
		{
			int unlockedRealmCount = GetUnlockedRealmCount();
			DSGUIHelper.ColoredLabel(string.Format(UILocalization.Get("collection_progress_fmt"), unlockedRealmCount, unlockedRealmCount * 100 / 13), new Color(0.8f, 0.8f, 0.8f, 1f), _currentFontSize - 1, (FontStyle)0);
			Rect progressBar = GUILayoutUtility.GetRect(210f, 8f);
			GUI.Box(progressBar, "");
			Rect progressFill = new Rect(progressBar.x, progressBar.y, progressBar.width * (float)unlockedRealmCount / 13f, progressBar.height);
			GUI.DrawTexture(progressFill, (Texture)(object)DSUITheme.AccentBlueTexture);
			GUILayout.Space(10f);
			GUILayout.Space(10f);
			DSGUIHelper.ColoredLabel(UILocalization.CurrentLanguage == "en" ? "Select a realm card to view its details" : UILocalization.Get("codex_click_tier_card"), DSUITheme.SecondaryText, _currentFontSize - 2, (FontStyle)0);
			GUILayout.Space(12f);
			if (_selectedRealmTier > 0)
			{
				int selectedTier = _selectedRealmTier;
				DSGUIHelper.ColoredLabel(GetTierName(selectedTier), DSUITheme.GetTierColor(selectedTier), _currentFontSize - 1, (FontStyle)1);
				GUILayout.Space(6f);
				string realmFeatureDescription = RealmFeatures.GetRealmFeatureDescription(selectedTier);
				if (!string.IsNullOrEmpty(realmFeatureDescription))
				{
					DSGUIHelper.ColoredLabel(realmFeatureDescription, new Color(0.85f, 0.85f, 0.85f, 1f), _currentFontSize - 2, (FontStyle)0);
				}
				GUILayout.Space(8f);
				DrawInfoRow(UILocalization.CurrentLanguage == "en" ? "Combat" : UILocalization.Get("codex_combat"), "x" + RealmFeatures.GetRealmCombatMultiplier(selectedTier).ToString("F1"), "#FF6060");
				DrawInfoRow(UILocalization.CurrentLanguage == "en" ? "Defense" : UILocalization.Get("codex_defense"), "x" + RealmFeatures.GetRealmDefenseMultiplier(selectedTier).ToString("F1"), "#6090FF");
				DrawInfoRow(UILocalization.CurrentLanguage == "en" ? "Cultivation" : UILocalization.Get("codex_cultivation"), "x" + RealmFeatures.GetRealmCultivationEfficiency(selectedTier).ToString("F1"), "#60FF90");
				string realmStatsText = BuildRealmStatsText(selectedTier);
				if (!string.IsNullOrEmpty(realmStatsText))
				{
					GUILayout.Space(6f);
					DSGUIHelper.ColoredLabel(realmStatsText, new Color(0.75f, 0.85f, 1f, 1f), _currentFontSize - 2, (FontStyle)0);
				}
			}
			break;
		}
		case Tab.Cultivators:
		{
			if (_selectedActorId >= 0)
			{
				Actor viewActor = FindActor(_selectedActorId);
				if (viewActor != null)
				{
					int realmTier = CultivationData.GetRealmTier(viewActor);
					DSGUIHelper.ColoredLabel(viewActor.getName(), DSUITheme.AccentGold, _currentFontSize, (FontStyle)0);
					GUILayout.Space(4f);
					DrawInfoRow(UILocalization.CurrentLanguage == "en" ? "Realm" : UILocalization.Get("codex_tier"), GetTierName(realmTier), "#FFD700");
					DrawInfoRow(UILocalization.CurrentLanguage == "en" ? "Turbulence" : UILocalization.Get("codex_turbulence"), CultivationData.GetTurbulence(viewActor).ToString("F1"), "#FF6060");
					DrawInfoRow(UILocalization.CurrentLanguage == "en" ? "Energy" : UILocalization.Get("codex_energy"), CultivationData.GetEnergy(viewActor).ToString("F0"), "#7AB8FF");
					string rankName = SectManager.GetRankName(CultivationData.GetSectRank(viewActor));
					if (!string.IsNullOrEmpty(rankName))
					{
						float cultBoost = Code.Core.EnergyTurbulenceCalculator.GetSectCultivationBoost(viewActor);
						if (cultBoost > 0f)
						{
							rankName += string.Format(UILocalization.Get("sect_cult_boost"), Mathf.RoundToInt(cultBoost * 100f));
						}
						DrawInfoRow(UILocalization.CurrentLanguage == "en" ? "Sect" : UILocalization.Get("unit_sect"), rankName, "#FFD37A");
					}
				}
			}
			else
			{
				DSGUIHelper.ColoredLabel(UILocalization.Get("select_cultivator"), DSUITheme.SecondaryText, _currentFontSize - 1, (FontStyle)0);
			}
			break;
		}
		case Tab.Stats:
			DSGUIHelper.ColoredLabel(UILocalization.CurrentLanguage == "en" ? "Global statistics of civilizations and cultivation" : UILocalization.Get("codex_global_stats"), DSUITheme.BodyText, _currentFontSize - 1, (FontStyle)0);
			break;
		case Tab.Sect:
			DSGUIHelper.ColoredLabel(UILocalization.CurrentLanguage == "en" ? "Factions and camps across the map" : UILocalization.Get("codex_factions"), DSUITheme.BodyText, _currentFontSize - 1, (FontStyle)0);
			break;
		case Tab.Disaster:
			DSGUIHelper.ColoredLabel(UILocalization.CurrentLanguage == "en" ? "Records of world-threatening disasters" : UILocalization.Get("codex_world_threat"), DSUITheme.BodyText, _currentFontSize - 1, (FontStyle)0);
			break;
		case Tab.Divine:
			DSGUIHelper.ColoredLabel(UILocalization.CurrentLanguage == "en" ? "The final layers of the path to godhood" : UILocalization.Get("codex_singularity_final"), DSUITheme.BodyText, _currentFontSize - 1, (FontStyle)0);
			break;
		}
		GUILayout.EndVertical();
		GUILayout.EndVertical();
	}

	private void DrawTabRealmCodex()
	{
		// 顶部标题与收集进度已移入左信息栏（DrawLeftInfoPanel）
		GUILayout.Space(4f);
		// 图谱式：上部卡片网格（全宽2列大卡片）+ 底部详情条
		float gridHeight = ContentHeight - 24f; // 卡片区全高（详情已移入左栏）
		_genericScroll = GUILayout.BeginScrollView(_genericScroll, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(gridHeight) });
		int num = 2;
		for (int i = 0; i < 13; i += num)
		{
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			for (int j = 0; j < num && i + j < 13; j++)
			{
				int tier = i + j + 1;
				DrawRealmCardCompact(tier);
				if (j < num - 1 && i + j + 1 < 13)
				{
					GUILayout.Space(16f);
				}
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(16f);
		}
		GUILayout.EndScrollView();

	}

	private int GetUnlockedRealmCount()
	{
		int num = 0;
		try
		{
			if (World.world != null && World.world.units != null)
			{
				foreach (Actor item in (SimSystemManager<Actor, ActorData>)(object)World.world.units)
				{
					if (item != null && item.isAlive())
					{
						int realmTier = CultivationData.GetRealmTier(item);
						if (realmTier > num)
						{
							num = realmTier;
						}
					}
				}
			}
		}
		catch
		{
		}
		return num;
	}

	private void DrawRealmCardCompact(int tier)
	{
		Color tierColor = DSUITheme.GetTierColor(tier);
		string tierName = UILocalization.GetTierName(tier);
		bool flag = _selectedRealmTier == tier;
		bool flag2 = tier <= GetUnlockedRealmCount();
		float num = Mathf.Min(290f, ((_windowRect).width - 246f - 40f) / 2f); // 卡片宽（2列大卡片）
		float num2 = 110f;
		Color backgroundColor = GUI.backgroundColor;
		if (flag)
		{
			GUI.backgroundColor = new Color(tierColor.r * 0.4f, tierColor.g * 0.4f, tierColor.b * 0.4f, 1f);
		}
		else if (flag2)
		{
			GUI.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
		}
		else
		{
			GUI.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
		}
		Rect rect = GUILayoutUtility.GetRect(num, num2);
		GUI.Box(rect, "");
		GUI.backgroundColor = backgroundColor;
		Rect val = new Rect((rect).x + 12f, (rect).y + 32f, 44f, 44f);
		GUI.DrawTexture(val, (Texture)(object)DSUITheme.GetTierGradientTexture(tier));
		GUI.color = Color.white;
		Rect val2 = val;
		string text = tier.ToString();
		GUIStyle val3 = new GUIStyle(GUI.skin.label)
		{
			alignment = (TextAnchor)4,
			fontSize = 18,
			fontStyle = (FontStyle)1
		};
		val3.normal.textColor = Color.white;
		GUI.Label(val2, text, val3);
		Rect val4 = new Rect((rect).x + 68f, (rect).y + 32f, num - 78f, 28f);
		GUI.color = (Color)(flag2 ? tierColor : new Color(0.5f, 0.5f, 0.5f));
		GUI.Label(val4, tierName, new GUIStyle(GUI.skin.label)
		{
			fontSize = 16,
			fontStyle = (FontStyle)1,
			alignment = (TextAnchor)3
		});
		Rect val5 = new Rect((rect).x + 68f, (rect).y + 66f, num - 78f, 24f);
		GUI.color = new Color(0.6f, 0.6f, 0.6f);
		GUI.Label(val5, string.Format(UILocalization.Get("realm_tier_fmt"), tier), new GUIStyle(GUI.skin.label)
		{
			fontSize = 12,
			alignment = (TextAnchor)3
		});
		GUI.color = Color.white;
		if ((int)Event.current.type == 0 && (rect).Contains(Event.current.mousePosition))
		{
			_selectedRealmTier = (flag ? (-1) : tier);
			Event.current.Use();
		}
	}

	/// <summary>
	/// 构建境界属性加成文本（读取境界特质的 base_stats，反射兼容原版BaseStats固定属性类）
	/// </summary>
	private string BuildRealmStatsText(int tier)
	{
		try
		{
			string traitId = Code.Traits.TraitManager.GetRealmTraitId(tier);
			if (string.IsNullOrEmpty(traitId)) return "";
			ActorTrait trait = AssetManager.traits.get(traitId);
			if (trait == null || trait.base_stats == null) return "";
			bool en = UILocalization.CurrentLanguage == "en";
			string[][] statDefs = new string[][]
			{
				new string[] { "damage", en ? "Atk" : "攻击" },
				new string[] { "health", en ? "HP" : "生命" },
				new string[] { "speed", en ? "Spd" : "速度" },
				new string[] { "mana", en ? "Mana" : "能量" },
				new string[] { "stamina", en ? "Sta" : "体力" },
				new string[] { "intelligence", en ? "Int" : "智力" },
				new string[] { "warfare", en ? "War" : "战争" },
				new string[] { "range", en ? "Range" : "射程" },
				new string[] { "attack_speed", en ? "AtkSpd" : "攻速" },
				new string[] { "armor", en ? "Armor" : "护甲" },
				new string[] { "lifespan", en ? "Life" : "寿命" }
			};
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach (string[] statDef in statDefs)
			{
				float val = GetBaseStatValue(trait.base_stats, statDef[0]);
				if (val != 0f)
				{
					if (sb.Length > 0) sb.Append("  ");
					string fmt = (Mathf.Abs(val) >= 1f) ? val.ToString("F0") : val.ToString("F1");
					sb.Append(statDef[1]).Append("+").Append(fmt);
				}
			}
			return sb.ToString();
		}
		catch (System.Exception dsEx)
		{
			Code.Core.DSDebug.Warning("[DivineAscension] BuildRealmStatsText 异常: " + dsEx.Message);
			return "";
		}
	}

	/// <summary>用反射读取BaseStats中指定属性值（BaseStats不是字典，是固定属性类）</summary>
	private float GetBaseStatValue(object stats, string key)
	{
		try
		{
			if (stats == null) return 0f;
			var prop = stats.GetType().GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (prop != null)
			{
				object val = prop.GetValue(stats);
				if (val != null) return System.Convert.ToSingle(val);
			}
			var field = stats.GetType().GetField(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (field != null)
			{
				object val = field.GetValue(stats);
				if (val != null) return System.Convert.ToSingle(val);
			}
		}
		catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] GetBaseStatValue 异常: " + dsEx.Message); }
		return 0f;
	}

	private void DrawRealmDetail(int tier)
	{
		Color tierColor = DSUITheme.GetTierColor(tier);
		float qiThreshold = RealmJudge.GetQiThreshold(tier);
		string tierName = UILocalization.GetTierName(tier);
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUI.color = tierColor;
		GUILayout.Label(string.Format(UILocalization.Get("realm_title_fmt"), tier, tierName), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(28f) });
		GUILayout.FlexibleSpace();
		GUI.color = new Color(0.7f, 0.7f, 0.7f);
		GUILayout.Label(string.Format(UILocalization.Get("realm_energy_threshold"), qiThreshold), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(28f) });
		GUI.color = Color.white;
		GUILayout.EndHorizontal();
		GUILayout.Space(10f);
		string realmFeatureDescription = RealmFeatures.GetRealmFeatureDescription(tier);
		if (!string.IsNullOrEmpty(realmFeatureDescription))
		{
			GUI.color = new Color(0.9f, 0.9f, 0.9f);
			GUILayout.Label(string.Format(UILocalization.Get("realm_feature"), realmFeatureDescription), new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true });
			GUI.color = Color.white;
		}
		GUILayout.Space(12f);
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUI.color = new Color(1f, 0.4f, 0.4f);
		GUILayout.Label(string.Format(UILocalization.Get("realm_combat"), RealmFeatures.GetRealmCombatMultiplier(tier)), (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(150f),
			GUILayout.Height(20f)
		});
		GUI.color = new Color(0.4f, 0.6f, 1f);
		GUILayout.Label(string.Format(UILocalization.Get("realm_defense"), RealmFeatures.GetRealmDefenseMultiplier(tier)), (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(150f),
			GUILayout.Height(20f)
		});
		GUI.color = new Color(0.4f, 1f, 0.6f);
		GUILayout.Label(string.Format(UILocalization.Get("realm_cultivation"), RealmFeatures.GetRealmCultivationEfficiency(tier)), (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(150f),
			GUILayout.Height(20f)
		});
		GUI.color = Color.white;
		GUILayout.EndHorizontal();
		GUILayout.Space(12f);
		List<RealmAbility> abilitiesForTier = RealmAbilities.GetAbilitiesForTier(tier);
		if (abilitiesForTier != null && abilitiesForTier.Count > 0)
		{
			GUI.color = new Color(0.8f, 0.6f, 1f);
			GUILayout.Label(UILocalization.Get("realm_detail_abilities"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(20f) });
			GUI.color = Color.white;
			foreach (RealmAbility item in abilitiesForTier)
			{
				GUILayout.BeginVertical((GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(40f) });
				GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
				GUILayout.Space(15f);
				GUI.color = new Color(0.9f, 0.9f, 0.9f);
				GUILayout.Label(" " + item.GetName(), (GUILayoutOption[])(object)new GUILayoutOption[2]
				{
					GUILayout.Width(150f),
					GUILayout.Height(18f)
				});
				GUILayout.FlexibleSpace();
				GUI.color = new Color(0.7f, 0.7f, 0.5f);
				GUILayout.Label(string.Format(UILocalization.Get("realm_skill_cost_cd"), item.EnergyCost, item.Cooldown), (GUILayoutOption[])(object)new GUILayoutOption[2]
				{
					GUILayout.Width(140f),
					GUILayout.Height(18f)
				});
				GUI.color = Color.white;
				GUILayout.EndHorizontal();
				GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
				GUILayout.Space(30f);
				GUI.color = new Color(0.6f, 0.6f, 0.6f);
				GUIStyle val = new GUIStyle(GUI.skin.label)
				{
					wordWrap = true,
					fontSize = 11
				};
				GUILayout.Label(item.GetDescription(), val);
				GUI.color = Color.white;
				GUILayout.EndHorizontal();
				GUILayout.EndVertical();
				GUILayout.Space(10f);
			}
		}
		GUILayout.Space(10f);
		GUI.color = new Color(0.6f, 0.8f, 1f);
		GUILayout.Label(UILocalization.Get("realm_detail_conditions"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(20f) });
		GUI.color = Color.white;
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUILayout.Space(15f);
		GUI.color = new Color(0.8f, 0.8f, 0.8f);
		GUILayout.Label(string.Format(UILocalization.Get("realm_cond_energy"), qiThreshold), (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(200f),
			GUILayout.Height(18f)
		});
		GUILayout.Label(UILocalization.Get("realm_cond_mastery"), (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(160f),
			GUILayout.Height(18f)
		});
		GUILayout.Label(UILocalization.Get("realm_cond_turbulence"), (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(140f),
			GUILayout.Height(18f)
		});
		if (tier >= 8)
		{
			GUILayout.Label(UILocalization.Get("realm_cond_enlight"), (GUILayoutOption[])(object)new GUILayoutOption[2]
			{
				GUILayout.Width(180f),
				GUILayout.Height(18f)
			});
		}
		GUI.color = Color.white;
		GUILayout.EndHorizontal();
	}

	private void DrawTabAbilityCodex()
	{
		DSGUIHelper.Title(UILocalization.Get("ability_codex_title"), _currentFontSize + 2);
		DSGUIHelper.Separator();
		DSGUIHelper.ColoredLabel(UILocalization.Get("ability_periodic_desc"), DSUITheme.SecondaryText, _currentFontSize - 2, (FontStyle)2);
		GUILayout.Space(6f);
		_genericScroll = GUILayout.BeginScrollView(_genericScroll, (GUILayoutOption[])(object)new GUILayoutOption[2] { GUILayout.ExpandWidth(true), GUILayout.Height(ContentHeight - 168f) });
		DrawPeriodicGrid();
		GUILayout.EndScrollView();
		DrawPeriodicDetail();
	}

	// ============ 基因序列表（谱系表） ============
	private static readonly string[] _periodicGroupZh = new string[] { "力量", "敏捷", "体质", "智力", "感知", "意志", "异能", "潜能" };
	private static readonly string[] _periodicGroupEn = new string[] { "Strength", "Agility", "Constitution", "Intelligence", "Perception", "Will", "Ability", "Potential" };
	private int _selectedPeriodicNumber = 3; // 默认选中基因

	private ElementDef.ElementInfo FindElementByNumber(int number)
	{
		foreach (var elem in ElementDef.AllElements)
		{
			if (ElementDef.GetNumber(elem) == number) return elem;
		}
		return null;
	}

	private void DrawPeriodicGrid()
	{
		bool en = UILocalization.CurrentLanguage == "en";
		string[] groupNames = en ? _periodicGroupEn : _periodicGroupZh;
		// 谱系表页无左栏：列宽按内容区实际宽度计算（族名列56 + 左右边距40）
			float cellW = Mathf.Max(80f, ((_windowRect).width - 64f - 40f) / 7f);
			float cellH = 52f;
		GUIStyle headerS = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
		// 表头行（与数据行同用 GetRect 机制，保证列对齐，不被布局扩展错位）
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUILayout.Label("", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(64f) });
		for (int p = 1; p <= 7; p++)
		{
			string[] periodNames = en ? new string[] { "Basic", "Advanced", "Master", "Super", "Rule", "Supreme", "Origin" } : new string[] { "基础表达", "进阶表达", "掌控表达", "超常表达", "规则表达", "至高表达", "本源表达" };
				string ph = periodNames[p - 1];
				if (p == 7) ph = en ? "Unknown" : "未知";
			headerS.normal.textColor = (p == 7) ? new Color(0.85f, 0.5f, 0.5f) : new Color(0.7f, 0.8f, 0.9f);
			Rect hr = GUILayoutUtility.GetRect(cellW, 20f);
			GUI.Label(hr, ph, headerS);
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(2f);
		for (int g = 0; g < 8; g++)
		{
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUIStyle gs = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
			gs.normal.textColor = (g >= 4) ? new Color(0.7f, 0.55f, 0.95f) : new Color(0.55f, 0.8f, 1f);
			GUILayout.Label(groupNames[g], gs, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(64f) });
			for (int p = 1; p <= 7; p++)
			{
				int number = (p - 1) * 8 + g + 1;
				DrawPeriodicCell(number, p, cellW, cellH);
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(3f);
		}

		// 神创基因横条
		GUILayout.Space(4f);
		Rect divineRect = GUILayoutUtility.GetRect(cellW * 7f, 30f);
		bool divineSel = _selectedPeriodicNumber == 57;
		GUI.color = divineSel ? new Color(0.6f, 0.5f, 0.2f, 1f) : new Color(0.2f, 0.18f, 0.1f, 0.9f);
		GUI.DrawTexture(divineRect, Texture2D.whiteTexture);
		GUI.color = Color.white;
		GUIStyle divineS = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
		divineS.normal.textColor = divineSel ? new Color(1f, 0.95f, 0.6f) : new Color(0.95f, 0.85f, 0.5f);
		GUI.Label(new Rect(divineRect.x, divineRect.y, divineRect.width, divineRect.height), "✦ 神创基因（可自定义） ✦", divineS);
		if (GUI.Button(divineRect, "", GUIStyle.none)) _selectedPeriodicNumber = 57;
	}

	private void DrawPeriodicCell(int number, int period, float cellW, float cellH)
	{
		bool en = UILocalization.CurrentLanguage == "en";
			bool known = period <= 6;
		ElementDef.ElementInfo e = known ? FindElementByNumber(number) : null;
		bool selected = _selectedPeriodicNumber == number;
		Color bg;
		if (!known) bg = new Color(0.20f, 0.17f, 0.17f, 1f);
		else if (selected) bg = new Color(0.45f, 0.38f, 0.20f, 1f);
		else if (e != null) bg = new Color(0.22f, 0.20f, 0.28f, 1f);
		else bg = new Color(0.15f, 0.20f, 0.28f, 1f);
		Rect r = GUILayoutUtility.GetRect(cellW, cellH);
		Color prev = GUI.color;
		GUI.color = bg;
		GUI.DrawTexture(r, Texture2D.whiteTexture);
		GUI.color = prev;
		GUIStyle numS = new GUIStyle(GUI.skin.label) { fontSize = 8, alignment = TextAnchor.UpperLeft };
		numS.normal.textColor = known ? new Color(0.75f, 0.78f, 0.85f) : new Color(0.7f, 0.5f, 0.5f);
		GUI.Label(new Rect(r.x + 3f, r.y + 1f, 20f, 11f), number.ToString(), numS);
			GUIStyle symS = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
		symS.normal.textColor = known ? Color.white : new Color(0.6f, 0.55f, 0.55f);
			string displayName = e != null ? (en ? e.NameEn : e.NameZh) : "?";
			if (displayName.Length > 4) displayName = displayName.Substring(0, 4);
			GUI.Label(new Rect(r.x + 2f, r.y + 12f, cellW - 4f, cellH - 14f), displayName, symS);
		if (GUI.Button(r, "", GUIStyle.none)) _selectedPeriodicNumber = number;
	}

	private void DrawPeriodicDetail()
	{
		ElementDef.ElementInfo e = FindElementByNumber(_selectedPeriodicNumber);
		bool en = UILocalization.CurrentLanguage == "en";
		GUIStyle ds = new GUIStyle(GUI.skin.box);
		ds.padding = new RectOffset(10, 10, 6, 6);
		GUILayout.BeginVertical(ds, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.MinHeight(110f) });
		GUILayout.FlexibleSpace(); // 详情内容整体下移
		if (e == null)
		{
			if (_selectedPeriodicNumber == 57)
			{
				GUILayout.Label(UILocalization.Get("divine_gene_title"), new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.95f, 0.85f, 0.5f) } });
				GUILayout.Label(UILocalization.Get("divine_gene_desc"), new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } });
			}
			else
			{
				GUILayout.Label(_selectedPeriodicNumber > 56 ? "57+" : _selectedPeriodicNumber.ToString(), new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
				GUILayout.Label(en ? "Unknown element reserved for future periods." : UILocalization.Get("codex_reserved"), new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } });
			}
		}
		else
		{
			bool known = _selectedPeriodicNumber <= 48;
			string[] groupNames = en ? _periodicGroupEn : _periodicGroupZh;
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUIStyle bigS = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
			bigS.normal.textColor = known ? new Color(1f, 0.85f, 0.5f) : new Color(0.7f, 0.6f, 0.6f);
			GUILayout.Label("[" + _selectedPeriodicNumber + "] " + e.Symbol, bigS, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(90f) });
			GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
			GUIStyle nameS = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
			GUILayout.Label(e.NameZh + " / " + e.NameEn, nameS);
			GUIStyle metaS = new GUIStyle(GUI.skin.label) { fontSize = 11 };
			metaS.normal.textColor = new Color(0.75f, 0.78f, 0.85f);
			GUILayout.Label(en ? ("Chromosome: " + groupNames[e.Group] + "  ·  Level: " + e.Period) : ("染色体: " + (_selectedPeriodicNumber == 57 ? "神明" : groupNames[e.Group]) + "  ·  表达层级: " + e.Period), metaS);
			GUILayout.EndVertical();
			GUILayout.FlexibleSpace();
			GUIStyle statS = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
			if (!known) { statS.normal.textColor = new Color(0.9f, 0.55f, 0.55f); GUILayout.Label(en ? "UNKNOWN" : UILocalization.Get("codex_unknown"), statS); }
			else { statS.normal.textColor = new Color(0.6f, 0.8f, 1f); GUILayout.Label(en ? "KNOWN" : UILocalization.Get("codex_known"), statS); }
			GUILayout.EndHorizontal();
			GUIStyle descS = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
			descS.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
			GUILayout.Label(en ? e.DescEn : e.DescZh, descS);
			// 基因突变系统：不再显示任务解锁条件，基因通过随机突变获得
			if (!known)
			{
				GUIStyle unkS = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true };
				unkS.normal.textColor = new Color(0.7f, 0.6f, 0.6f);
				GUILayout.Label(UILocalization.Get("gene_not_unlocked_hint"), unkS);
			}
		}
		GUILayout.EndVertical();
	}

	private string GetUnlockTypeName(GeneUnlockType type, float param)
	{
		bool en = UILocalization.CurrentLanguage == "en";
		switch (type)
		{
			case GeneUnlockType.CombatKills: return en ? ("Defeat " + param + " enemies") : (UILocalization.Get("codex_defeat") + param + UILocalization.Get("codex_enemies"));
			case GeneUnlockType.CombatDefense: return en ? ("Sustain " + param + " attacks") : (UILocalization.Get("codex_tank") + param + UILocalization.Get("codex_attacks"));
			case GeneUnlockType.ContinuousCultivation: return en ? ("Cultivate continuously for " + param + " years") : (UILocalization.Get("codex_cultivate") + param + UILocalization.Get("codex_years"));
			case GeneUnlockType.LowCorruption: return en ? ("Turbulence <70 for " + param + " years") : (UILocalization.Get("codex_low_turbulence") + param + UILocalization.Get("codex_years"));
			case GeneUnlockType.DeepEnlightenment: return en ? ("Deep enlightenment x" + param) : (UILocalization.Get("codex_deep_awakening") + param + UILocalization.Get("codex_times"));
			case GeneUnlockType.Ritual: return en ? "Unlock all 5 prior periods of the same group" : UILocalization.Get("codex_unlock_family");
			case GeneUnlockType.EnergyThreshold: return en ? ("Reach " + param + " energy") : (UILocalization.Get("codex_energy_reach") + param);
			case GeneUnlockType.AgeRequirement: return en ? ("Reach age " + param) : (UILocalization.Get("codex_age_reach") + param + UILocalization.Get("codex_age_years"));
			case GeneUnlockType.SourceUnknown: return en ? "Unknown" : UILocalization.Get("codex_unknown");
			default: return type.ToString();
		}
	}

	private void DrawTabCultivators()
	{
		DSGUIHelper.Title(UILocalization.Get("cultivator_list"), _currentFontSize + 2);
		DSGUIHelper.Separator();
		_genericScroll = GUILayout.BeginScrollView(_genericScroll, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(ContentHeight) });
		List<long> activeIds = GetActiveIds();
		int count = activeIds.Count;
		int num2 = 0;
		float num3 = 0f;
		foreach (long item in activeIds)
		{
			Actor val = FindActor(item);
			if (val != null)
			{
								{
					num2++;
				}
				num3 += CultivationData.GetTurbulence(val);
			}
		}
		if (count > 0)
		{
			num3 /= (float)count;
		}
		DrawInfoRow(UILocalization.Get("total_cultivators"), count.ToString(), "#FFD700");

		DrawInfoRow(UILocalization.Get("avg_turbulence"), $"{num3:F1}", (num3 >= 70f) ? "#FF6060" : ((num3 >= 50f) ? "#FFD060" : "#80FF80"));
		GUILayout.Space(10f);
		bool en = UILocalization.CurrentLanguage == "en";
		// ===== 前十排行榜（按战力降序） =====
		List<KeyValuePair<long, float>> ranked = new List<KeyValuePair<long, float>>();
		foreach (long item2 in activeIds)
		{
			Actor val2 = FindActor(item2);
			if (val2 != null)
			{
				ranked.Add(new KeyValuePair<long, float>(item2, CalculatePower(val2, CultivationData.GetRealmTier(val2))));
			}
		}
		ranked.Sort((KeyValuePair<long, float> a, KeyValuePair<long, float> b) => b.Value.CompareTo(a.Value));
		int show = Mathf.Min(10, ranked.Count);
		for (int i = 0; i < show; i++)
		{
			Actor val3 = FindActor(ranked[i].Key);
			if (val3 != null)
			{
				DrawCultivatorCard(val3, ranked[i].Key, i + 1, ranked[i].Value);
			}
		}
		if (show == 0)
		{
			DSGUIHelper.ColoredLabel(en ? "No cultivators yet." : UILocalization.Get("codex_no_awakened"), DSUITheme.SecondaryText, _currentFontSize - 1, (FontStyle)0);
		}
		GUILayout.EndScrollView();
	}

	private float CalculatePower(Actor actor, int tier)
	{
		float num = (float)tier * 1000f;
		float qi = CultivationData.GetEnergy(actor);
		float num2 = qi * 1f;
		int num3 = 0;
		float num4 = (float)num3 * 200f;
		float turbulence = CultivationData.GetTurbulence(actor);
		float num5 = turbulence * 5f;
		float num6 = num + num2 + num4 - num5;
		return Mathf.Max(0f, num6);
	}

	private void DrawCultivatorCard(Actor actor, long id, int rank = 0, float power = 0f)
	{
		bool en = UILocalization.CurrentLanguage == "en";
		int realmTier = CultivationData.GetRealmTier(actor);
		float qi = CultivationData.GetEnergy(actor);
		float turbulence = CultivationData.GetTurbulence(actor);
		float qiThreshold = RealmJudge.GetQiThreshold(realmTier + 1);
		bool flag = _selectedActorId == id;
		Color tierColor = DSUITheme.GetTierColor(realmTier);
		Color backgroundColor = GUI.backgroundColor;
		GUI.backgroundColor = (flag ? DSUITheme.CardBgActive : DSUITheme.CardBg);
		GUILayout.BeginVertical((GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		GUI.backgroundColor = backgroundColor;
		GUILayout.Space(12f);
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		string arg = rank switch
		{
			3 => "#CD7F32", 
			2 => "#C0C0C0", 
			1 => "#FFD700", 
			_ => "#8A8490", 
		};
		string text = ((rank > 0) ? $"<color={arg}>#{rank}</color>  " : "");
		int fontSize = GUI.skin.label.fontSize;
		bool richText = GUI.skin.label.richText;
		GUI.skin.label.fontSize = _currentFontSize;
		GUI.skin.label.richText = true;
		GUILayout.Label(text, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(40f) });
		GUI.skin.label.fontSize = fontSize;
		GUI.skin.label.richText = richText;
		GUI.color = tierColor;
		GUILayout.Box("", (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(4f),
			GUILayout.Height((float)(_currentFontSize + 8))
		});
		GUI.color = Color.white;
		GUILayout.Space(12f);
		DSGUIHelper.ColoredLabel(actor.getName(), flag ? DSUITheme.AccentGold : DSUITheme.BodyText, _currentFontSize, (FontStyle)1);
		GUILayout.FlexibleSpace();
		if (power > 0f)
		{
			DSGUIHelper.ColoredLabel((en ? "Power " : UILocalization.Get("codex_power")) + power.ToString("F0"), new Color(1f, 0.9f, 0.55f), _currentFontSize - 1, (FontStyle)1);
			GUILayout.Space(12f);
		}
		DSGUIHelper.ColoredLabel(string.Format(UILocalization.Get("realm_tier_fmt"), realmTier), tierColor, _currentFontSize - 1, (FontStyle)1);
		GUILayout.Space(12f);
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUILayout.Space(12f);
		DSGUIHelper.ColoredLabel(GetTierName(realmTier), DSUITheme.SecondaryText, _currentFontSize - 2, (FontStyle)0);
		GUILayout.Space(12f);
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUILayout.Space(12f);
		DSGUIHelper.ColoredLabel(UILocalization.Get("qi"), DSUITheme.SecondaryText, _currentFontSize - 3, (FontStyle)0, GUILayout.Width(50f));
		Rect rect = GUILayoutUtility.GetRect(0f, 6f, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		GUI.color = DSUITheme.PanelBg;
		GUI.DrawTexture(rect, (Texture)(object)Texture2D.whiteTexture);
		float num = Mathf.Clamp01(qi / Mathf.Max(1f, qiThreshold));
		GUI.color = DSUITheme.EnergyColor;
		GUI.DrawTexture(new Rect((rect).x, (rect).y, (rect).width * num, (rect).height), (Texture)(object)Texture2D.whiteTexture);
		GUI.color = Color.white;
		GUILayout.Space(12f);
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUILayout.Space(12f);
		DSGUIHelper.ColoredLabel(UILocalization.Get("turbulence"), DSUITheme.SecondaryText, _currentFontSize - 3, (FontStyle)0, GUILayout.Width(50f));
		Rect rect2 = GUILayoutUtility.GetRect(0f, 6f, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		GUI.color = DSUITheme.PanelBg;
		GUI.DrawTexture(rect2, (Texture)(object)Texture2D.whiteTexture);
		float num2 = Mathf.Clamp01(turbulence / 100f);
		GUI.color = ((turbulence >= 70f) ? Color.red : ((turbulence >= 50f) ? Color.yellow : Color.green));
		GUI.DrawTexture(new Rect((rect2).x, (rect2).y, (rect2).width * num2, (rect2).height), (Texture)(object)Texture2D.whiteTexture);
		GUI.color = Color.white;
		GUILayout.Space(12f);
		GUILayout.EndHorizontal();
		GUILayout.Space(12f);
		GUILayout.EndVertical();
		Rect lastRect = GUILayoutUtility.GetLastRect();
		Event current = Event.current;
		if ((int)current.type == 0 && (lastRect).Contains(current.mousePosition) && current.button == 0)
		{
			_selectedActorId = id;
			try
			{
				// 打开原游戏单位面板（使用ActionLibrary.openUnitWindow，这是WorldBox官方API）
				if (actor != null && actor.isAlive())
				{
					ActionLibrary.openUnitWindow(actor);
				}
			}
			catch (Exception ex)
			{
				DSDebug.Warning("[DivineAscension] 打开单位面板失败: " + ex.Message);
			}
			current.Use();
		}
		GUILayout.Space(12f);
	}

	private void DrawTabStats()
	{
		_genericScroll = GUILayout.BeginScrollView(_genericScroll, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(ContentHeight) });
		CultivationStats statsSnapshot = AnnualTickManager.GetStatsSnapshot();
		int num = _currentFontSize + 8;
		int num2 = _currentFontSize + 10;
		float num3 = Mathf.Clamp(((_windowRect).width - 246f) * 0.4f, 180f, 380f);
		GUILayout.Label(string.Format(UILocalization.Get("stats_summary_fmt"), statsSnapshot.TotalCultivators, statsSnapshot.AverageTurbulence), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
		GUILayout.Label(string.Format(UILocalization.Get("last_settlement_fmt"), AnnualTickManager.LastSettledCount, AnnualTickManager.LastSettleDurationMs), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
		if (World.world != null)
		{
			float num4 = (float)World.world.getCurWorldTime();
			int num5 = Mathf.FloorToInt(num4 / 60f) + 1;
			int num6 = Mathf.FloorToInt(num4 % 60f / 5f) + 1;
			if (num6 > 12)
			{
				num6 = 12;
			}
			GUILayout.Label(string.Format(UILocalization.Get("current_year_fmt"), num5, num6), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
		}
		GUILayout.Space(SpacingMedium);
		DrawSeparator();
		GUILayout.Label(UILocalization.Get("realm_distribution"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
		int[] tierDist = GetTierDist();
		string[] array = new string[13]
		{
			UILocalization.GetTierName(1),
			UILocalization.GetTierName(2),
			UILocalization.GetTierName(3),
			UILocalization.GetTierName(4),
			UILocalization.GetTierName(5),
			UILocalization.GetTierName(6),
			UILocalization.GetTierName(7),
			UILocalization.GetTierName(8),
			UILocalization.GetTierName(9),
			UILocalization.GetTierName(10),
			UILocalization.GetTierName(11),
			UILocalization.GetTierName(12),
			UILocalization.GetTierName(13)
		};
		int num7 = 1;
		for (int i = 0; i < 13; i++)
		{
			if (tierDist[i] > num7)
			{
				num7 = tierDist[i];
			}
		}
		float num8 = Mathf.Clamp(((_windowRect).width - 246f) * 0.3f, 100f, 250f);
		for (int j = 0; j < 13; j++)
		{
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Label($"{j + 1,2}. {array[j]}: {tierDist[j]}", (GUILayoutOption[])(object)new GUILayoutOption[2]
			{
				GUILayout.Width(num3),
				GUILayout.Height((float)num2)
			});
			GUI.color = DSUITheme.GetTierColor(j + 1);
			GUILayout.Box("", (GUILayoutOption[])(object)new GUILayoutOption[2]
			{
				GUILayout.Width((float)tierDist[j] / (float)num7 * num8),
				GUILayout.Height((float)(num2 - 6))
			});
			GUI.color = Color.white;
			GUILayout.EndHorizontal();
		}
		GUILayout.Space(SpacingMedium);
		DrawSeparator();
		GUILayout.Label(string.Format(UILocalization.Get("global_counts_fmt"), SectManager.GetAllSects().Count, DisasterManager.GetRiftCount(), Code.Dimension.DimensionRealmManager.GetInhabitantCount()), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
		GUILayout.Label(string.Format(UILocalization.Get("barrier_status_fmt"), DisasterManager.GetBarrierIntegrity(), DisasterManager.GetBarrierStatus()), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });

		// === 分隔线 + 境界建筑建造区 ===
		GUILayout.Space(SpacingMedium);
		DrawSeparator();
		GUILayout.Label("\U0001f3d7\ufe0f 境界建筑", new GUIStyle(GUI.skin.label) { fontSize = _currentFontSize, fontStyle = FontStyle.Bold });

		try
		{
// 			if (Code.Core.DengShenConfig.EnableRealmBuildings && World.world != null && World.world.units != null)
			{
				Actor bestBuilder = null;
				float bestEnergy = 0f;
				foreach (var u in World.world.units.units_only_alive)
				{
					if (u == null || !u.isAlive()) continue;
					int tier = Code.Data.CultivationData.GetRealmTier(u);
					if (tier < 3) continue;
					float energy = Code.Data.CultivationData.GetEnergy(u);
					if (energy > bestEnergy) { bestEnergy = energy; bestBuilder = u; }
				}

// 				if (bestBuilder != null)
// 				{
// 					int builderTier = Code.Data.CultivationData.GetRealmTier(bestBuilder);
// 					GUILayout.Label(string.Format("建造者: {0} (T{1}, 能量{2:F0})", bestBuilder.getName(), builderTier, bestEnergy), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(20f) });
// 
// // 					var buildings = Code.Realm.RealmBuildings.GetBuildingsForTier(builderTier);
// 					if (buildings != null && buildings.Count > 0)
// 					{
// 						foreach (var bld in buildings)
// 						{
// 							bool canBuild = bestEnergy >= bld.BuildCost;
// 							string btnLabel = string.Format("{0} ({1}{2})", bld.Name, bld.BuildCost, UILocalization.Get("skill_energy_unit"));
// 							GUI.enabled = canBuild;
// 							if (GUILayout.Button(btnLabel, GUILayout.Height(22)))
// 							{
// 								float bx = bestBuilder.current_position.x;
// 								float by = bestBuilder.current_position.y;
// // 								bool success = Code.Realm.RealmBuildings.BuildBuilding(bestBuilder, bld.Id, bx, by);
// 								if (success) DSDebug.Verbose($"[神力栏] {bestBuilder.getName()} 建造了: {bld.Name}");
// 							}
// 							GUI.enabled = true;
// 						}
// 					}
// 					else
// 					{
// 						GUILayout.Label("（当前境界无可建建筑）", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(18f) });
// 					}
// 				}
// 				else
// 				{
// 					GUILayout.Label("（没有3阶以上异能者作为建造者）", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(18f) });
// 				}
			}
		}
		catch (Exception bldEx)
		{
			DSDebug.Verbose($"[神力栏] 建筑区渲染跳过: {bldEx.Message}");
		}

		GUILayout.EndScrollView();
	}


	private void DrawTabSect()
	{
		_genericScroll = GUILayout.BeginScrollView(_genericScroll, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(ContentHeight) });
		List<SectData> allSects = SectManager.GetAllSects();
		int num = _currentFontSize + 6;
		int num2 = _currentFontSize + 8;
		float num3 = Mathf.Clamp(((_windowRect).width - 246f) * 0.25f, 100f, 200f);
		GUILayout.Label(string.Format(UILocalization.Get("sect_total_fmt"), allSects.Count), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
		int num4 = 0;
		foreach (SectData item in allSects)
		{
			num4 += item.MemberIds.Count;
			}
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		DrawInfoRow("\ud83d\udccb " + UILocalization.Get("total_members"), num4.ToString(), "#FFD700");
		GUILayout.EndHorizontal();
		GUILayout.Space(SpacingMedium);
		DrawSeparator();
		GUILayout.Label("\ud83c\udfdb\ufe0f " + UILocalization.Get("found_sect_title"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		_newSectName = GUILayout.TextField(_newSectName, (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(num3),
			GUILayout.Height((float)num2)
		});
		GUI.color = new Color(0.6f, 0.8f, 1f);
		if (GUILayout.Button(UILocalization.Get("random_name"), (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(80f),
			GUILayout.Height((float)num2)
		}))
		{
			_newSectName = SectManager.GenerateRandomSectName();
		}
		GUI.color = Color.white;
		GUI.color = new Color(0.4f, 1f, 0.6f);
		if (GUILayout.Button(UILocalization.Get("random_found"), (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(100f),
			GUILayout.Height((float)num2)
		}))
		{
			List<Actor> list = new List<Actor>();
			foreach (long activeId in GetActiveIds())
			{
				Actor val = FindActor(activeId);
				if (val != null)
				{
					int realmTier = CultivationData.GetRealmTier(val);
					string sectId = CultivationData.GetSectId(val);
					if (realmTier >= 1 && string.IsNullOrEmpty(sectId))
					{
						list.Add(val);
					}
				}
			}
			if (list.Count > 0)
			{
				Actor val2 = list[UnityEngine.Random.Range(0, list.Count)];
				string text = (string.IsNullOrEmpty(_newSectName) || _newSectName == UILocalization.Get("new_sect_name")) ? SectManager.GenerateRandomSectName() : _newSectName;
				SectData sectData = SectManager.FoundSect(val2, text);
				SetStatus((sectData != null) ? string.Format(UILocalization.Get("sect_founded_by"), val2.getName(), text) : UILocalization.Get("found_fail"));
				_newSectName = "";
			}
			else
			{
				SetStatus(UILocalization.Get("no_available_user"));
			}
		}
		GUI.color = Color.white;
		GUILayout.EndHorizontal();
		GUILayout.Space(SpacingMedium);
		DrawSeparator();
		GUILayout.Label(string.Format("\ud83c\udfdb\ufe0f {0} ({1})", UILocalization.Get("sect_list"), allSects.Count), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
		if (allSects.Count == 0)
		{
			GUILayout.Label(UILocalization.Get("no_sect_hint"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
		}
		foreach (SectData item2 in allSects)
		{
			GUILayout.BeginVertical(GUI.skin.box, Array.Empty<GUILayoutOption>());
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			// 组织名可点击：点击展开/收起组织详情（含创始人完整称号）
			bool isExpanded = _selectedSectId == item2.Id;
			GUI.color = isExpanded ? new Color(0.4f, 0.8f, 1f) : Color.white;
			if (GUILayout.Button("\ud83c\udfdb\ufe0f <b>" + item2.Name + "</b>" + (isExpanded ? " ▲" : " ▼"), GUI.skin.label, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)(num + 4)) }))
			{
				_selectedSectId = isExpanded ? null : item2.Id;
			}
			GUI.color = Color.white;
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			Actor val4 = FindActor(item2.MasterId);
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Label(UILocalization.Get("sect_leader"), (GUILayoutOption[])(object)new GUILayoutOption[2]
			{
				GUILayout.Width(50f),
				GUILayout.Height((float)num)
			});
			if (val4 != null)
			{
				GUI.color = new Color(0.4f, 0.8f, 1f);
				if (GUILayout.Button(val4.getName(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)(num - 2)) }))
				{
					ActionLibrary.openUnitWindow(val4);
				}
				GUI.color = Color.white;
			}
			else
			{
				GUILayout.Label(UILocalization.Get("none_label"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
			}
			GUILayout.FlexibleSpace();
			if (!string.IsNullOrEmpty(item2.AncestorName))
			{
				// 列表简洁显示：纯名字+境界，不显示称号（称号在组织详情中显示）
				string founderBaseName = Code.Realm.TitleSuffixManager.ExtractBaseName(item2.AncestorName);
				string founderTierName = (item2.AncestorTier > 0) ? UILocalization.GetTierName(item2.AncestorTier) : "";
				string founderDisplay = string.IsNullOrEmpty(founderTierName) ? founderBaseName : string.Format("{0}（{1}）", founderBaseName, founderTierName);
				GUILayout.Label(string.Format(UILocalization.Get("sect_founder"), founderDisplay), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
			}
			else
			{
				// 传承断裂：创始人名已失传，显示世界观标记（含断裂次数）
				GUILayout.Label(string.Format(UILocalization.Get("sect_founder_lost"), Mathf.Max(1, item2.Breaks)), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
			}
			GUILayout.Label(string.Format(UILocalization.Get("sect_founded"), item2.FoundingYear), (GUILayoutOption[])(object)new GUILayoutOption[2] { GUILayout.Width(95f), GUILayout.Height((float)num) });
			GUILayout.Label(string.Format(UILocalization.Get("sect_heritage"), item2.HeritageGen), (GUILayoutOption[])(object)new GUILayoutOption[2] { GUILayout.Width(95f), GUILayout.Height((float)num) });
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Label(string.Format(UILocalization.Get("sect_members"), item2.MemberIds.Count), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
			GUILayout.Label(string.Format(UILocalization.Get("sect_contribution"), item2.TotalContribution), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
			GUILayout.EndHorizontal();
			// 展开详情：创始人完整称号（含称号）+ 成员列表
			if (_selectedSectId == item2.Id)
			{
				GUILayout.Space(4f);
				DrawSeparator();
				// 创始人完整称号（含称号，列表中不显示的在这里显示）
				if (!string.IsNullOrEmpty(item2.AncestorName))
				{
					GUILayout.Label(string.Format(UILocalization.Get("sect_founder_full"), item2.AncestorName), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
				}
				// 成员列表
				GUILayout.Label(UILocalization.Get("sect_member_list"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)num) });
				foreach (long memberId in item2.MemberIds)
				{
					Actor member = FindActor(memberId);
					if (member != null)
					{
						int memberRank = Data.CultivationData.GetSectRank(member);
						string memberRankName = memberRank switch
						{
							1 => UILocalization.Get("sect_rank_probation"),
							2 => UILocalization.Get("sect_rank_member"),
							3 => UILocalization.Get("sect_rank_senior"),
							4 => UILocalization.Get("sect_rank_captain"),
							5 => UILocalization.Get("sect_rank_commander"),
							6 => UILocalization.Get("sect_rank_leader"),
							7 => UILocalization.Get("sect_rank_deputy"),
							8 => UILocalization.Get("sect_rank_guardian"),
							9 => UILocalization.Get("sect_rank_advisor"),
							10 => UILocalization.Get("sect_rank_heritor"),
							11 => UILocalization.Get("sect_rank_founder"),
							_ => UILocalization.Get("sect_rank_member")
						};
						GUI.color = new Color(0.4f, 0.8f, 1f);
						if (GUILayout.Button("  " + member.getName() + "（" + memberRankName + "）", GUI.skin.label, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)(num - 2)) }))
						{
							ActionLibrary.openUnitWindow(member);
						}
						GUI.color = Color.white;
					}
				}
			}
			GUILayout.Space(12f);
			GUILayout.EndVertical();
		}
			GUILayout.EndScrollView();
		}

	private void DrawTabDisaster()
	{
		_genericScroll = GUILayout.BeginScrollView(_genericScroll, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(ContentHeight) });
		float barrierIntegrity = DisasterManager.GetBarrierIntegrity();
		float barrierDecayRate = DisasterManager.GetBarrierDecayRate();
		List<VoidRift> activeRifts = DisasterManager.GetActiveRifts();
		GUILayout.Label(UILocalization.Get("heaven_barrier"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(20f) });
		GUILayout.Label(string.Format(UILocalization.Get("barrier_integrity_fmt"), barrierIntegrity, DisasterManager.GetBarrierStatus()), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(18f) });
		DrawBar(barrierIntegrity / 100f, (barrierIntegrity >= 60f) ? Color.green : ((barrierIntegrity >= 30f) ? Color.yellow : Color.red));
		GUILayout.Label(string.Format(UILocalization.Get("annual_decay_fmt"), barrierDecayRate), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(18f) });
		GUILayout.Label(string.Format(UILocalization.Get("rift_threshold"), DisasterManager.GetRiftSpawnThreshold()), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(18f) });
		GUILayout.Space(12f);
		DrawSeparator();
		GUILayout.Label(string.Format(UILocalization.Get("void_rifts_fmt"), activeRifts.Count), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(20f) });
		if (activeRifts.Count == 0)
		{
			GUILayout.Label(UILocalization.Get("no_rifts"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(18f) });
		}
		else
		{
			foreach (VoidRift item in activeRifts)
			{
				GUILayout.BeginVertical(GUI.skin.box, Array.Empty<GUILayoutOption>());
				GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
				GUILayout.Label(string.Format(UILocalization.Get("rift_info_fmt"), item.Id, item.X, item.Y), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(20f) });
				GUILayout.FlexibleSpace();
				GUI.color = new Color(0.4f, 0.7f, 1f);
				if (GUILayout.Button(UILocalization.Get("jump_btn"), (GUILayoutOption[])(object)new GUILayoutOption[2]
				{
					GUILayout.Width(70f),
					GUILayout.Height(20f)
				}))
				{
					JumpToPosition(item.X, item.Y);
					SetStatus(string.Format(UILocalization.Get("jump_to_rift"), item.Id));
				}
				GUI.color = Color.white;
				GUILayout.EndHorizontal();
				GUILayout.Label(string.Format(UILocalization.Get("rift_detail_fmt"), item.Power, item.Duration, item.BirthYear), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(20f) });
				float num = Mathf.Clamp01(item.Power / 100f);
				Rect rect = GUILayoutUtility.GetRect(0f, 6f, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
				GUI.color = new Color(0.2f, 0.1f, 0.3f);
				GUI.DrawTexture(rect, (Texture)(object)Texture2D.whiteTexture);
				GUI.color = Color.Lerp(new Color(0.5f, 0f, 0.8f), new Color(1f, 0.2f, 0.2f), num);
				GUI.DrawTexture(new Rect((rect).x, (rect).y, (rect).width * num, (rect).height), (Texture)(object)Texture2D.whiteTexture);
				GUI.color = Color.white;
				GUILayout.EndVertical();
				GUILayout.Space(10f);
			}
		}
		GUILayout.Space(12f);
		DrawSeparator();
		GUIStyle disasterS = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
		GUILayout.Label(UILocalization.Get("disaster_1"), disasterS);
		GUILayout.Label(UILocalization.Get("disaster_2"), disasterS);
		GUILayout.Label(UILocalization.Get("disaster_3"), disasterS);
		GUILayout.Label(UILocalization.Get("disaster_4"), disasterS);
		GUILayout.EndScrollView();
	}

	private void DrawConfigField(string label, ref string field, Action<float> apply)
	{
		int num = _currentFontSize + 8;
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUILayout.Label(label + ":", (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(120f),
			GUILayout.Height((float)num)
		});
		field = GUILayout.TextField(field, (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(80f),
			GUILayout.Height((float)num)
		});
		if (GUILayout.Button(new GUIContent(UILocalization.Get("apply"), UILocalization.Get("apply_tip")), (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(50f),
			GUILayout.Height((float)num)
		}))
		{
			if (float.TryParse(field, out var result))
			{
				apply(result);
				SetStatus(string.Format(UILocalization.Get("config_set_fmt"), label, result));
			}
			else
			{
				SetStatus(UILocalization.Get("invalid_value"));
			}
		}
		GUILayout.EndHorizontal();
	}

	private bool TipButton(string textKey, string tipKey, params GUILayoutOption[] options)
	{
		return GUILayout.Button(new GUIContent(UILocalization.Get(textKey), UILocalization.Get(tipKey)), options);
	}

	private void DrawCultivatorTraits(Actor actor)
	{
		if (actor == null || actor.traits == null)
		{
			GUILayout.Label(UILocalization.Get("no_traits"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(20f) });
			return;
		}
		int num = 0;
		foreach (ActorTrait trait in actor.traits)
		{
			string text = ((trait != null) ? ((Asset)trait).id : "");
			if (string.IsNullOrEmpty(text) || !text.StartsWith("ds_"))
			{
				continue;
			}
			num++;
			string text2 = text;
			try
			{
				ActorTrait val = ((AssetLibrary<ActorTrait>)(object)AssetManager.traits).get(text);
				if (val != null)
				{
					text2 = ((Asset)val).id;
					if (!string.IsNullOrEmpty(((BaseAugmentationAsset)val).special_locale_id))
					{
						text2 = ((BaseAugmentationAsset)val).special_locale_id;
					}
				}
			}
			catch
			{
			}
			string traitDisplayName = GetTraitDisplayName(text);
			string traitDisplayDesc = GetTraitDisplayDesc(text);
			GUILayout.BeginVertical(GUI.skin.box, Array.Empty<GUILayoutOption>());
			GUILayout.Label(string.Format(UILocalization.Get("trait_item_fmt"), traitDisplayName), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(20f) });
			if (!string.IsNullOrEmpty(traitDisplayDesc))
			{
				GUILayout.Label(traitDisplayDesc, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(14f) });
			}
			GUILayout.EndVertical();
			GUILayout.Space(10f);
		}
		if (num == 0)
		{
			GUILayout.Label(UILocalization.Get("no_cultivator_traits"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(20f) });
		}
	}

	private static string GetTraitDisplayName(string traitId)
	{
		if (traitId.StartsWith("ds_tier_"))
		{
			string[] array = traitId.Split('_');
			if (array.Length >= 3 && int.TryParse(array[2], out var result))
			{
				return UILocalization.GetTierName(result);
			}
		}
		return traitId;
	}

	private static string GetTraitDisplayDesc(string traitId)
	{
		return traitId switch
		{
			"ds_tier_1_ganqi" => UILocalization.Get("trait_desc_tier_1"), 
			"ds_tier_5_hengyuan" => UILocalization.Get("trait_desc_tier_5"), 
			"ds_tier_6_lingyu" => UILocalization.Get("trait_desc_tier_6"), 
			"ds_tier_8_mingdao" => UILocalization.Get("trait_desc_tier_8"), 
			"ds_tier_11_sanctuary" => UILocalization.Get("trait_desc_tier_11"), 
			"ds_tier_12_ascended" => UILocalization.Get("trait_desc_tier_12"), 
			"ds_tier_13_truegod" => UILocalization.Get("trait_desc_tier_13"), 
			_ => "", 
		};
	}

	private void DrawSeparator()
	{
		GUILayout.Space(10f);
		Rect rect = GUILayoutUtility.GetRect(0f, 1f, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		Color color = GUI.color;
		GUI.color = new Color(0.4f, 0.5f, 0.7f, 0.5f);
		GUI.DrawTexture(rect, (Texture)(object)Texture2D.whiteTexture);
		GUI.color = color;
		GUILayout.Space(10f);
	}

	private void DrawInfoRow(string label, string value, string valueColor = "#E8D080", int fontSize = 0)
	{
		int num = ((fontSize > 0) ? fontSize : _currentFontSize);
		string text = "<color=#8A8490>" + label + ":</color> <color=" + valueColor + ">" + value + "</color>";
		int fontSize2 = GUI.skin.label.fontSize;
		bool richText = GUI.skin.label.richText;
		GUI.skin.label.fontSize = num;
		GUI.skin.label.richText = true;
		GUILayout.Label(text, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)(num + 8)) });
		GUI.skin.label.fontSize = fontSize2;
		GUI.skin.label.richText = richText;
	}

	private void DrawRealmProgress(int currentTier)
	{
		if (currentTier > 0)
		{
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			for (int i = 1; i <= 13; i++)
			{
				bool flag = i <= currentTier;
				bool flag2 = i == currentTier;
				Color color = (Color)(flag ? DSUITheme.GetTierColor(i) : new Color(0.3f, 0.3f, 0.35f, 0.5f));
				GUI.color = color;
				string text = (flag2 ? $"[{i}]" : (flag ? $"{i}" : "·"));
				GUILayout.Box(text, (GUILayoutOption[])(object)new GUILayoutOption[2]
				{
					GUILayout.Width(28f),
					GUILayout.Height(22f)
				});
				GUI.color = Color.white;
			}
			GUILayout.EndHorizontal();
			string tierName = GetTierName(currentTier);
			string arg = ColorUtility.ToHtmlStringRGB(DSUITheme.GetTierColor(currentTier));
			int fontSize = GUI.skin.label.fontSize;
			bool richText = GUI.skin.label.richText;
			GUI.skin.label.fontSize = _currentFontSize - 1;
			GUI.skin.label.richText = true;
			GUILayout.Label(string.Format("<color=#{0}>" + UILocalization.Get("current_realm_fmt") + "</color>", arg, currentTier, tierName), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)(_currentFontSize + 2)) });
			GUI.skin.label.fontSize = fontSize;
			GUI.skin.label.richText = richText;
		}
	}

	private void DrawSectionTitle(string title)
	{
		string text = "<color=#C0A060><b> " + title + " </b></color>";
		int fontSize = GUI.skin.label.fontSize;
		bool richText = GUI.skin.label.richText;
		GUI.skin.label.fontSize = _currentFontSize + 1;
		GUI.skin.label.richText = true;
		GUILayout.Label(text, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)(_currentFontSize + 6)) });
		GUI.skin.label.fontSize = fontSize;
		GUI.skin.label.richText = richText;
	}

	private void DrawBar(float ratio, Color color)
	{
		ratio = Mathf.Clamp01(ratio);
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUILayout.Box("", (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(150f),
			GUILayout.Height(8f)
		});
		Rect lastRect = GUILayoutUtility.GetLastRect();
		GUI.color = color;
		GUI.DrawTexture(new Rect((lastRect).x + 1f, (lastRect).y + 1f, ((lastRect).width - 2f) * ratio, (lastRect).height - 2f), (Texture)(object)Texture2D.whiteTexture);
		GUI.color = Color.white;
		GUILayout.EndHorizontal();
	}

	private void SetStatus(string msg)
	{
		_statusMessage = msg;
		_statusTimer = 3f;
	}


	private static List<long> GetActiveIds()
	{
		FieldInfo field = typeof(AnnualTickManager).GetField("_activeCultivatorIds", BindingFlags.Static | BindingFlags.NonPublic);
		return (field != null) ? ((field.GetValue(null) as List<long>) ?? new List<long>()) : new List<long>();
	}

	private void JumpToActor(Actor actor)
	{
		if (actor == null)
		{
			return;
		}
		try
		{
			JumpToPosition(((BaseSimObject)actor).current_position.x, ((BaseSimObject)actor).current_position.y);
			try
			{
				if (World.world != null && World.world.units != null)
				{
					FieldInfo field = typeof(World).GetField("selected_actor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (field != null)
					{
						field.SetValue(World.world, actor);
					}
				}
			}
			catch
			{
			}
		}
		catch (Exception ex)
		{
			DSDebug.Warning("[DivineAscension] JumpToActor Error: " + ex.Message);
		}
	}

	private void JumpToPosition(float x, float y)
	{
		try
		{
			if (Camera.main != null)
			{
				Vector3 position = new Vector3(x, y, ((Component)Camera.main).transform.position.z);
				((Component)Camera.main).transform.position = position;
			}
			if (!(MapBox.instance != null))
			{
				return;
			}
			FieldInfo field = typeof(MapBox).GetField("mainCamera", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				object value = field.GetValue(MapBox.instance);
				Camera val = (Camera)((value is Camera) ? value : null);
				if (val != null)
				{
					((Component)val).transform.position = new Vector3(x, y, ((Component)val).transform.position.z);
				}
			}
		}
		catch (Exception ex)
		{
			DSDebug.Warning("[DivineAscension] JumpToPosition Error: " + ex.Message);
		}
	}

	private static Actor FindActor(long id)
	{
		return SystemManagerExtensions.FindActorById(id);
	}

	private static string GetTierName(int tier)
	{
		return UILocalization.GetTierName(tier);
	}

	private void DrawTabDivine()
	{
		DSGUIHelper.Title(UILocalization.Get("dimension_title"), _currentFontSize + 2);
		DSGUIHelper.Separator();
		int num = 0;
		int num2 = 0;
		foreach (long activeId in GetActiveIds())
		{
			Actor val = FindActor(activeId);
			if (val != null)
			{
				int realmTier = CultivationData.GetRealmTier(val);
				if (realmTier == 12)
				{
					num++;
				}
				if (realmTier == 13)
				{
					num2++;
				}
			}
		}
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUI.color = DSUITheme.AccentGold;
		GUILayout.Label(string.Format(UILocalization.Get("ascended_count"), num), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		GUI.color = new Color(1f, 0.85f, 0.4f);
		GUILayout.Label(string.Format(UILocalization.Get("truegod_count"), num2), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		GUI.color = Color.white;
		GUILayout.EndHorizontal();
		GUILayout.Space(SpacingMedium);

		// === 当前世界真神列表 ===
		DSGUIHelper.ColoredLabel("当前世界真神级", DSUITheme.AccentGold, _currentFontSize, (FontStyle)0);
		int rowH = _currentFontSize + 6;
		bool hasActiveGods = false;
		GUILayout.BeginVertical((GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		foreach (long activeId in GetActiveIds())
		{
			Actor god = FindActor(activeId);
			if (god != null && CultivationData.GetRealmTier(god) >= 13)
			{
				hasActiveGods = true;
				GUILayout.BeginHorizontal((GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)rowH) });
				GUILayout.Label(string.Format(UILocalization.Get("codex_true_god_row"), god.getName(), god.getAge()), GUILayout.Width(220));
				GUILayout.EndHorizontal();
			}
		}
		if (!hasActiveGods)
		{
			GUILayout.Label(UILocalization.Get("codex_no_true_god"), GUILayout.Height((float)rowH));
		}
		GUILayout.EndVertical();
		GUILayout.Space(SpacingMedium);
		DSGUIHelper.ColoredLabel(UILocalization.Get("dimension_opened"), DSUITheme.AccentGold, _currentFontSize, (FontStyle)0);

		// 圣所（真神名单）展示面板唯一归属，调试面板已移除重复
		DSGUIHelper.ColoredLabel("圣所（跨存档）", DSUITheme.AccentGold, _currentFontSize, (FontStyle)0);
		List<Code.Realm.TrueGodManager.TrueGodData> godsList = Code.Realm.TrueGodManager.GetAllTrueGods();
		int pantheonRowH = _currentFontSize + 6;
		if (godsList != null && godsList.Count > 0)
		{
			GUILayout.BeginVertical((GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
			foreach (Code.Realm.TrueGodManager.TrueGodData g in godsList)
			{
				GUILayout.BeginHorizontal((GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)pantheonRowH) });
				GUILayout.Label(string.Format("{0}  |  T{1}  |  {2}" + UILocalization.Get("codex_years"), g.Name, g.RealmTier, g.YearsLived), GUILayout.Width(200));
				if (GUILayout.Button(UILocalization.Get("codex_spawn"), GUILayout.Width(60), GUILayout.Height(pantheonRowH)))
				{
					Code.Realm.TrueGodManager.SpawnTrueGod(g);
				}
				if (GUILayout.Button(UILocalization.Get("codex_delete"), GUILayout.Width(60), GUILayout.Height(pantheonRowH)))
				{
					Code.Realm.TrueGodManager.DeleteTrueGod(g.Name, g.OriginWorld);
				}
				GUILayout.EndHorizontal();
			}
			GUILayout.EndVertical();
		}
		else
		{
			GUILayout.Label(UILocalization.Get("none_label"), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height((float)pantheonRowH) });
		}
	}

	private int[] GetTierDist()
	{
		int[] array = new int[13];
		foreach (long activeId in GetActiveIds())
		{
			Actor val = FindActor(activeId);
			if (val != null)
			{
				int realmTier = CultivationData.GetRealmTier(val);
				if (realmTier >= 1 && realmTier <= 13)
				{
					array[realmTier - 1]++;
				}
			}
		}
		return array;
	}

	public AbilityCodexPanel()
	{
		_visible = false;
		_wasVisible = false;
		_windowRect = new Rect(20f, 20f, 850f, 700f);
		_isResizing = false;
		_currentTab = Tab.RealmCodex;
		_listScroll = Vector2.zero;
		_detailScroll = Vector2.zero;
		_selectedActorId = -1L;
		_genericScroll = Vector2.zero;
		_selectedRealmTier = -1;
		_newSectName = UILocalization.Get("new_sect_name");
		_statusMessage = "";
		_statusTimer = 0f;
		_lastRescanTime = 0f;
		// 自动字体大小默认14
		_currentFontSize = 14;
}
}






