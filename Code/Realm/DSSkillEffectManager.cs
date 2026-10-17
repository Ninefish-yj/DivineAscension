// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Core;

namespace Code.Realm
{
    /// <summary>
    /// 技能特效管理器（全部重做版）
    /// 完全参考西幻世界 VFXManagement 实现
    /// </summary>
    public static class DSSkillEffectManager
    {
        // ============================================================
        // 帧缓存（性能优化）
        // ============================================================
        private static readonly Dictionary<string, Sprite[]> _spriteCache = new Dictionary<string, Sprite[]>();

        // ============================================================
        // 技能动画配置（11个技能全部注册）
        // ============================================================
        public class SkillEffectConfig
        {
            public string AnimationPath;
            public float Scale;
            public float FrameInterval;
            public bool FollowActor;
            public float PositionOffsetY;
            public bool Loop;
            public float Duration;

            public SkillEffectConfig(string path, float scale, float frameInterval,
                bool followActor = false, float offsetY = 0f, bool loop = false, float duration = 0f)
            {
                AnimationPath = path;
                Scale = scale;
                FrameInterval = frameInterval;
                FollowActor = followActor;
                PositionOffsetY = offsetY;
                Loop = loop;
                Duration = duration;
            }
        }

        /// <summary>11个技能全部注册动画配置</summary>
        private static readonly Dictionary<string, SkillEffectConfig> _effectConfigs =
            new Dictionary<string, SkillEffectConfig>(StringComparer.OrdinalIgnoreCase)
            {
                // 6阶 能量弹：投射物特效
                { "ds_ability_energy_bolt",
                    new SkillEffectConfig("skill/energy_bolt", 0.35f, 0.05f, followActor: true, offsetY: 0.2f) },

                // 7阶 能量刃（虚拟武器）：跟随施法者
                { "ds_ability_energy_blade",
                    new SkillEffectConfig("skill/energy_blade", 0.4f, 0.08f, followActor: true, offsetY: 0.2f) },

                // 7阶 治疗之光：跟随施法者，持续3秒
                { "ds_ability_healing_light",
                    new SkillEffectConfig("skill/healing_light", 0.4f, 0.1f, followActor: true, offsetY: 0.3f, duration: 3.0f) },

                // 8阶 能量风暴：范围伤害
                { "ds_ability_energy_storm",
                    new SkillEffectConfig("skill/energy_storm", 8f, 0.12f, followActor: true, offsetY: 0.3f, duration: 3.0f) },

                // 9阶 领域展开：跟随施法者，持续5秒
                { "ds_ability_domain_expand",
                    new SkillEffectConfig("skill/domain_expand", 5f, 0.15f, followActor: true, offsetY: 0.3f, duration: 5.0f) },

                // 9阶 法则压制：目标位置
                { "ds_ability_law_suppress",
                    new SkillEffectConfig("skill/law_suppress", 2.5f, 0.1f, followActor: true, offsetY: 0.3f, duration: 2.0f) },

                // 10阶 本源汲取：目标位置
                { "ds_ability_origin_drain",
                    new SkillEffectConfig("skill/origin_drain", 3f, 0.12f, followActor: false, duration: 2.5f) },

                // 10阶 空间撕裂：目标位置
                { "ds_ability_space_tear",
                    new SkillEffectConfig("skill/space_tear", 2f, 0.1f, followActor: false, duration: 2.5f) },

                // 11阶 圣域：跟随施法者，持续10秒
                { "ds_ability_sanctuary",
                    new SkillEffectConfig("skill/sanctuary", 1.5f, 0.12f, followActor: true, offsetY: 0.3f, duration: 10.0f) },

                // 12阶 维度打击：大范围
                { "ds_ability_dimensional_strike",
                    new SkillEffectConfig("skill/dimensional_strike", 12f, 0.08f, followActor: false, duration: 3.0f) },

                // 13阶 神罚：全屏，超大
                { "ds_ability_divine_punishment",
                    new SkillEffectConfig("skill/divine_punishment", 12f, 0.05f, followActor: false, duration: 4.0f) },
            };

        // ============================================================
        // 原版特效组合（叠加在帧动画上）
        // ============================================================
        public class OriginalEffectConfig
        {
            public string EffectName;
            public float Scale;
            public float Delay;
            public bool FollowActor;

            public OriginalEffectConfig(string name, float scale = 1f, float delay = 0f, bool followActor = false)
            {
                EffectName = name;
                Scale = scale;
                Delay = delay;
                FollowActor = followActor;
            }
        }

        /// <summary>原版特效组合（各阶位技能都配上对应原版效果）</summary>
        private static readonly Dictionary<string, List<OriginalEffectConfig>> _originalEffectConfigs =
            new Dictionary<string, List<OriginalEffectConfig>>(StringComparer.OrdinalIgnoreCase)
            {
                // 6阶 能量弹：小火花
                { "ds_ability_energy_bolt", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_spark", 0.5f),
                }},

                // 7阶 能量刃：挥砍特效
                { "ds_ability_energy_blade", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_slash", 0.6f),
                }},

                // 7阶 治疗之光：治疗光环
                { "ds_ability_healing_light", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_heal", 1.0f),
                }},

                // 8阶 能量风暴：火焰+旋风
                { "ds_ability_energy_storm", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_fire", 1.0f),
                    new OriginalEffectConfig("fx_storm", 0.8f, delay: 0.1f),
                }},

                // 9阶 领域展开：能量场域
                { "ds_ability_domain_expand", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_energy_field", 1.2f),
                }},

                // 9阶 法则压制：debuff效果
                { "ds_ability_law_suppress", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_debuff", 0.8f),
                }},

                // 10阶 本源汲取：吸取能量
                { "ds_ability_origin_drain", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_drain", 1.0f),
                }},

                // 10阶 空间撕裂：冲击波+裂痕
                { "ds_ability_space_tear", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_shockwave", 1.5f),
                    new OriginalEffectConfig("fx_crack", 1.2f, delay: 0.1f),
                }},

                // 11阶 圣域：神圣光环+增益
                { "ds_ability_sanctuary", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_holy_field", 1.5f),
                    new OriginalEffectConfig("fx_buff", 1.0f, delay: 0.2f),
                }},

                // 12阶 维度打击：大爆炸+风暴
                { "ds_ability_dimensional_strike", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_explosion", 1.5f),
                    new OriginalEffectConfig("fx_storm", 1.2f, delay: 0.1f),
                }},

                // 13阶 神罚：超级爆炸+闪电+大爆炸
                { "ds_ability_divine_punishment", new List<OriginalEffectConfig> {
                    new OriginalEffectConfig("fx_explosion", 2.5f),
                    new OriginalEffectConfig("fx_lightning", 2.0f, delay: 0.1f),
                    new OriginalEffectConfig("fx_bigexplosion", 3.0f, delay: 0.2f),
                }},
            };

        // ============================================================
        // 待停止的特效列表（循环播放的定时停止）
        // ============================================================
        private static readonly List<(BaseEffect effect, float stopTime)> _pendingStops = new List<(BaseEffect, float)>();

        // ============================================================
        // 初始化与预加载
        // ============================================================
        public static void Init()
        {
            DSDebug.Verbose("[技能特效] 初始化（全部重做版）");
            PreloadAllEffects();
        }

        /// <summary>预加载所有技能的帧序列</summary>
        private static void PreloadAllEffects()
        {
            int success = 0, fail = 0;
            foreach (var kv in _effectConfigs)
            {
                try
                {
                    var sprites = LoadSprites(kv.Value.AnimationPath);
                    if (sprites != null && sprites.Length > 0)
                    {
                        success++;
                    }
                    else
                    {
                        fail++;
                        DSDebug.Warning($"[技能特效] 预加载失败: {kv.Key} -> {kv.Value.AnimationPath}");
                    }
                }
                catch (Exception e)
                {
                    fail++;
                    DSDebug.Warning($"[技能特效] 预加载异常: {kv.Key} -> {e.Message}");
                }
            }
            DSDebug.Verbose($"[技能特效] 预加载完成: 成功 {success}/{_effectConfigs.Count}, 失败 {fail}");
        }

        /// <summary>检查技能是否有特效</summary>
        public static bool HasEffect(string abilityId)
        {
            return !string.IsNullOrEmpty(abilityId) && _effectConfigs.ContainsKey(abilityId);
        }

        // ============================================================
        // 核心播放方法（完全参考西幻世界实现）
        // ============================================================
        public static BaseEffect PlayEffect(Actor actor, string abilityId, Actor target = null)
        {
            if (actor == null || string.IsNullOrEmpty(abilityId)) return null;

            // 同时播放原版特效 + 自定义帧动画
            if (_originalEffectConfigs.TryGetValue(abilityId, out var originalEffects))
            {
                PlayOriginalEffects(actor, abilityId, target, originalEffects);
            }

            if (!_effectConfigs.TryGetValue(abilityId, out SkillEffectConfig config))
            {
                return null;
            }

            // 确定播放位置
            Vector2 position;
            if (config.FollowActor)
            {
                position = actor.current_position;
                position.y += config.PositionOffsetY;
            }
            else if (target != null && target.isAlive())
            {
                position = target.current_position;
            }
            else
            {
                position = actor.current_position;
                position.x += actor.is_looking_left ? -3f : 3f;
            }

            // v0.18.70 领域展开：范围技能（Scale≥2 世界宽，即影响范围）释放时加符文法阵演出
            try
            {
                if (config.Scale >= 2f)
                {
                    Code.Combat.Fx.CombatFxDirector.DomainCircle(position, config.Scale,
                        new Color(0.55f, 0.62f, 1f));
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 领域法阵演出失败: " + dsEx.Message); }

            return PlayEffectAtPosition(position, config, actor, target);
        }

        /// <summary>在指定位置播放特效（核心方法）</summary>
        public static BaseEffect PlayEffectAtPosition(Vector2 position, SkillEffectConfig config,
            Actor caster = null, Actor target = null)
        {
            if (config == null || string.IsNullOrEmpty(config.AnimationPath)) return null;

            try
            {
                WorldTile tile = World.world.GetTileSimple((int)position.x, (int)position.y);
                if (tile == null) return null;

                // 用原版 fx_slash 生成特效
                BaseEffect effect = EffectsLibrary.spawn("fx_slash", tile, null, null, 0f, -1f, -1f, null);
                if (effect == null) return null;

                // 加载帧序列（带缓存）——提前加载以取帧宽做尺寸归一化
                var sprites = LoadSprites(config.AnimationPath);
                // v0.18.69：尺寸按帧宽归一化——原版 prepare 的 scale 是精灵缩放倍数（世界宽=scale×帧宽/100），
                // 技能素材 64/128px 混用导致同 scale 下世界尺寸漂移近一倍（"大小不对"同源根因）。
                // scale' = Scale×100/帧宽 → 世界宽恒等于 Scale（Scale 语义=期望世界宽）。
                float frameW = sprites != null && sprites.Length > 0 ? sprites[0].rect.width : 128f;
                if (frameW <= 0f) frameW = 128f;
                float worldScale = config.Scale * 100f / frameW;

                // 非跟随技能播在目标/AOE 中心脚底，中心上移对准目标身体（同 AbilityFx 位置修复）
                Vector2 spawnPos = position;
                if (!config.FollowActor) spawnPos.y += 0.3f;

                // 清理对象池残留
                effect.transform.localScale = Vector3.one;
                effect.prepare(spawnPos, worldScale);

                SpriteAnimation anim = effect.GetComponent<SpriteAnimation>();
                SpriteRenderer spriteRenderer = effect.GetComponent<SpriteRenderer>();
                if (anim == null) return effect;

                // 帧序列（已在上方加载，此处直接用）
                if (sprites != null && sprites.Length > 0)
                {
                    anim.setFrames(sprites);
                    anim.timeBetweenFrames = config.FrameInterval;
                    anim.looped = config.Loop;
                    anim.returnToPool = !config.Loop;
                    anim.currentFrameIndex = 0;
                    anim.nextFrameTime = 0f;
                    anim.dirty = true;

                    if (spriteRenderer != null)
                    {
                        spriteRenderer.sprite = sprites[0];
                        // 把特效放在人物后面，避免覆盖单位
                        spriteRenderer.sortingOrder = -100;
                        // 设置半透明，显示更好看
                        var color = spriteRenderer.color;
                        color.a = 0.5f;  // 50%不透明度，50%透明
                        spriteRenderer.color = color;
                    }
                }

                // 循环播放的定时停止
                if (config.Duration > 0f && config.Loop)
                {
                    ScheduleEffectStop(effect, config.Duration);
                }

                return effect;
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[技能特效] 播放失败: {e.Message}");
                return null;
            }
        }

        // ============================================================
        // 原版特效播放
        // ============================================================
        private static void PlayOriginalEffects(Actor actor, string abilityId, Actor target,
            List<OriginalEffectConfig> configs)
        {
            foreach (var cfg in configs)
            {
                try
                {
                    Vector2 pos;
                    if (cfg.FollowActor)
                    {
                        pos = actor.current_position;
                    }
                    else if (target != null && target.isAlive())
                    {
                        pos = target.current_position;
                    }
                    else
                    {
                        pos = actor.current_position;
                    }

                    WorldTile tile = World.world.GetTileSimple((int)pos.x, (int)pos.y);
                    if (tile != null)
                    {
                        var fx = EffectsLibrary.spawn(cfg.EffectName, tile, null, null, 0f, -1f, -1f, null);
                        if (fx != null)
                        {
                            fx.prepare(pos, 1f); // 先置标准缩放，下面帧宽归一化
                            // v0.18.90：cfg.Scale 语义=「人类单位宽倍数」，目标世界宽 = Scale×0.55（人类显示宽≈0.55 世界单位）。
                            // 修复原硬编码 0.5 倍仍达 1~2 世界单位（2~4 倍人类宽）的"护盾/爆炸特效过大"根因。
                            Code.Combat.Fx.CombatFxCore.ScaleFxToWorldWidth(fx, cfg.Scale * 0.55f);
                        }
                    }
                }
                catch (Exception e)
                {
                    DSDebug.Verbose($"[技能特效] 原版特效播放失败: {cfg.EffectName} -> {e.Message}");
                }
            }
        }

        // ============================================================
        // 帧序列加载（带缓存）
        // ============================================================
        private static Sprite[] LoadSprites(string path)
        {
            if (_spriteCache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            try
            {
                var sprites = Resources.LoadAll<Sprite>(path);
                _spriteCache[path] = sprites;
                return sprites;
            }
            catch
            {
                _spriteCache[path] = new Sprite[0];
                return new Sprite[0];
            }
        }

        // ============================================================
        // 定时停止（循环特效）
        // ============================================================
        private static void ScheduleEffectStop(BaseEffect effect, float duration)
        {
            _pendingStops.Add((effect, Time.time + duration));
        }

        public static void UpdateScheduledStops(float deltaTime)
        {
            for (int i = _pendingStops.Count - 1; i >= 0; i--)
            {
                if (Time.time >= _pendingStops[i].stopTime)
                {
                    try
                    {
                        var anim = _pendingStops[i].effect.GetComponent<SpriteAnimation>();
                        if (anim != null)
                        {
                            anim.looped = false;
                            anim.returnToPool = true;
                        }
                    }
                    catch { }
                    _pendingStops.RemoveAt(i);
                }
            }
        }

        public static void UpdatePendingEffects(float deltaTime)
        {
            // 预留：未来扩展用
        }
    }
}