// ============================================================
// 战斗表现层·击停（HitStop，v0.18.47）
// 大冲击时让整个世界停住一小会儿（动画的「impact frame」），
// 由 CombatFxFramePatch 拦截 MapBox.checkMainSimulationUpdate 实现：
// 击停期间跳过世界模拟帧，演出粒子（Cine 类）继续运动。
// 仅限玩家近距注视的冲击，且 1.8s 冷却，防止大战场卡顿。
// ============================================================
using UnityEngine;

namespace Code.Combat.Fx
{
    public static class CombatFxHitStop
    {
        private const float Cooldown = 1.8f;

        private static float _left;
        private static float _next;

        /// <summary>世界是否正被击停。</summary>
        public static bool Active => _left > 0f;

        /// <summary>
        /// 申请一次击停（单位秒）。门禁：世界存在、相机近距、冲击在屏幕内、冷却已过。
        /// </summary>
        public static void Request(Vector2 at, float seconds)
        {
            if (World.world == null) return;
            Camera cam = Camera.main;
            if (cam == null || cam.orthographicSize > 45f || !CombatFxCore.OnScreen(at, 0f)) return;
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + Cooldown;
            _left = Mathf.Max(_left, seconds);
        }

        /// <summary>以真实时间倒计时（冻结的世界没有游戏时间）。</summary>
        public static void Tick()
        {
            if (_left > 0f)
            {
                _left -= Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            }
        }

        public static void Clear()
        {
            _left = 0f;
            _next = 0f;
        }
    }
}
