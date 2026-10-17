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
using System.Collections.Generic;
using UnityEngine;

namespace Code.Combat.Fx
{
    public static class CombatFxDirector
    {
        /// <summary>登神暗幕自动恢复的计时器（秒）。</summary>
        private static float _ascendVeilTimer;
        private const float MaxFullZoom = 60f;

        private static void EnsureArt()
        {
            CombatFxArt.EnsureInit();
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
                // v0.18.91：技能释放从单位身体中心爆开（脚底+0.17），原贴脚底/悬头顶
                pos.y += 0.17f;
                Color c = TierColor(tier);
                // 中心光斑（快速收缩；px=0.08×帧宽21→1.68×size，目标 0.8→0.3 世界单位）
                CombatFxCore.Spawn(CombatFxArt.Burst, pos, 0.08f, 0.25f, FxLayer.Over, 30)
                    .Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(0.48f, 0.18f);
                // 命中星 2-3 颗（旋转发散；px=0.1×帧宽17→1.7×size，目标 0.5→0.2）
                int stars = 2 + Mathf.Min(1, tier / 6);
                for (int i = 0; i < stars; i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float spd = UnityEngine.Random.Range(4f, 9f);
                    CombatFxCore.Spawn(CombatFxArt.HitStars[UnityEngine.Random.Range(0, CombatFxArt.HitStars.Length)],
                        pos, 0.1f, 0.4f, FxLayer.Top, 40)
                        .Color(c).Sized(0.29f, 0.12f)
                        .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd, 0f, 2f, 0f);
                }
                // 火花迸发（数量随境界；px=0.1×帧宽9→0.9×size，目标 0.35→0.15）
                int sparks = 3 + Mathf.Min(6, tier / 2);
                for (int i = 0; i < sparks; i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float spd = UnityEngine.Random.Range(2f, 7f + tier);
                    float life = UnityEngine.Random.Range(0.2f, 0.5f);
                    CombatFxCore.Spawn(CombatFxArt.Sparks[UnityEngine.Random.Range(0, CombatFxArt.Sparks.Length)],
                        pos, 0.1f, life, FxLayer.Top, 45)
                        .Color(c, new Color(c.r, c.g, c.b, 0f))
                        .Sized(0.39f, 0.17f)
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
        /// 伤害命中演出：低阈值（>20）+ 频率冷却防刷屏（同一单位 0.12s 内至多一次），规模随伤害上升。
        /// 在 Actor.getHit 前缀内调用（此时伤害尚未结算）。
        /// </summary>
        private static readonly Dictionary<Actor, float> _hitCooldown = new Dictionary<Actor, float>();

        public static void HitImpact(Actor victim, float damage)
        {
            try
            {
                EnsureArt();
                if (victim == null || !victim.isAlive()) return;
                Vector2 pos = victim.current_position;
                if (!CanFull(pos)) return;
                if (damage < 20f) return;  // 低阈值：普通伤害也有受击火花
                // 频率冷却：防普攻每帧刷屏
                if (_hitCooldown.TryGetValue(victim, out float last))
                {
                    if (Time.unscaledTime - last < 0.12f) return;
                }
                _hitCooldown[victim] = Time.unscaledTime;
                float scale = Mathf.Min(1f, damage / 3000f);
                // v0.18.91：受击演出对准单位身体中心（大众认知：伤害作用在身体上）
                pos.y += 0.17f;
                Color c = new Color(1f, 0.9f, 0.55f);
                // 命中星（低伤害 1 颗，高伤害 2 颗；px=0.1×帧宽17→1.7×size，目标 0.4→0.18）
                int stars = scale > 0.4f ? 2 : 1;
                for (int i = 0; i < stars; i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    CombatFxCore.Spawn(CombatFxArt.HitStars[stars - 1], pos, 0.1f, 0.3f, FxLayer.Top, 40)
                        .Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(0.24f, 0.11f)
                        .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 6f, 0f, 2f, 0f);
                }
                // 火花（低伤害 1-2 颗，规模线性；px=0.1×帧宽9→0.9×size，目标 0.33→0.15）
                int sparks = 1 + (int)(scale * 6f);
                for (int i = 0; i < sparks; i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float spd = UnityEngine.Random.Range(3f, 6f + scale * 6f);
                    CombatFxCore.Spawn(CombatFxArt.Sparks[UnityEngine.Random.Range(0, CombatFxArt.Sparks.Length)],
                        pos, 0.1f, UnityEngine.Random.Range(0.15f, 0.4f), FxLayer.Top, 45)
                        .Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(0.37f, 0.17f)
                        .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd, UnityEngine.Random.Range(1f, 3f), 1.5f, 0f);
                }
                // 大伤害追加碎石 + 击停（px=0.09×帧宽11→0.99×size，目标 0.5→0.28）
                if (damage >= 800f)
                {
                    for (int i = 0; i < 3 + (int)(scale * 3f); i++)
                    {
                        float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                        float spd = UnityEngine.Random.Range(2f, 5f);
                        CombatFxCore.Spawn(CombatFxArt.Debris[UnityEngine.Random.Range(0, CombatFxArt.Debris.Length)],
                            pos, 0.09f, UnityEngine.Random.Range(0.25f, 0.5f), FxLayer.Top, 46)
                            .Color(new Color(0.75f, 0.7f, 0.55f)).Sized(0.5f, 0.28f)
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
                float cap = Mathf.Min(radius, 60f); // 演出尺寸封顶（v0.18.88 40→60，破坏规模同步放大，特效跟上破坏力）
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

        // ----------------------------------------------------- 异能冲刺 --

        /// <summary>
        /// 异能战斗动作演出（v0.18.66，参考「战斗之神」Choreo 突进/残影/受击反应思路，纯特效模拟不碰 AI）：
        /// 攻击者→目标 冲刺拖影光点链 + 攻击者原位置残影淡出 + 目标命中环。
        /// 出镜率由调用方（AbilityFx 50% 门禁）控制，这里只做门禁 + 预算。
        /// </summary>
        public static void DashStrike(Actor attacker, Vector2 targetPos)
        {
            try
            {
                EnsureArt();
                if (attacker == null) return;
                Vector2 from = attacker.current_position;
                if (!CanFull(targetPos) || !CanFull(from)) return;
                // v0.18.91：演出高度对准身体中心（脚底+0.17），拖影/残影/命中环不再贴地或悬头顶
                from.y += 0.17f;
                targetPos.y += 0.17f;
                Color c = new Color(1f, 0.95f, 0.75f);
                // 冲刺拖影：沿直线撒光点（快速掠过感；px=0.08×帧宽17→1.36×size，目标 0.4→0.15）
                Vector2 dir = targetPos - from;
                float dist = dir.magnitude;
                if (dist < 0.5f) dist = 1f;
                dir /= dist;
                int dots = 5 + (int)Mathf.Min(4f, dist * 0.5f);
                for (int i = 0; i < dots; i++)
                {
                    float t = (float)i / dots;
                    Vector2 p = Vector2.Lerp(from, targetPos, t);
                    CombatFxCore.Spawn(CombatFxArt.HitStars[0], p, 0.08f, 0.28f, FxLayer.Over, 30)
                        .Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(0.29f, 0.12f);
                }
                // 攻击者残影（身后淡出 ghost ×2；px=0.07×帧宽21→1.47×size，目标 0.8→0.4）
                for (int i = 0; i < 2; i++)
                {
                    Vector2 gpos = from - dir * (0.4f + i * 0.35f);
                    CombatFxCore.Spawn(CombatFxArt.Burst, gpos, 0.07f, 0.35f - i * 0.1f, FxLayer.Behind, 4)
                        .Color(new Color(c.r, c.g, c.b, 0.28f), new Color(c.r, c.g, c.b, 0f))
                        .Sized(0.54f - i * 0.11f, 0.2f);
                }
                // 目标命中环（扩张淡出；px=0.09×帧宽17→1.53×size，目标 1.0→0.4）
                CombatFxCore.Spawn(CombatFxArt.Rings[0], targetPos, 0.09f, 0.35f, FxLayer.Top, 30)
                    .Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(0.65f, 0.26f);
                CombatFxSound.Play(CombatFxSound.Whoosh, targetPos);
                // 真突进：攻击者本体朝目标前冲（视觉偏移，AI 无感）
                ActorDash(attacker, targetPos - from, Mathf.Min(0.55f, dist * 0.35f));
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] DashStrike: " + e.Message);
            }
        }

        // ----------------------------------------------------- 御剑剑气斩（v0.18.70）--

        /// <summary>
        /// 御剑剑气斩：攻击者→目标中点横扫月牙剑弧（旋转扫过，修仙剑光），
        /// 再向目标飞射 2 道剑气。适配 MultiShot/Execute 等斩系标签，
        /// 纯特效模拟不碰 AI，与 DashStrike 同级门禁。
        /// </summary>
        public static void SlashArc(Actor attacker, Vector2 targetPos)
        {
            try
            {
                EnsureArt();
                if (attacker == null) return;
                Vector2 from = attacker.current_position;
                if (!CanFull(targetPos) || !CanFull(from)) return;
                Vector2 dir = targetPos - from;
                float dist = dir.magnitude;
                if (dist < 0.5f) dist = 1f;
                dir /= dist;
                Vector2 mid = (from + targetPos) * 0.5f;
                // v0.18.91：剑弧/剑气对准身体中心高度（脚底+0.17），不再悬在头顶
                mid.y += 0.17f;
                Color c = new Color(0.82f, 0.93f, 1f);              // 剑气寒光
                float baseAng = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                // 横扫剑弧：3 段叠扫（大→小，旋转扫过 + 淡出；px=0.08×帧宽33→2.64×size，目标 1.0→0.4）
                for (int i = 0; i < 3; i++)
                {
                    float t = i / 3f;
                    var arc = CombatFxCore.Spawn(CombatFxArt.Slash, mid, 0.08f, 0.3f - t * 0.1f, FxLayer.Over, 32);
                    if (arc == null) continue;
                    arc.Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(0.38f, 0.16f);
                    arc.Rot = baseAng - 90f + (t - 0.5f) * 70f;     // 弧面朝向目标，从一侧扫向另一侧
                    arc.Spin = (1f - t) * 55f;
                }
                // 剑气飞射：2 道 Blade 朝目标（从身体中上部飞出）
                Vector2 origin = from + new Vector2(0f, 0.17f);
                for (int i = 0; i < 2; i++)
                {
                    Vector2 off = new Vector2(dir.y, -dir.x) * (i == 0 ? -0.35f : 0.35f);
                    Vector2 bFrom = origin + off;
                    Vector2 bDir = targetPos - bFrom;
                    if (bDir.magnitude < 0.2f) bDir = dir;
                    var blade = CombatFxCore.Spawn(CombatFxArt.Blade, bFrom, 0.1f, 0.42f, FxLayer.Over, 35);
                    if (blade == null) continue;
                    blade.Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(0.42f, 0.16f)
                         .Moving(bDir.normalized * (Mathf.Max(0.6f, bDir.magnitude) / 0.4f), 0f, 1.2f, 0f);
                    blade.Rot = Mathf.Atan2(bDir.y, bDir.x) * Mathf.Rad2Deg - 90f;
                }
                CombatFxSound.Play(CombatFxSound.Whoosh, mid);
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] SlashArc: " + e.Message);
            }
        }

        // ----------------------------------------------------- 震地重击（v0.18.70）--

        /// <summary>
        /// 震地重击：命中点击停定帧 + 地面放射裂纹 + 粗冲击波环 + 碎石飞溅。
        /// 适配 Knockback/Gravity/Petrify 等重击系标签。
        /// </summary>
        public static void GroundSlam(Actor attacker, Vector2 targetPos)
        {
            try
            {
                EnsureArt();
                if (attacker == null) return;
                if (!CanFull(targetPos)) return;
                Color c = new Color(0.96f, 0.84f, 0.58f);           // 大地土黄
                // 击停（重击定帧）
                CombatFxHitStop.Request(targetPos, 0.1f);
                // 地面裂纹（贴地 Behind 层，淡出）
                CombatFxCore.Spawn(CombatFxArt.Crack, targetPos, 0.09f, 0.7f, FxLayer.Behind, 2)
                    .Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(0.42f, 0.28f);
                // 粗冲击波环（扩张淡出）
                CombatFxCore.Spawn(CombatFxArt.Shocks[2], targetPos, 0.09f, 0.55f, FxLayer.Over, 30)
                    .Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(0.24f, 0.5f);
                // 碎石飞溅（4 块，抛物线）
                for (int i = 0; i < 4; i++)
                {
                    float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float spd = UnityEngine.Random.Range(3f, 6f);
                    CombatFxCore.Spawn(CombatFxArt.Debris[UnityEngine.Random.Range(0, CombatFxArt.Debris.Length)],
                        targetPos, 0.1f, 0.55f, FxLayer.Top, 40)
                        .Color(c, new Color(c.r, c.g, c.b, 0f))
                        .Sized(0.9f, 0.35f)
                        .Moving(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * spd, UnityEngine.Random.Range(2f, 4f), 2f, -4f);
                }
                CombatFxSound.Play(CombatFxSound.DropImpact, targetPos);
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] GroundSlam: " + e.Message);
            }
        }

        // ----------------------------------------------------- 雷法电链（v0.18.70）--

        /// <summary>
        /// 雷法电链：命中处放射 2~3 道锯齿电弧（短闪电，表现溅射连锁）。
        /// 适配 Chain 标签（引擎已实现 30% 溅射伤害，这里是溅射的视觉呈现）。
        /// 纯视觉方案，不遍历单位（避免依赖不确定的原版遍历 API）。
        /// </summary>
        public static void ChainArc(Actor victim)
        {
            try
            {
                EnsureArt();
                if (victim == null || !victim.isAlive()) return;
                Vector2 pos = victim.current_position;
                if (!CanFull(pos)) return;
                // v0.18.91：电弧从身体中心放射（脚底+0.17），原贴脚底弧光被地面截断
                pos.y += 0.17f;
                Color c = new Color(0.55f, 0.82f, 1f);              // 雷蓝
                int arcs = 2 + UnityEngine.Random.Range(0, 2);
                for (int i = 0; i < arcs; i++)
                {
                    float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float len = UnityEngine.Random.Range(1.5f, 2.6f);
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    // 每道电弧拆 2 段（中段抖动，贴合 Zap 折线观感）
                    Vector2 mid = pos + dir * len * 0.5f + new Vector2(
                        UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(-0.4f, 0.4f));
                    SpawnZap(pos, mid, c);
                    SpawnZap(mid, pos + dir * len, c);
                }
                CombatFxSound.Play(CombatFxSound.HitMetal, pos);
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] ChainArc: " + e.Message);
            }
        }

        /// <summary>单道锯齿电弧：Zap 图元旋转朝向连线（宽=线长，高≈0.35 世界单位）。</summary>
        private static void SpawnZap(Vector2 from, Vector2 to, Color c)
        {
            Vector2 d = to - from;
            float dist = Mathf.Max(0.3f, d.magnitude);
            Vector2 dir = d / dist;
            var zap = CombatFxCore.Spawn(CombatFxArt.Zap, (from + to) * 0.5f, 0.09f, 0.22f, FxLayer.Over, 36);
            if (zap == null) return;
            zap.Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(dist * 0.17f, dist * 0.34f);
            zap.Rot = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        }

        // ----------------------------------------------------- 领域展开（v0.18.70）--

        /// <summary>
        /// 领域展开：符文法阵环（旋转扩散 2 波）+ 圆周 4 根光柱（Blade 竖条，半透明）
        /// + 中心光斑。范围技能释放时调用，尺寸=影响范围（半透明不挡视野）。
        /// </summary>
        public static void DomainCircle(Vector2 pos, float radius, Color c)
        {
            try
            {
                EnsureArt();
                if (!CanFull(pos)) return;
                if (radius < 0.5f) radius = 0.5f;
                // 中心光斑（快速扩散淡出）
                CombatFxCore.Spawn(CombatFxArt.Burst, pos, 0.08f, 0.5f, FxLayer.Behind, 3)
                    .Color(new Color(c.r, c.g, c.b, 0.4f), new Color(c.r, c.g, c.b, 0f))
                    .Sized(0.4f, radius * 0.45f);
                // 符文环 2 波（旋转扩散）
                for (int w = 0; w < 2; w++)
                {
                    var ring = CombatFxCore.Spawn(CombatFxArt.RuneRing, pos, 0.09f, 0.65f, FxLayer.Over, 26);
                    if (ring == null) continue;
                    ring.Color(c, new Color(c.r, c.g, c.b, 0f)).Sized(radius * 0.2f, radius * 0.45f);
                    ring.Rot = w * 15f;
                    ring.Spin = 30f;
                }
                // 圆周 4 根光柱（Blade 竖条，半透明）
                for (int i = 0; i < 4; i++)
                {
                    float a = 45f + i * 90f;
                    Vector2 bp = pos + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * radius * 0.62f;
                    var beam = CombatFxCore.Spawn(CombatFxArt.Blade, bp, 0.08f, 0.7f, FxLayer.Behind, 4);
                    if (beam == null) continue;
                    beam.Color(new Color(c.r, c.g, c.b, 0.32f), new Color(c.r, c.g, c.b, 0f))
                             .Sized(radius * 0.42f, 0.1f);
                    beam.Rot = a;                                    // Blade 横向 → 旋转成竖/斜光柱
                }
                CombatFxSound.Play(CombatFxSound.Powerup, pos);
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] DomainCircle: " + e.Message);
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
                // v0.18.91：登神演出从身体中心展开（脚底+0.17），神形从身体上方 0.3 浮现而非头顶 0.8
                pos.y += 0.17f;
                Color gold = new Color(1f, 0.85f, 0.3f);
                // 时间：慢动作 0.25×1.2s + 冻结 0.35s（冲击帧）
                CombatFxCinema.SlowMo(0.25f, 1.2f);
                CombatFxCinema.Freeze(0.35f);
                CombatFxCinema.Dim(0.5f, new Color(0.02f, 0.02f, 0.06f), 0.5f);
                _ascendVeilTimer = 1.1f;
                // 金色冲击环 ×2（Cine 层：冻结中仍扩张发光）
                CombatFxCore.Spawn(CombatFxArt.Shocks[2], pos, 0.12f, 0.9f, FxLayer.Cine, 10)
                    .Color(gold, new Color(gold.r, gold.g, gold.b, 0f))
                    .Sized(0.2f, 0.5f);
                CombatFxCore.Spawn(CombatFxArt.Rings[2], pos, 0.12f, 1.1f, FxLayer.Cine, 5)
                    .Color(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.8f, 0.3f, 0f))
                    .Sized(0.25f, 0.62f);
                // 金色星雨（px=0.1×帧宽17→1.7×size，目标 0.6→0.2）
                for (int i = 0; i < 10; i++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float spd = UnityEngine.Random.Range(3f, 8f);
                    CombatFxCore.Spawn(CombatFxArt.HitStars[UnityEngine.Random.Range(0, CombatFxArt.HitStars.Length)],
                        pos, 0.1f, UnityEngine.Random.Range(0.5f, 0.9f), FxLayer.Cine, 20)
                        .Color(gold, new Color(gold.r, gold.g, gold.b, 0f)).Sized(0.35f, 0.12f)
                        .Moving(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd, UnityEngine.Random.Range(2f, 5f), 0.5f, 0f);
                }
                CombatFxSound.Play(CombatFxSound.Inspiration, pos);
                CombatFxSound.Play(CombatFxSound.ExplosionMeteorite, pos);
                // 神形显现（v0.18.66 unit_gifs）：登神单位上方浮现半透明虚影动画
                var deity = AbilityFx.PickUnitAnim();
                if (deity != null)
                {
                    // v0.18.91：神形按帧宽归一化到 1.5 世界单位宽（原 3.4 宽×6.5 高遮屏），
                    // 从身体上方 0.3 浮起缓慢上升，金色半透明
                    float fw = deity[0] != null ? deity[0].rect.width : 128f;
                    if (fw <= 0f) fw = 128f;
                    float size = 1.5f / (0.012f * fw);
                    CombatFxCore.Spawn(deity, pos + new Vector2(0f, 0.3f), 0.012f, 1.6f, FxLayer.Cine, 15)
                        .Color(new Color(1f, 0.9f, 0.5f, 0.85f), new Color(1f, 0.8f, 0.35f, 0f))
                        .Sized(size, size * 0.4f)
                        .Moving(new Vector2(0f, 1.1f), 0f, 0f, 0f);
                }
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Verbose("[CombatFx] AscensionCeremony: " + e.Message);
            }
        }

        // ----------------------------------------------------- 真突进 --

        /// <summary>
        /// 真突进（v0.18.66，参考「战斗之神」Lunge 思路）：
        /// 攻击瞬间给攻击者 move_jump_offset.x 一个朝目标的前冲偏移，随后滑回。
        /// 只动视觉偏移字段（x 分量原版从不写，y 归跳跃悬浮），不碰 AI 移动/寻路，无同步风险。
        /// </summary>
        private sealed class DashState
        {
            public float OriginX;   // 突进前 move_jump_offset.x
            public float Peak;      // 峰值偏移（含方向）
            public float Timer;     // 剩余时间
            public float Dur;       // 总时长
        }

        private static readonly Dictionary<Actor, DashState> _dashes = new Dictionary<Actor, DashState>();

        internal static void ActorDash(Actor a, Vector2 dir, float dist, float dur = 0.18f)
        {
            if (a == null || dir.sqrMagnitude < 0.0001f) return;
            if (_dashes.TryGetValue(a, out var cur))
            {
                // 连续突进：先还原再重设
                a.move_jump_offset = new Vector2(cur.OriginX, a.move_jump_offset.y);
            }
            Vector2 norm = dir.normalized;
            _dashes[a] = new DashState
            {
                OriginX = a.move_jump_offset.x,
                Peak = norm.x * dist,
                Timer = dur,
                Dur = dur
            };
            a.move_jump_offset = new Vector2(_dashes[a].OriginX + _dashes[a].Peak, a.move_jump_offset.y);
        }

        private static void TickDashes(float dt)
        {
            if (_dashes.Count == 0) return;
            var dead = new List<Actor>();
            foreach (var kv in _dashes)
            {
                Actor a = kv.Key;
                DashState s = kv.Value;
                if (a == null || !a.isAlive())
                {
                    dead.Add(kv.Key);
                    continue;
                }
                s.Timer -= dt;
                float t = s.Dur > 0f ? Mathf.Clamp01(s.Timer / s.Dur) : 0f;
                a.move_jump_offset = new Vector2(s.OriginX + s.Peak * t, a.move_jump_offset.y);
                if (s.Timer <= 0f) dead.Add(kv.Key);
            }
            foreach (var k in dead) _dashes.Remove(k);
        }

        // ---------------------------------------------------------- 驱动 --

        /// <summary>每帧驱动（FramePatch Postfix 调用）：登神暗幕定时恢复 + 突进滑回衰减。</summary>
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
            TickDashes(Time.unscaledDeltaTime);
        }

        /// <summary>世界销毁/存档切换：清空全部演出状态。</summary>
        public static void ClearAll()
        {
            CombatFxCore.Clear();
            CombatFxHitStop.Clear();
            CombatFxCinema.Clear();
            foreach (var kv in _dashes)
            {
                if (kv.Key != null)
                    kv.Key.move_jump_offset = new Vector2(kv.Value.OriginX, kv.Key.move_jump_offset.y);
            }
            _dashes.Clear();
            _hitCooldown.Clear();
            _ascendVeilTimer = 0f;
        }
    }
}
