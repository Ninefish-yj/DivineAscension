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

        // 能量刃投射物ID
        private const string ENERGY_BLADE_PROJECTILE_ID = "ds_energy_blade_projectile";

        // 投射物是否已初始化
        private static bool _projectileInitialized = false;

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
                DSDebug.Verbose("[DivineAscension] 虚拟武器系统初始化（伤害加成已合并到AbilityStatusPatch）");
                // 初始化能量刃投射物
                InitEnergyBladeProjectile();
                DSDebug.Verbose("[DivineAscension] 虚拟武器系统初始化完成");
            }
            catch (Exception e)
            {
                DSDebug.Error($"[DivineAscension] 虚拟武器系统初始化失败: {e.Message}\n{e.StackTrace}");
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
                Type projectileAssetType = Type.GetType("Code.Game.ProjectileAsset, Assembly-CSharp");
                if (projectileAssetType == null)
                {
                    DSDebug.Verbose("[虚拟武器] 未找到ProjectileAsset类型，使用延迟特效模拟远程攻击");
                    return;
                }

                // 查找UpsertProjectile方法
                var upsertMethod = typeof(ProjectileLibrary).GetMethod("upsert", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (upsertMethod == null)
                {
                    // 尝试其他方法名
                    foreach (var m in typeof(ProjectileLibrary).GetMethods(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                    {
                        if (m.Name.ToLower().Contains("upsert") || m.Name.ToLower().Contains("getorcreate"))
                        {
                            upsertMethod = m;
                            break;
                        }
                    }
                }

                if (upsertMethod == null)
                {
                    DSDebug.Verbose("[虚拟武器] 未找到UpsertProjectile方法，使用延迟特效模拟远程攻击");
                    return;
                }

                // 创建能量刃投射物（基于fireball）
                var projectile = upsertMethod.Invoke(null, new object[] { ENERGY_BLADE_PROJECTILE_ID, "fireball" });
                if (projectile == null)
                {
                    DSDebug.Verbose("[虚拟武器] 创建能量刃投射物失败，使用延迟特效模拟远程攻击");
                    return;
                }

                // 设置投射物属性（参考红袍法师的火球）
                SetPropertyIfExists(projectile, "texture", "ds_energy_blade");
                SetPropertyIfExists(projectile, "animation_speed", 12f);
                SetPropertyIfExists(projectile, "speed", 25f);
                SetPropertyIfExists(projectile, "speed_random", 3f);
                SetPropertyIfExists(projectile, "scale_start", 0.04f);
                SetPropertyIfExists(projectile, "scale_target", 0.12f);
                SetPropertyIfExists(projectile, "draw_light_area", true);
                SetPropertyIfExists(projectile, "draw_light_size", 0.12f);
                SetPropertyIfExists(projectile, "trail_effect_enabled", true);
                SetPropertyIfExists(projectile, "trail_effect_id", "fx_sparkle");
                SetPropertyIfExists(projectile, "trail_effect_scale", 0.08f);
                SetPropertyIfExists(projectile, "trail_effect_timer", 0.05f);
                SetPropertyIfExists(projectile, "end_effect", "fx_explosion");
                SetPropertyIfExists(projectile, "end_effect_scale", 0.15f);
                SetPropertyIfExists(projectile, "trigger_on_collision", true);
                SetPropertyIfExists(projectile, "look_at_target", true);

                DSDebug.Verbose("[虚拟武器] 能量刃投射物初始化成功");
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[虚拟武器] 能量刃投射物初始化失败: {e.Message}，使用延迟特效模拟远程攻击");
            }
        }

        /// <summary>
        /// 设置对象的属性（如果存在）
        /// </summary>
        private static void SetPropertyIfExists(object obj, string propertyName, object value)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName, 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(obj, value);
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[DivineAscension] SetPropertyIfExists 异常: " + dsEx.Message); }
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
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] GetVirtualWeaponArmorPenetration 异常: " + dsEx.Message); }
            
            // 护甲穿透上限50%
            return Mathf.Min(0.5f, baseArmorPen);
        }

        /// <summary>
        /// 播放虚拟武器攻击视觉效果
        /// 在单位攻击时调用，使用原版投射物系统发射能量刃（参考红袍法师的火球攻击）
        /// </summary>
        public static void PlayWeaponAttackEffect(Actor attacker, Actor target)
        {
            try
            {
                if (attacker == null || target == null) return;
                if (!HasVirtualEnergyWeapon(attacker)) return;

                // 尝试使用原版投射物系统发射能量刃
                if (TrySpawnProjectile(attacker, target))
                {
                    DSDebug.Verbose($"[虚拟武器] {attacker.getName()} 发射能量刃投射物（远程攻击）");
                    return;
                }

                // 回退：使用延迟特效模拟远程攻击
                // 1. 在施法者位置播放能量刃发射特效
                if (DSSkillEffectManager.HasEffect(VIRTUAL_WEAPON_EFFECT_ID))
                {
                    DSSkillEffectManager.PlayEffect(attacker, VIRTUAL_WEAPON_EFFECT_ID, null);
                }

                // 2. 延迟一段时间后，在目标位置播放能量刃命中特效
                ScheduleDelayedEffect(target, 0.3f);
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[DivineAscension] 虚拟武器攻击特效播放失败: {e.Message}");
            }
        }

        /// <summary>
        /// 尝试使用原版投射物系统发射能量刃
        /// </summary>
        private static bool TrySpawnProjectile(Actor attacker, Actor target)
        {
            try
            {
                if (World.world == null || World.world.projectiles == null) return false;

                // 获取发射位置和目标位置
                Vector2 launchPos = attacker.current_position;
                Vector2 targetPos = target.current_position;

                // 使用反射调用World.world.projectiles.spawn方法
                var projectiles = World.world.projectiles;
                var spawnMethod = projectiles.GetType().GetMethod("spawn", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (spawnMethod == null)
                {
                    // 尝试查找其他方法名
                    foreach (var m in projectiles.GetType().GetMethods(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        if (m.Name.ToLower().Contains("spawn") || m.Name.ToLower().Contains("fire") || 
                            m.Name.ToLower().Contains("launch"))
                        {
                            spawnMethod = m;
                            break;
                        }
                    }
                }

                if (spawnMethod == null) return false;

                // 尝试调用spawn方法（参数：caster, target, projectileId, launchPos, targetPos）
                var parameters = spawnMethod.GetParameters();
                if (parameters.Length >= 5)
                {
                    spawnMethod.Invoke(projectiles, new object[] { 
                        attacker, target, ENERGY_BLADE_PROJECTILE_ID, launchPos, targetPos 
                    });
                    return true;
                }
                else if (parameters.Length >= 3)
                {
                    // 简化版本：caster, target, projectileId
                    spawnMethod.Invoke(projectiles, new object[] { 
                        attacker, target, ENERGY_BLADE_PROJECTILE_ID 
                    });
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                // 只输出一次，避免刷屏
                if (!_projectileErrorLogged)
                {
                    DSDebug.Warning($"[虚拟武器] 发射投射物参数不匹配: {e.Message}（后续不再重复输出）");
                    _projectileErrorLogged = true;
                }
                return false;
            }
        }

        /// <summary>
        /// 待播放的延迟特效列表
        /// </summary>
        private static readonly List<DelayedEffect> _delayedEffects = new List<DelayedEffect>();

        /// <summary>
        /// 延迟特效数据
        /// </summary>
        private class DelayedEffect
        {
            public Actor Target;
            public float RemainingTime;
        }

        /// <summary>
        /// 调度延迟特效
        /// </summary>
        private static void ScheduleDelayedEffect(Actor target, float delay)
        {
            _delayedEffects.Add(new DelayedEffect
            {
                Target = target,
                RemainingTime = delay
            });
        }

        /// <summary>
        /// 更新延迟特效（由ModClass.Update调用）
        /// </summary>
        public static void UpdateDelayedEffects(float deltaTime)
        {
            if (_delayedEffects.Count == 0) return;

            for (int i = _delayedEffects.Count - 1; i >= 0; i--)
            {
                var delayed = _delayedEffects[i];
                delayed.RemainingTime -= deltaTime;
                if (delayed.RemainingTime <= 0f)
                {
                    // 在目标位置播放能量刃命中特效
                    if (delayed.Target != null && delayed.Target.isAlive())
                    {
                        try
                        {
                            // 播放命中特效（使用原版爆炸特效）
                            Vector2 pos = delayed.Target.current_position;
                            PlayHitEffect(pos);
                        }
                        catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] UpdateDelayedEffects 异常: " + dsEx.Message); }
                    }
                    _delayedEffects.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 播放能量刃命中特效
        /// </summary>
        private static void PlayHitEffect(Vector2 position)
        {
            try
            {
                if (World.world == null) return;
                WorldTile tile = World.world.GetTileSimple((int)position.x, (int)position.y);
                if (tile == null) return;

                // 使用原版爆炸特效作为命中特效
                var spawnMethods = typeof(EffectsLibrary).GetMethods(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                foreach (var method in spawnMethods)
                {
                    if (method.Name == "spawn" && method.GetParameters().Length == 8)
                    {
                        method.Invoke(null, new object[] {
                            "fx_explosion", tile, null, null, 0f, 1.5f, -1f, null
                        });
                        return;
                    }
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] PlayHitEffect 异常: " + dsEx.Message); }
        }
    }
}
