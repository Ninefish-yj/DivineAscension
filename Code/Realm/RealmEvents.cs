// ============================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Code.Core;
using Code.Data;
using Code.Traits;
using Code.UI;

namespace Code.Realm
{
    /// <summary>境界事件类型</summary>
    public enum RealmEventType
    {
        Breakthrough,      // 突破事件
        RuleBacklash, // 规则反噬事件
        HeavenVision,      // 天地异象
        DivineRevelation,  // 神启事件
        WorldAnomaly       // 世界异常
    }

    /// <summary>境界事件定义</summary>
    public class RealmEvent
    {
        public int RequiredTier;        // 触发境界
        public RealmEventType Type;     // 事件类型
        public string Id;                // 事件ID
        public string Name;              // 事件名称
        public string Description;       // 事件描述
        public float Damage;             // 事件伤害（天劫用）
        public float EnergyBonus;        // 事件能量奖励
        public bool IsGlobal;            // 是否全局事件
    }

    public static class RealmEvents
    {
        private static readonly Dictionary<int, List<RealmEvent>> _eventsByTier = new Dictionary<int, List<RealmEvent>>();
        private static readonly List<RealmEvent> _activeEvents = new List<RealmEvent>();
        private static bool _initialized = false;

        /// <summary>初始化所有境界事件</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            // 8阶 干涉者 - 规则反噬
            RegisterEvent(new RealmEvent
            {
                RequiredTier = 8,
                Type = RealmEventType.RuleBacklash,
                Id = "ds_event_law_tribulation",
                Name = UILocalization.Get("event_rule_backlash"),
                Description = UILocalization.Get("event_rule_backlash_desc"),
                Damage = 500f,
                EnergyBonus = 10000f,
                IsGlobal = false
            });

            // 10阶 使徒级 - 空间撕裂异象
            RegisterEvent(new RealmEvent
            {
                RequiredTier = 10,
                Type = RealmEventType.WorldAnomaly,
                Id = "ds_event_space_tear",
                Name = UILocalization.Get("event_space_tear"),
                Description = UILocalization.Get("event_space_tear_desc"),
                Damage = 200f,
                EnergyBonus = 30000f,
                IsGlobal = false
            });

            // 11阶 登神级 - 超神级降临（全局事件）
            RegisterEvent(new RealmEvent
            {
                RequiredTier = 11,
                Type = RealmEventType.DivineRevelation,
                Id = "ds_event_sanctuary_global",
                Name = UILocalization.Get("event_superbody_descend"),
                Description = UILocalization.Get("event_superbody_descend_desc"),
                Damage = 0f,
                EnergyBonus = 50000f,
                IsGlobal = true
            });

            // 12阶 真神级 - 维度风暴（全局事件）
            RegisterEvent(new RealmEvent
            {
                RequiredTier = 12,
                Type = RealmEventType.RuleBacklash,
                Id = "ds_event_dengshen_tribulation",
                Name = UILocalization.Get("event_dimension_storm"),
                Description = UILocalization.Get("event_dimension_storm_desc"),
                Damage = 2000f,
                EnergyBonus = 100000f,
                IsGlobal = true
            });

            // 13阶 超神级 - 真神降世（全局事件）
            RegisterEvent(new RealmEvent
            {
                RequiredTier = 13,
                Type = RealmEventType.DivineRevelation,
                Id = "ds_event_zhenshen",
                Name = UILocalization.Get("event_truegod_descend"),
                Description = UILocalization.Get("event_truegod_descend_desc"),
                Damage = 0f,
                EnergyBonus = 500000f,
                IsGlobal = true
            });

            DSDebug.Verbose("境界专属事件初始化完成，共" + _eventsByTier.Count + "个境界有事件");
        }

        private static void RegisterEvent(RealmEvent realmEvent)
        {
            if (!_eventsByTier.ContainsKey(realmEvent.RequiredTier))
                _eventsByTier[realmEvent.RequiredTier] = new List<RealmEvent>();

            _eventsByTier[realmEvent.RequiredTier].Add(realmEvent);
        }

        /// <summary>突破时触发事件</summary>
        public static void OnBreakthrough(Actor actor, int newTier)
        {
            if (actor == null || !actor.isAlive()) return;
            if (!_eventsByTier.ContainsKey(newTier)) return;

            foreach (var evt in _eventsByTier[newTier])
            {
                TriggerEvent(actor, evt);
            }
        }

        /// <summary>安全造成伤害（用反射调用getHit，避免访问权限问题）</summary>
        private static void SafeDealDamage(Actor actor, float damage)
        {
            if (actor == null || !actor.isAlive() || damage <= 0f) return;
            try
            {
                // 尝试用反射调用getHit方法
                MethodInfo getHitMethod = typeof(Actor).GetMethod("getHit",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(float), typeof(bool), typeof(int), typeof(BaseSimObject), typeof(bool), typeof(bool), typeof(bool) },
                    null);
                
                if (getHitMethod != null)
                {
                    getHitMethod.Invoke(actor, new object[] { damage, false, 0, null, false, false, false });
                    return;
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[境界事件] 反射调用getHit失败: " + e.Message);
            }
            
            // 回退：直接修改生命值字段
            try
            {
                FieldInfo hpField = typeof(Actor).GetField("hp", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (hpField == null)
                {
                    hpField = typeof(Actor).GetField("currentHealth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (hpField == null)
                {
                    hpField = typeof(Actor).GetField("health", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                
                if (hpField != null)
                {
                    float currentHp = (float)hpField.GetValue(actor);
                    hpField.SetValue(actor, currentHp - damage);
                    return;
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[境界事件] 直接修改生命值失败: " + e.Message);
            }
        }

        /// <summary>触发事件</summary>
        private static void TriggerEvent(Actor actor, RealmEvent evt)
        {
            // 使用事件管理器记录事件（供图鉴面板显示，减少控制台刷屏，个人史诗风格）
            // 世界异象事件（如空间撕裂）：标题=事件名，描述=触发者+事件描述
            // 普通突破/其他事件：标题=事件名，描述=突破格式
            string eventTitle = evt.Name;
            string eventDesc;
            
            if (evt.Type == RealmEventType.WorldAnomaly)
            {
                // 世界异象：描述包含触发者名字
                eventDesc = string.Format("{0}引发了{1}。{2}", actor.getName(), evt.Name, evt.Description);
            }
            else
            {
                // 普通事件：用突破格式
                eventDesc = string.Format(UILocalization.Get("event_breakthrough"), actor.getName(), evt.RequiredTier, UILocalization.GetTierName(evt.RequiredTier));
            }

            DSEventManager.RecordEvent(
                evt.Type == RealmEventType.RuleBacklash ? DSEventType.RuleBacklash :
                evt.Type == RealmEventType.WorldAnomaly ? DSEventType.Disaster :
                evt.IsGlobal ? DSEventType.Divine : DSEventType.Breakthrough,
                eventTitle,
                eventDesc,
                actor.getName(),
                ActorDataAccessor.GetData(actor).id, evt.RequiredTier, actor);

            // 规则反噬伤害
            if (evt.Damage > 0f)
            {
                SafeDealDamage(actor, evt.Damage);

                // 如果单位死亡，记录警告
                if (!actor.isAlive())
                {
                    DSEventManager.RecordEvent(
                        DSEventType.Warning,
                        UILocalization.Get("event_gene_collapse"),
                        string.Format(UILocalization.Get("event_backfire"), actor.getName(), evt.Name),
                        actor.getName(),
                        ActorDataAccessor.GetData(actor).id, 0, actor);
                    return;
                }
            }

            // 能量奖励
            if (evt.EnergyBonus > 0f)
            {
                float currentEnergy = CultivationData.GetEnergy(actor);
                CultivationData.SetEnergy(actor, currentEnergy + evt.EnergyBonus);
            }

            // 全局事件效果
            if (evt.IsGlobal)
            {
                ApplyGlobalEventEffect(actor, evt);
            }

            // 特殊事件效果
            ApplySpecialEventEffect(actor, evt);

            // 添加到活跃事件列表
            _activeEvents.Add(evt);
        }

        /// <summary>全局事件效果</summary>
        private static void ApplyGlobalEventEffect(Actor actor, RealmEvent evt)
        {
            if (World.world == null || World.world.units == null) return;

            int affectedCount = 0;
            switch (evt.Id)
            {
                case "ds_event_sanctuary_global":
                    // 超神级降临：所有异能者获得短暂增益
                    foreach (var unit in World.world.units.units_only_alive)
                    {
                        if (unit == null || !unit.isAlive()) continue;
                        int tier = CultivationData.GetRealmTier(unit);
                        if (tier > 0)
                        {
                            unit.addTrait("blessed", false);
                            float energy = CultivationData.GetEnergy(unit);
                            CultivationData.SetEnergy(unit, energy + 1000f);
                            affectedCount++;
                        }
                    }
                    DSDebug.Verbose($"[DivineAscension]  超神级降临全局效果：{affectedCount}个异能者获得能量洗礼");
                    break;

                case "ds_event_dengshen_tribulation":
                    // 维度风暴：全局异能者受到风暴余波
                    foreach (var unit in World.world.units.units_only_alive)
                    {
                        if (unit == null || !unit.isAlive()) continue;
                        int tier = CultivationData.GetRealmTier(unit);
                        if (tier > 0 && unit != actor)
                        {
                            SafeDealDamage(unit, 100f);
                            affectedCount++;
                        }
                    }
                    DSDebug.Verbose($"[DivineAscension]  维度风暴全局效果：{affectedCount}个异能者受到风暴余波");
                    break;

                case "ds_event_zhenshen":
                    // 真神降世：全局异能者获得大量能量奖励
                    foreach (var unit in World.world.units.units_only_alive)
                    {
                        if (unit == null || !unit.isAlive()) continue;
                        int tier = CultivationData.GetRealmTier(unit);
                        if (tier > 0)
                        {
                            float energy = CultivationData.GetEnergy(unit);
                            CultivationData.SetEnergy(unit, energy + 5000f);
                            unit.addTrait("blessed", false);
                            affectedCount++;
                        }
                    }
                    DSDebug.Verbose($"[DivineAscension]  真神降世全局效果：{affectedCount}个异能者获得能量恩赐！");
                    break;
            }
        }

        /// <summary>特殊事件效果</summary>
        private static void ApplySpecialEventEffect(Actor actor, RealmEvent evt)
        {
            switch (evt.Id)
            {
                case "ds_event_space_tear":
                    // 空间撕裂：在单位位置生成虚空裂隙
                    Disaster.DisasterManager.SpawnDebugRift(actor.current_position.x, actor.current_position.y);
                    break;

                case "ds_event_law_tribulation":
                    // 规则反噬：单位获得规则抗性（用immune_to_disease模拟）
                    actor.addTrait("immune_to_disease", false);
                    break;

                case "ds_event_zhenshen":
                    // 真神降世：单位获得不朽
                    actor.addTrait("immortal", false);
                    break;
            }
        }

        /// <summary>年度事件更新</summary>
        public static void OnAnnualTick()
        {
            // 清理过期的活跃事件（保留最近10个）
            if (_activeEvents.Count > 10)
            {
                _activeEvents.RemoveRange(0, _activeEvents.Count - 10);
            }
        }
    }
}







