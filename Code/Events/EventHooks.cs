// ============================================================

using System;
using System.Collections.Generic;
using Code.Core;
using Code.Data;
using UnityEngine;

namespace Code.Events
{
    public static class EventHooks
    {
        // 已注册死亡回调的单位ID集合（防止重复注册）
        private static readonly HashSet<long> _hookedActors = new HashSet<long>();

        // OnWorldLoaded 是否已注册（防止mod热重载时重复挂载静态事件委托，M-13修复）
        private static bool _worldLoadedHooked = false;

        // 失控指数增长配置
        private const float TURB_PER_KILL = 2f;       // 每次击杀基础失控指数增长
        private const float TURB_PER_MASS_KILL = 5f;  // 大规模击杀额外增长
        private const int MASS_KILL_THRESHOLD = 5;     // 年度击杀数阈值

        /// <summary>反射获取private字段值</summary>
        private static T GetPrivateField<T>(object obj, string fieldName) where T : class
        {
            try
            {
                var field = obj.GetType().GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (field == null) return null;
                return field.GetValue(obj) as T;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 初始化事件钩子。在ModClass.OnModLoad中调用。
        /// 监听世界加载事件，加载后批量注册所有单位的死亡回调。
        /// </summary>
        public static void Init()
        {
            // 防止重复挂载静态事件委托（mod热重载时）
            if (_worldLoadedHooked) return;

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
                    _worldLoadedHooked = true;
                    DSDebug.Verbose("事件钩子已初始化（on_death监听已挂载，反射方式）");
                }
                else
                {
                    DSDebug.Warning("未找到on_world_loaded字段，事件钩子初始化失败");
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("事件钩子初始化失败: " + e.Message);
            }
        }

        /// <summary>
        /// 世界加载后：批量给所有存活单位注册死亡回调。
        /// </summary>
        private static void OnWorldLoaded()
        {
            _hookedActors.Clear();
            if (World.world == null || World.world.units == null) return;

            // 每次世界加载：重建组织系统（清空旧存档残留）+ 重置灾变/维度（读档即重置，纯随档不跨档）+ 启用战斗加成
            Code.Sect.SectManager.Init();
            Code.Disaster.DisasterManager.Init();
            Code.Dimension.DimensionRealmManager.Init();
                        Code.Combat.VirtualWeaponSystem.Init();
            Code.Realm.DSSkillEffectManager.Init();
            //  重置年度Tick时间追踪（读档/新世界：时间基准归零，防止跨档时间错乱）
            try { Code.ModClass.ResetAnnualTickTracker(); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] OnWorldLoaded 异常: " + dsEx.Message); }
            //  重置年度结算防重标记 + 重建活跃异能者列表（读档后立刻重建，旧档单位不丢、首年不被防重误跳）
            try { Code.Core.AnnualTickManager.ResetYearTracker(); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] OnWorldLoaded 异常: " + dsEx.Message); }
            //  重置全局统计累计计数器（纯随档，不跨存档）
            try { Code.Core.DSGlobalStats.Reset(); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] OnWorldLoaded 异常: " + dsEx.Message); }
            //  清空运行时缓存（防止跨存档串数据）
            try { Code.Data.CultivationData.ClearRuntimeCaches(); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] OnWorldLoaded 异常: " + dsEx.Message); }

            int count = 0;
            foreach (var actor in World.world.units.units_only_alive)
            {
                if (actor != null && HookActorDeath(actor))
                    count++;
            }
            DSDebug.Verbose(string.Format("[DivineAscension] 世界加载完成，已为{0}个单位注册死亡回调", count));
        }

        /// <summary>
        /// 给单个单位注册死亡回调。返回true表示新注册，false表示已注册。
        /// </summary>
        public static bool HookActorDeath(Actor actor)
        {
            if (actor == null) return false;

            long id;
            try
            {
                // 原版 getID() = getData().id；单位数据已失效（getData()为null）时这里会抛空引用，
                // 必须捕获并跳过——否则会冒泡中断整个全局年度结算。
                id = actor.getID();
            }
            catch (System.Exception)
            {
                return false;
            }

            if (_hookedActors.Contains(id))
                return false;

            // 追加死亡回调（不覆写原生callbacks_on_death，仅+=追加）
            actor.callbacks_on_death += OnActorDeath;
            _hookedActors.Add(id);
            return true;
        }

        /// <summary>
        /// 单位死亡回调。由callbacks_on_death委托调用。
        /// 检测攻击者，给攻击者增加失控指数。
        /// </summary>
        private static void OnActorDeath(Actor deadActor)
        {
            if (deadActor == null) return;

            // 标记境界计数缓存为脏（单位死亡时更新缓存）
            try { Code.Realm.RealmJudge.MarkTierCacheDirty(); } catch (System.Exception ex) { DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }

            // 注销死亡回调委托，防止对象池复用时重复触发
            deadActor.callbacks_on_death -= OnActorDeath;

            // 从已注册集合移除（getID可能因data失效抛空引用，绝不能传播进原版死亡流程）
            try { _hookedActors.Remove(deadActor.getID()); } catch (System.Exception ex) { DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }

            // 组织首领陨落  换届继承（组织延续；纯随档不跨存档）
            // 凤凰重生(fenix_born)：单位会原地重生，不算真正陨落，不触发换届
            bool isPhoenixRebirth = false;
            try { isPhoenixRebirth = deadActor.hasTrait("fenix_born"); } catch (System.Exception ex) { DSDebug.Warning("[DivineAscension] 操作失败: " + ex.Message); }
            if (!isPhoenixRebirth)
            {
                try { Code.Sect.SectManager.OnLeaderDied(deadActor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] OnActorDeath 异常: " + dsEx.Message); }
            }

            // 检测攻击者（attackedBy字段记录最后一个攻击者，需要用反射访问private字段）
            BaseSimObject attackerObj = GetPrivateField<BaseSimObject>(deadActor, "attackedBy");
            if (attackerObj == null) return;

            // 攻击者必须是Actor（单位），a字段是private的，需要用反射访问
            Actor attacker = GetPrivateField<Actor>(attackerObj, "a");
            if (attacker == null || !attacker.isAlive()) return;

            // 只有异能者才累积失控指数（凡人也可以累积，但上限低）
            // 文档9.2节：屠戮生灵增长失控指数
            float turbGain = TURB_PER_KILL;

            // 高阶异能者击杀低阶单位，失控指数增长更多（杀戮越重，浊气越重）
            int attackerTier = CultivationData.GetRealmTier(attacker);
            int deadTier = CultivationData.GetRealmTier(deadActor);
            if (attackerTier > 0 && deadTier > 0)
            {
                // 异能者击杀异能者，失控指数增长加倍
                turbGain *= 2f;
                // 高阶击杀低阶，额外增长
                if (attackerTier > deadTier)
                    turbGain += (attackerTier - deadTier) * 0.5f;
            }

            // 记录年度击杀数（用于大规模击杀判定）
            int killCount = CultivationData.GetCooldown(attacker, "annual_kills");
            killCount++;
            CultivationData.SetCooldown(attacker, "annual_kills", killCount);

            // 大规模击杀额外增长
            if (killCount >= MASS_KILL_THRESHOLD)
            {
                turbGain += TURB_PER_MASS_KILL;
            }

            // 基因图谱战斗进度：记录击杀（仅异能者）
            if (attackerTier > 0)
            {
                try { CultivationData.AddGeneCombatKill(attacker); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] OnActorDeath 异常: " + dsEx.Message); }
            }

            //  单位详细统计钩子：总击杀数+1，异能者额外记录异能击杀
            try { CultivationData.AddKill(attacker); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] OnActorDeath 异常: " + dsEx.Message); }
            if (attackerTier > 0)
            {
                try { CultivationData.AddAbilityKill(attacker); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] OnActorDeath 异常: " + dsEx.Message); }
            }

            // 给攻击者增加失控指数（通过年度结算统一应用）
            EnergyTurbulenceCalculator.AddExternalTurbulence(attacker, turbGain);

            // 调试日志（可开关）
            if (AnnualTickManager.PerformanceDiagnosticsEnabled)
            {
                DSDebug.Verbose(string.Format("[DivineAscension] {0} 击杀 {1}，增加失控指数 {2:F1}",
                    attacker.getName(), deadActor.getName(), turbGain));
            }
        }

        /// <summary>
        /// 年度结算时调用：重置年度击杀计数 + 应用战乱失控指数 + 地形破坏失控指数
        /// 由AnnualTickManager在年度结算开始时调用。
        /// </summary>
        public static void OnAnnualTick()
        {
            // 统计全局战乱程度
            int totalKills = 0;
            int warThreshold = 20; // 全局年度击杀超过20视为战乱

            foreach (long id in GetActiveCultivatorIds())
            {
                Actor actor = FindActorById(id);
                if (actor == null) continue;
                int kills = CultivationData.GetCooldown(actor, "annual_kills");
                totalKills += kills;
                // 重置个人击杀计数
                CultivationData.SetCooldown(actor, "annual_kills", 0);
            }

            // 战乱失控指数：全局击杀超过阈值时，所有异能者增加战乱失控指数
            if (totalKills >= warThreshold)
            {
                float warTurb = (totalKills - warThreshold) * 0.1f;
                warTurb = Mathf.Min(warTurb, 10f); // 上限10

                foreach (long id in GetActiveCultivatorIds())
                {
                    Actor actor = FindActorById(id);
                    if (actor != null)
                    {
                        EnergyTurbulenceCalculator.AddExternalTurbulence(actor, warTurb);
                    }
                }

                if (AnnualTickManager.PerformanceDiagnosticsEnabled)
                {
                    DSDebug.Verbose(string.Format("[DivineAscension] 战乱失控指数: 全局击杀{0}，所有异能者+{1:F1}失控指数", totalKills, warTurb));
                }
            }

            // 地形破坏失控指数：统计地图上的岩浆/废墟地块
            ApplyTerrainDestructionTurbulence();
        }

        /// <summary>
        /// 地形破坏失控指数：检测世界中的灾难事件数量（陨石/龙卷风/地震等），
        /// 给附近异能者增加失控指数。在年度结算中调用（非逐帧）。
        /// </summary>
        private static void ApplyTerrainDestructionTurbulence()
        {
            if (World.world == null) return;

            int disasterCount = 0;
            try
            {
                // 通过反射检测World中的灾难/事件计数
                var worldType = World.world.GetType();
                string[] disasterFields = { "meteor_count", "disaster_count", "tornado_count", "earthquake_count", "active_disasters" };
                foreach (string fieldName in disasterFields)
                {
                    var field = worldType.GetField(fieldName,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        object val = field.GetValue(World.world);
                        if (val is int) disasterCount += (int)val;
                        else if (val is System.Collections.ICollection) disasterCount += ((System.Collections.ICollection)val).Count;
                    }
                }
            }
            catch (Exception e)
            {
                if (AnnualTickManager.PerformanceDiagnosticsEnabled)
                    DSDebug.Verbose("地形统计异常: " + e.Message);
            }

            // 地形破坏失控指数：每个灾难事件给全局异能者+0.5失控指数，上限5
            if (disasterCount >= 2)
            {
                float terrainTurb = disasterCount * 0.5f;
                terrainTurb = Mathf.Min(terrainTurb, 5f);

                foreach (long id in GetActiveCultivatorIds())
                {
                    Actor actor = FindActorById(id);
                    if (actor != null)
                    {
                        EnergyTurbulenceCalculator.AddExternalTurbulence(actor, terrainTurb);
                    }
                }

                if (AnnualTickManager.PerformanceDiagnosticsEnabled)
                {
                    DSDebug.Verbose(string.Format("[DivineAscension] 地形破坏失控指数: {0}个灾难事件，所有异能者+{1:F1}失控指数", disasterCount, terrainTurb));
                }
            }
        }

        /// <summary>
        /// 定期检查新单位并补充注册死亡回调。
        /// 由AnnualTickManager在年度结算时调用（年度级别检查，非逐帧）。
        /// </summary>
        public static void CheckAndHookNewActors()
        {
            if (World.world == null || World.world.units == null) return;

            // 注意：组织系统/存档数据只在世界真正加载时（OnWorldLoaded）初始化一次；
            // 此处为年度结算的例行检查，若重复 Init 会清空组织运行时数据并重读存档，导致年度内数据丢失。
            int newCount = 0;
            foreach (var actor in World.world.units.units_only_alive)
            {
                try
                {
                    if (actor != null && HookActorDeath(actor))
                        newCount++;
                }
                catch (System.Exception)
                {
                    // 单个单位注册失败不影响其他单位（防御：列表遍历期间的意外）
                }
            }

            if (newCount > 0 && AnnualTickManager.PerformanceDiagnosticsEnabled)
            {
                DSDebug.Verbose(string.Format("[DivineAscension] 补充注册{0}个新单位的死亡回调", newCount));
            }
        }

        // ============================================================
        //  工具方法
        // ============================================================
        private static List<long> GetActiveCultivatorIds()
        {
            var field = typeof(AnnualTickManager).GetField("_activeCultivatorIds",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(null) as List<long> ?? new List<long>();
            return new List<long>();
        }

        private static Actor FindActorById(long id) { return SystemManagerExtensions.FindActorById(id); }
    }
}



