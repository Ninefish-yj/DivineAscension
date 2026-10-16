// ============================================================
// 战斗表现层·音效（v0.18.47）
// 调用原版 FMOD 事件（MusicBox.playSound 坐标版本）。
// 事件路径取自原版 bank：event:/SFX/...
// ============================================================
using UnityEngine;

namespace Code.Combat.Fx
{
    public static class CombatFxSound
    {
        // ---- 常用原版 FMOD 事件路径（均存在于原版音效库） ----
        public const string HitGeneric = "event:/SFX/HIT/HitGeneric";
        public const string HitStone = "event:/SFX/HIT/HitStone";
        public const string HitMetal = "event:/SFX/HIT/HitMetal";
        public const string ExplosionSmall = "event:/SFX/EXPLOSIONS/ExplosionSmall";
        public const string ExplosionMeteorite = "event:/SFX/EXPLOSIONS/ExplosionMeteorite";
        public const string ExplosionForce = "event:/SFX/EXPLOSIONS/ExplosionForce";
        public const string DropImpact = "event:/SFX/DESTRUCTION/DropSimpleImpact";
        public const string Powerup = "event:/SFX/POWERS/Powerup";
        public const string Inspiration = "event:/SFX/POWERS/Inspiration";
        public const string Blessing = "event:/SFX/POWERS/Blessing";
        public const string Whoosh = "event:/SFX/UNIQUE/SpawnWhoosh";

        /// <summary>在世界坐标播放音效（仅游戏视角）。失败静默（缺失音效不打断演出）。</summary>
        public static void Play(string eventPath, Vector2 pos)
        {
            try
            {
                MusicBox.playSound(eventPath, pos.x, pos.y, false, false);
            }
            catch
            {
                // 缺失音效不阻止演出
            }
        }
    }
}
