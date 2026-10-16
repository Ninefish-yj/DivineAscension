// ============================================================
// 异能特效层（v0.18.28）
// 标签 → 原版特效映射：异能触发时播放视觉表现（复用原版 EffectsLibrary 粒子）
// 纯展示层，不写任何业务数据；失败静默降级（try/catch + 出镜率限制防刷屏）。
// 播放模式 = DisasterManager 已验证：EffectsLibrary.spawn(effectName, tile, ...)
// 特效 id 清单取自原版反编译 EffectsLibrary.cs（92 个 fx_*）。
// ============================================================

using UnityEngine;

namespace Code.Realm.Prototype
{
    public static class PrototypeFx
    {
        /// <summary>异能特效总开关（默认开；卡顿时可关）</summary>
        public static bool Enabled = true;

        /// <summary>攻击标签 → 原版特效 id（目标位置播放）</summary>
        private static string GetAttackFx(PrototypeTag tag)
        {
            switch (tag)
            {
                case PrototypeTag.Burn: return "fx_fireball_explosion";      // 灼烧：火球爆
                case PrototypeTag.Plasma: return "fx_plasma_ball_explosion";// 等离子：离子爆
                case PrototypeTag.Frost:
                case PrototypeTag.Frostbite: return "fx_cast_ground_blue";   // 冰：蓝色施法
                case PrototypeTag.TimeSlow: return "fx_cast_top_blue";       // 时缓：蓝光
                case PrototypeTag.Stun: return "fx_hit";                     // 眩晕：打击
                case PrototypeTag.Crit: return "fx_hit_critical";            // 暴击：重击
                case PrototypeTag.Telekinesis: return "fx_teleport_singularity"; // 念力：扭曲
                case PrototypeTag.Poison: return "fx_spores";                // 中毒：毒雾
                case PrototypeTag.Curse: return "fx_cast_ground_purple";     // 诅咒：紫光
                case PrototypeTag.Fear: return "fx_cast_ground_purple";      // 恐惧：紫光
                case PrototypeTag.Slow: return "fx_cast_top_blue";           // 减速：蓝光
                case PrototypeTag.Lifesteal:
                case PrototypeTag.Drain:
                case PrototypeTag.Blood:
                case PrototypeTag.Soul: return "fx_hearts";                  // 吸血系：心形
                case PrototypeTag.Heal: return "fx_hearts";                  // 治疗：心形
                case PrototypeTag.Chain: return "fx_lightning_medium";       // 连锁：闪电
                case PrototypeTag.Knockback: return "fx_explosion_tiny";     // 击退：小爆
                case PrototypeTag.Gravity: return "fx_boulder";              // 重力：落石
                case PrototypeTag.Berserk: return "fx_fire_smoke";           // 狂暴：怒火
                // ---- v0.18.24 大众认知补充 ----
                case PrototypeTag.Entangle: return "fx_fishnet";             // 束缚：渔网
                case PrototypeTag.Petrify: return "fx_boulder_impact";       // 石化：落石
                case PrototypeTag.Aging: return "fx_cast_ground_purple";     // 衰老：紫光
                case PrototypeTag.Weakness: return "fx_cast_ground_purple";  // 虚弱：紫光
                case PrototypeTag.ArmorBreak: return "fx_spark";             // 破甲：火花
                case PrototypeTag.Execute: return "fx_grin_reaper";          // 致死：死神
                case PrototypeTag.Sleep: return "fx_cast_top_blue";          // 睡眠：蓝光
                case PrototypeTag.MultiShot: return "fx_slash";              // 多重：连续斩
                case PrototypeTag.HealAlly: return "fx_cast_ground_green";   // 群疗：绿光
                default: return null;
            }
        }

        /// <summary>防守标签 → 原版特效 id（自身位置播放）</summary>
        private static string GetDefendFx(PrototypeTag tag)
        {
            switch (tag)
            {
                case PrototypeTag.Shield: return "fx_shield_hit";            // 护盾：盾击
                case PrototypeTag.Dodge:
                case PrototypeTag.Illusion: return "fx_dodge";               // 闪避/幻象：闪避
                case PrototypeTag.Invisibility: return "fx_teleport_blue";   // 隐身：蓝传
                case PrototypeTag.Teleport: return "fx_teleport_singularity";// 瞬移：扭曲
                case PrototypeTag.Clone: return "fx_teleport_red";           // 分身：红传
                case PrototypeTag.Blessing: return "fx_cast_ground_green";   // 祝福：绿光
                case PrototypeTag.Regeneration: return "fx_cast_ground_green";// 再生：绿光
                case PrototypeTag.Magnetism: return "fx_lightning_small";    // 磁力：小闪电
                case PrototypeTag.Vibration:
                case PrototypeTag.Sound: return "fx_explosion_tiny";         // 震动/声波：小爆
                case PrototypeTag.Thorns: return "fx_spark";                 // 反弹：火花
                case PrototypeTag.ElementResist: return "fx_shield_hit";     // 元素抗性：盾击
                case PrototypeTag.Cleanse: return "fx_cast_top_green";       // 净化：圣光
                default: return null;
            }
        }

        /// <summary>攻击触发：目标位置播放特效（35% 出镜率，防每击刷屏）</summary>
        public static void PlayAttackFx(Actor attacker, Actor target, PrototypeTag tag, bool force = false)
        {
            if (!Enabled || attacker == null || target == null) return;
            string fx = GetAttackFx(tag);
            if (string.IsNullOrEmpty(fx)) return;
            if (!force && UnityEngine.Random.value > 0.35f) return;
            PlayAt(target, fx);
        }

        /// <summary>防守触发：自身位置播放特效（chance 出镜率；常驻减伤类传低值防视觉疲劳）</summary>
        public static void PlayDefendFx(Actor defender, PrototypeTag tag, float chance = 0.6f)
        {
            if (!Enabled || defender == null) return;
            string fx = GetDefendFx(tag);
            if (string.IsNullOrEmpty(fx)) return;
            if (UnityEngine.Random.value > chance) return;
            PlayAt(defender, fx);
        }

        /// <summary>在单位位置播放原版特效（模组已验证模式：DisasterManager.cs）</summary>
        private static void PlayAt(Actor actor, string fxId)
        {
            if (actor == null || !actor.isAlive() || World.world == null) return;
            try
            {
                var pos = actor.current_position;
                WorldTile tile = World.world.GetTileSimple((int)pos.x, (int)pos.y);
                if (tile == null) return;
                EffectsLibrary.spawn(fxId, tile, null, null, 0f, -1f, -1f, null);
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] 异能特效失败 " + fxId + ": " + dsEx.Message); }
        }
    }
}
