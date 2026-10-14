// ============================================================

using System;
using System.Linq;
using System.Collections.Generic;
using Code.UI;
using Code.Core;
using Code.Data;
using UnityEngine;

namespace Code.Dimension
{
    /// <summary>维度空间中的单位数据</summary>
    public class DimensionInhabitant
    {
        public long ActorId;
        public string ActorName;
        public int RealmTier;
        public float X;  // 在维度空间中的位置
        public float Y;
        public float Energy;
        public bool IsOwner;  // 是否是空间主人
    }

    /// <summary>
    /// 宏观维度空间管理器
    /// 管理共享的维度空间，所有12阶以上单位可进入
    /// </summary>
    public static class DimensionRealmManager
    {
        private static readonly List<DimensionInhabitant> _inhabitants = new List<DimensionInhabitant>();
        private static bool _initialized = false;

        // 维度空间全局状态
        private static int _realmLevel = 1;
        private static float _realmEnergy = 0f;
        private static int _realmSize = 100;
        private static long _ownerId = -1;  // 空间主人（第一个进入的真神）
        private static string _ownerName = "";
        private static bool _energyStable = true;   // 空间能量是否足以维持（影响修炼加速）
        private static readonly HashSet<long> _bannedIds = new HashSet<long>();  // 被主宰封禁的单位
        private static float _cultivationBonus = CULTIVATION_BONUS;  // 主宰调控的修炼倍率（0.5~3）

        // 常量
        private const float CULTIVATION_BONUS = 1.0f;  // 修炼加速100%
        private const float TURBULENCE_DECAY = 5f;     // 失控消解5/年
        private const float ENERGY_REGEN = 10f;         // 空间能量基础恢复10/年
        private const float ENERGY_PER_INHABITANT = 50f;  // 每位居民每年贡献空间能量
        private const float ENERGY_MAINTENANCE = 100f;    // 空间每级每年维持成本
        private const float ENERGY_ACCEL_COST = 50f;      // 修炼加速每人每年消耗
        private const float ENERGY_OWNER_BONUS = 500f;    // 主宰每年额外注入

        /// <summary>初始化维度空间</summary>
        public static void Init()
        {
            if (_initialized) return;
            _inhabitants.Clear();
            _initialized = true;
            // 挂载多世界切换系统（存档目录/原存档路径记录）
            DimensionWorldManager.Init();
            RebuildInhabitants();
            DSDebug.Verbose("[维度空间] 宏观维度空间系统初始化完成");
        }

        /// <summary>从单位数据重建空间居民列表（仅维度空间地图运行时收集化身；主地图清空）</summary>
        public static void RebuildInhabitants()
        {
            _inhabitants.Clear();
            // 世界级：不在维度空间地图时，空间居民列表为空（主地图无"在空间内"单位）
            if (!DimensionWorldManager.IsInDimensionWorld) return;
            try
            {
                if (World.world == null || World.world.units == null) return;
                foreach (Actor actor in World.world.units.units_only_alive)
                {
                    if (actor == null || !actor.isAlive()) continue;
                    if (CultivationData.IsInDimensionSpace(actor))
                    {
                        AddInhabitant(actor);
                    }
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] 重建居民列表失败: " + e.Message);
            }
        }

        /// <summary>获取所有可进入维度空间但尚未进入的单位（12阶以上存活单位）</summary>
        public static List<Actor> GetEligibleActors()
        {
            var result = new List<Actor>();
            try
            {
                if (World.world == null || World.world.units == null) return result;
                foreach (Actor actor in World.world.units.units_only_alive)
                {
                    if (actor == null || !actor.isAlive()) continue;
                    if (CultivationData.IsInDimensionSpace(actor)) continue;
                    int tier = CultivationData.GetRealmTier(actor);
                    if (tier >= 12) result.Add(actor);
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[维度空间] 获取可进入单位失败: " + e.Message);
            }
            return result;
        }

        /// <summary>单位进入维度空间（v0.13.0：世界切换——真独立地图，所有已登记空间居民随行）</summary>
        public static bool EnterDimension(Actor actor)
        {
            // 世界切换入口：备份主世界 → 加载/生成维度空间地图 → 化身重建
            return DimensionWorldManager.TryEnterDimensionWorld(actor);
        }

        /// <summary>单位退出维度空间（v0.13.0：世界切换——返回主世界，修炼成果写回）</summary>
        public static bool ExitDimension(Actor actor)
        {
            // 世界级返回：保存空间地图 → 加载主世界备份 → 数据回写
            return DimensionWorldManager.ReturnToMainWorld();
        }

        /// <summary>登记空间居民（世界级自动登记，不切图；进入空间时随行）</summary>
        public static bool RegisterAsInhabitant(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return false;
            try
            {
                try { CultivationData.SyncFromTraits(actor); } catch (Exception) { }
                int tier = CultivationData.GetRealmTier(actor);
                if (tier < 12) return false;
                if (_bannedIds.Contains(actor.getID())) return false;
                if (CultivationData.IsInDimensionSpace(actor)) return false;

                CultivationData.SetInDimensionSpace(actor, true);

                // 初始化空间数据（首次）
                if (CultivationData.GetDimensionSpaceLevel(actor) == 0)
                {
                    CultivationData.SetDimensionSpaceLevel(actor, tier >= 13 ? 2 : 1);
                    CultivationData.SetDimensionSpaceSize(actor, tier >= 13 ? 100 : 50);
                }

                // 设置全局空间主人（第一个登记的真神）
                if (_ownerId == -1 && tier >= 13)
                {
                    _ownerId = actor.getID();
                    _ownerName = actor.getName();
                    _realmLevel = 2;
                    _realmSize = 200;
                }
                return true;
            }
            catch (Exception e)
            {
                DSDebug.Warning("[GeneCultivation] 登记空间居民失败: " + e.Message);
                return false;
            }
        }

        /// <summary>取消空间居民登记（排斥出空间）</summary>
        public static bool UnregisterInhabitant(Actor actor)
        {
            if (actor == null) return false;
            try
            {
                CultivationData.SetInDimensionSpace(actor, false);
                _inhabitants.RemoveAll(i => i.ActorId == actor.getID());
                return true;
            }
            catch (Exception e)
            {
                DSDebug.Warning("[GeneCultivation] 取消空间居民登记失败: " + e.Message);
                return false;
            }
        }

        /// <summary>切换维度空间状态（v0.13.0：世界级——主地图进入 / 空间地图返回）</summary>
        public static bool ToggleDimension(Actor actor)
        {
            if (actor == null) return false;
            if (DimensionWorldManager.IsInDimensionWorld)
                return ExitDimension(actor);      // 已在空间地图 → 返回主世界
            else
                return EnterDimension(actor);     // 在主地图 → 进入维度空间
        }

        /// <summary>添加空间居民</summary>
        private static void AddInhabitant(Actor actor)
        {
            long id = actor.getID();
            if (_inhabitants.Exists(i => i.ActorId == id)) return;

            // 随机分配在维度空间中的位置
            float x = UnityEngine.Random.Range(10f, _realmSize - 10f);
            float y = UnityEngine.Random.Range(10f, _realmSize - 10f);

            var inhabitant = new DimensionInhabitant
            {
                ActorId = id,
                ActorName = actor.getName(),
                RealmTier = CultivationData.GetRealmTier(actor),
                X = x,
                Y = y,
                Energy = CultivationData.GetEnergy(actor),
                IsOwner = (id == _ownerId)
            };
            _inhabitants.Add(inhabitant);
        }

        /// <summary>年度结算：应用维度空间效果（v0.13.0：仅在维度空间地图运行时结算）</summary>
        public static void OnAnnualTick()
        {
            if (!_initialized) Init();

            // 世界级守卫：不在维度空间地图时不结算空间效果（主地图居民列表为空，能量经济暂停）
            if (!DimensionWorldManager.IsInDimensionWorld)
            {
                _inhabitants.Clear();
                return;
            }

            try
            {
                // 清理已死亡或已退出的居民
                _inhabitants.RemoveAll(i =>
                {
                    Actor actor = FindActorById(i.ActorId);
                    return actor == null || !actor.isAlive() || !CultivationData.IsInDimensionSpace(actor);
                });

                // 对空间内的单位应用效果
                foreach (var inhabitant in _inhabitants)
                {
                    Actor actor = FindActorById(inhabitant.ActorId);
                    if (actor == null || !actor.isAlive()) continue;

                    ApplyDimensionEffects(actor, inhabitant);
                }

                // 自动出入逻辑
                AutoEnterExitLogic();

                // 能量经济闭环：基础恢复 + 居民产出 + 主宰注入 - 等级维持 - 加速消耗
                float income = _inhabitants.Count * ENERGY_PER_INHABITANT;
                if (_ownerId != -1) income += ENERGY_OWNER_BONUS;
                float maintenance = _realmLevel * ENERGY_MAINTENANCE;
                float accelCost = _inhabitants.Count * ENERGY_ACCEL_COST;
                float delta = income - maintenance - accelCost;
                _realmEnergy += ENERGY_REGEN + delta;
                if (_realmEnergy < 0f) { _realmEnergy = 0f; _energyStable = false; }
                else if (income > 0f) { _energyStable = (_realmEnergy >= maintenance); }
                else { _energyStable = (_realmEnergy >= maintenance); }

                // 空间随机事件
                ProcessDimensionEvent();
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] 年度结算失败: " + e.Message);
            }
        }

        /// <summary>自动登记/排斥空间居民（v0.13.0：登记制，不切图——进入空间仍手动，登记者在进入时随行）</summary>
        private static void AutoEnterExitLogic()
        {
            try
            {
                // 1. 排斥：空间居民中非12阶单位 → 取消登记
                var inhabitantsCopy = new List<DimensionInhabitant>(_inhabitants);
                foreach (var inhabitant in inhabitantsCopy)
                {
                    Actor actor = FindActorById(inhabitant.ActorId);
                    if (actor == null || !actor.isAlive()) continue;

                    int tier = CultivationData.GetRealmTier(actor);
                    if (tier < 12)
                    {
                        // 非12阶单位在维度空间里 → 取消登记（世界级：切回主世界后不再随行）
                        DSDebug.Verbose($"[维度空间] 非12阶单位 {actor.getName()} 被维度空间排斥");
                        UnregisterInhabitant(actor);
                    }
                }

                // 2. 登记：遍历所有存活单位，12阶以上修炼中的自动登记为空间居民（不切图）
                if (World.world.units == null || World.world.units.units_only_alive == null) return;

                foreach (Actor actor in World.world.units.units_only_alive)
                {
                    if (actor == null || !actor.isAlive()) continue;

                    // 已经是空间居民，跳过
                    if (CultivationData.IsInDimensionSpace(actor)) continue;

                    int tier = CultivationData.GetRealmTier(actor);
                    if (tier < 12) continue;

                    // 修炼中 → 自动登记（进入空间时随行）
                    bool shouldRegister = false;
                    try { if (actor.hasTrait("ds_cultivating")) shouldRegister = true; } catch { }

                    if (shouldRegister)
                    {
                        RegisterAsInhabitant(actor);
                    }
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] 自动登记/排斥逻辑失败: " + e.Message);
            }
        }

        /// <summary>维度空间随机事件（每年触发，影响能量与居民失控）</summary>
        private static void ProcessDimensionEvent()
        {
            if (_inhabitants.Count == 0) return;
            float roll = UnityEngine.Random.value;
            if (roll < 0.10f) TriggerEnergySurge();
            else if (roll < 0.22f) TriggerSpaceRift();
            else if (roll < 0.32f) TriggerInvasion();
            // 其余年份空间平静
        }

        /// <summary>能量潮汐：空间能量爆发（正事件）</summary>
        private static void TriggerEnergySurge()
        {
            float surge = _realmLevel * 500f;
            _realmEnergy += surge;
            _energyStable = true;
            DSNotificationManager.NotifyMajor("维度空间·能量潮汐",
                string.Format("空间能量爆发，充盈 {0:F0} 点能量。居民修炼加速恢复满额。", surge));
            DSDebug.Warning("[GeneCultivation] 维度空间能量潮汐: +" + surge.ToString("F0"));
        }

        /// <summary>空间裂缝：能量流失 + 居民失控（负事件）</summary>
        private static void TriggerSpaceRift()
        {
            _realmEnergy *= 0.8f;
            foreach (var inhabitant in _inhabitants)
            {
                Actor actor = FindActorById(inhabitant.ActorId);
                if (actor == null || !actor.isAlive()) continue;
                float turb = CultivationData.GetTurbulence(actor);
                CultivationData.SetTurbulence(actor, turb + 2f);
            }
            DSNotificationManager.NotifyMajor("维度空间·空间裂缝",
                "空间出现裂缝，能量流失20%，居民失控指数小幅上升。");
            DSDebug.Warning("[GeneCultivation] 维度空间空间裂缝: 能量流失");
        }

        /// <summary>入侵者：空间能量被掠夺（负事件）</summary>
        private static void TriggerInvasion()
        {
            float loss = _realmLevel * 300f;
            _realmEnergy = Mathf.Max(0f, _realmEnergy - loss);
            if (_realmEnergy < _realmLevel * ENERGY_MAINTENANCE) _energyStable = false;
            DSNotificationManager.NotifyMajor("维度空间·入侵者",
                string.Format("虚空生物入侵维度空间，掠夺了 {0:F0} 点能量。", loss));
            DSDebug.Warning("[GeneCultivation] 维度空间入侵者: -" + loss.ToString("F0"));
        }

        /// <summary>应用维度空间效果到单个单位</summary>
        private static void ApplyDimensionEffects(Actor actor, DimensionInhabitant inhabitant)
        {
            // 1. 修炼加成（额外异能能量；空间能量不足时减半；倍率受主宰调控）
            float accelRatio = _energyStable ? 1f : 0.5f;
            float bonusQi = CultivationData.GetEnergy(actor) * _cultivationBonus * accelRatio * 0.01f;
            if (bonusQi > 0.1f)
            {
                CultivationData.SetEnergy(actor, CultivationData.GetEnergy(actor) + bonusQi);
            }

            // 2. 失控指数自动消解
            float turb = CultivationData.GetTurbulence(actor);
            if (turb > 0f)
            {
                float decay = Mathf.Min(turb, TURBULENCE_DECAY);
                CultivationData.SetTurbulence(actor, turb - decay);
            }

            // 3. 更新居民能量
            inhabitant.Energy = CultivationData.GetEnergy(actor);
        }

        /// <summary>空间升级</summary>
        public static bool UpgradeRealm(Actor actor)
        {
            if (actor == null) return false;
            float energy = CultivationData.GetDimensionSpaceEnergy(actor);
            if (energy < 10000f)
            {
                DSNotificationManager.NotifyInfo(UILocalization.Get("dim_upgrade_fail"));
                return false;
            }

            CultivationData.SetDimensionSpaceEnergy(actor, energy - 10000f);
            int level = CultivationData.GetDimensionSpaceLevel(actor);
            CultivationData.SetDimensionSpaceLevel(actor, level + 1);
            int size = CultivationData.GetDimensionSpaceSize(actor);
            CultivationData.SetDimensionSpaceSize(actor, size + 20);

            // 全局空间也升级
            _realmLevel = Math.Max(_realmLevel, level + 1);
            _realmSize = Math.Max(_realmSize, size + 20);

            DSNotificationManager.NotifyInfo(string.Format(UILocalization.Get("dim_upgrade_success"), level + 1, size + 20));
            return true;
        }

        /// <summary>能量充能</summary>
        public static bool ChargeEnergy(Actor actor)
        {
            if (actor == null) return false;
            float qi = CultivationData.GetEnergy(actor);
            if (qi < 1000f)
            {
                DSNotificationManager.NotifyInfo(UILocalization.Get("dim_charge_fail"));
                return false;
            }

            CultivationData.SetEnergy(actor, qi - 1000f);
            float spaceEnergy = CultivationData.GetDimensionSpaceEnergy(actor);
            CultivationData.SetDimensionSpaceEnergy(actor, spaceEnergy + 100f);
            _realmEnergy += 100f;

            DSNotificationManager.NotifyInfo(UILocalization.Get("dim_charge_success"));
            return true;
        }

        /// <summary>创造能量生命体（仅13阶超神级）</summary>
        public static bool CreateEnergyLifeform(Actor actor)
        {
            if (actor == null) return false;
            int tier = CultivationData.GetRealmTier(actor);
            if (tier < 13)
            {
                DSNotificationManager.NotifyInfo(UILocalization.Get("dim_create_fail_tier"));
                return false;
            }

            float energy = CultivationData.GetDimensionSpaceEnergy(actor);
            if (energy < 50000f)
            {
                DSNotificationManager.NotifyInfo(UILocalization.Get("dim_create_fail_energy"));
                return false;
            }

            CultivationData.SetDimensionSpaceEnergy(actor, energy - 50000f);

            // 获得临时属性加成（持续1年）
            try
            {
                DSReflectionHelper.SafeAddStatusEffect(actor, "ds_energy_lifeform", 60f, true);
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] CreateEnergyLifeform 异常: " + dsEx.Message); }

            DSNotificationManager.NotifyMajor(UILocalization.Get("dim_life_born"),
                string.Format(UILocalization.Get("dim_life_born_macro"), actor.getName()));

            DSHistoryManager.LogEvent("divine", UILocalization.Get("dim_life_born"),
                string.Format(UILocalization.Get("dim_life_born_simple"), actor.getName()), actor.getName());

            return true;
        }

        // ============================================================
        //  公共查询
        // ============================================================

        public static List<DimensionInhabitant> GetAllInhabitants()
        {
            return new List<DimensionInhabitant>(_inhabitants);
        }

        public static int GetInhabitantCount()
        {
            return _inhabitants.Count;
        }

        public static int GetRealmLevel()
        {
            return _realmLevel;
        }

        public static bool GetRealmEnergyStable() { return _energyStable; }

        public static float GetCultivationBonus() { return _cultivationBonus; }

        public static bool IsBanned(long actorId) { return _bannedIds.Contains(actorId); }

        public static bool IsOwner(Actor actor)
        {
            return actor != null && actor.getID() == _ownerId;
        }

        /// <summary>主宰封禁单位（逐出空间并禁止再入）</summary>
        public static bool BanInhabitant(Actor owner, Actor target)
        {
            if (owner == null || target == null) return false;
            if (owner.getID() != _ownerId)
            {
                DSNotificationManager.NotifyInfo("维度空间封禁失败：仅空间主宰可行使此权限");
                return false;
            }
            if (target.getID() == _ownerId)
            {
                DSNotificationManager.NotifyInfo("维度空间封禁失败：不能封禁空间主宰");
                return false;
            }
            if (CultivationData.IsInDimensionSpace(target)) UnregisterInhabitant(target);
            _bannedIds.Add(target.getID());
            DSNotificationManager.NotifyMajor("维度空间·封禁",
                string.Format("{0} 已被空间主宰封禁，驱逐出维度空间。", target.getName()));
            DSDebug.Warning("[GeneCultivation] 主宰封禁: " + target.getName());
            return true;
        }

        /// <summary>主宰解除封禁</summary>
        public static bool UnbanInhabitant(Actor owner, long targetId)
        {
            if (owner == null || owner.getID() != _ownerId) return false;
            _bannedIds.Remove(targetId);
            DSNotificationManager.NotifyInfo("维度空间封禁已解除。");
            return true;
        }

        /// <summary>主宰调控修炼倍率（0.5~3倍）</summary>
        public static bool SetCultivationBonus(Actor owner, float multiplier)
        {
            if (owner == null || owner.getID() != _ownerId) return false;
            _cultivationBonus = Mathf.Clamp(multiplier, 0.5f, 3f);
            DSNotificationManager.NotifyMajor("维度空间·规则调整",
                string.Format("空间主宰将修炼加速调整为 {0:F1} 倍。", _cultivationBonus));
            DSDebug.Warning("[GeneCultivation] 主宰调控修炼倍率: x" + _cultivationBonus.ToString("F1"));
            return true;
        }

        /// <summary>UI便捷：封禁指定居民（宏观面板=主宰操作）</summary>
        public static bool BanSelectedInhabitant(long targetId)
        {
            if (targetId == _ownerId)
            {
                DSNotificationManager.NotifyInfo("维度空间封禁失败：不能封禁空间主宰");
                return false;
            }
            Actor target = FindActorById(targetId);
            if (target == null)
            {
                DSNotificationManager.NotifyInfo("维度空间封禁失败：目标不存在");
                return false;
            }
            if (CultivationData.IsInDimensionSpace(target)) UnregisterInhabitant(target);
            _bannedIds.Add(targetId);
            DSNotificationManager.NotifyMajor("维度空间·封禁",
                string.Format("{0} 已被空间主宰封禁，驱逐出维度空间。", target.getName()));
            DSDebug.Warning("[GeneCultivation] 主宰封禁(UI): " + target.getName());
            return true;
        }

        /// <summary>UI便捷：修炼倍率循环档位（0.5/1/1.5/2/2.5/3）</summary>
        public static void CycleCultivationBonus()
        {
            float[] steps = { 0.5f, 1f, 1.5f, 2f, 2.5f, 3f };
            int idx = System.Array.IndexOf(steps, _cultivationBonus);
            int next = idx < 0 ? 1 : (idx + 1) % steps.Length;
            _cultivationBonus = steps[next];
            DSNotificationManager.NotifyMajor("维度空间·规则调整",
                string.Format("空间主宰将修炼加速调整为 {0:F1} 倍。", _cultivationBonus));
            DSDebug.Warning("[GeneCultivation] 主宰调控(UI): x" + _cultivationBonus.ToString("F1"));
        }

        public static float GetRealmEnergy()
        {
            return _realmEnergy;
        }

        public static int GetRealmSize()
        {
            return _realmSize;
        }

        public static string GetOwnerName()
        {
            return _ownerName;
        }

        /// <summary>检查单位是否在维度空间中</summary>
        public static bool IsInDimension(Actor actor)
        {
            if (actor == null) return false;
            return CultivationData.IsInDimensionSpace(actor);
        }

        // ============================================================
        //  工具方法
        // ============================================================

        private static Actor FindActorById(long id)
        {
            return SystemManagerExtensions.FindActorById(id);
        }
    }
}



