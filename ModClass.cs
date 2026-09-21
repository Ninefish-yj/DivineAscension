// ============================================================
// 文件: ModClass.cs
// 模组: 登神长阶 (DengShenChangJie)
// 职责: NeoModLoader入口，注册Trait、启动年度Tick驱动、加载配置
// 禁令遵守:
//   [禁令1] 不覆写原生底层  仅注册新资产、监听事件、附加独立组件
//   [禁令6] 不跨存档互通  所有数据随世界存档，读档时重置状态
// ============================================================

using System;
using NeoModLoader.api;
using UnityEngine;
using HarmonyLib;
using Code.Core;
using Code.Disaster;
using Code.Dimension;
using Code.Events;
using Code.Sect;
using Code.Traits;
using Code.UI;

namespace Code
{
    public class ModClass : BasicMod<ModClass>
    {
        private static DebugPanel _debugPanel;
        private static AbilityCodexPanel _divinePanel;
        private static AnnualTickDriver _tickDriver;
        private static Harmony _harmony;

        /// <summary>重置年度Tick时间追踪（读档/新世界时调用，防止跨档时间错乱）</summary>
        public static void ResetAnnualTickTracker()
        {
            if (_tickDriver != null)
            {
                _tickDriver.ResetTracker();
            }
        }


        protected override void OnModLoad()
        {
            // 0. 初始化Actor数据反射访问器（解决运行时actor.data字段不可访问问题）
            Data.ActorDataAccessor.Init();

            // 0.1 加载模组配置管理器（功能开关、UI设置等）
            Core.DSConfigManager.LoadConfig();

            // 1. 加载全局配置
            LoadCultivationConfig();

            // 2. 注册全部异能Trait（13阶境界 + 4组织 + 20异能，体系和状态改为元数据/状态栏）
            TraitManager.RegisterAllTraits();


            // 2.1 创建Harmony实例，Patch所有带[HarmonyPatch]特性的类
            //     包括：ActorUpdateAgePatch（年度Tick驱动）、UnitWindowIntegration（单位窗口境界行）
            _harmony = new Harmony("DengShenChangJie");
            _harmony.PatchAll();

            // 2.2 初始化鼠标事件拦截器（Unity UI Overlay Canvas，防止面板点击穿透到游戏）
            UI.MouseOverlayBlocker.EnsureInitialized();

            // 2.3 激活Harmony Patch输入拦截（参考WorldBoxAIMod，直接拦截游戏输入方法）
            UI.InputBlocker.Patch();

            // 3. 启动开发调试面板（F3切换显示）
            GameObject uiObj = new GameObject("[DengShen] DebugPanel");
            UnityEngine.Object.DontDestroyOnLoad(uiObj);
            _debugPanel = uiObj.AddComponent<DebugPanel>();

            // 4. 启动登神道藏综合面板（Shift+L切换显示，玩家主界面）
            GameObject divineObj = new GameObject("[DengShen] DivineLibrary");
            UnityEngine.Object.DontDestroyOnLoad(divineObj);
            _divinePanel = divineObj.AddComponent<AbilityCodexPanel>();

            // 4.0.1 启动基因图谱弹窗（点击单位窗口的基因图谱按钮打开）
            UI.GeneGraphWindow.Ensure();

            // 4.0 启动全局年度Tick驱动（MonoBehaviour，每帧检查时间，不遍历单位）
            //      这是主要的年度Tick触发方式，年龄驱动作为补充
            GameObject tickObj = new GameObject("[DengShen] AnnualTickDriver");
            UnityEngine.Object.DontDestroyOnLoad(tickObj);
            _tickDriver = tickObj.AddComponent<AnnualTickDriver>();

            // 4.1 先初始化境界专属内容（建筑要先注册，神力栏才能找到建筑按钮）
            Realm.RealmAbilities.Init();
            Realm.DSSkillEffectManager.Init();
            Realm.RealmEvents.Init();
//             Realm.RealmBuildings.Init();
            Realm.RealmFeatures.Init();

            // 4.2 注册神力栏tab（NML TabManager，点击按钮打开登神道藏/强制结算等）
            UI.AbilityCodexPowerTab.Register(_divinePanel, _debugPanel);

            // 5. 初始化事件钩子（on_death监听，杀戮产生紊值）
            EventHooks.Init();

            // 6. 初始化各子系统（组织、灾变、维度）
            SectManager.Init();
            DisasterManager.Init();
            Code.Dimension.DimensionRealmManager.Init();
            Code.Realm.VanillaInteractionManager.Init();

            // 6.5.0.6 初始化纪元桥接（突破真神级/超神级时自动切换原版时代）
            Code.Realm.EraBridge.Init();

            // 6.5.0.7 接入原版BuildOrder系统：把所有建筑注册到城市建造队列，工人自动来建
//             Code.Realm.BuildOrderBridge.Init();
//             Code.Realm.BuildOrderBridge.RegisterBuilding("ds_building_awakening_altar", 5);      // 觉醒祭坛
//             Code.Realm.BuildOrderBridge.RegisterBuilding("ds_building_energy_well", 10);         // 能量井
//             Code.Realm.BuildOrderBridge.RegisterBuilding("ds_building_domain_tower", 15);        // 场域塔
//             Code.Realm.BuildOrderBridge.RegisterBuilding("ds_building_law_hall", 20);           // 规则殿
//             Code.Realm.BuildOrderBridge.RegisterBuilding("ds_building_space_anchor", 25);         // 空间锚点
//             Code.Realm.BuildOrderBridge.RegisterBuilding("ds_building_dimensional_gate", 30);       // 维度之门
//             Code.Realm.BuildOrderBridge.RegisterBuilding("ds_building_sanctuary", 40);           // 圣所
//             Code.Realm.BuildOrderBridge.RegisterBuilding("ds_building_divine_temple", 50);               // 神殿
            // 关键修复：之前漏了这行，订单集合根本没提交到 city_build_orders，城市不知道有这个订单
//             Code.Realm.BuildOrderBridge.CommitToLibrary();

            // 6.5.1 初始化技能状态效果系统（StatusAsset注册 + 战斗计算Patch）
            Core.DSStatusAsset.Init();
            
            // 6.5.4 注册登神级种族特质（4阶方碑进化/11阶登神级升华需要；提前注册避免运行时动态add）
            Realm.TranscendentSpeciesSystem.RegisterTraits();

            // 6.5.5 初始化基因图谱晋升系统（54个基因节点定义）
            Realm.GeneGraphManager.Init();

            // 6.5.6 初始化真神跨存档互通系统
            Code.Realm.TrueGodManager.Init();

            // 6.6 初始化历史系统和屏幕通知系统（接入WorldBox原生WorldLog/WorldTip）
            Core.DSHistoryManager.Init();
            Core.DSNotificationManager.Init();

            // 6.6.1 初始化先驱者存档补丁（首位突破记录跨存档持久化）
            Code.Realm.PioneerSavePatcher.Init();

            // 6.6.2 初始化组织灭门档案补丁（死灰复燃·限时，随存档独立存储）
            Code.Sect.SectArchivePatcher.Init();


            // 6.7 初始化事件管理器（统一管理模组事件和日志，依赖历史/通知系统）
            DSEventManager.Init();

            // 7. 监听世界加载事件（读档/新世界时重置全部状态）
            // 使用反射访问private字段 on_world_loaded
            try
            {
                var worldLoadedField = typeof(MapBox).GetField("on_world_loaded",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (worldLoadedField != null)
                {
                    var handler = (System.Action)worldLoadedField.GetValue(null);
                    handler += OnWorldLoaded;
                    worldLoadedField.SetValue(null, handler);
                    DSDebug.Verbose("[登神长阶] 世界加载事件监听已挂载");
                }
                else
                {
                    DSDebug.Verbose("[登神长阶] 未找到on_world_loaded字段，使用轮询实现");
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("[登神长阶] 挂载世界加载事件失败: " + e.Message);
            }

            LogInfo("[登神长阶] 模组加载完成");
        }

        private void OnWorldLoaded()
        {
            AnnualTickManager.ResetYearTracker();
            // 重置年度Tick驱动
            if (_tickDriver != null) _tickDriver.ResetTracker();
            // 重置先驱者记录（每个新世界重新记录各境界先驱者）
            Realm.RealmJudge.ResetPioneers();
            // 重置纪元（每个新世界从第1纪元开始）
            Realm.RealmJudge.ResetEras();
            SectManager.Init();
            DisasterManager.Init();
            Code.Dimension.DimensionRealmManager.Init();
            // 读档后重建维度居民列表（Init的_initialized防重会跳过，必须显式重建，
            // 否则已在维度空间的单位丢失年度修炼加成/失控消解）
            Code.Dimension.DimensionRealmManager.RebuildInhabitants();
            // 重建境界计数缓存（游戏加载时调用一次）
            Realm.RealmJudge.RebuildTierCountCache();
            
            // 修剪溢出的高阶单位（两者结合方案：超过软上限时杀掉最弱小的）
            Realm.RealmJudge.TrimTierOverflow();
            DSDebug.Verbose("[登神长阶] 世界加载完成");
        }

        /// <summary>从模组配置文件加载全局可调参数（文档19.2节）</summary>
        private void LoadCultivationConfig()
        {
            try
            {
                var config = GetConfig();
                if (config == null) return;

                var defaultSection = config["ConfigDengShenChangJie"];
                if (defaultSection == null) return;

                // 修炼倍率（范围限制：0.1-100）
                var rateItem = defaultSection["CultivationRateMultiplier"];
                if (rateItem != null && float.TryParse(rateItem.GetValue() as string, out float rate))
                {
                    CultivationConfig.CultivationRateMultiplier = Mathf.Clamp(rate, 0.1f, 100f);
                }

                // 失控指数衰减速率（范围限制：0.01-10）
                var decayItem = defaultSection["TurbulenceDecayRate"];
                if (decayItem != null && float.TryParse(decayItem.GetValue() as string, out float decay))
                {
                    CultivationConfig.TurbulenceDecayRate = Mathf.Clamp(decay, 0.01f, 10f);
                }

                // 凡人觉醒概率（范围限制：0.001-1）
                var awakenItem = defaultSection["MortalAwakenChance"];
                if (awakenItem != null && float.TryParse(awakenItem.GetValue() as string, out float awaken))
                {
                    CultivationConfig.MortalAwakenChance = Mathf.Clamp(awaken, 0.001f, 1f);
                }

                // 注意：PerformanceDiagnostics配置项已移除，默认关闭（AnnualTickManager.PerformanceDiagnosticsEnabled = false）
                // 如果需要启用性能诊断，请在代码中手动设置，或在配置文件中添加对应配置项
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("[登神长阶] 配置加载失败: " + e.Message);
            }
        }
    }
}


