// ============================================================
// 战斗表现层·演出导演（v0.18.47）
// 业务侧的唯一切口（单一职责：表现编排 + 门禁 + 预算）：
//   SpellBurst          —— 技能释放演出（粒子/音效/轻击停，按境界缩放）
//   HitImpact           —— 伤害命中演出（星形/火花/碎片/音效/击停，伤害阈值防刷屏）
//   Shockwave           —— 地形破坏冲击波（地面环/碎石/音效）
//   AscensionCeremony   —— 登神升华演出（慢动作/冻结/暗幕/金色冲击环）
// 门禁策略（防性能问题，参考「战斗之神」）：
//   近距（相机缩放 ≤ 60）才全演出；击停独立冷却；粒子总量预算。
// ============================================================
using System;
using UnityEngine;

namespace Code.Combat.Fx
{
    public static class CombatFxDirector
    {
        /// <summary>登神暗幕自动恢复的计时器（秒）。</summary>
        private static float _ascendVeilTimer;
        private const float MaxFullZoom = 60f;

        private static bool _artReady;

        private static void EnsureArt()
        {
            if (_artReady) return;
            _artReady = true;
            CombatFxArt.Init();
        }

        /// <summary>门禁：世界运行中、相机近距、目标在屏幕内——才允许全演出。</summary>
        internal static bool CanFull(Vector2 at)
        {
            if (World.world == null || World.world.isPaused()) return false;
            Camera cam = Camera.main;
            if (cam == null || cam.orthographicSize > MaxFullZoom) return false;
            return CombatFxCore.OnScreen(at, 0.1f);
        }

        /// <summary>按境界档选择主色。</summary>
        private static Color TierColor(int tier)
        {
            if (tier >= 13) return new Color(1f, 0.85f, 0.25f); // 超神：金
            if (tier >= 11) return new Color(0.75f, 0.9f, 1f);  // 登神：苍蓝
            if (tier >= 6) return new Color(0.55f, 0.8f, 1f);   // 使徒：蓝
            return new Color(1f, 1f, 1f);                       // 低阶：白
        }

        // ----------------------------------------------------- 技能释放 --

        /// <summary>技能释放演出：中心光斑 + 命中星 + 火花迸发 + 音效；高境界追加轻击停。</summary>
        public static void SpellBurst(Actor actor, Vector2 pos, int tier)
        {
            try
            {
                EnsureArt();
                if (!CanFull(pos)) return;
                Color c = TierColor(tier);
                // 中心光斑（快速收缩）
                CombatFxCore.Spawn(CombatFxArt.Burst, pos, 0.08f, 0.25f, FxLayer.Over, 30)
                    .Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(3.2f, 0.6f);
                // 命中星 2-3 颗（旋转发散）
                int stars = 2 + Mathf.Min(1, tier / 6);
                for (int i = 0; i < stars; i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float spd = UnityEngine.Random.Range(4f, 9f);
                    CombatFxCore.Spawn(CombatFxArt.HitStars[UnityEngine.Random.Range(0, CombatFxArt.HitStars.Length)],
                        pos, 0.1f, 0.4f, FxLayer.Top, 40)
                        .Color(c).Sized(1f, 0.3f)
                        .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd, 0f, 2f, 0f);
                }
                // 火花迸发（数量随境界）
                int sparks = 3 + Mathf.Min(6, tier / 2);
                for (int i = 0; i < sparks; i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float spd = UnityEngine.Random.Range(2f, 7f + tier);
                    float life = UnityEngine.Random.Range(0.2f, 0.5f);
                    CombatFxCore.Spawn(CombatFxArt.Sparks[UnityEngine.Random.Range(0, CombatFxArt.Sparks.Length)],
                        pos, 0.1f, life, FxLayer.Top, 45)
                        .Color(c, new Color(c.r, c.g, c.b, 0f))
                        .Sized(1f, 0.4f)
                        .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd, UnityEngine.Random.Range(1f, 3f), 1.5f, 0f);
                }
                CombatFxSound.Play(tier >= 6 ? CombatFxSound.Powerup : CombatFxSound.Whoosh, pos);
                if (tier >= 8)
                {
                    CombatFxHitStop.Request(pos, Mathf.Min(0.16f, 0.06f + 0.01f * tier));
                }
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] SpellBurst: " + e.Message);
            }
        }

        // ----------------------------------------------------- 伤害命中 --

        /// <summary>
        /// 伤害命中演出：足够大的伤害才触发（防普攻刷屏），规模随伤害上升。
        /// 在 Actor.getHit 前缀内调用（此时伤害尚未结算）。
        /// </summary>
        public static void HitImpact(Actor victim, float damage)
        {
            try
            {
                EnsureArt();
                if (victim == null || !victim.isAlive()) return;
                Vector2 pos = victim.current_position;
                if (!CanFull(pos)) return;
                if (damage < 150f) return; // 阈值：避免每帧普攻刷屏
                float scale = Mathf.Min(1f, damage / 3000f);
                Color c = new Color(1f, 0.9f, 0.55f);
                // 命中星（1-2 颗）
                int stars = scale > 0.4f ? 2 : 1;
                for (int i = 0; i < stars; i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    CombatFxCore.Spawn(CombatFxArt.HitStars[stars - 1], pos, 0.1f, 0.3f, FxLayer.Top, 40)
                        .Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(1f, 0.25f)
                        .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 6f, 0f, 2f, 0f);
                }
                // 火花（规模线性）
                int sparks = 2 + (int)(scale * 5f);
                for (int i = 0; i < sparks; i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float spd = UnityEngine.Random.Range(3f, 6f + scale * 6f);
                    CombatFxCore.Spawn(CombatFxArt.Sparks[UnityEngine.Random.Range(0, CombatFxArt.Sparks.Length)],
                        pos, 0.1f, UnityEngine.Random.Range(0.15f, 0.4f), FxLayer.Top, 45)
                        .Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(1f, 0.3f)
                        .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd, UnityEngine.Random.Range(1f, 3f), 1.5f, 0f);
                }
                // 大伤害追加碎石 + 击停
                if (damage >= 800f)
                {
                    for (int i = 0; i < 3 + (int)(scale * 3f); i++)
                    {
                        float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                        float spd = UnityEngine.Random.Range(2f, 5f);
                        CombatFxCore.Spawn(CombatFxArt.Debris[UnityEngine.Random.Range(0, CombatFxArt.Debris.Length)],
                            pos, 0.09f, UnityEngine.Random.Range(0.25f, 0.5f), FxLayer.Top, 46)
                            .Color(new Color(0.75f, 0.7f, 0.55f)).Sized(1f, 0.5f)
                            .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd, UnityEngine.Random.Range(2f, 5f), 1f, 6f);
                    }
                    CombatFxHitStop.Request(pos, Mathf.Min(0.22f, 0.05f + damage / 40000f));
                }
                CombatFxSound.Play(damage >= 2000f ? CombatFxSound.HitStone : CombatFxSound.HitGeneric, pos);
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] HitImpact: " + e.Message);
            }
        }

        // ----------------------------------------------------- 地形破坏 --

        /// <summary>地形破坏冲击波：地面细环 + 粗冲击环 + 碎石扬起 + 重音效；大范围追加击停。</summary>
        public static void Shockwave(Vector2 pos, float radius)
        {
            try
            {
                EnsureArt();
                if (!CanFull(pos)) return;
                float cap = Mathf.Min(radius, 40f); // 演出尺寸封顶，避免巨大环溢出屏幕
                // 地面细环（扩张淡出）
                int ringIdx = cap >= 25f ? 2 : (cap >= 12f ? 1 : 0);
                CombatFxCore.Spawn(CombatFxArt.Rings[ringIdx], pos, 0.1f, 0.55f, FxLayer.Ground, 10)
                    .Color(new Color(0.85f, 0.75f, 0.5f, 0f), new Color(0.6f, 0.5f, 0.3f, 0f))
                    .Sized(0.4f + cap * 0.12f, 0.9f + cap * 0.16f);
                // 粗冲击环（高亮、快速扩张）
                int shkIdx = cap >= 25f ? 2 : (cap >= 12f ? 1 : 0);
                CombatFxCore.Spawn(CombatFxArt.Shocks[shkIdx], pos, 0.1f, 0.4f, FxLayer.Top, 30)
                    .Color(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.75f, 0.3f, 0f))
                    .Sized(0.3f + cap * 0.1f, 0.7f + cap * 0.14f);
                // 碎石扬起（带重力回落）
                for (int i = 0; i < 6 + (int)(cap * 0.4f); i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float spd = UnityEngine.Random.Range(1.5f, 4f);
                    CombatFxCore.Spawn(CombatFxArt.Debris[UnityEngine.Random.Range(0, CombatFxArt.Debris.Length)],
                        pos, 0.09f, UnityEngine.Random.Range(0.3f, 0.7f), FxLayer.Top, 40)
                        .Color(new Color(0.7f, 0.6f, 0.45f)).Sized(1f, 0.5f)
                        .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd, UnityEngine.Random.Range(3f, 7f), 0.8f, 8f);
                }
                // 尘土软斑
                CombatFxCore.Spawn(CombatFxArt.Dust[3], pos, 0.1f, 0.7f, FxLayer.Behind, 5)
                    .Color(new Color(0.8f, 0.75f, 0.65f, 0f), new Color(0.6f, 0.55f, 0.45f, 0f))
                    .Sized(0.5f + cap * 0.12f, 1.2f + cap * 0.2f);
                CombatFxSound.Play(radius >= 20f ? CombatFxSound.ExplosionSmall : CombatFxSound.DropImpact, pos);
                if (radius >= 20f) CombatFxHitStop.Request(pos, 0.12f);
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] Shockwave: " + e.Message);
            }
        }

        // ----------------------------------------------------- 登神升华 --

        /// <summary>
        /// 登神升华演出：慢动作 + 瞬间冻结 + 暗幕 + 金色冲击环（Cine 层，冻结中继续发光）。
        /// 世界「屏息」一瞬，然后恢复。
        /// </summary>
        public static void AscensionCeremony(Actor actor)
        {
            try
            {
                EnsureArt();
                if (actor == null) return;
                Vector2 pos = actor.current_position;
                if (!CanFull(pos)) return;
                Color gold = new Color(1f, 0.85f, 0.3f);
                // 时间：慢动作 0.25×1.2s + 冻结 0.35s（冲击帧）
                CombatFxCinema.SlowMo(0.25f, 1.2f);
                CombatFxCinema.Freeze(0.35f);
                CombatFxCinema.Dim(0.5f, new Color(0.02f, 0.02f, 0.06f), 0.5f);
                _ascendVeilTimer = 1.1f;
                // 金色冲击环 ×2（Cine 层：冻结中仍扩张发光）
                CombatFxCore.Spawn(CombatFxArt.Shocks[2], pos, 0.12f, 0.9f, FxLayer.Cine, 10)
                    .Color(gold, new Color(gold.r, gold.g, gold.b, 0f))
                    .Sized(0.5f, 2.6f);
                CombatFxCore.Spawn(CombatFxArt.Rings[2], pos, 0.12f, 1.1f, FxLayer.Cine, 5)
                    .Color(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.8f, 0.3f, 0f))
                    .Sized(0.3f, 3.2f);
                // 金色星雨
                for (int i = 0; i < 10; i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float spd = UnityEngine.Random.Range(3f, 8f);
                    CombatFxCore.Spawn(CombatFxArt.HitStars[UnityEngine.Random.Range(0, CombatFxArt.HitStars.Length)],
                        pos, 0.1f, UnityEngine.Random.Range(0.5f, 0.9f), FxLayer.Cine, 20)
                        .Color(gold, new Color(gold.r, gold.g, gold.b, 0f)).Sized(1.2f, 0.2f)
                        .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd, UnityEngine.Random.Range(2f, 5f), 0.5f, 0f);
                }
                CombatFxSound.Play(CombatFxSound.Inspiration, pos);
                CombatFxSound.Play(CombatFxSound.ExplosionMeteorite, pos);
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] AscensionCeremony: " + e.Message);
            }
        }

        // ---------------------------------------------------------- 驱动 --

        /// <summary>每帧驱动（FramePatch Postfix 调用）：登神暗幕定时恢复。</summary>
        internal static void Tick()
        {
            if (_ascendVeilTimer > 0f)
            {
                _ascendVeilTimer -= Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                if (_ascendVeilTimer <= 0f)
                {
                    CombatFxCinema.Undim(0.8f);
                }
            }
        }

        /// <summary>世界销毁/存档切换：清空全部演出状态。</summary>
        public static void ClearAll()
        {
            CombatFxCore.Clear();
            CombatFxHitStop.Clear();
            CombatFxCinema.Clear();
            _ascendVeilTimer = 0f;
        }
    }
}
