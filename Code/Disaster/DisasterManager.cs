// ============================================================

using System;
using Code.UI;
using System.Collections.Generic;
using Code.Core;
using Code.Data;
using Code.Realm;
using Code.Traits;
using UnityEngine;

namespace Code.Disaster
{
    /// <summary>被虚空裂隙修改的地块记录（用于裂隙消散后恢复原始地形）</summary>
    public class RiftModifiedTile
    {
        public int X;
        public int Y;
        public object OriginalType; // 原始地块类型枚举值
    }

    /// <summary>虚空裂隙数据</summary>
    public class VoidRift
    {
        public int Id;
        public float X;
        public float Y;
        public float Power;       // 裂隙强度
        public int BirthYear;     // 生成年份
        public int Duration;      // 持续年数
        public bool IsActive;
        public bool VisualApplied; // 视觉效果是否已应用
        public int NextPulseTime; // 下次脉冲时间（年）
        public List<RiftModifiedTile> ModifiedTiles = new List<RiftModifiedTile>(); // 被修改的地块记录
    }

    public static class DisasterManager
    {
        // 天地屏障完整度（全局，0~100）
        private static float _barrierIntegrity = 100f;
        private static float _barrierDecayRate = 0f;
        private static readonly List<VoidRift> _rifts = new List<VoidRift>();
        private static int _riftIdCounter = 0;
        private static bool _initialized = false;
        private static bool _barrierBrokenApplied = false; // 破碎瞬间的炸弹特效+全图伤害只触发一次

        // 阈值
        private const float RIFT_SPAWN_THRESHOLD = 80f;   // 屏障低于80%开始生成裂隙
        private const float RIFT_CRITICAL_THRESHOLD = 30f; // 屏障低于30%裂隙频繁生成
        private const int MAX_RIFTS = 10;                   // 最大同时存在裂隙数

        /// <summary>初始化灾变系统</summary>
        public static void Init()
        {
            _barrierIntegrity = 100f;
            _barrierDecayRate = 0f;
            _barrierBrokenApplied = false;
            _rifts.Clear();
            _riftIdCounter = 0;
            _initialized = true;
            DSDebug.Verbose("灾变系统初始化完成，天地屏障完整度=100%");
        }

        /// <summary>年度结算：更新屏障完整度，管理虚空裂隙</summary>

        /// <summary>裂隙生成阈值（屏障完整度低于该值时开始生成裂隙）</summary>
        public static float GetRiftSpawnThreshold() { return RIFT_SPAWN_THRESHOLD; }
        public static void OnAnnualTick()
        {
            if (!_initialized) Init();

            // 1. 计算屏障衰减率
            CalculateBarrierDecay();

            // 2. 应用衰减
            _barrierIntegrity = Mathf.Clamp(_barrierIntegrity - _barrierDecayRate, 0f, 100f);

            // 3. 屏障自然恢复（如果没有高阶异能者失控指数压力）
            if (_barrierDecayRate < 0.5f && _barrierIntegrity < 100f)
            {
                _barrierIntegrity = Mathf.Clamp(_barrierIntegrity + 0.3f, 0f, 100f);
            }

            // 3.5 高阶异能者自动修复屏障（8阶+，境界越高修复越多：0.1×(境界-7)/人/年）
            float activeRepair = CalculateActiveRepair();
            if (activeRepair > 0f && _barrierIntegrity < 100f)
            {
                _barrierIntegrity = Mathf.Clamp(_barrierIntegrity + activeRepair, 0f, 100f);
            }

            // 3.6 天地狂乱：屏障破碎且衰减无法被压制（自然恢复不触发）时，高阶异能者互相仇恨残杀，直到屏障能增长
            if (_barrierIntegrity <= 0f && _barrierDecayRate >= 0.5f)
            {
                ApplyWorldMadness();
            }

            // 4. 虚空裂隙生成（受NML配置开关控制）
            if (Code.Core.DengShenConfig.EnableVoidRifts)
            {
                if (_barrierIntegrity < RIFT_SPAWN_THRESHOLD && _rifts.Count < MAX_RIFTS)
                {
                    float spawnChance = (RIFT_SPAWN_THRESHOLD - _barrierIntegrity) / RIFT_SPAWN_THRESHOLD * 0.3f;
                    if (_barrierIntegrity < RIFT_CRITICAL_THRESHOLD)
                        spawnChance *= 2f;

                    //  13阶超神级全局影响：全球灾变发生概率-20%
                    if (Realm.RealmFeatures.HasTrueGodInWorld())
                    {
                        spawnChance *= 0.80f;
                    }

                    if (UnityEngine.Random.value < spawnChance)
                    {
                        SpawnRift();
                    }
                }

                // 6. 裂隙伤害（对附近单位）
                ApplyRiftDamage();
            }

            // 5. 更新现有裂隙（移到if外面，确保即使关闭了虚空裂隙功能，已有的裂隙也会被更新和移除）
            UpdateRifts();
        }

        /// <summary>计算高阶异能者自动修复量（8阶+，境界越高修复越多：0.04×(境界-7)/人/年）</summary>
        private static float CalculateActiveRepair()
        {
            float repair = 0f;
            var ids = GetActiveCultivatorIds();
            foreach (long id in ids)
            {
                Actor actor = FindActorById(id);
                if (actor == null) continue;
                int tier = CultivationData.GetRealmTier(actor);
                if (tier >= 8)
                    repair += 0.04f * (tier - 7);
            }
            return repair;
        }

        /// <summary>
        /// 天地狂乱：屏障破碎且衰减率≥0.5（自然恢复被压制）时，所有8阶+异能者互相建立仇恨，
        /// 失控最高者成为众矢之的（所有人锁定攻击它），由原版AI驱动自相残杀，直到屏障能增长
        /// </summary>
        private static void ApplyWorldMadness()
        {
            if (World.world == null || World.world.units == null) return;

            // 收集存活的8阶+异能者
            var highTiers = new List<Actor>();
            var ids = GetActiveCultivatorIds();
            foreach (long id in ids)
            {
                Actor actor = FindActorById(id);
                if (actor == null || !actor.isAlive()) continue;
                if (CultivationData.GetRealmTier(actor) >= 8)
                    highTiers.Add(actor);
            }
            if (highTiers.Count < 2) return;

            // 失控最高的单位成为众矢之的
            Actor primeTarget = null;
            float worstTurb = -1f;
            foreach (Actor a in highTiers)
            {
                float turb = CultivationData.GetTurbulence(a);
                if (turb > worstTurb) { worstTurb = turb; primeTarget = a; }
            }

            // 互相建立仇恨 + 锁定攻击目标
            foreach (Actor a in highTiers)
            {
                foreach (Actor b in highTiers)
                {
                    if (b == a) continue;
                    if (!a.isInAggroList(b))
                        a.addAggro(b);
                }
                if (primeTarget != null && primeTarget != a)
                    SetAttackTargetViaReflection(a, primeTarget);
            }

            DSDebug.Warning($"[DivineAscension] 天地狂乱：{highTiers.Count}名高阶异能者陷入互相仇恨，失控最严重的{primeTarget?.getName()}被全员围杀，直至屏障恢复");
        }

        /// <summary>反射设置攻击目标（attack_target字段运行时非public，避免FieldAccess异常）</summary>
        private static void SetAttackTargetViaReflection(Actor attacker, Actor target)
        {
            try
            {
                var field = typeof(BaseSimObject).GetField("attack_target",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                    field.SetValue(attacker, target);
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"[DivineAscension] 设置攻击目标失败: {e.Message}");
            }
        }

        /// <summary>计算屏障衰减率（基于高阶异能者数量和失控指数）</summary>
        private static void CalculateBarrierDecay()
        {
            float decay = 0f;
            var ids = GetActiveCultivatorIds();

            foreach (long id in ids)
            {
                Actor actor = FindActorById(id);
                if (actor == null) continue;
                int tier = CultivationData.GetRealmTier(actor);
                float turb = CultivationData.GetTurbulence(actor);

                // 高阶异能者对屏障的压力（8阶以上开始有显著影响）
                if (tier >= 8)
                {
                    decay += (tier - 7) * 0.05f;
                    // 失控指数加剧屏障衰减
                    if (turb > 50f)
                        decay += (turb - 50f) * 0.01f;
                }

                // 奇点以上异能者如果失控指数失控，大幅削弱屏障
                if (tier >= 12 && turb > 70f)
                {
                    decay += (turb - 70f) * 0.05f;
                }
            }

            _barrierDecayRate = decay;
        }

        /// <summary>生成虚空裂隙</summary>
        /// <summary>播放原版粒子特效（使用spawnAtTile方法，正确的参数顺序）</summary>
        private static void PlayOriginalEffect(string effectName, Vector2 position, float scale = 1f)
        {
            try
            {
                if (World.world == null) return;

                // 获取目标瓦片
                WorldTile tile = World.world.GetTileSimple((int)position.x, (int)position.y);
                if (tile == null) return;

                // 使用spawnAtTile方法（正确的参数顺序，内部会调用prepare设置缩放）
                BaseEffect effect = EffectsLibrary.spawn(effectName, tile, null, null, 0f, -1f, -1f, null);
                if (effect == null)
                {
                    DSDebug.Verbose($"[灾变特效] EffectsLibrary.spawnAtTile返回null（可能达到同屏上限），特效: {effectName}");
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"[灾变特效] 播放特效失败 {effectName}: {e.Message}");
            }
        }

        private static void SpawnRift()
        {
            if (World.world == null) return;

            // 在地图随机位置生成（M-12修复：动态获取地图大小，不再硬编码100f）
            float x = UnityEngine.Random.Range(0, Mathf.Max(1f, MapBox.width));
            float y = UnityEngine.Random.Range(0, Mathf.Max(1f, MapBox.height));

            var rift = new VoidRift
            {
                Id = ++_riftIdCounter,
                X = x,
                Y = y,
                Power = UnityEngine.Random.Range(10f, 30f) * (1f + (100f - _barrierIntegrity) / 100f),
                BirthYear = GetCurrentYear(),
                Duration = UnityEngine.Random.Range(3, 10),
                IsActive = true
            };
            _rifts.Add(rift);

            //  全局统计钩子：灾变计数+1
            try { Code.Core.DSGlobalStats.IncrementDisasterCount(); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] SpawnRift 异常: " + dsEx.Message); }

            // 应用裂隙视觉效果
            ApplyRiftVisualEffect(rift);

            DSDebug.Verbose(string.Format("[DivineAscension] 虚空裂隙生成! 位置=({0:F0},{1:F0}) 强度={2:F1} 屏障={3:F1}%",
                x, y, rift.Power, _barrierIntegrity));
        }

        /// <summary>更新现有裂隙（过期移除，恢复被修改的地块）</summary>
        private static void UpdateRifts()
        {
            int currentYear = GetCurrentYear();
            _rifts.RemoveAll(r =>
            {
                if (currentYear - r.BirthYear >= r.Duration)
                {
                    // 裂隙消散：恢复被修改的原始地块类型
                    RestoreRiftVisualEffect(r);
                    DSDebug.Verbose(string.Format("[DivineAscension] 虚空裂隙#{0} 已消散，已恢复{1}个地块", r.Id, r.ModifiedTiles.Count));
                    return true;
                }
                return false;
            });
        }

        /// <summary>恢复裂隙修改的地块为原始类型</summary>
        private static void RestoreRiftVisualEffect(VoidRift rift)
        {
            if (rift == null) return;

            try
            {
                if (rift.ModifiedTiles == null || rift.ModifiedTiles.Count == 0) return;

                if (World.world == null) return;

                // 获取map引用
                object map = null;
                var mapProp = World.world.GetType().GetProperty("map",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (mapProp != null)
                {
                    map = mapProp.GetValue(World.world);
                }
                else
                {
                    var mapField = World.world.GetType().GetField("map",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (mapField != null)
                    {
                        map = mapField.GetValue(World.world);
                    }
                }

                if (map == null) return;

                var getTileMethod = map.GetType().GetMethod("GetTile",
                    new Type[] { typeof(int), typeof(int) });
                if (getTileMethod == null) return;

                int restored = 0;
                foreach (var mt in rift.ModifiedTiles)
                {
                    try
                    {
                        var tile = getTileMethod.Invoke(map, new object[] { mt.X, mt.Y });
                        if (tile == null) continue;

                        var typeField = tile.GetType().GetField("type",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (typeField != null && mt.OriginalType != null)
                        {
                            typeField.SetValue(tile, mt.OriginalType);
                            restored++;
                        }
                    }
                    catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] RestoreRiftVisualEffect 异常: " + dsEx.Message); }
                }

                rift.ModifiedTiles.Clear();
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[DivineAscension] RestoreRiftVisualEffect异常: {e.Message}");
            }
        }

        /// <summary>裂隙对附近单位造成伤害（所有单位，不仅仅是异能者）</summary>
        private static void ApplyRiftDamage()
        {
            if (World.world == null || World.world.units == null) return;

            int damagedCount = 0;
            int corruptedCount = 0;

            foreach (var rift in _rifts)
            {
                if (!rift.IsActive) continue;

                // 对所有存活单位造成伤害（非逐帧遍历，年度级别）
                foreach (var actor in World.world.units.units_only_alive)
                {
                    if (actor == null || !actor.isAlive()) continue;

                    float dist = Vector2.Distance(
                        new Vector2(rift.X, rift.Y),
                        actor.current_position);

                    // 裂隙影响范围30格（扩大范围，增强效果）
                    if (dist < 30f)
                    {
                        float damage = rift.Power * (1f - dist / 30f) * 2f; // 伤害翻倍
                        
                        // 获取境界（用于后面的虚空侵蚀状态和统计判断）
                        int tier = CultivationData.GetRealmTier(actor);
                        
                        // 接入GE公式（基因能量统一公式）：基因能量越高，对虚空裂隙伤害的抗性越高
                        // 替换硬编码的境界抗性，使用GE公式的防御加成动态计算
                        float geArmorBonus = Code.Core.GeneEnergyCalculator.GetArmorBonus(actor);
                        // 防御加成越高，受到的伤害越低（最低10%伤害，避免完全免疫）
                        damage *= Mathf.Max(0.1f, 1f - geArmorBonus * 0.5f);

                        if (damage > 1f)
                        {
                            ApplyDamage(actor, damage);
                            damagedCount++;
                        }

                        // 裂隙附近的单位自动获得虚空侵蚀状态（15格以内）
                        if (dist < 15f && tier > 0)
                        {
                            DSStatusManager.AddVoidCorruption(actor);
                            corruptedCount++;
                            // 虚空侵蚀增加失控指数
                            float currentTurb = CultivationData.GetTurbulence(actor);
                            CultivationData.SetTurbulence(actor, Mathf.Min(100f, currentTurb + 2f));
                        }

                        //  统计钩子：裂隙影响范围内（30格）的异能者记录经历灾变
                        if (tier > 0)
                        {
                            try { CultivationData.AddDisasterSurvived(actor); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[DivineAscension] ApplyRiftDamage 异常: " + dsEx.Message); }
                        }
                    }
                }
            }

            if (damagedCount > 0 || corruptedCount > 0)
            {
                DSDebug.Verbose($"[DivineAscension] 虚空裂隙伤害: 伤害{damagedCount}个单位, 虚空侵蚀{corruptedCount}个异能者");
            }

            // 天地屏障破碎后的全局效果
            if (_barrierIntegrity <= 0f)
            {
                ApplyBarrierBrokenEffect();
            }
        }

        // ============================================================
        //  视觉效果系统
        // ============================================================

        /// <summary>应用裂隙视觉效果（修改周围地块）</summary>
        /// <summary>
        /// 应用裂隙视觉效果（以裂隙坐标为中心的环形扩散 + 持续脉冲）
        /// 修复：之前只在一个点播放一次，玩家容易错过；现在实现多层环形扩散+持续脉冲
        /// </summary>
        private static void ApplyRiftVisualEffect(VoidRift rift)
        {
            if (rift == null) return;

            try
            {
                if (World.world == null) return;

                Vector2 center = new Vector2(rift.X, rift.Y);
                float effectScale = Mathf.Clamp(rift.Power / 80f, 0.2f, 0.8f);

                //  只播放虚空裂隙帧动画特效（不播放其他原版特效，避免覆盖）

                //  标记裂隙需要持续脉冲效果（在Update中驱动）
                rift.VisualApplied = true;
                rift.NextPulseTime = GetCurrentYear() + 1; // 1年后第一次脉冲

                DSDebug.Verbose($"[DivineAscension] 虚空裂隙 #{rift.Id} 生成特效已播放 (中心:{rift.X:F0},{rift.Y:F0}, 强度:{rift.Power:F1}, 缩放:{effectScale:F2})");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[DivineAscension] ApplyRiftVisualEffect异常: {e.Message}");
            }
        }


        /// <summary>应用天地屏障破碎全局视觉效果（屏幕震动+全图虚空能量爆发）</summary>
        private static void ApplyBarrierBrokenVisualEffect()
        {
            try
            {
                // 1. 全局屏幕震动效果（通过相机震动）
                if (Camera.main != null)
                {
                    var shaker = Camera.main.gameObject.GetComponent<ScreenShaker>();
                    if (shaker == null)
                    {
                        shaker = Camera.main.gameObject.AddComponent<ScreenShaker>();
                    }
                    shaker.Shake(1.0f, 0.5f); // 更强的震动
                }

                // 2. 全图随机位置播放虚空能量爆发特效（10-15个，营造全球灾变感）
                if (World.world != null && MapBox.width > 0 && MapBox.height > 0)
                {
                    int eruptionCount = UnityEngine.Random.Range(10, 16);
                    for (int i = 0; i < eruptionCount; i++)
                    {
                        Vector2 randomPos = new Vector2(
                            UnityEngine.Random.Range(5f, MapBox.width - 5f),
                            UnityEngine.Random.Range(5f, MapBox.height - 5f)
                        );
                        float scale = UnityEngine.Random.Range(0.6f, 1.5f);
                        PlayOriginalEffect("fx_cast_ground_purple", randomPos, scale);
                        PlayOriginalEffect("fx_cast_top_purple", randomPos, scale * 0.7f);
                    }
                    DSDebug.Verbose($"[DivineAscension] 天地屏障破碎：全图{eruptionCount}处虚空能量爆发！");
                }

                // 3. 全局提示消息
                DSDebug.Verbose(" 天地屏障已破碎！虚空裂隙大量涌现，全局异能者失控指数暴涨！");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[DivineAscension] ApplyBarrierBrokenVisualEffect异常: {e.Message}");
            }
        }

        /// <summary>屏幕震动组件（用于天地屏障破碎效果）</summary>
        private class ScreenShaker : MonoBehaviour
        {
            private float _shakeDuration = 0f;
            private float _shakeIntensity = 0f;
            private Vector3 _originalPosition;

            public void Shake(float duration, float intensity)
            {
                _shakeDuration = duration;
                _shakeIntensity = intensity;
                _originalPosition = transform.position;
            }

            private void Update()
            {
                if (_shakeDuration > 0)
                {
                    transform.position = _originalPosition + UnityEngine.Random.insideUnitSphere * _shakeIntensity;
                    _shakeDuration -= Time.deltaTime;
                    if (_shakeDuration <= 0)
                    {
                        transform.position = _originalPosition;
                        Destroy(this); // 震动结束后自动移除组件，防止Camera.main上组件累积（L-4修复）
                    }
                }
            }
        }

        /// <summary>天地屏障破碎后的全局效果</summary>
        private static void ApplyBarrierBrokenEffect()
        {
            if (World.world == null || World.world.units == null) return;

            // 破碎瞬间（仅一次）：虚空裂隙爆发特效（空间撕裂+紫色虚空能量）+ 全图湮灭伤害 + 全局通知
            if (!_barrierBrokenApplied)
            {
                _barrierBrokenApplied = true;
                TriggerRiftEruption();

                // 全图湮灭伤害：灭世级，高阶异能者按境界减伤
                int casualtyCount = 0;
                foreach (var actor in World.world.units.units_only_alive)
                {
                    if (actor == null || !actor.isAlive()) continue;
                    float dmg = UnityEngine.Random.Range(120f, 260f);
                    int tier = CultivationData.GetRealmTier(actor);
                    if (tier >= 8) dmg *= 0.5f;
                    if (tier >= 12) dmg *= 0.3f;
                    ApplyDamage(actor, dmg);
                    casualtyCount++;
                }
                DSDebug.Verbose($"[DivineAscension] 天地屏障破碎！虚空裂隙爆发，全图湮灭伤害波及{casualtyCount}个单位");

                // 全图失控指数暴涨（破碎瞬间，仅一次）
                int affectedCount = ApplyTurbulenceSurge();

                // 全局通知（灾变级大事件，只显示标题）
                if (affectedCount > 0)
                {
                    DSNotificationManager.NotifyMajor(
                        string.Format(UILocalization.Get("disaster_barrier_broken_global"), affectedCount), "");
                }
                return;
            }

            // 屏障持续为0期间：继续施加全局视觉效果 + 失控指数增长（不通知、不刷日志）
            ApplyBarrierBrokenVisualEffect();
            ApplyTurbulenceSurge();
        }

        /// <summary>屏障破碎后所有异能者失控指数暴涨（返回受影响数量）</summary>
        private static int ApplyTurbulenceSurge()
        {
            int affectedCount = 0;
            foreach (var actor in World.world.units.units_only_alive)
            {
                if (actor == null || !actor.isAlive()) continue;
                int tier = CultivationData.GetRealmTier(actor);
                if (tier <= 0) continue;

                // 屏障破碎后，所有异能者失控指数增长加速
                float currentTurb = CultivationData.GetTurbulence(actor);
                float turbGain = 3f * (1f + tier * 0.1f); // 高阶异能者受影响更大
                CultivationData.SetTurbulence(actor, Mathf.Min(100f, currentTurb + turbGain));

                // 失控指数超过70自动触发能力失控
                if (currentTurb + turbGain >= 70f)
                {
                    DSStatusManager.AddOutOfControl(actor);
                }

                affectedCount++;
            }
            return affectedCount;
        }

        /// <summary>破碎瞬间触发虚空裂隙爆发特效（符合灾害设定：不是炸弹，是空间撕裂+虚空能量涌出）</summary>
        private static void TriggerRiftEruption()
        {
            try
            {
                if (World.world == null) return;

                // 1. 每个裂隙位置生成紫色虚空能量爆发特效（原版施法特效，契合虚空/空间撕裂气质）
                foreach (var rift in _rifts)
                {
                    if (!rift.IsActive) continue;
                    Vector2 pos = new Vector2(rift.X, rift.Y);
                    PlayOriginalEffect("fx_cast_ground_purple", pos, 0.5f);
                    PlayOriginalEffect("fx_cast_top_purple", pos, 0.5f);
                }

                // 2. 裂隙地块湮灭强化：重置视觉标记后重新应用（半径翻倍，虚空地块向外扩散）
                foreach (var rift in _rifts)
                {
                    if (!rift.IsActive) continue;
                    rift.VisualApplied = false;
                    float originalPower = rift.Power;
                    rift.Power *= 1.5f; // 破碎瞬间裂隙暴涨
                    ApplyRiftVisualEffect(rift);
                    rift.Power = originalPower;
                }

                // 3. 无裂隙时至少在地图中心来一发虚空能量爆发
                if (_rifts.Count == 0 && MapBox.width > 0 && MapBox.height > 0)
                {
                    Vector2 center = new Vector2(MapBox.width / 2f, MapBox.height / 2f);
                    PlayOriginalEffect("fx_cast_ground_purple", center, 0.8f);
                    PlayOriginalEffect("fx_cast_top_purple", center, 0.8f);
                }

                DSDebug.Verbose($"[DivineAscension] 天地屏障破碎：虚空裂隙爆发（{_rifts.Count}道裂隙喷涌）");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[DivineAscension] TriggerRiftEruption异常: {e.Message}");
            }
        }

        /// <summary>玩家强制修复天地屏障（调试/建筑用）</summary>
        public static void ForceRepairBarrier(float amount)
        {
            bool wasBroken = (_barrierIntegrity <= 0f);
            _barrierIntegrity = Mathf.Clamp(_barrierIntegrity + amount, 0f, 100f);

            // 屏障从0恢复到>0时，重置破碎标记
            if (wasBroken && _barrierIntegrity > 0f)
            {
                _barrierBrokenApplied = false;
                DSDebug.Verbose("[DivineAscension] 天地屏障已强制修复，破碎爆发标记已重置");
            }

            DSDebug.Verbose($"[DivineAscension] 强制修复天地屏障 +{amount:F1}%，当前={_barrierIntegrity:F1}%");
        }

        /// <summary>在指定位置生成虚空裂隙（调试/技能用）</summary>
        public static void SpawnDebugRift(float x, float y)
        {
            if (!_initialized) Init();

            var rift = new VoidRift
            {
                Id = ++_riftIdCounter,
                X = x,
                Y = y,
                Power = UnityEngine.Random.Range(20f, 50f),
                BirthYear = GetCurrentYear(),
                Duration = UnityEngine.Random.Range(5, 15),
                IsActive = true
            };
            _rifts.Add(rift);

            // 应用裂隙视觉效果（修复：调试面板生成裂隙时也播放特效）
            ApplyRiftVisualEffect(rift);

            DSDebug.Verbose($"[DivineAscension] 虚空裂隙生成(技能触发)! 位置=({x:F0},{y:F0}) 强度={rift.Power:F1}");
        }

        // ============================================================
        //  公共查询接口
        // ============================================================

        public static float GetBarrierIntegrity() { return _barrierIntegrity; }
        public static void SetBarrierIntegrity(float value) { _barrierIntegrity = Mathf.Clamp(value, 0f, 100f); }
        public static bool IsBarrierBrokenApplied() { return _barrierBrokenApplied; }
        public static void SetBarrierBrokenApplied(bool value) { _barrierBrokenApplied = value; }
        public static float GetBarrierDecayRate() { return _barrierDecayRate; }
        public static List<VoidRift> GetActiveRifts() { return new List<VoidRift>(_rifts); }
        public static int GetRiftCount() { return _rifts.Count; }

        public static string GetBarrierStatus()
        {
            if (_barrierIntegrity >= 80f) return UILocalization.Get("disaster_barrier_stable");
            if (_barrierIntegrity >= 60f) return UILocalization.Get("disaster_barrier_cracked");
            if (_barrierIntegrity >= 30f) return UILocalization.Get("disaster_barrier_breaking");
            if (_barrierIntegrity > 0f) return UILocalization.Get("disaster_barrier_critical");
            return UILocalization.Get("disaster_barrier_broken_state");
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

        /// <summary>造成伤害（参考西幻世界：直接扣血+处理死亡，不用反射）</summary>
        private static void ApplyDamage(Actor actor, float damage)
        {
            if (actor == null || Code.Data.ActorDataAccessor.GetData(actor) == null)
                return;
            if (!actor.isAlive())
                return;
            if (damage <= 0f)
                return;

            try
            {
                int hpLoss = (int)Math.Floor(damage);
                if (hpLoss > 0)
                {
                    actor.changeHealth(-hpLoss);
                }

                // 处理死亡（参考西幻世界）
                if (!actor.hasHealth() && Code.Data.ActorDataAccessor.GetData(actor) != null)
                {
                    if (actor.batch != null && actor.batch.c_check_deaths != null)
                    {
                        if (!actor.batch.c_check_deaths.Contains(actor))
                            actor.batch.c_check_deaths.Add(actor);
                    }
                    actor.checkCallbacksOnDeath();
                }
            }
            catch
            {
                // 静默失败
            }
        }

        private static Actor FindActorById(long id) { return SystemManagerExtensions.FindActorById(id); }

        private static int GetCurrentYear()
        {
            if (World.world == null) return 0;
            return (int)(World.world.getCurWorldTime() / 365.0);
        }
    }
}




