using System;
using Code.Core;
using System.Collections.Generic;
using Code.UI;
using UnityEngine;
using Code.Data;
using Code.Realm;
using Code.Traits;

namespace Code.UI
{
	/// <summary>
	/// 基因图谱窗口 - 左侧个人信息+操作，右侧基因列表
	/// 包含基因觉醒功能（修炼的起点）
	/// </summary>
	public class GeneGraphWindow
	{
		#region 静态实例
		private static GeneGraphWindow _instance;
		public static GeneGraphWindow Instance => _instance ?? (_instance = new GeneGraphWindow());
		#endregion

		#region 常量
		private const float WINDOW_WIDTH = 1000f;
		private const float WINDOW_HEIGHT = 750f;
		private const float TITLE_HEIGHT = 36f;
		private const float LEFT_PANEL_WIDTH = 280f;
		private const float DETAIL_HEIGHT = 130f;
		private const float ELEMENT_HEIGHT = 48f;
		private const float CATEGORY_HEIGHT = 26f;
		#endregion

		#region 实例变量
		private Rect _windowRect;
		private bool _visible;
		private Actor _selectedActor;
		private int _selectedElementIndex = -1;
		private Vector2 _scrollPosition;
		private bool _isDragging;
		private Vector2 _dragStartMouse;
		private Vector2 _dragStartWindowPos;
		private Texture2D _bgTex;
		private Texture2D _titleBgTex;
		private Texture2D _leftPanelBgTex;
		private Texture2D _elementBgTex;
		private Texture2D _elementUnlockedBgTex;
		private Texture2D _categoryBgTex;
		private Texture2D _detailBgTex;
		private Texture2D _awakenBtnTex;
		// 神创基因编辑输入框（13个自定义数值，默认"0"）
		private string _divineAtkText = "0";
		private string _divineDefText = "0";
		private string _divineSpdText = "0";
		private string _divineHpText = "0";
		private string _divineMaxEnText = "0";
		private string _divineEnRecText = "0";
		private string _divineCultText = "0";
		private string _divineStabText = "0";
		private string _divineCdText = "0";
		private string _divineDmgRedText = "0";
		private string _divineCritText = "0";
		private string _divineLifestealText = "0";
		private string _divineHealText = "0";
		private bool _divineTextsLoaded = false;
		#endregion

		#region 静态方法
		public static void Ensure()
		{
			if (_instance == null) _instance = new GeneGraphWindow();
		}

		public static void Show(Actor actor)
		{
			bool wasVisible = Instance._visible;
			Instance._selectedActor = actor;
			Instance._visible = true;
			if (!wasVisible) MouseOverlayBlocker.RequestBlock();
			Instance._selectedElementIndex = -1;
			Instance._scrollPosition = Vector2.zero;
			Instance._divineTextsLoaded = false; // 切换单位时重新从存档加载神创数值
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
			if (_leftPanelBgTex == null) _leftPanelBgTex = MakeSolidTexture(2, 2, new Color(0.13f, 0.14f, 0.18f, 0.95f));
			if (_elementBgTex == null) _elementBgTex = MakeSolidTexture(2, 2, new Color(0.15f, 0.16f, 0.20f, 0.9f));
			if (_elementUnlockedBgTex == null) _elementUnlockedBgTex = MakeSolidTexture(2, 2, new Color(0.22f, 0.24f, 0.30f, 0.95f));
			if (_categoryBgTex == null) _categoryBgTex = MakeSolidTexture(2, 2, new Color(0.20f, 0.21f, 0.26f, 0.95f));
			if (_detailBgTex == null) _detailBgTex = MakeSolidTexture(2, 2, new Color(0.13f, 0.14f, 0.17f, 0.95f));
			if (_awakenBtnTex == null) _awakenBtnTex = MakeSolidTexture(2, 2, new Color(0.45f, 0.50f, 0.62f, 0.9f));
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

			// 窗口拖动（标题栏区域，排除关闭按钮）
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


			// 窗口背景
			GUI.DrawTexture(_windowRect, _bgTex);

			// 标题栏
			GUI.DrawTexture(titleRect, _titleBgTex);
			GUI.color = DSUITheme.TitleText;
			GUI.Label(new Rect(titleRect.x + 12, titleRect.y + 8, titleRect.width - 60, 24),
				GetTitleText(), new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold });
			GUI.color = Color.white;

			// 关闭按钮
			GUI.color = new Color(0.9f, 0.3f, 0.3f);
			if (GUI.Button(closeRect, "×", new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold }))
			{
				MouseOverlayBlocker.ReleaseBlock();
				_visible = false;
				return;
			}
			GUI.color = Color.white;

			// 内容区（流式自动排布）
			float contentY = _windowRect.y + TITLE_HEIGHT + 6;
			float contentHeight = _windowRect.height - TITLE_HEIGHT - DETAIL_HEIGHT - 18;
			Rect contentArea = new Rect(_windowRect.x + 6, contentY, _windowRect.width - 12, contentHeight + DETAIL_HEIGHT - 6f);
			GUILayout.BeginArea(contentArea);
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			// 左栏：个人信息180px
			GUILayout.BeginVertical(GUILayout.Width(180f), GUILayout.ExpandHeight(true));
			DrawLeftPanel();
			GUILayout.EndVertical();
			GUILayout.Space(6f);
			// 中栏：详情自适应宽
			GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			DrawElementDetail(contentHeight);
			GUILayout.EndVertical();
			GUILayout.Space(6f);
			// 右栏：基因列表320px
			GUILayout.BeginVertical(GUILayout.Width(300f), GUILayout.ExpandHeight(true));
			DrawElementList(contentHeight);
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			GUILayout.EndArea();

			// 同步UGUI拦截区域（与图鉴面板同款MouseOverlayBlocker机制），阻止鼠标穿透到游戏世界
			MouseOverlayBlocker.UpdateBlockArea(_windowRect, e != null && _windowRect.Contains(e.mousePosition)); // 仅鼠标在窗口内时阻挡 UGUI（离开窗口立即放行地图操作）
			InputBlocker.ReportWindowRect(_windowRect); // 上报窗口矩形给InputBlocker（Harmony拦截点击选中/滚轮缩放）

			// 兜底拦截：窗口内的所有鼠标事件一律消费，防止穿透到地图与下层UI
			// （已由ScrollView/按钮消费的事件重复Use无副作用；窗口外的事件不受影响）
			// 注意：地图拖动/右键检视等Input轮询输入由InputBlocker的Harmony补丁拦截
			if (_windowRect.Contains(e.mousePosition) &&
				(e.type == EventType.ScrollWheel ||
				 e.type == EventType.MouseDown ||
				 e.type == EventType.MouseUp ||
				 e.type == EventType.MouseDrag ||
				 e.type == EventType.DragUpdated ||
				 e.type == EventType.DragPerform ||
				 e.type == EventType.DragExited))
			{
				e.Use();
			}
		}
		#endregion

		#region 左侧个人信息面板
		private void DrawLeftPanel()
		{
			GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
			panelStyle.normal.background = _leftPanelBgTex;
			panelStyle.padding = new RectOffset(10, 10, 8, 8);
			GUILayout.BeginVertical(panelStyle, (GUILayoutOption[])(object)new GUILayoutOption[2] { GUILayout.Width(LEFT_PANEL_WIDTH), GUILayout.ExpandHeight(true) });

			// === 单位基本信息 ===
			GUILayout.Label(_selectedActor.name ?? "Unknown", new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = DSUITheme.TitleText } });
			GUILayout.Space(4f);

			int realm = TraitManager.GetCurrentRealmTier(_selectedActor);
                string realmName = realm > 0 ? Code.UI.UnitWindowIntegration.GetTierName(realm) : UILocalization.Get("graph_mortal");
			GUILayout.Label(UILocalization.Get("graph_tier_label") + realmName, new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.7f, 0.9f, 1f) } });
			GUILayout.Label(UILocalization.Get("graph_energy_label") + FormatNumber(CultivationData.GetEnergy(_selectedActor)), new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1f, 0.85f, 0.6f) } });
			float enlightExp = CultivationData.GetEnlightenmentExp(_selectedActor);
			GUILayout.Label(UILocalization.Get("graph_deep_awakening") + FormatNumber(enlightExp) + UILocalization.Get("graph_god_requirement"), new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.7f, 0.55f, 1f) } });

			var unlockedElements = CultivationData.GetUnlockedElements(_selectedActor);
			int unlockedCount = unlockedElements != null ? unlockedElements.Count : 0;
			GUILayout.Label(UILocalization.Get("graph_unlocked_elements") + unlockedCount + "/56", new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.8f, 1f, 0.7f) } });

			float corruption = CultivationData.GetTurbulence(_selectedActor);
			GUILayout.Label(UILocalization.Get("graph_turbulence_label") + corruption.ToString("F0"), new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = corruption > 70 ? new Color(1f, 0.4f, 0.4f) : new Color(0.8f, 0.8f, 0.6f) } });

			// 组织称号（仅显示在基因图谱面板，不写入名字）
			string dsSectId = CultivationData.GetSectId(_selectedActor);
			if (!string.IsNullOrEmpty(dsSectId))
			{
				string dsSectName = "";
				var dsSects = Code.Sect.SectManager.GetAllSects();
				if (dsSects != null)
				{
					foreach (var ds in dsSects) { if (ds.Id == dsSectId) { dsSectName = ds.Name; break; } }
				}
				int dsRank = CultivationData.GetSectRank(_selectedActor);
				string dsRankName = dsRank > 0 ? Code.Sect.SectManager.GetRankName(dsRank) : "";
				string dsSectText = string.IsNullOrEmpty(dsSectName) ? dsSectId : dsSectName;
				if (!string.IsNullOrEmpty(dsRankName)) dsSectText += "（" + dsRankName + "）";
				float cultBoost = Code.Core.EnergyTurbulenceCalculator.GetSectCultivationBoost(_selectedActor);
				if (cultBoost > 0f)
				{
					dsSectText += string.Format(UILocalization.Get("sect_cult_boost"), Mathf.RoundToInt(cultBoost * 100f));
				}
				GUILayout.Label(UILocalization.Get("graph_sect") + dsSectText, new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.6f, 0.95f, 0.9f) } });
			}
			GUILayout.Space(6f);

			// 分隔线
			Rect sepRect = GUILayoutUtility.GetRect(1f, 1f, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
			GUI.DrawTexture(sepRect, _awakenBtnTex);
			GUILayout.Space(6f);

			// === 基因觉醒 ===
			bool isAwakened = CultivationData.IsAwakened(_selectedActor);
			if (isAwakened)
			{
				GUIStyle awakStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
				awakStyle.normal.textColor = DSUITheme.SuccessGreen;
				GUILayout.Label(UILocalization.Get("graph_gene_awakened"), awakStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(36f) });
			}
			else
			{
				GUI.color = new Color(0.8f, 0.5f, 1f, 0.8f);
				if (GUILayout.Button(UILocalization.Get("graph_gene_awakening"), new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold }, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(36f) }))
				{
					PerformGeneAwakening();
				}
				GUI.color = Color.white;
			}

			GUILayout.Label(isAwakened ? UILocalization.Get("graph_awakening_desc1") : UILocalization.Get("graph_awakening_desc2"), new GUIStyle(GUI.skin.label) { fontSize = 9, wordWrap = true });

			// === 玩家操作按钮 ===
			GUI.color = new Color(0.4f, 0.7f, 1f, 0.7f);
			if (GUILayout.Button(UILocalization.Get("graph_unlock_random"), new GUIStyle(GUI.skin.button) { fontSize = 11 })) UnlockRandomElement();
			GUI.color = Color.white;

			bool canAscend = realm > 0 && realm < 13;
			GUI.color = canAscend ? new Color(1f, 0.7f, 0.4f, 0.7f) : new Color(0.4f, 0.4f, 0.4f, 0.5f);
			if (GUILayout.Button(UILocalization.Get("graph_try_advance"), new GUIStyle(GUI.skin.button) { fontSize = 11 })) { if (canAscend) TryAscend(); }
			GUI.color = Color.white;

			GUI.color = new Color(0.8f, 0.4f, 0.4f, 0.6f);
			if (GUILayout.Button(UILocalization.Get("graph_reset_elements"), new GUIStyle(GUI.skin.button) { fontSize = 11 })) ResetElements();
			GUI.color = Color.white;

			// === 图谱进度（所有节点解锁比例） ===
			{
				var allNodes = Code.Realm.GeneGraphManager.GetAllNodes();
				int unlockedHere = 0;
				foreach (var nd in allNodes.Values) if (CultivationData.IsGeneUnlocked(_selectedActor, nd.Id)) unlockedHere++;
				float progPct = allNodes.Values.Count > 0 ? (float)unlockedHere / allNodes.Values.Count : 0f;
				string progText = UILocalization.CurrentLanguage == "en"
					? string.Format("Graph progress: {0}/{1} ({2:F0}%)", unlockedHere, allNodes.Values.Count, progPct * 100f)
					: string.Format(UILocalization.Get("graph_progress"), unlockedHere, allNodes.Values.Count, progPct * 100f);
				GUILayout.Label(progText, new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.7f, 0.85f, 0.7f) } });
			}

			// === 晋升条件提示 ===
			GUILayout.Space(4f);
			GUILayout.Label(GetAscensionHint(realm, unlockedCount), new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Italic, wordWrap = true, normal = { textColor = new Color(0.85f, 0.75f, 0.95f) } });

			GUILayout.EndVertical();
		}

		/// <summary>执行基因觉醒</summary>
		private void PerformGeneAwakening()
		{
			if (_selectedActor == null) return;
			
			//  设定：基因觉醒是修炼的起点，觉醒动作本身不再额外给予基因或随机基因
			CultivationData.SetAwakened(_selectedActor, true);
			
			// 如果还不是异能者，设为1阶觉醒者
			if (TraitManager.GetCurrentRealmTier(_selectedActor) <= 0)
			{
				TraitManager.SetRealmTrait(_selectedActor, 1);
			}
		}

		/// <summary>随机解锁一个基因</summary>
		private void UnlockRandomElement()
		{
			if (_selectedActor == null) return;
			
			var allElements = ElementDef.AllElements;
			var unlocked = CultivationData.GetUnlockedElements(_selectedActor);
			
			// 找未解锁的基因
			var available = new List<ElementDef.ElementInfo>();
			foreach (var elem in allElements)
			{
				if (unlocked == null || !unlocked.Contains(elem.Id))
				{
					available.Add(elem);
				}
			}
			
			if (available.Count > 0)
			{
				var randomElem = available[UnityEngine.Random.Range(0, available.Count)];
				CultivationData.UnlockElement(_selectedActor, randomElem.Id);
				DSDebug.Verbose(string.Format(UILocalization.Get("graph_unlock_element"), _selectedActor.name, randomElem.NameZh));
				
				// 检查基因连锁
				GeneLinkageSystem.CheckAllLinkages(_selectedActor);
			}
		}

		/// <summary>尝试晋升</summary>
		private void TryAscend()
		{
			if (_selectedActor == null) return;
			int currentTier = TraitManager.GetCurrentRealmTier(_selectedActor);
			if (currentTier <= 0 || currentTier >= 13) return;
			
			// 检查晋升条件
			if (GeneGraphManager.AreAllCoreNodesUnlocked(_selectedActor, currentTier + 1))
			{
				// 直接调用完整的突破流程，和正常突破一样
				Code.Realm.RealmJudge.DoBreakthrough(_selectedActor);
				int nextTier = currentTier + 1;
				DSDebug.Verbose(string.Format(UILocalization.Get("graph_advance_success"), _selectedActor.name, currentTier, nextTier));
			}
			else
			{
				DSDebug.Verbose(string.Format(UILocalization.Get("graph_advance_failed"), _selectedActor.name));
			}
		}

		/// <summary>重置基因</summary>
		private void ResetElements()
		{
			if (_selectedActor == null) return;
			CultivationData.ClearElements(_selectedActor);
			_selectedElementIndex = -1;
			DSDebug.Verbose(string.Format(UILocalization.Get("graph_reset_success"), _selectedActor.name));
		}

		/// <summary>从存档加载神创基因13个数值到输入框（首次选中时调用）</summary>
		private void LoadDivineTexts()
		{
			if (_selectedActor == null) return;
			_divineAtkText = CultivationData.GetDivineFloat(_selectedActor, "divine_atk", 0f).ToString("F0");
			_divineDefText = CultivationData.GetDivineFloat(_selectedActor, "divine_def", 0f).ToString("F0");
			_divineSpdText = CultivationData.GetDivineFloat(_selectedActor, "divine_spd", 0f).ToString("F0");
			_divineHpText = CultivationData.GetDivineFloat(_selectedActor, "divine_hp", 0f).ToString("F0");
			_divineMaxEnText = CultivationData.GetDivineFloat(_selectedActor, "divine_maxen", 0f).ToString("F0");
			_divineEnRecText = CultivationData.GetDivineFloat(_selectedActor, "divine_enrec", 0f).ToString("F0");
			_divineCultText = CultivationData.GetDivineFloat(_selectedActor, "divine_cult", 0f).ToString("F0");
			_divineStabText = CultivationData.GetDivineFloat(_selectedActor, "divine_stab", 0f).ToString("F0");
			_divineCdText = CultivationData.GetDivineFloat(_selectedActor, "divine_cd", 0f).ToString("F0");
			_divineDmgRedText = CultivationData.GetDivineFloat(_selectedActor, "divine_dmgred", 0f).ToString("F0");
			_divineCritText = CultivationData.GetDivineFloat(_selectedActor, "divine_crit", 0f).ToString("F0");
			_divineLifestealText = CultivationData.GetDivineFloat(_selectedActor, "divine_lifesteal", 0f).ToString("F0");
			_divineHealText = CultivationData.GetDivineFloat(_selectedActor, "divine_heal", 0f).ToString("F0");
			_divineTextsLoaded = true;
		}

		/// <summary>解析输入框数值（非法输入按0处理）</summary>
		private static float ParseDivineValue(string text)
		{
			float result;
			if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result))
				return result;
			return 0f;
		}

		/// <summary>保存13个输入框数值到存档（不点保存不生效）</summary>
		private void SaveDivineTexts()
		{
			if (_selectedActor == null) return;
			CultivationData.SetDivineFloat(_selectedActor, "divine_atk", ParseDivineValue(_divineAtkText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_def", ParseDivineValue(_divineDefText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_spd", ParseDivineValue(_divineSpdText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_hp", ParseDivineValue(_divineHpText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_maxen", ParseDivineValue(_divineMaxEnText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_enrec", ParseDivineValue(_divineEnRecText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_cult", ParseDivineValue(_divineCultText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_stab", ParseDivineValue(_divineStabText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_cd", ParseDivineValue(_divineCdText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_dmgred", ParseDivineValue(_divineDmgRedText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_crit", ParseDivineValue(_divineCritText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_lifesteal", ParseDivineValue(_divineLifestealText));
			CultivationData.SetDivineFloat(_selectedActor, "divine_heal", ParseDivineValue(_divineHealText));
		}
		#endregion

		#region 绘制基因列表
		private void DrawElementList(float height)
		{
			GUILayout.BeginVertical((GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(height) });
			_scrollPosition = GUILayout.BeginScrollView(_scrollPosition, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
			var allElements = ElementDef.AllElements;
			var unlockedElements = CultivationData.GetUnlockedElements(_selectedActor);

			for (int i = 0; i < allElements.Count; i++)
			{
				var elem = allElements[i];
				int geneGroup = elem.Group;
				Color categoryColor = GetGroupColor(geneGroup);
				bool isUnlocked = unlockedElements != null && unlockedElements.Contains(elem.Id);
				bool isSelected = _selectedElementIndex == i;

				Rect elementRect = GUILayoutUtility.GetRect(1f, ELEMENT_HEIGHT - 4f, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });

				if (isSelected)
				{
					GUI.DrawTexture(elementRect, MakeSolidTexture(2, 2, new Color(categoryColor.r * 0.5f, categoryColor.g * 0.5f, categoryColor.b * 0.5f, 0.95f)));
				}
				else if (isUnlocked)
				{
					GUI.DrawTexture(elementRect, _elementUnlockedBgTex);
				}
				else
				{
					GUI.DrawTexture(elementRect, _elementBgTex);
				}

				GUI.color = isUnlocked ? categoryColor : new Color(categoryColor.r * 0.4f, categoryColor.g * 0.4f, categoryColor.b * 0.4f, 0.6f);
				GUI.Label(new Rect(elementRect.x + 6, elementRect.y + 6, 32, 32), elem.Symbol, new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
				GUI.color = Color.white;

				GUI.color = isUnlocked ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.8f);
				string nameText = elem.NameZh + " / " + elem.NameEn;
				if (nameText.Length > 16) nameText = nameText.Substring(0, 16) + "...";
				GUI.Label(new Rect(elementRect.x + 42, elementRect.y + 4, elementRect.width - 100, 18), nameText, new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold });

				string desc = UILocalization.CurrentLanguage == "en" ? elem.DescEn : elem.DescZh;
				if (desc.Length > 30) desc = desc.Substring(0, 30) + "...";
				GUI.color = isUnlocked ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.7f);
				GUI.Label(new Rect(elementRect.x + 42, elementRect.y + 22, elementRect.width - 100, 16), desc, new GUIStyle(GUI.skin.label) { fontSize = 9 });
				GUI.color = Color.white;

				Rect statusRect = new Rect(elementRect.xMax - 55, elementRect.y + 12, 50, 18);
				if (isUnlocked)
				{
					GUI.color = new Color(0.5f, 1f, 0.5f);
					GUI.Label(statusRect, UILocalization.Get("graph_unlocked"), new GUIStyle(GUI.skin.label) { fontSize = 9, fontStyle = FontStyle.Bold });
				}
				else
				{
					GUI.color = new Color(0.6f, 0.6f, 0.6f);
					GUI.Label(statusRect, UILocalization.Get("graph_locked"), new GUIStyle(GUI.skin.label) { fontSize = 9 });
				}
				GUI.color = Color.white;

				if (GUI.Button(elementRect, "", GUIStyle.none))
				{
					_selectedElementIndex = i;
				}
			}
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
		}
		#endregion

		#region 绘制基因详情
		private void DrawElementDetail(float height)
		{
			GUIStyle detailStyle = new GUIStyle(GUI.skin.box);
			detailStyle.normal.background = _detailBgTex;
			detailStyle.padding = new RectOffset(10, 10, 6, 6);
			GUILayout.BeginVertical(detailStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(height) });

			if (_selectedElementIndex < 0 || _selectedElementIndex >= ElementDef.AllElements.Count)
			{
				GUIStyle hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
				hintStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
				GUILayout.Label(UILocalization.CurrentLanguage == "en" ? "Click an element in the list to view details" : UILocalization.Get("graph_click_hint"), hintStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandHeight(true) });
				GUILayout.EndVertical();
				return;
			}

			var elem = ElementDef.AllElements[_selectedElementIndex];

			// 神创基因（第57号，索引56）
			if (_selectedElementIndex == 56)
			{
				GUILayout.Label(UILocalization.Get("divine_gene_title"), new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.95f, 0.85f, 0.5f) } });
				GUILayout.Label(UILocalization.Get("divine_gene_desc"), new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } });
				GUILayout.Space(15f);
				GUILayout.Label(UILocalization.Get("divine_gene_edit_title"), new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold });
				GUILayout.Space(10f);

				// 首次进入时从存档加载当前数值到输入框
				if (_selectedActor != null && !_divineTextsLoaded)
				{
					LoadDivineTexts();
				}

				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_atk"), GUILayout.Width(70f));
				_divineAtkText = GUILayout.TextField(_divineAtkText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_def"), GUILayout.Width(70f));
				_divineDefText = GUILayout.TextField(_divineDefText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_spd"), GUILayout.Width(70f));
				_divineSpdText = GUILayout.TextField(_divineSpdText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_hp"), GUILayout.Width(70f));
				_divineHpText = GUILayout.TextField(_divineHpText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_maxen"), GUILayout.Width(70f));
				_divineMaxEnText = GUILayout.TextField(_divineMaxEnText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_enrec"), GUILayout.Width(70f));
				_divineEnRecText = GUILayout.TextField(_divineEnRecText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_cult"), GUILayout.Width(70f));
				_divineCultText = GUILayout.TextField(_divineCultText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_stab"), GUILayout.Width(70f));
				_divineStabText = GUILayout.TextField(_divineStabText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_cd"), GUILayout.Width(70f));
				_divineCdText = GUILayout.TextField(_divineCdText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_dmgred"), GUILayout.Width(70f));
				_divineDmgRedText = GUILayout.TextField(_divineDmgRedText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_crit"), GUILayout.Width(70f));
				_divineCritText = GUILayout.TextField(_divineCritText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_lifesteal"), GUILayout.Width(70f));
				_divineLifestealText = GUILayout.TextField(_divineLifestealText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal();
				GUILayout.Label(UILocalization.Get("divine_attr_heal"), GUILayout.Width(70f));
				_divineHealText = GUILayout.TextField(_divineHealText, GUILayout.ExpandWidth(true));
				GUILayout.EndHorizontal();
				GUILayout.Space(15f);
				GUI.color = new Color(0.3f, 0.6f, 0.9f, 0.8f);
				if (GUILayout.Button(UILocalization.Get("divine_gene_save"), new GUIStyle(GUI.skin.button) { fontSize = 12, fixedHeight = 30f }))
				{
					// 用我们自己的CultivationData存储（读取13个输入框，非法输入按0处理）
					if (_selectedActor != null)
					{
						SaveDivineTexts();
						DSDebug.Verbose("[DivineAscension] 神创基因数值已保存");
					}
				}
				GUI.color = Color.white;
				GUILayout.EndVertical();
				return;
			}
			Color categoryColor = GetGroupColor(elem.Group);
			bool isUnlocked = CultivationData.IsElementUnlocked(_selectedActor, elem.Id);
			string categoryName = _selectedElementIndex == 56 ? "神明" : GetGroupName(elem.Group);

			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Label(elem.Symbol, new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = categoryColor } }, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(50f) });
			GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
			GUILayout.Label(elem.NameZh + " / " + elem.NameEn, new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
			string catName = _selectedElementIndex == 56 ? "神明" : categoryName;
			GUILayout.Label("【" + catName + "】 ID: " + elem.Id, new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } });
			GUILayout.EndVertical();
			GUILayout.BeginVertical((GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(130f) });
			if (isUnlocked)
			{
				GUILayout.Label(UILocalization.Get("graph_unlocked"), new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.5f, 1f, 0.5f) } });
			}
			else
			{
				GUILayout.Label(UILocalization.Get("graph_locked"), new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.8f, 0.6f, 0.6f) } });
				GUI.color = new Color(0.4f, 0.7f, 1f, 0.7f);
				if (GUILayout.Button(UILocalization.Get("graph_unlock_this"), new GUIStyle(GUI.skin.button) { fontSize = 10 }))
				{
					CultivationData.UnlockElement(_selectedActor, elem.Id);
					GeneLinkageSystem.CheckAllLinkages(_selectedActor);
				}
				GUI.color = Color.white;
			}
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();

			string desc = UILocalization.CurrentLanguage == "en" ? elem.DescEn : elem.DescZh;
			GUILayout.Label(desc, new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } });

			var combos = GeneLinkageSystem.GetLinkageHintsForGene(elem.Id);
			if (combos != null && combos.Count > 0)
			{
				GUILayout.Label(UILocalization.Get("graph_related_combos") + combos.Count, new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.9f, 0.8f, 0.6f) } });
				for (int i = 0; i < Math.Min(3, combos.Count); i++)
				{
					string comboName = combos[i];
					GUILayout.Label(" " + comboName, new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = new Color(0.75f, 0.85f, 1f) } });
				}
			}
			GUILayout.EndVertical();

			// === 基因表达层级显示 ===
			if (_selectedActor != null && CultivationData.IsElementUnlocked(_selectedActor, elem.Id))
			{
				int exprLevel = CultivationData.GetGeneExpressionLevel(_selectedActor, elem.Id);
				string[] levelNames = { "沉默", "低表达", "中表达", "高表达", "过表达", "突变" };
				string[] levelNamesEn = { "Silent", "Low", "Medium", "High", "Over", "Mutant" };
				Color[] levelColors = {
					new Color(0.5f, 0.5f, 0.5f),
					new Color(0.6f, 0.8f, 0.6f),
					new Color(0.4f, 0.8f, 0.4f),
					new Color(0.2f, 0.8f, 0.2f),
					new Color(0.8f, 0.6f, 0.2f),
					new Color(0.9f, 0.3f, 0.3f)
				};
				string levelName = UILocalization.CurrentLanguage == "en" ? levelNamesEn[exprLevel] : levelNames[exprLevel];
				GUILayout.Label("表达层级: " + levelName, new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = levelColors[exprLevel] } });
			}

			// === 效果概览（基因效果数值，贴底显示） ===
			GUILayout.FlexibleSpace();
GUILayout.Label(UILocalization.CurrentLanguage == "en" ? "EFFECT OVERVIEW" : UILocalization.Get("graph_effect_overview"), new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.75f, 0.8f, 0.9f) } });
GUILayout.Label(GetElementEffectText(elem), new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, normal = { textColor = new Color(0.85f, 0.9f, 0.8f) } });
		}
		#endregion

		#region 辅助方法
		private string GetTitleText()
		{
			return UILocalization.CurrentLanguage == "en" 
				? "Gene Map - Element System" 
				: UILocalization.Get("graph_title");
		}

		private string FormatNumber(float num)
		{
			if (num >= 100000000) return (num / 100000000f).ToString("F2") + "亿";
			if (num >= 10000) return (num / 10000f).ToString("F1") + "万";
			return num.ToString("F0");
		}

		private string GetElementEffectText(ElementDef.ElementInfo e)
		{
			if (e == null) return "";
			bool en = UILocalization.CurrentLanguage == "en";
			float v = e.EffectValue;
			
			// 如果选中了单位，考虑基因表达层级的效果倍率
			if (_selectedActor != null && CultivationData.IsElementUnlocked(_selectedActor, e.Id))
			{
				int exprLevel = CultivationData.GetGeneExpressionLevel(_selectedActor, e.Id);
				float[] multipliers = { 0f, 0.25f, 0.5f, 1.0f, 1.5f, 2.0f };
				if (exprLevel >= 0 && exprLevel < multipliers.Length)
				{
					v *= multipliers[exprLevel];
				}
			}
			
			switch (e.EffectType)
			{
				case GeneEffectType.AttackBonus: return (en ? "Attack" : UILocalization.Get("graph_attr_attack")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.DefenseBonus: return (en ? "Defense" : UILocalization.Get("graph_attr_defense")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.SpeedBonus: return (en ? "Speed" : UILocalization.Get("graph_attr_speed")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.HealthBonus: return (en ? "Health" : UILocalization.Get("graph_attr_health")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.EnergyMaxBonus: return (en ? "Energy Max" : UILocalization.Get("graph_attr_energy_max")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.EnergyRegenBonus: return (en ? "Energy Regen" : UILocalization.Get("graph_attr_energy_regen")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.CultivationBonus: return (en ? "Cultivation" : UILocalization.Get("graph_attr_cultivation")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.CorruptionReduction: return (en ? "Corruption" : UILocalization.Get("graph_attr_turbulence")) + " -" + v.ToString("F0");
				case GeneEffectType.CooldownReduction: return (en ? "Cooldown" : UILocalization.Get("graph_attr_cooldown")) + " -" + v.ToString("F0") + "%";
				case GeneEffectType.DamageReduction: return (en ? "Damage Reduction" : UILocalization.Get("graph_attr_damage_reduction")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.CriticalChance: return (en ? "Critical" : UILocalization.Get("graph_attr_crit_rate")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.Lifesteal: return (en ? "Lifesteal" : UILocalization.Get("graph_attr_lifesteal")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.HealBonus: return (en ? "Healing" : UILocalization.Get("graph_attr_healing")) + " +" + v.ToString("F0") + "%";
				case GeneEffectType.SpecialAbility: return en ? "Unlocks passive ability" : UILocalization.Get("graph_attr_passive");
				default: return e.EffectType.ToString();
			}
		}

		private string GetAscensionHint(int realm, int unlockedCount)
		{
			if (realm <= 0)
			{
				return UILocalization.CurrentLanguage == "en" 
					? "Click 'Gene Awakening' to begin cultivation" 
					: UILocalization.Get("graph_start_hint");
			}
			if (realm >= 13)
			{
				return UILocalization.CurrentLanguage == "en" 
					? "Already at highest realm (Apex)" 
					: UILocalization.Get("graph_max_tier");
			}

			int requiredElements = GeneGraphManager.GetRequiredElementsForTier(realm + 1);
			int requiredCombos = GeneGraphManager.GetRequiredCombosForTier(realm + 1);

			if (unlockedCount < requiredElements)
			{
				return string.Format(
					UILocalization.CurrentLanguage == "en" 
						? "Next realm needs {0} elements (have {1})" 
						: UILocalization.Get("graph_next_tier_elements"),
					requiredElements, unlockedCount);
			}

			if (requiredCombos > 0)
			{
				return string.Format(
					UILocalization.CurrentLanguage == "en" 
						? "Next realm needs {0} element combinations" 
						: UILocalization.Get("graph_next_tier_combos"),
					requiredCombos);
			}

			return UILocalization.CurrentLanguage == "en" 
				? "Elements sufficient, click 'Try Ascend' to advance" 
				: UILocalization.Get("graph_ready_advance");
		}
		
		private static Color GetGroupColor(int group)
		{
			Color[] groupColors = {
				new Color(0.8f, 0.4f, 0.4f, 1f),  // 力量 - 红
				new Color(0.4f, 0.8f, 0.4f, 1f),  // 敏捷 - 绿
				new Color(0.4f, 0.6f, 0.8f, 1f),  // 体质 - 蓝
				new Color(0.8f, 0.8f, 0.4f, 1f),  // 智力 - 黄
				new Color(0.8f, 0.4f, 0.8f, 1f),  // 感知 - 紫
				new Color(0.4f, 0.8f, 0.8f, 1f),  // 意志 - 青
				new Color(0.9f, 0.6f, 0.3f, 1f),  // 异能 - 橙
				new Color(0.6f, 0.6f, 0.6f, 1f),  // 潜能 - 灰
			};
			if (group >= 0 && group < groupColors.Length) return groupColors[group];
			return Color.gray;
		}

		private static string GetGroupName(int group)
		{
			string[] groupNames = { "力量", "敏捷", "体质", "智力", "感知", "意志", "异能", "潜能" };
			if (group >= 0 && group < groupNames.Length) return groupNames[group];
			return "未知";
		}
		#endregion
	}
}




