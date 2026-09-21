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

        // 常量
        private const float CULTIVATION_BONUS = 1.0f;  // 修炼加速100%
        private const float TURBULENCE_DECAY = 5f;     // 失控消解5/年
        private const float ENERGY_REGEN = 10f;         // 空间能量恢复10/年

        /// <summary>初始化维度空间</summary>
        public static void Init()
        {
            if (_initialized) return;
            _inhabitants.Clear();
            _initialized = true;
            RebuildInhabitants();
            DSDebug.Verbose("[维度空间] 宏观维度空间系统初始化完成");
        }

        /// <summary>从单位数据重建空间居民列表</summary>
        public static void RebuildInhabitants()
        {
            _inhabitants.Clear();
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

        /// <summary>单位进入维度空间</summary>
        public static bool EnterDimension(Actor actor)
        {
            if (actor == null) return false;

            try
            {
                // 防御：单位有境界特质但custom_data未同步（调试赐特质/旧存档）时，先同步再判断
                // 否则GetRealmTier返回0，12阶单位也会被"无法进入"拒绝
                try { CultivationData.SyncFromTraits(actor); } catch (System.Exception syncEx) { Code.Core.DSDebug.Warning("[DivineAscension] EnterDimension 同步境界失败: " + syncEx.Message); }

                int tier = CultivationData.GetRealmTier(actor);
                if (tier < 12)
                {
                    DSDebug.Warning(string.Format("[DivineAscension] 进入维度被拒: {0} 当前境界={1} (需要12阶以上)", actor.getName(), tier));
                    DSNotificationManager.NotifyInfo(UILocalization.Get("dim_cannot_enter"));
                    return false;
                }

                if (CultivationData.IsInDimensionSpace(actor))
                {
                    DSNotificationManager.NotifyInfo(string.Format(UILocalization.Get("dim_already_inside"), actor.getName()));
                    return false;
                }

                CultivationData.SetInDimensionSpace(actor, true);

                // 添加维度空间状态效果（半透明、无敌、不可选中、AI暂停）
                try { DSReflectionHelper.SafeAddStatusEffect(actor, "ds_in_dimension", 999999f, true); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] EnterDimension 异常: " + dsEx.Message); }

                // 初始化空间数据
                if (CultivationData.GetDimensionSpaceLevel(actor) == 0)
                {
                    CultivationData.SetDimensionSpaceLevel(actor, tier >= 13 ? 2 : 1);
                    CultivationData.SetDimensionSpaceSize(actor, tier >= 13 ? 100 : 50);
                }

                // 设置全局空间主人（第一个进入的真神）
                if (_ownerId == -1 && tier >= 13)
                {
                    _ownerId = actor.getID();
                    _ownerName = actor.getName();
                    _realmLevel = 2;
                    _realmSize = 200;
                }

                AddInhabitant(actor);

                DSNotificationManager.NotifyMajor(UILocalization.Get("dim_enter"),
                    string.Format(UILocalization.Get("dim_enter_macro"), actor.getName()));

                DSHistoryManager.LogEvent("system", UILocalization.Get("dim_enter"),
                    string.Format(UILocalization.Get("dim_enter_simple"), actor.getName()), actor.getName());

                DSDebug.Warning(string.Format("[DivineAscension] {0} 已进入维度空间 (境界={1})", actor.getName(), tier));
                return true;
            }
            catch (System.Exception e)
            {
                // 异常时回滚标记，避免"标记已设但流程中断"的状态不一致
                try { CultivationData.SetInDimensionSpace(actor, false); } catch { }
                DSDebug.Warning("[DivineAscension] 进入维度空间失败: " + e.Message);
                return false;
            }
        }

        /// <summary>单位退出维度空间</summary>
        public static bool ExitDimension(Actor actor)
        {
            if (actor == null) return false;
            if (!CultivationData.IsInDimensionSpace(actor)) return false;

            CultivationData.SetInDimensionSpace(actor, false);

            // 移除维度空间状态效果
            try { actor.finishStatusEffect("ds_in_dimension"); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ExitDimension 异常: " + dsEx.Message); }

            _inhabitants.RemoveAll(i => i.ActorId == actor.getID());

            // 如果是空间主人退出，转移主人
            if (actor.getID() == _ownerId)
            {
                var newOwner = _inhabitants.Find(i => i.RealmTier >= 13);
                if (newOwner != null)
                {
                    _ownerId = newOwner.ActorId;
                    _ownerName = newOwner.ActorName;
                }
                else
                {
                    _ownerId = -1;
                    _ownerName = "";
                }
            }

            DSNotificationManager.NotifyInfo(string.Format(UILocalization.Get("dim_exit"), actor.getName()));
            return true;
        }

        /// <summary>切换单位在维度空间中的状态</summary>
        public static bool ToggleDimension(Actor actor)
        {
            if (actor == null) return false;
            if (CultivationData.IsInDimensionSpace(actor))
                return ExitDimension(actor);
            else
                return EnterDimension(actor);
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

        /// <summary>年度结算：应用维度空间效果</summary>
        public static void OnAnnualTick()
        {
            if (!_initialized) Init();

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

                // 空间能量自动恢复
                _realmEnergy += ENERGY_REGEN;
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] 年度结算失败: " + e.Message);
            }
        }

        /// <summary>自动出入维度空间逻辑</summary>
        private static void AutoEnterExitLogic()
        {
            try
            {
                // 1. 检查维度空间里的非12阶单位 → 扣血并排斥出去
                var inhabitantsCopy = new List<DimensionInhabitant>(_inhabitants);
                foreach (var inhabitant in inhabitantsCopy)
                {
                    Actor actor = FindActorById(inhabitant.ActorId);
                    if (actor == null || !actor.isAlive()) continue;

                    int tier = CultivationData.GetRealmTier(actor);
                    if (tier < 12)
                    {
                        // 非12阶单位在维度空间里 → 扣血并排斥
                        DSDebug.Verbose($"[维度空间] 非12阶单位 {actor.getName()} 被维度空间排斥");
                        
                        // 退出维度空间
                        ExitDimension(actor);
                    }
                }

                // 2. 遍历所有存活单位，检查是否需要自动进入
                if (World.world.units == null || World.world.units.units_only_alive == null) return;

                foreach (Actor actor in World.world.units.units_only_alive)
                {
                    if (actor == null || !actor.isAlive()) continue;
                    
                    // 已经在维度空间里了，跳过
                    if (CultivationData.IsInDimensionSpace(actor)) continue;

                    int tier = CultivationData.GetRealmTier(actor);
                    
                    // 只有12阶以上才能进入
                    if (tier < 12) continue;

                    bool shouldEnter = false;

                    // 条件1：在修炼（有修炼buff）
                    try
                    {
                        if (actor.hasTrait("ds_cultivating"))
                        {
                            shouldEnter = true;
                        }
                    }
                    catch { }

                    // 条件2：靠近维度之门建筑（10格范围内）
//                     if (!shouldEnter)
//                     {
//                         try
//                         {
// // //                             var buildings = Code.Realm.RealmBuildings.GetAllBuildings();
//                             Vector2 actorPos = actor.current_position;
//                             
// //                             foreach (var b in buildings)
//                             {
//                                 if (b.BuildingId == "ds_building_dimensional_gate" && b.IsActive)
//                                 {
//                                     float dx = actorPos.x - b.X;
//                                     float dy = actorPos.y - b.Y;
//                                     float dist = Mathf.Sqrt(dx * dx + dy * dy);
//                                     if (dist < 10f) // 10格范围内
//                                     {
//                                         shouldEnter = true;
//                                         break;
//                                     }
//                                 }
//                             }
//                         }
//                         catch { }
//                     }

                    // 满足条件就自动进入
                    if (shouldEnter)
                    {
                        EnterDimension(actor);
                        DSDebug.Verbose($"[维度空间] {actor.getName()} 自动进入维度空间");
                    }
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning("[维度空间] 自动出入逻辑失败: " + e.Message);
            }
        }

        /// <summary>应用维度空间效果到单个单位</summary>
        private static void ApplyDimensionEffects(Actor actor, DimensionInhabitant inhabitant)
        {
            // 1. 修炼加成（额外异能能量）
            float bonusQi = CultivationData.GetEnergy(actor) * CULTIVATION_BONUS * 0.01f;
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
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] CreateEnergyLifeform 异常: " + dsEx.Message); }

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



