// ============================================================

using System;
using System.Collections.Generic;
using System.Text;
using Code.Core;
using Code.Data;
using Code.Realm;
using Code.Traits;
using UnityEngine;

namespace Code.UI
{
    public class DebugPanel : MonoBehaviour
    {
        private enum Tab { SystemStatus, DebugTools, ActorDebug, GlobalData }

        private bool _visible = false;
                private bool _wasVisible = false;
        private Rect _windowRect = new Rect(20, 20, 520, 500);
        private Tab _currentTab = Tab.SystemStatus;

        private Vector2 _scroll = Vector2.zero;
        private Actor _debugActor = null;
        private List<Actor> _actorListCache = new List<Actor>();
        private bool _actorListDirty = true;
        private Vector2 _actorListScroll = Vector2.zero;
        private string _statusMessage = "";
        private float _statusTimer = 0f;

        // FPS计算
        private float _fpsTimer = 0f;
        private int _fpsCounter = 0;
        private float _currentFps = 0f;

        private static readonly string[] TabNames = { UILocalization.Get("debug_tab_system"), UILocalization.Get("debug_tab_tools"), UILocalization.Get("debug_tab_actor"), UILocalization.Get("debug_tab_global") };

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
                _visible = !_visible;

            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0f) _statusMessage = "";
            }

            _fpsCounter++;
            _fpsTimer += Time.deltaTime;
            if (_fpsTimer >= 1f)
            {
                _currentFps = _fpsCounter / _fpsTimer;
                _fpsCounter = 0;
                _fpsTimer = 0f;
            }
        }

        private void OnGUI()
        {
            // 只在可见状态变化时调用RequestBlock/ReleaseBlock，避免计数混乱
            if (_visible != _wasVisible)
            {
                if (_visible)
                    MouseOverlayBlocker.RequestBlock();
                else
                    MouseOverlayBlocker.ReleaseBlock();
                _wasVisible = _visible;
            }

            if (!_visible)
                return;

            // 检测鼠标是否在面板区域内
            Event e = Event.current;
            bool mouseOverPanel = (e != null && _windowRect.Contains(e.mousePosition));

            // 使用MouseOverlayBlocker拦截鼠标事件
            MouseOverlayBlocker.UpdateBlockArea(_windowRect, mouseOverPanel);

            // 同时也用e.Use()作为双重保险
            if (e != null && mouseOverPanel)
            {
                if (e.type == EventType.MouseDown ||
                    e.type == EventType.MouseUp ||
                    e.type == EventType.MouseDrag ||
                    e.type == EventType.ScrollWheel ||
                    e.type == EventType.MouseMove)
                {
                    e.Use();
                }
            }

            _windowRect = GUI.Window(0xD5C5, _windowRect, DrawWindow, UILocalization.Get("debug_title"));

            //  上报窗口矩形给InputBlocker
            InputBlocker.ReportWindowRect(_windowRect);

            // 二次拦截
            e = Event.current;
            if (e != null && _windowRect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown ||
                    e.type == EventType.MouseUp ||
                    e.type == EventType.MouseDrag ||
                    e.type == EventType.ScrollWheel)
                {
                    e.Use();
                }
            }
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            DrawTabBar();
            GUILayout.Space(10);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            switch (_currentTab)
            {
                case Tab.SystemStatus: DrawSystemStatus(); break;
                case Tab.DebugTools: DrawDebugTools(); break;
                case Tab.ActorDebug: DrawActorDebug(); break;
                case Tab.GlobalData: DrawGlobalData(); break;
                            }

            GUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.Space(10);
                GUI.color = Color.yellow;
                GUILayout.Label(_statusMessage, GUILayout.Height(20));
                GUI.color = Color.white;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < TabNames.Length; i++)
            {
                bool active = ((int)_currentTab == i);
                GUI.color = active ? new Color(0.4f, 0.7f, 1f) : new Color(0.7f, 0.7f, 0.7f);
                if (GUILayout.Button(TabNames[i], GUILayout.Height(26)))
                    _currentTab = (Tab)i;
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();
        }

        // ============================================================
        //  Tab1: 系统状态监控
        // ============================================================
        private void DrawSystemStatus()
        {
            GUILayout.Label(UILocalization.Get("debug_core_status"), GUILayout.Height(22));

            DrawStatusRow(UILocalization.Get("debug_tick_driver"), AnnualTickDriver.TickInitialized ? UILocalization.Get("debug_running") : UILocalization.Get("debug_not_init"),
                AnnualTickDriver.TickInitialized ? Color.green : Color.red);
            DrawStatusRow(UILocalization.Get("debug_next_settle"), $"{60f - (AnnualTickDriver.AccumulatedTime - AnnualTickDriver.LastSettleTime):F1}{UILocalization.Get("unit_day")}", Color.yellow);
            DrawStatusRow(UILocalization.Get("debug_active_cultivators"), AnnualTickManager.GetActiveCultivatorCount().ToString(), Color.green);
            DrawStatusRow(UILocalization.Get("debug_sect_count"), Sect.SectManager.GetAllSects().Count.ToString(), Color.white);
            DrawStatusRow(UILocalization.Get("debug_void_rifts"), Disaster.DisasterManager.GetRiftCount().ToString(), Color.red);
            DrawStatusRow(UILocalization.Get("debug_barrier"), $"{Disaster.DisasterManager.GetBarrierIntegrity():F1}%",
                Disaster.DisasterManager.GetBarrierIntegrity() > 50 ? Color.green : Color.red);
            DrawStatusRow(UILocalization.Get("debug_cult_rate"), $"{CultivationConfig.CultivationRateMultiplier:F1}x", Color.white);
            DrawStatusRow(UILocalization.Get("debug_awaken_chance"), $"{CultivationConfig.MortalAwakenChance * 100:F1}%", Color.white);
            DrawStatusRow(UILocalization.Get("debug_verbose_log"), DSDebug.EnableVerboseLog ? UILocalization.Get("debug_on") : UILocalization.Get("debug_off"),
                DSDebug.EnableVerboseLog ? Color.yellow : Color.gray);

            GUILayout.Space(4);
        }

        // ============================================================
        //  Tab2: 调试工具
        // ============================================================
        private void DrawDebugTools()
        {
            GUILayout.Label(UILocalization.Get("debug_annual_tick"), GUILayout.Height(22));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UILocalization.Get("debug_force_settle"), GUILayout.Height(26)))
            {
                AnnualTickManager.ForceAnnualSettlement();
                SetStatus(UILocalization.Get("debug_status_force_settle"));
            }
            if (GUILayout.Button(UILocalization.Get("debug_rebuild_list"), GUILayout.Height(26)))
            {
                AnnualTickManager.RebuildActiveList();
                SetStatus(UILocalization.Get("debug_status_list_rebuilt"));
            }
            GUILayout.EndHorizontal();


            GUILayout.Space(12);
            GUILayout.Label(UILocalization.Get("debug_batch_awaken"), GUILayout.Height(22));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UILocalization.Get("debug_awaken_10"), GUILayout.Height(26)))
            {
                int count = ForceAwaken(10);
                SetStatus(string.Format(UILocalization.Get("debug_status_awaken_count"), count));
            }
            if (GUILayout.Button(UILocalization.Get("debug_awaken_50"), GUILayout.Height(26)))
            {
                int count = ForceAwaken(50);
                SetStatus(string.Format(UILocalization.Get("debug_status_awaken_count"), count));
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label(UILocalization.Get("debug_log_control"), GUILayout.Height(22));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(DSDebug.EnableVerboseLog ? UILocalization.Get("debug_verbose_off") : UILocalization.Get("debug_verbose_on"), GUILayout.Height(26)))
            {
                DSDebug.EnableVerboseLog = !DSDebug.EnableVerboseLog;
                SetStatus(string.Format(UILocalization.Get("debug_status_verbose_toggle"), DSDebug.EnableVerboseLog ? UILocalization.Get("debug_on") : UILocalization.Get("debug_off")));
            }
            if (GUILayout.Button(UILocalization.Get("debug_clear_log"), GUILayout.Height(26)))
            {
                DSDebug.ClearLogFile();
                SetStatus(UILocalization.Get("debug_status_log_cleared"));
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label(UILocalization.Get("debug_disaster"), GUILayout.Height(22));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UILocalization.Get("debug_gen_rift"), GUILayout.Height(26)))
            {
                Disaster.DisasterManager.SpawnDebugRift(100f, 100f);
                SetStatus(UILocalization.Get("debug_status_rift_spawned"));
            }
            if (GUILayout.Button(UILocalization.Get("debug_repair_barrier"), GUILayout.Height(26)))
            {
                Disaster.DisasterManager.ForceRepairBarrier(100f);
                SetStatus(UILocalization.Get("debug_status_barrier_repaired"));
            }
            GUILayout.EndHorizontal();
        }

        // ============================================================
        //  Tab3: 单位调试
        // ============================================================
        private void DrawActorDebug()
        {
            GUILayout.Label(UILocalization.Get("debug_actor_info"), GUILayout.Height(22));

            // 游戏内选中单位
            Actor gameSelected = GetSelectedActor();
            if (gameSelected != null)
            {
                GUILayout.BeginHorizontal();
                DrawStatusRow(UILocalization.Get("debug_actor_sel"), gameSelected.getName() + " (ID:" + gameSelected.id + ")", Color.cyan);
                if (GUILayout.Button(UILocalization.Get("debug_actor_take"), GUILayout.Width(60), GUILayout.Height(22)))
                {
                    _debugActor = gameSelected;
                    SetStatus(gameSelected.getName());
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label(UILocalization.Get("debug_actor_list"), GUILayout.Height(22));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UILocalization.Get("debug_actor_refresh"), GUILayout.Height(24)))
            {
                RefreshActorList();
                SetStatus(string.Format(UILocalization.Get("debug_actor_count"), _actorListCache.Count));
            }
            GUILayout.EndHorizontal();

            // 单位列表（滚动选择）
            if (_actorListDirty) RefreshActorList();
            if (_actorListCache.Count > 0)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                _actorListScroll = GUILayout.BeginScrollView(_actorListScroll, GUILayout.MaxHeight(180));
                for (int i = 0; i < _actorListCache.Count; i++)
                {
                    Actor a = _actorListCache[i];
                    if (a == null || !a.isAlive()) continue;
                    int tier = CultivationData.GetRealmTier(a);
                    string tierName = tier > 0 ? UILocalization.GetTierName(tier) : "-";
                    GUI.color = (a == _debugActor) ? new Color(0.4f, 0.7f, 1f) : new Color(0.8f, 0.8f, 0.8f);
                    if (GUILayout.Button(string.Format("{0}  |  {1}  |  {2:F0}", a.getName(), tierName, CultivationData.GetEnergy(a)), GUILayout.Height(22)))
                    {
                        _debugActor = a;
                        SetStatus(a.getName());
                    }
                    GUI.color = Color.white;
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }

            GUILayout.Space(10);
            if (_debugActor == null || !_debugActor.isAlive())
            {
                GUILayout.Label(UILocalization.Get("debug_actor_none"), GUILayout.Height(20));
                return;
            }

            Actor actor = _debugActor;
            int curTier = CultivationData.GetRealmTier(actor);
            int geneCount = CultivationData.GetUnlockedElements(actor) != null ? CultivationData.GetUnlockedElements(actor).Count : 0;

            DrawStatusRow("Name", actor.getName() + " (ID:" + actor.id + ")", Color.white);
            DrawStatusRow("Awakened", CultivationData.IsAwakened(actor) ? "Yes" : "No", Color.white);
            DrawStatusRow("Tier", curTier > 0 ? curTier + " - " + UILocalization.GetTierName(curTier) : "Mortal", Color.cyan);
            DrawStatusRow("Qi", CultivationData.GetEnergy(actor).ToString("F0"), Color.yellow);
            DrawStatusRow("Turbulence", CultivationData.GetTurbulence(actor).ToString("F1"), CultivationData.GetTurbulence(actor) >= 70f ? Color.red : Color.green);
            DrawStatusRow("Mastery", CultivationData.GetMastery(actor).ToString("F1") + "/100", Color.cyan);
            DrawStatusRow("DeepAwaken", CultivationData.GetEnlightenmentExp(actor).ToString("F0"), new Color(0.7f, 0.55f, 1f));
            DrawStatusRow("Genes", geneCount.ToString(), Color.white);

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UILocalization.Get("debug_actor_awaken"), GUILayout.Height(26)))
            {
                bool ok = CultivationData.IsAscended(actor) || RealmJudge.AwakenMortal(actor);
                if (ok) AnnualTickManager.RegisterCultivator(actor);
                SetStatus(ok ? "Awaken OK" : "Awaken Fail");
            }
            if (GUILayout.Button(UILocalization.Get("debug_actor_break"), GUILayout.Height(26)))
            {
                var result = RealmJudge.DoBreakthrough(actor);
                SetStatus("Breakthrough: " + result);
            }
            if (GUILayout.Button(UILocalization.Get("debug_actor_enlighten"), GUILayout.Height(26)))
            {
                var result = RealmJudge.DoEnlightenmentBreakthrough(actor);
                SetStatus("Enlighten: " + result);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int t = 1; t <= 13; t += 3)
            {
                int tier = t;
                if (GUILayout.Button(string.Format(UILocalization.Get("debug_actor_set_tier"), tier), GUILayout.Height(26)))
                {
                    ForceSetTier(actor, tier);
                    SetStatus(string.Format("Tier -> {0} {1}", tier, UILocalization.GetTierName(tier)));
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UILocalization.Get("debug_actor_qi_100m"), GUILayout.Height(26)))
            {
                CultivationData.SetEnergy(actor, CultivationData.GetEnergy(actor) + 100000000f);
                SetStatus("Qi +1e8");
            }
            if (GUILayout.Button(UILocalization.Get("debug_actor_qi_fill"), GUILayout.Height(26)))
            {
                float next = RealmJudge.GetQiThreshold(curTier + 1);
                CultivationData.SetEnergy(actor, next);
                SetStatus("Qi -> " + next);
            }
            if (GUILayout.Button(UILocalization.Get("debug_actor_qi_zero"), GUILayout.Height(26)))
            {
                CultivationData.SetEnergy(actor, 0f);
                SetStatus("Qi = 0");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UILocalization.Get("debug_actor_turb20"), GUILayout.Height(26)))
            {
                CultivationData.SetTurbulence(actor, CultivationData.GetTurbulence(actor) + 20f);
                SetStatus("Turb +20");
            }
            if (GUILayout.Button(UILocalization.Get("debug_actor_turb0"), GUILayout.Height(26)))
            {
                CultivationData.SetTurbulence(actor, 0f);
                SetStatus("Turb = 0");
            }
            if (GUILayout.Button(UILocalization.Get("debug_actor_mastery"), GUILayout.Height(26)))
            {
                CultivationData.SetMastery(actor, 100f);
                SetStatus("Mastery = 100");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UILocalization.Get("debug_actor_unlock_all"), GUILayout.Height(26)))
            {
                int cnt = UnlockAllGenes(actor);
                SetStatus("Unlocked " + cnt + " elements");
            }
            if (GUILayout.Button(UILocalization.Get("debug_actor_unlock_truth"), GUILayout.Height(26)))
            {
                CultivationData.UnlockElement(actor, "gene_life_sublimation");
                SetStatus("Truth unlocked");
            }
            if (GUILayout.Button(UILocalization.Get("debug_actor_export"), GUILayout.Height(26)))
            {
                ExportRawData(actor);
                SetStatus("Exported to log");
            }
            GUILayout.EndHorizontal();
        }

        // ============================================================
        //  Tab4: 全局数据
        // ============================================================
        private void DrawGlobalData()
        {
            //  使用全局统计快照，一次性遍历计算所有数据，避免UI多次遍历
            var snap = DSGlobalStats.GetStatsSnapshot();

            // ===== 全局统计卡片（顶部）=====
            GUILayout.Label(UILocalization.Get("debug_global_overview"), GUILayout.Height(22));
            DrawStatusRow(UILocalization.Get("stats_total_awakened"), string.Format("{0} / {1} ({2:F1}%)",
                snap.TotalAwakened, snap.TotalPopulation, snap.AwakenedRatio * 100f), Color.cyan);
            DrawStatusRow(UILocalization.Get("stats_total_breakthroughs"), snap.TotalBreakthroughs.ToString(), Color.yellow);
            DrawStatusRow(UILocalization.Get("stats_total_awakenings"), snap.TotalAwakenings.ToString(), Color.green);
            DrawStatusRow(UILocalization.Get("stats_total_disasters"), snap.TotalDisasters.ToString(), Color.red);
            DrawStatusRow(UILocalization.Get("stats_total_ability_uses"), snap.TotalAbilityUses.ToString(), Color.magenta);
            DrawStatusRow(UILocalization.Get("stats_avg_turbulence"), string.Format("{0:F1}", snap.AverageTurbulence),
                snap.AverageTurbulence >= 50f ? Color.red : Color.green);
            DrawStatusRow(UILocalization.Get("stats_highest_tier"), snap.HighestTierReached > 0
                ? snap.HighestTierReached + " - " + UILocalization.GetTierName(snap.HighestTierReached)
                : "无", Color.yellow);
            DrawStatusRow(UILocalization.Get("stats_total_kills"), string.Format("{0} / {1}", snap.TotalKills, snap.TotalAbilityKills), Color.white);

            // ===== 境界分布（增强：百分比 + 分组统计）=====
            GUILayout.Space(8);
            GUILayout.Label(UILocalization.Get("debug_global_dist"), GUILayout.Height(22));

            // 分组统计
            int lowTier = DSGlobalStats.GetLowTierCount(snap.TierDistribution);
            int midTier = DSGlobalStats.GetMidTierCount(snap.TierDistribution);
            int highTier = DSGlobalStats.GetHighTierCount(snap.TierDistribution);
            int ultraTier = DSGlobalStats.GetUltraTierCount(snap.TierDistribution);
            int trueTier = DSGlobalStats.GetTrueTierCount(snap.TierDistribution);
            DrawStatusRow(UILocalization.Get("stats_tier_low"), lowTier.ToString(), Color.gray);
            DrawStatusRow(UILocalization.Get("stats_tier_mid"), midTier.ToString(), Color.cyan);
            DrawStatusRow(UILocalization.Get("stats_tier_high"), highTier.ToString(), Color.yellow);
            DrawStatusRow(UILocalization.Get("stats_tier_ultra"), ultraTier.ToString(), Color.magenta);
            DrawStatusRow(UILocalization.Get("stats_tier_truegod"), trueTier.ToString(), Color.red);

            GUILayout.Space(4);
            int maxTierCount = 1;
            for (int i = 1; i <= 13; i++) if (snap.TierDistribution[i] > maxTierCount) maxTierCount = snap.TierDistribution[i];

            for (int t = 1; t <= 13; t++)
            {
                if (snap.TierDistribution[t] == 0) continue;
                float pct = snap.TotalAwakened > 0 ? (float)snap.TierDistribution[t] / snap.TotalAwakened * 100f : 0f;
                GUILayout.BeginHorizontal();
                GUILayout.Label(t + " " + UILocalization.GetTierName(t), GUILayout.Width(120), GUILayout.Height(18));
                float barWidth = (float)snap.TierDistribution[t] / maxTierCount * 180f;
                GUI.color = new Color(0.4f, 0.7f, 1f);
                GUILayout.Box("", GUILayout.Width(Mathf.Max(barWidth, 4f)), GUILayout.Height(14));
                GUI.color = Color.white;
                GUILayout.Label(string.Format("{0} ({1:F1}%)", snap.TierDistribution[t], pct), GUILayout.Width(80), GUILayout.Height(18));
                GUILayout.EndHorizontal();
            }

            // ===== 基因分布（横向条形图 + 连锁数）=====
            GUILayout.Space(8);
            GUILayout.Label(UILocalization.Get("stats_gene_dist"), GUILayout.Height(22));

            DrawStatusRow(UILocalization.Get("stats_total_linkages"), snap.TotalGeneLinkages.ToString(), Color.yellow);

            GUILayout.Space(4);
            if (snap.GeneDistribution != null && snap.GeneDistribution.Count > 0)
            {
                // 找出最大值用于条形图缩放
                int maxElemCount = 1;
                foreach (var kv in snap.GeneDistribution)
                    if (kv.Value > maxElemCount) maxElemCount = kv.Value;

                // 按掌握人数降序排列
                var sortedElems = new List<KeyValuePair<string, int>>(snap.GeneDistribution);
                sortedElems.Sort((a, b) => b.Value.CompareTo(a.Value));

                foreach (var kv in sortedElems)
                {
                    var elemInfo = ElementDef.GetById(kv.Key);
                    string elemName = elemInfo != null ? elemInfo.NameZh : kv.Key;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(elemName, GUILayout.Width(80), GUILayout.Height(18));
                    float barWidth = (float)kv.Value / maxElemCount * 180f;
                    GUI.color = new Color(0.6f, 0.8f, 1f);
                    GUILayout.Box("", GUILayout.Width(Mathf.Max(barWidth, 4f)), GUILayout.Height(14));
                    GUI.color = Color.white;
                    GUILayout.Label(kv.Value.ToString(), GUILayout.Width(40), GUILayout.Height(18));
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label(UILocalization.Get("stats_no_gene_data"), GUILayout.Height(18));
            }

            // ===== 组织与灾变（增强）=====
            GUILayout.Space(8);
            GUILayout.Label(UILocalization.Get("debug_global_sects"), GUILayout.Height(22));
            try
            {
                var sects = Sect.SectManager.GetAllSects();
                if (sects != null && sects.Count > 0)
                {
                    // 组织总人数/平均境界统计
                    int totalSectMembers = 0;
                    float totalSectTier = 0f;
                    int sectTierCount = 0;
                    foreach (var s in sects)
                    {
                        if (s == null || s.MemberIds == null) continue;
                        totalSectMembers += s.MemberIds.Count;
                        foreach (long mid in s.MemberIds)
                        {
                            try
                            {
                                Actor m = SystemManagerExtensions.FindActorById(mid);
                                if (m != null && m.isAlive())
                                {
                                    totalSectTier += CultivationData.GetRealmTier(m);
                                    sectTierCount++;
                                }
                            }
                            catch { }
                        }
                    }
                    float avgSectTier = sectTierCount > 0 ? totalSectTier / sectTierCount : 0f;
                    DrawStatusRow(UILocalization.Get("stats_sect_total"), string.Format("{0} / {1}", sects.Count, totalSectMembers), Color.cyan);
                    DrawStatusRow(UILocalization.Get("stats_sect_avg_tier"), string.Format("{0:F1}", avgSectTier), Color.yellow);

                    GUILayout.Space(4);
                    foreach (var s in sects)
                    {
                        if (s == null) continue;
                        DrawStatusRow(s.Name, string.Format("{0} members, Heritage: {1}", s.MemberIds != null ? s.MemberIds.Count : 0, s.HeritageGen), Color.white);
                    }
                }
                else
                {
                    GUILayout.Label("(none)", GUILayout.Height(18));
                }
            }
            catch (Exception e) { DSDebug.Warning("DrawGlobalData sects Error: " + e.Message); }

            // 灾变统计
            GUILayout.Space(4);
            GUILayout.Label(UILocalization.Get("stats_disaster"), GUILayout.Height(22));
            DrawStatusRow(UILocalization.Get("stats_total_disasters"), snap.TotalDisasters.ToString(), Color.red);
            DrawStatusRow(UILocalization.Get("stats_active_rifts"), snap.ActiveRifts.ToString(), Color.yellow);
            DrawStatusRow(UILocalization.Get("stats_barrier"), string.Format("{0:F1}% ({1})",
                snap.BarrierIntegrity, Disaster.DisasterManager.GetBarrierStatus()),
                snap.BarrierIntegrity > 50f ? Color.green : Color.red);

            // ===== 真神纪元系统（新增）=====
            GUILayout.Space(8);
            GUILayout.Label(UILocalization.Get("stats_truegod_era"), GUILayout.Height(22));
            try
            {
                int currentEra = RealmJudge.GetCurrentEra();
                string eraName = RealmJudge.GetEraName(currentEra);
                var dominator = RealmJudge.GetCurrentDominator();

                DrawStatusRow(UILocalization.Get("stats_current_era"), string.Format(UILocalization.Get("debug_current_era"), currentEra), Color.cyan);
                DrawStatusRow(UILocalization.Get("stats_era_name"), eraName, Color.cyan);
                if (dominator != null)
                {
                    DrawStatusRow(UILocalization.Get("stats_era_dominator"), string.Format(UILocalization.Get("debug_dominator_info"), dominator.Name, dominator.Energy, dominator.ElementCount), Color.yellow);
                }
                else
                {
                    DrawStatusRow(UILocalization.Get("stats_era_dominator"), UILocalization.Get("stats_chaos_no_master"), Color.gray);
                }

                // 纪元历史（最近3个纪元）
                GUILayout.Space(4);
                GUILayout.Label(UILocalization.Get("debug_era_history"), GUILayout.Height(18));
                for (int era = currentEra; era >= Mathf.Max(1, currentEra - 2); era--)
                {
                    var history = RealmJudge.GetEraHistory(era);
                    string eraDisplayName = RealmJudge.GetEraName(era);
                    if (history != null && history.Count > 0)
                    {
                        var lastDom = history[history.Count - 1];
                        DrawStatusRow(string.Format(UILocalization.Get("debug_era_item"), era), string.Format(UILocalization.Get("debug_era_dominator"), lastDom.Name, history.Count), new Color(0.7f, 0.7f, 0.9f));
                    }
                    else
                    {
                        DrawStatusRow(string.Format(UILocalization.Get("debug_era_item"), era), UILocalization.Get("stats_chaos_no_master"), Color.gray);
                    }
                }
            }
            catch (Exception e) { DSDebug.Warning("DrawGlobalData era Error: " + e.Message); }

            // ===== 真神神殿（已有，保留）=====
            GUILayout.Space(8);
            GUILayout.Label(UILocalization.Get("debug_global_pantheon"), GUILayout.Height(22));
            try
            {
                var gods = TrueGodManager.GetAllTrueGods();
                if (gods != null && gods.Count > 0)
                {
                    foreach (var g in gods)
                    {
                        if (g == null) continue;
                        DrawStatusRow(g.Name, string.Format("{0} · Qi:{1:F0}", g.Race, g.Qi), Color.yellow);
                    }
                }
                else
                {
                    GUILayout.Label("(none)", GUILayout.Height(18));
                }

                GUILayout.Space(4);
                if (GUILayout.Button(UILocalization.Get("debug_global_clear"), GUILayout.Height(24)))
                {
                    TrueGodManager.ClearAllTrueGods();
                    SetStatus("Pantheon cleared");
                }
            }
            catch (Exception e) { DSDebug.Warning("DrawGlobalData pantheon Error: " + e.Message); }

            // ===== 数据导出按钮（新增）=====
            GUILayout.Space(8);
            if (GUILayout.Button(UILocalization.Get("stats_export"), GUILayout.Height(28)))
            {
                ExportGlobalStats(snap);
                SetStatus(UILocalization.Get("stats_export_done"));
            }
        }

        /// <summary>导出全局统计数据到控制台日志</summary>
        private void ExportGlobalStats(GlobalStatsSnapshot snap)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(UILocalization.Get("debug_export_header"));
                sb.AppendLine(string.Format(UILocalization.Get("debug_total_awakened"), snap.TotalAwakened, snap.TotalPopulation, snap.AwakenedRatio * 100f));
                sb.AppendLine(string.Format(UILocalization.Get("debug_total_breakthroughs"), snap.TotalBreakthroughs));
                sb.AppendLine(string.Format(UILocalization.Get("debug_total_awakenings"), snap.TotalAwakenings));
                sb.AppendLine(string.Format(UILocalization.Get("debug_total_disasters"), snap.TotalDisasters));
                sb.AppendLine(string.Format(UILocalization.Get("debug_total_ability_uses"), snap.TotalAbilityUses));
                sb.AppendLine(string.Format(UILocalization.Get("debug_avg_turbulence"), snap.AverageTurbulence));
                sb.AppendLine(string.Format(UILocalization.Get("debug_highest_tier"), snap.HighestTierReached));
                sb.AppendLine(string.Format(UILocalization.Get("debug_total_kills"), snap.TotalKills, snap.TotalAbilityKills));
                sb.AppendLine(string.Format(UILocalization.Get("debug_total_combos"), snap.TotalGeneLinkages));
                int totalGeneUsers = 0;
                foreach (var kv in snap.GeneDistribution) totalGeneUsers += kv.Value;
                sb.AppendLine(string.Format("Gene Users: {0}", totalGeneUsers));
                sb.AppendLine(string.Format(UILocalization.Get("debug_total_sects"), snap.TotalSects));
                sb.AppendLine(string.Format(UILocalization.Get("debug_active_rifts"), snap.ActiveRifts));
                sb.AppendLine(string.Format(UILocalization.Get("debug_barrier_integrity"), snap.BarrierIntegrity));
                sb.AppendLine(string.Format(UILocalization.Get("debug_true_god_count"), snap.TrueGodCount));
                sb.AppendLine(string.Format(UILocalization.Get("debug_current_era_info"), RealmJudge.GetCurrentEra(), RealmJudge.GetEraName(RealmJudge.GetCurrentEra())));
                var dom = RealmJudge.GetCurrentDominator();
                if (dom != null) sb.AppendLine(string.Format(UILocalization.Get("debug_era_dominator_info"), dom.Name, dom.Energy, dom.ElementCount));

                sb.AppendLine(UILocalization.Get("debug_tier_distribution"));
                for (int t = 1; t <= 13; t++)
                {
                    if (snap.TierDistribution[t] > 0)
                        sb.AppendLine(string.Format(UILocalization.Get("debug_tier_item"), t, UILocalization.GetTierName(t), snap.TierDistribution[t]));
                }

                sb.AppendLine(UILocalization.Get("debug_element_distribution"));
                if (snap.GeneDistribution != null)
                {
                    foreach (var kv in snap.GeneDistribution)
                    {
                        var elemInfo = ElementDef.GetById(kv.Key);
                        string name = elemInfo != null ? elemInfo.NameZh : kv.Key;
                        sb.AppendLine($"  {name}: {kv.Value}");
                    }
                }

                sb.AppendLine("==============================================");
                DSDebug.Verbose(sb.ToString());
            }
            catch (Exception e)
            {
                DSDebug.Warning("ExportGlobalStats Error: " + e.Message);
            }
        }

        // ============================================================
        //  辅助方法
        // ============================================================
        private string FindActorNameById(long id)
        {
            try
            {
                var units = World.world?.units?.units_only_alive;
                if (units != null)
                {
                    foreach (var a in units)
                    {
                        if (a != null && a.isAlive() && a.getID() == id) return a.getName();
                    }
                }
            }
            catch (Exception e) { DSDebug.Warning("FindActorNameById Error: " + e.Message); }
            return UILocalization.Get("debug_sect_fallen");
        }

        private void RefreshActorList()
        {
            _actorListCache.Clear();
            _actorListDirty = false;
            try
            {
                var units = World.world?.units?.units_only_alive;
                if (units == null) return;
                foreach (var a in units)
                {
                    if (a == null || !a.isAlive()) continue;
                    if (!CultivationData.IsAscended(a)) continue;
                    _actorListCache.Add(a);
                }
            }
            catch (Exception e) { DSDebug.Warning("RefreshActorList Error: " + e.Message); }
        }

        private int UnlockAllGenes(Actor actor)
        {
            int cnt = 0;
            if (actor == null) return 0;
            try
            {
                var all = ElementDef.AllElements;
                if (all == null) return 0;
                foreach (var el in all)
                {
                    if (el.Id == "gene_cell_division") continue; // 基因=觉醒起点，不手动解锁
                    if (!CultivationData.IsElementUnlocked(actor, el.Id))
                    {
                        CultivationData.UnlockElement(actor, el.Id);
                        cnt++;
                    }
                }
                GeneLinkageSystem.CheckAllLinkages(actor);
            }
            catch (Exception e) { DSDebug.Warning("UnlockAllGenes Error: " + e.Message); }
            return cnt;
        }

        private void ForceSetTier(Actor actor, int tier)
        {
            if (actor == null) return;
            try
            {
                CultivationData.SetRealmTier(actor, tier, allowBigJump: true);
                TraitManager.SetRealmTrait(actor, tier);
                if (!CultivationData.IsAscended(actor))
                {
                    CultivationData.SetEnergy(actor, RealmJudge.GetQiThreshold(tier));
                    CultivationData.SetAwakened(actor, true);
                    AnnualTickManager.RegisterCultivator(actor);
                }
            }
            catch (Exception e) { DSDebug.Warning("ForceSetTier Error: " + e.Message); }
        }
        private void DrawStatusRow(string label, string value, Color valueColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(160), GUILayout.Height(20));
            GUI.color = valueColor;
            GUILayout.Label(value, GUILayout.Height(20));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        private Actor GetSelectedActor()
        {
            try
            {
                if (MapBox.instance != null)
                {
                    var selectedField = typeof(MapBox).GetField("selected_actor",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (selectedField != null)
                    {
                        Actor actor = selectedField.GetValue(MapBox.instance) as Actor;
                        if (actor != null && actor.isAlive()) return actor;
                    }
                }
            }
            catch { }
            return null;
        }

        private int ForceAwaken(int count)
        {
            if (World.world == null || World.world.units == null) return 0;

            int awakened = 0;
            try
            {
                var aliveUnits = World.world.units.units_only_alive;
                if (aliveUnits == null) return 0;

                foreach (var actor in aliveUnits)
                {
                    if (awakened >= count) break;
                    if (actor == null || !actor.isAlive()) continue;
                    if (!RealmJudge.CanAwaken(actor)) continue;
                    if (CultivationData.IsAscended(actor)) continue;

                    if (RealmJudge.AwakenMortal(actor))
                    {
                        AnnualTickManager.RegisterCultivator(actor);
                        awakened++;
                    }
                }
            }
            catch (Exception e)
            {
                DSDebug.Error($"ForceAwaken Error: {e.Message}");
            }
            return awakened;
        }

        private void ExportRawData(Actor actor)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(string.Format(UILocalization.Get("debug_raw_data_header"), actor.getName(), actor.id));
                sb.AppendLine(string.Format(UILocalization.Get("debug_raw_tier"), CultivationData.GetRealmTier(actor)));
                sb.AppendLine(string.Format(UILocalization.Get("debug_raw_energy"), CultivationData.GetEnergy(actor)));
                sb.AppendLine(string.Format(UILocalization.Get("debug_raw_turbulence"), CultivationData.GetTurbulence(actor)));
                sb.AppendLine(string.Format(UILocalization.Get("debug_raw_affinity"), ""));
                sb.AppendLine(string.Format(UILocalization.Get("debug_raw_sect"), CultivationData.GetSectId(actor)));
                sb.AppendLine(string.Format(UILocalization.Get("debug_raw_sect_rank"), CultivationData.GetSectRank(actor)));
                sb.AppendLine(string.Format(UILocalization.Get("debug_raw_anchor"), CultivationData.GetAnchorMind(actor)));
                sb.AppendLine(string.Format(UILocalization.Get("debug_raw_enlightenment"), CultivationData.GetEnlightenmentExp(actor)));
                sb.AppendLine(string.Format(UILocalization.Get("debug_raw_enlightenment_duration"), CultivationData.GetEnlightenmentDuration(actor)));

                sb.AppendLine(UILocalization.Get("debug_mod_traits"));
                if (actor.traits != null)
                {
                    foreach (ActorTrait trait in actor.traits)
                        if (trait != null && trait.id != null && trait.id.StartsWith("ds_"))
                            sb.AppendLine($"  {trait.id}");
                }

                DSDebug.Verbose(sb.ToString());
            }
            catch (Exception e)
            {
                DSDebug.Error($"ExportRawData Error: {e.Message}");
            }
        }

        private void SetStatus(string msg)
        {
            _statusMessage = msg;
            _statusTimer = 3f;
        }
        // ============================================================

    }
}



