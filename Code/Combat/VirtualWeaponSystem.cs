using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using Code.Core;
using Code.Data;
using Code.Realm;

namespace Code.Combat
{
    /// <summary>
    /// 虚拟武器系统：能量武器特质提供一把不占用装备栏的虚拟武器
    /// 伤害加成已合并到AbilityStatusPatch中处理，这里只负责攻击特效
    /// </summary>
    public static class VirtualWeaponSystem
    {
        // 能量武器特质ID
        public const string ENERGY_WEAPON_TRAIT = "ds_status_energy_weapon";

        // 能量武器属性（基础值，实际通过特质stats生效）
        public const float WEAPON_DAMAGE = 80f;        // 额外伤害（在特质stats基础上叠加）
        public const float WEAPON_ARMOR_PENETRATION = 0.2f; // 护甲穿透20%

        // 虚拟武器攻击特效ID
        private const string VIRTUAL_WEAPON_EFFECT_ID = "ds_ability_energy_blade";

        // 能量刃投射物ID（public：副手武器系统双持开火复用同一投射物）
        public const string ENERGY_BLADE_PROJECTILE_ID = "ds_energy_blade_projectile";
        // 能量刃真实武器ID（v0.18.77：虚拟武器真实化——注册为原版武器资产，觉醒单位真实装备，AI自动发射投射物）
        private const string WEAPON_ITEM_ID = "ds_energy_blade";

        // 武器资产是否已注册
        private static bool _weaponInitialized = false;

        // 投射物是否已初始化
        private static bool _projectileInitialized = false;
        /// <summary>投射物帧是否就绪（v0.18.76：原版 drawProjectiles 会取 frames[0]，
        /// 贴图缺失→空帧数组→每帧 ArgumentOutOfRangeException 刷屏。注册后立即预加载校验，
        /// 未就绪则 TrySpawnProjectile 回退到延迟特效，绝不发射空帧投射物）。</summary>
        private static bool _projectileFramesReady = false;

        private static bool _initialized = false;

        // 投射物错误是否已输出（避免刷屏）
        private static bool _projectileErrorLogged = false;

        /// <summary>
        /// 初始化虚拟武器系统
        /// 伤害加成已合并到AbilityStatusPatch中处理，这里只初始化攻击特效
        /// </summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                DSDebug.Verbose("[GeneCultivation] 虚拟武器系统初始化（伤害加成已合并到AbilityStatusPatch）");
                // 初始化能量刃投射物 + 注册真实武器资产
                InitEnergyBladeProjectile();
                InitEnergyBladeWeapon();
                DSDebug.Verbose("[GeneCultivation] 虚拟武器系统初始化完成");
            }
            catch (Exception e)
            {
                DSDebug.Error($"[GeneCultivation] 虚拟武器系统初始化失败: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 初始化能量刃投射物（参考红袍法师的火球投射物）
        /// </summary>
        private static void InitEnergyBladeProjectile()
        {
            if (_projectileInitialized) return;
            _projectileInitialized = true;

            try
            {
                // 直接用原版资产库注册能量刃投射物（ProjectileAsset 全 public 字段；AssetManager.projectiles 为 ProjectileLibrary）
                // 此前反射找 ProjectileLibrary.upsert/getorcreate 均不存在（该库只有 init），投射物资产从未真正注册，
                // 导致发射时引用空资产——远程特效实际从未生效。改用 AssetLibrary.add 注册。
                ProjectileAsset asset = new ProjectileAsset();
                asset.id = ENERGY_BLADE_PROJECTILE_ID;
                asset.texture = "ds_energy_blade";
                asset.animation_speed = 12f;
                asset.speed = 25f;
                asset.speed_random = 3f;
                asset.scale_start = 0.04f;
                asset.scale_target = 0.12f;
                asset.draw_light_area = true;
                asset.draw_light_size = 0.12f;
                asset.trail_effect_enabled = true;
                asset.trail_effect_id = "fx_sparkle";
                asset.trail_effect_scale = 0.08f;
                asset.trail_effect_timer = 0.05f;
                asset.end_effect = "fx_explosion";
                asset.end_effect_scale = 0.15f;
                asset.trigger_on_collision = true;
                asset.look_at_target = true;
                AssetManager.projectiles.add(asset);

                // v0.18.76：预加载投射物帧（原版 Projectile.spawn 懒加载 effects/projectiles/<texture>，
                // 加载失败 frames 为空数组 → QuantumSpriteLibrary.drawProjectiles 取 frames[0] 越界刷屏）。
                // 这里主动加载并赋给 asset.frames，保证渲染链路拿到真实帧。
                var projFrames = SpriteTextureLoader.getSpriteList("effects/projectiles/" + asset.texture);
                asset.frames = projFrames;
                _projectileFramesReady = projFrames != null && projFrames.Length > 0;
                DSDebug.Verbose($"[虚拟武器] 能量刃投射物注册成功 frames={(_projectileFramesReady ? projFrames.Length.ToString() : "缺失")}");
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[虚拟武器] 能量刃投射物初始化失败: {e.Message}，使用延迟特效模拟远程攻击");
            }
        }

        /// <summary>
        /// 注册能量刃真实武器（v0.18.77）
        /// 参考原版 $range 武器模板（ItemLibrary.initTemplatesWeapons）：Range 型 + projectile 绑定能量刃投射物，
        /// 单位装备后原版 AI 攻击自动发射投射物（Actor攻击逻辑 getWeaponAsset().projectile + hasRangeAttack()），
        /// 不再需要反射 hack；武器持握贴图 items/weapons/w_ds_energy_blade。
        /// </summary>
        private static void InitEnergyBladeWeapon()
        {
            if (_weaponInitialized) return;
            _weaponInitialized = true;
            try
            {
                if (AssetManager.items.get(WEAPON_ITEM_ID) != null) return; // 已注册（重载/多次Init）

                EquipmentAsset blade = new EquipmentAsset();
                blade.id = WEAPON_ITEM_ID;
                blade.equipment_type = EquipmentType.Weapon;
                blade.equipment_subtype = "sword";
                blade.attack_type = WeaponType.Range;          // 远程：AI自动发射投射物
                blade.projectile = ENERGY_BLADE_PROJECTILE_ID; // 绑定能量刃投射物
                blade.path_slash_animation = "effects/slashes/slash_sword";
                blade.path_gameplay_sprite = "items/weapons/w_ds_energy_blade";
                blade.has_locales = false;
                blade.base_stats["damage"] = 6f;               // 武器基础伤害（特质加成在AbilityStatusPatch叠加，不冲突）
                blade.base_stats["damage_range"] = 0.8f;
                blade.base_stats["range"] = 14f;               // 能量刃射程
                blade.base_stats["projectiles"] = 1f;
                blade.base_stats["critical_chance"] = 0.1f;
                blade.base_stats["critical_damage_multiplier"] = 0.5f;
                AssetManager.items.add(blade);
                DSDebug.Verbose("[虚拟武器] 能量刃真实武器注册成功 " + WEAPON_ITEM_ID);
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[虚拟武器] 能量刃武器注册失败: {e.Message}");
            }
        }

        /// <summary>
        /// 确保能量武器单位装备真实能量刃（v0.18.77）
        /// 觉醒能量武器特质但未装备时，用原版 createNewWeapon 创建并装备到武器槽——
        /// 武器真实可见、可被单位窗查看，AI 使用原版武器攻击链（Range→自动发射投射物）。
        /// </summary>
        public static void EnsureEnergyWeapon(Actor actor)
        {
            try
            {
                if (actor == null || !actor.isAlive()) return;
                if (!HasVirtualEnergyWeapon(actor)) return;    // 无特质不干预
                if (actor.equipment == null) return;
                // 已装备能量刃则不动（保留玩家自定义武器）
                if (!actor.equipment.weapon.isEmpty())
                {
                    var cur = actor.equipment.weapon.getItem();
                    if (cur != null && cur.getAsset() != null && cur.getAsset().id == WEAPON_ITEM_ID) return;
                    return; // 有其他武器：尊重现状，不强行替换
                }
                // 空槽才装备能量刃
                if (actor.canUseItems())
                {
                    actor.createNewWeapon(WEAPON_ITEM_ID);
                    DSDebug.Verbose($"[虚拟武器] {actor.getName()} 装备能量刃");
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[虚拟武器] EnsureEnergyWeapon 异常: {e.Message}");
            }
        }

        /// <summary>
        /// 检查单位是否有虚拟能量武器
        /// </summary>
        public static bool HasVirtualEnergyWeapon(Actor actor)
        {
            if (actor == null || !actor.isAlive()) return false;
            return actor.hasTrait(ENERGY_WEAPON_TRAIT);
        }

        /// <summary>
        /// 获取虚拟武器的额外伤害加成
        /// 在特质stats基础上叠加的额外伤害
        /// 注意：GE公式（基因能量统一公式）通过AbilityStatusPatch统一接入，
        ///       这里只计算基础伤害，避免双重加成。
        /// </summary>
        public static float GetVirtualWeaponDamage(Actor attacker)
        {
            if (!HasVirtualEnergyWeapon(attacker)) return 0f;

            float baseDamage = WEAPON_DAMAGE;

            // 根据境界提升伤害（7阶基础，每高1阶+15%）
            int tier = CultivationData.GetRealmTier(attacker);
            if (tier > 7)
            {
                baseDamage *= (1f + 0.15f * (tier - 7));
            }

            // GE公式（基因能量统一公式）通过AbilityStatusPatch统一接入：
            // 在AbilityStatusPatch.PrefixNamed中，虚拟武器伤害加到总伤害后，
            // 会统一应用基因能量伤害加成（damage *= (1f + geDamage / 30f)）
            // 这里不再单独接入，避免双重加成。

            return baseDamage;
        }

        /// <summary>
        /// 获取虚拟武器的护甲穿透
        /// 接入GE公式：基因能量越高，护甲穿透越高
        /// </summary>
        public static float GetVirtualWeaponArmorPenetration(Actor attacker)
        {
            if (!HasVirtualEnergyWeapon(attacker)) return 0f;
            
            float baseArmorPen = WEAPON_ARMOR_PENETRATION;
            
            // 接入GE公式：基因能量越高，护甲穿透越高
            try
            {
                float geBonus = Code.Core.GeneEnergyCalculator.GetDamageBonus(attacker);
                // GE加成每10点，护甲穿透+5%（基础20%，GE=10时25%，GE=20时30%）
                baseArmorPen += geBonus * 0.005f;
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] GetVirtualWeaponArmorPenetration 异常: " + dsEx.Message); }
            
            // 护甲穿透上限50%
            return Mathf.Min(0.5f, baseArmorPen);
        }

        /// <summary>
        /// 虚拟武器攻击演出（v0.18.77 重构）
        /// 真实武器化后：命中时仅确保单位装备能量刃（原版 AI 攻击链会自动发射投射物，命中端特效由投射物
        /// end_effect=fx_explosion 播放），这里不再反射发射投射物——旧实现每次 getHit 都反射 spawn，
        /// 与原版武器发射叠加造成投射物泛滥/双发。
        /// </summary>
        public static void PlayWeaponAttackEffect(Actor attacker, Actor target)
        {
            try
            {
                if (attacker == null || target == null) return;
                if (!HasVirtualEnergyWeapon(attacker)) return;

                // 1. 保证真实武器（空武器槽才装备，尊重玩家已装武器）
                EnsureEnergyWeapon(attacker);

                // 2. 副手武器槽（v0.18.78）：主手武器保持玩家已有，能量刃自动入副手 → 双持共存
                OffhandWeaponSystem.EnsureEnergyBladeOffhand(attacker);

                // 3. 施法演出（能量刃出鞘特效；投射物飞行动画由原版投射物系统负责）
                if (DSSkillEffectManager.HasEffect(VIRTUAL_WEAPON_EFFECT_ID))
                {
                    DSSkillEffectManager.PlayEffect(attacker, VIRTUAL_WEAPON_EFFECT_ID, null);
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[GeneCultivation] 虚拟武器攻击特效播放失败: {e.Message}");
            }
        }

        /// <summary>
        /// 更新延迟特效（v0.18.77 保留空壳：真实武器化后原版武器系统自动发射投射物，
        /// 命中特效由投射物 end_effect 播放，不再需要延迟模拟。框架仍调用本方法，保持空转无害）
        /// </summary>
        public static void UpdateDelayedEffects(float deltaTime)
        {
        }
    }
}
