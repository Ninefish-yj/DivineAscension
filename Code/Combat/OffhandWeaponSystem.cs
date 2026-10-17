// ============================================================
// 副手武器槽系统（v0.18.78）
//
// 背景：原版 ActorEquipment 固定 6 槽（helmet/armor/weapon/boots/ring/amulet，
//   EquipmentType 枚举不可运行时扩展），存档按枚举 type 回填、装备 UI 按枚举遍历，
//   无法在原版框架内加第 7 个槽。本系统在模组侧自建"副手武器槽"：
//   - 数据：逻辑槽位（"世界种子_单位id" → 武器资产id），不占原版任何数据结构，
//     不实例化 Item 实体（无挂载/泄漏问题），存档即真相
//   - 存档：persistentDataPath/GeneCultivation/offhand.json（变更即存，JSON）
//   - 战斗：AbilityStatusPatch 读取副手能量刃追加伤害 + 出鞘演出
//   - 显示：单位窗 showStatsRows Postfix 增加"副手武器"行
//
// 依赖方向：Offhand → VirtualWeaponSystem（单向），不反向引用。
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Code.Core;

namespace Code.Combat
{
    /// <summary>副手武器槽：模组自建逻辑槽位，独立于原版装备系统。</summary>
    public static class OffhandWeaponSystem
    {
        /// <summary>副手能量刃武器资产ID（与 VirtualWeaponSystem.WEAPON_ITEM_ID 一致）</summary>
        public const string OFFHAND_ENERGY_BLADE_ID = "ds_energy_blade";

        /// <summary>副手能量刃每击追加伤害（固定值，不随特质翻倍，防止与主手虚拟伤害叠加爆炸）</summary>
        public const float OFFHAND_ENERGY_BLADE_BONUS = 4f;

        /// <summary>逻辑槽位：key = "{世界种子}_{单位data.id}" → 武器资产id</summary>
        private static readonly Dictionary<string, string> _offhandByKey = new Dictionary<string, string>();

        /// <summary>当前已加载的世界种子（种子变化 → 清空内存，防止跨存档串数据）</summary>
        private static int _currentSeed = int.MinValue;

        /// <summary>存档是否已加载（惰性加载）</summary>
        private static bool _saveLoaded = false;

        /// <summary>清理计时（每 60 秒全遍历一次清除死亡单位条目）</summary>
        private static float _cleanupTimer = 0f;

        /// <summary>存档根目录（与 DSDebug/维度空间同风格：persistentDataPath 下独立目录）</summary>
        private static string SaveRoot => Path.Combine(Application.persistentDataPath, "GeneCultivation");

        private static string SaveFile => Path.Combine(SaveRoot, "offhand.json");

        private static string Key(Actor actor) => MapBox.current_world_seed_id + "_" + actor.data.id;

        /// <summary>
        /// 世界一致性检查：种子变化时清空内存并重新加载存档（惰性）。
        /// </summary>
        private static void EnsureCurrentWorld()
        {
            if (_currentSeed != MapBox.current_world_seed_id)
            {
                _offhandByKey.Clear();
                _currentSeed = MapBox.current_world_seed_id;
                _saveLoaded = false;
            }
            if (!_saveLoaded)
            {
                LoadAll();
                _saveLoaded = true;
            }
        }

        /// <summary>单位是否有副手武器</summary>
        public static bool HasOffhand(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return false;
            try
            {
                EnsureCurrentWorld();
                return _offhandByKey.ContainsKey(Key(actor));
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[副手武器] HasOffhand 异常: " + e.Message);
                return false;
            }
        }

        /// <summary>读取单位副手武器资产ID（无则 null）</summary>
        public static string GetOffhandAssetId(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return null;
            try
            {
                EnsureCurrentWorld();
                string id;
                if (_offhandByKey.TryGetValue(Key(actor), out id)) return id;
                return null;
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[副手武器] GetOffhandAssetId 异常: " + e.Message);
                return null;
            }
        }

        /// <summary>副手武器资产（无则 null）</summary>
        public static EquipmentAsset GetOffhandAsset(Actor actor)
        {
            string id = GetOffhandAssetId(actor);
            if (string.IsNullOrEmpty(id)) return null;
            try { return AssetManager.items.get(id) as EquipmentAsset; }
            catch { return null; }
        }

        /// <summary>装备副手武器（替换旧副手，变更即存）</summary>
        public static void SetOffhand(Actor actor, string itemAssetId)
        {
            if (actor == null || !actor.isAlive() || string.IsNullOrEmpty(itemAssetId)) return;
            try
            {
                EnsureCurrentWorld();
                _offhandByKey[Key(actor)] = itemAssetId;
                SaveAll();
                DSDebug.Verbose($"[副手武器] {actor.getName()} 副手装备 {itemAssetId}");
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[副手武器] SetOffhand 异常: " + e.Message);
            }
        }

        /// <summary>卸下副手武器（变更即存）</summary>
        public static void ClearOffhand(Actor actor)
        {
            if (actor == null) return;
            try
            {
                EnsureCurrentWorld();
                if (_offhandByKey.Remove(Key(actor)))
                {
                    SaveAll();
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[副手武器] ClearOffhand 异常: " + e.Message);
            }
        }

        /// <summary>
        /// 能量刃自动入副手（v0.18.78）
        /// 能量武器单位：主手保持玩家已有武器不变（尊重玩家装备），副手空槽时自动装备能量刃——
        /// 即"加一个武器槽"的核心入口：主手任意武器 + 副手能量刃 双持共存。
        /// 主手已是能量刃时不重复装副手。
        /// </summary>
        public static void EnsureEnergyBladeOffhand(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return;
            try
            {
                if (!VirtualWeaponSystem.HasVirtualEnergyWeapon(actor)) return;
                // 主手是能量刃 → 副手不重复装
                if (actor.equipment != null && !actor.equipment.weapon.isEmpty())
                {
                    var mainWeapon = actor.equipment.weapon.getItem();
                    if (mainWeapon != null && mainWeapon.getAsset() != null &&
                        mainWeapon.getAsset().id == OFFHAND_ENERGY_BLADE_ID) return;
                }
                if (HasOffhand(actor)) return;
                SetOffhand(actor, OFFHAND_ENERGY_BLADE_ID);
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[副手武器] EnsureEnergyBladeOffhand 异常: " + e.Message);
            }
        }

        /// <summary>
        /// 副手追加伤害（AbilityStatusPatch.getHit 调用）
        /// 副手能量刃：每击追加固定能量伤害（不随特质翻倍）。
        /// </summary>
        public static float GetOffhandBonusDamage(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return 0f;
            try
            {
                string id = GetOffhandAssetId(actor);
                if (id == OFFHAND_ENERGY_BLADE_ID) return OFFHAND_ENERGY_BLADE_BONUS;
                return 0f;
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[副手武器] GetOffhandBonusDamage 异常: " + e.Message);
                return 0f;
            }
        }

        /// <summary>
        /// 每帧清理（由 AnnualTickDriver 调用）：每 60 秒全遍历世界单位，
        /// 清除已死亡/已移除单位的副手条目（变更即存）。
        /// </summary>
        public static void UpdateCleanup(float deltaTime)
        {
            try
            {
                EnsureCurrentWorld();
                if (_offhandByKey.Count == 0) return;
                _cleanupTimer += deltaTime;
                if (_cleanupTimer < 60f) return;
                _cleanupTimer = 0f;

                if (World.world == null || World.world.units == null) return;
                // 收集当前存活单位 id 集合（同存档内 data.id 唯一）
                HashSet<long> aliveIds = new HashSet<long>();
                foreach (var unit in World.world.units)
                {
                    if (unit != null && unit.isAlive()) aliveIds.Add(unit.data.id);
                }
                bool changed = false;
                List<string> staleKeys = null;
                string seedPrefix = MapBox.current_world_seed_id + "_";
                foreach (var kv in _offhandByKey)
                {
                    // 只处理当前种子条目（历史种子条目随 EnsureCurrentWorld 已清）
                    if (!kv.Key.StartsWith(seedPrefix, StringComparison.Ordinal)) continue;
                    long actorId;
                    if (!long.TryParse(kv.Key.Substring(seedPrefix.Length), out actorId)) continue;
                    if (!aliveIds.Contains(actorId))
                    {
                        if (staleKeys == null) staleKeys = new List<string>();
                        staleKeys.Add(kv.Key);
                        changed = true;
                    }
                }
                if (staleKeys != null)
                {
                    foreach (var k in staleKeys) _offhandByKey.Remove(k);
                }
                if (changed) SaveAll();
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[副手武器] UpdateCleanup 异常: " + e.Message);
            }
        }

        // ---------- 存档（JSON，变更即存，惰性加载） ----------

        private static void SaveAll()
        {
            try
            {
                if (!Directory.Exists(SaveRoot)) Directory.CreateDirectory(SaveRoot);
                string json = JsonConvert.SerializeObject(_offhandByKey);
                File.WriteAllText(SaveFile, json);
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[副手武器] 存档失败: " + e.Message);
            }
        }

        private static void LoadAll()
        {
            try
            {
                if (!File.Exists(SaveFile)) return;
                string json = File.ReadAllText(SaveFile);
                var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (loaded == null) return;
                foreach (var kv in loaded)
                {
                    // 只装载当前种子条目（跨存档条目忽略）
                    if (kv.Key.StartsWith(MapBox.current_world_seed_id + "_", StringComparison.Ordinal))
                    {
                        _offhandByKey[kv.Key] = kv.Value;
                    }
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose("[副手武器] 读档失败: " + e.Message);
            }
        }
    }
}
