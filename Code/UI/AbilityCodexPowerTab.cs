// ============================================================

using System.Collections.Generic;
using Code.Core;
using Code.Data;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using UnityEngine;

namespace Code.UI
{
    public static class AbilityCodexPowerTab
    {
        private const string TAB_ID = "ds_divine_library_tab";
        private static bool _initialized = false;
        private static PowersTab _tab;
        private static AbilityCodexPanel _panelRef;
        private static DebugPanel _debugPanelRef;

        /// <summary>注册神力栏tab（在ModClass.OnModLoad中调用）</summary>
        public static void Register(AbilityCodexPanel panel, DebugPanel debugPanel = null)
        {
            if (_initialized) return;
            _panelRef = panel;
            _debugPanelRef = debugPanel;

            try
            {
                // 加载自定义图标（现代异能风格）
                Sprite tabIcon = TryLoadSprite("ui/ds_tab_icon", "ui/iconBook");
                Sprite openIcon = TryLoadSprite("ui/ds_open_library", "ui/iconBook");
                Sprite debugIcon = TryLoadSprite("ui/ds_debug_panel", "ui/iconBook");
                Sprite dimSpaceIcon = TryLoadSprite("ui/ds_dimension_space", "ui/iconBook");

                // NML封装API创建tab（第2、3参数是本地化key，不是直接文字）
                // 第5参数为tooltip底部灰字本地化key（不传会使用默认hotkey_tip_tab_other，显示"热键: [Tab]"误导玩家）
                _tab = TabManager.CreateTab(
                    TAB_ID,
                    "ds_power_tab_title",        // 标题本地化key
                    "ds_power_tab_description",   // 描述本地化key
                    tabIcon,
                    "ds_power_tab_hotkey"         // 底部热键提示本地化key（显示真实热键[Shift+L]）
                );

                if (_tab == null)
                {
                    DSDebug.Warning("[DivineAscension] TabManager.CreateTab返回null，神力栏tab注册失败。id=" + TAB_ID);
                    return;
                }

                DSDebug.Verbose("TabManager.CreateTab成功，tab=" + _tab.GetType().Name);

                // 设置布局分组
                _tab.SetLayout(new List<string> { "tools" });
                DSDebug.Verbose("SetLayout成功");

                // === tools组：打开面板（避免快捷键被其他模组冲突） ===
                PowerButton openPanelBtn = PowerButtonCreator.CreateSimpleButton(
                    "ds_open_library",
                    () => TogglePanel(),
                    openIcon
                );
                SetPowerButtonTooltip(openPanelBtn, UILocalization.Get("tab_open_codex"), "Open Ability Codex Panel");
                _tab.AddPowerButton("tools", openPanelBtn);
                DSDebug.Verbose("添加打开图鉴面板按钮成功");

                // 打开调试面板按钮（避免快捷键F3被其他模组冲突）
                PowerButton debugPanelBtn = PowerButtonCreator.CreateSimpleButton(
                    "ds_open_debug_panel",
                    () => ToggleDebugPanel(),
                    debugIcon
                );
                SetPowerButtonTooltip(debugPanelBtn, UILocalization.Get("tab_open_debug"), "Open Debug Panel");
                _tab.AddPowerButton("tools", debugPanelBtn);
                DSDebug.Verbose("添加调试面板按钮成功");

                // 维度空间按钮（宏观管控面板，12阶以上可用）
                PowerButton dimSpaceBtn = PowerButtonCreator.CreateSimpleButton(
                    "ds_open_dimension_space",
                    () => ToggleDimensionSpace(),
                    dimSpaceIcon
                );
                SetPowerButtonTooltip(dimSpaceBtn, UILocalization.Get("tab_open_dimension"), "Open Dimension Space [Shift+B]");
                _tab.AddPowerButton("tools", dimSpaceBtn);
                DSDebug.Verbose("添加维度空间按钮成功");

                // 建筑放置按钮（已删除建筑系统）
                // try
                // {
                //     var allPowers = AssetManager.powers.list;
                //     foreach (var power in allPowers)
                //     {
                //         if (power.id.StartsWith("ds_building_") && !string.IsNullOrEmpty(power.drop_id))
                //         {
                //             PowerButton buildBtn = PowerButtonCreator.CreateGodPowerButton(power.id, null);
                //             SetPowerButtonTooltip(buildBtn, power.id.Underscore(), power.id);
                //             _tab.AddPowerButton("tools", buildBtn);
                //             DSDebug.Verbose("添加建筑按钮: " + power.id);
                //         }
                //     }
                // }
                // catch (System.Exception ex)
                // {
                //     DSDebug.Warning("添加建筑按钮失败: " + ex.Message);
                // }

                // === actions组：快捷操作 ===

                // 更新布局
                _tab.UpdateLayout();

                _initialized = true;
                DSDebug.Verbose("神力栏tab注册成功（NML TabManager）: " + TAB_ID + "，按钮已添加，UpdateLayout已调用");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("[DivineAscension] 神力栏tab注册异常: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>切换异能图鉴面板显示</summary>
        private static void TogglePanel()
        {
            if (_panelRef == null) return;
            try
            {
                var field = typeof(AbilityCodexPanel).GetField("_visible",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    bool current = (bool)field.GetValue(_panelRef);
                    field.SetValue(_panelRef, !current);
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("[DivineAscension] 切换面板异常: " + e.Message);
            }
        }

        /// <summary>
        /// 给PowerButton添加TipButton组件并设置悬停提示
        /// </summary>
        private static void SetPowerButtonTooltip(PowerButton button, string zhText, string enText)
        {
            if (button == null || button.gameObject == null) return;
            try
            {
                string tipText = UILocalization.CurrentLanguage == "en" ? enText : zhText;
                
                // 给PowerButton的GameObject添加TipButton组件（游戏原生提示组件）
                var tipButton = button.gameObject.GetComponent<TipButton>();
                if (tipButton == null)
                {
                    tipButton = button.gameObject.AddComponent<TipButton>();
                }
                
                if (tipButton != null)
                {
                    tipButton.textOnClick = tipText;
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Verbose("设置PowerButton悬停提示失败: " + e.Message);
            }
        }

        /// <summary>切换调试面板显示</summary>
        private static void ToggleDebugPanel()
        {
            if (_debugPanelRef == null) return;
            try
            {
                var field = typeof(DebugPanel).GetField("_visible",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    bool current = (bool)field.GetValue(_debugPanelRef);
                    field.SetValue(_debugPanelRef, !current);
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("[DivineAscension] 切换调试面板异常: " + e.Message);
            }
        }

        /// <summary>切换维度空间面板显示（不需要选中单位，直接打开宏观维度空间）</summary>
        // /// <summary>切换建筑放置模式</summary>
        // private static void ToggleBuildMode()
        // {
        //     if (BuildPlacementMode.IsActive)
        //     {
        //         BuildPlacementMode.Stop();
        //     }
        //     else
        //     {
        //         Actor selected = null;
        //         var field = typeof(World).GetField("selected_actor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        //         if (field != null) selected = field.GetValue(World.world) as Actor;
        //         if (selected == null || !selected.isAlive())
        //         {
        //             DSDebug.Warning("[DivineAscension] 请先选中一个单位");
        //             return;
        //         }
        //         BuildPlacementMode.Start(selected);
        //     }
        // }

        private static void ToggleDimensionSpace()
        {
            try
            {
                // 直接打开维度空间（不需要12阶以上单位，在空间中可以选择单位）
                // 如果有选中单位，则自动选中该单位
                Actor selectedActor = null;
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
                            var getActorMethod = unitWindowType.GetMethod("GetActor",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (getActorMethod != null)
                            {
                                selectedActor = getActorMethod.Invoke(unitWindow, null) as Actor;
                            }
                        }
                    }
                }
                catch { }

                if (selectedActor != null && selectedActor.isAlive())
                {
                    DimensionRealmWindow.Toggle(selectedActor);
                }
                else
                {
                    DimensionRealmWindow.Toggle();
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("[DivineAscension] 切换维度空间异常: " + e.Message);
            }
        }



        /// <summary>尝试加载Sprite，失败返回null</summary>
        private static Sprite TryLoadSprite(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                try
                {
                    Sprite sprite = SpriteTextureLoader.getSprite(key);
                    if (sprite != null) return sprite;
                }
                catch { }
            }
            return null;
        }
    }
}




