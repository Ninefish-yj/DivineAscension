// ============================================================
// 战斗表现层·电影化（CinemaLite，v0.18.47）
// 世界的「屏息」时刻：
//   - Freeze：冻结世界（跳过模拟帧）
//   - SlowMo：慢动作（按比例跳帧）
//   - Dim/Undim：全屏暗幕（演出压过暗幕继续发光）
// 所有计时用真实时间（unscaled），暂停游戏时一并停止。
// ============================================================
using UnityEngine;

namespace Code.Combat.Fx
{
    public static class CombatFxCinema
    {
        private static float _freezeLeft;
        private static float _rate = 1f, _rateLeft, _acc;

        private static SpriteRenderer _veil;
        private static float _veilA, _veilTarget, _veilSpeed;
        private static Color _veilColor = Color.black;

        /// <summary>世界是否正被冻结。</summary>
        public static bool Frozen => _freezeLeft > 0f;

        /// <summary>世界速度倍率（慢动作时 &lt;1，正常为 1）。</summary>
        public static float Rate => _rateLeft > 0f ? _rate : 1f;

        /// <summary>本帧电影实时步（真实时间，暂停时为 0）。</summary>
        public static float Dt;

        /// <summary>冻结世界 'seconds' 秒（真实时间）。</summary>
        public static void Freeze(float seconds)
        {
            _freezeLeft = Mathf.Max(_freezeLeft, seconds);
        }

        /// <summary>世界以 'rate'（0.05..1）运行 'seconds' 秒真实时间。</summary>
        public static void SlowMo(float rate, float seconds)
        {
            _rate = Mathf.Clamp(rate, 0.05f, 1f);
            _rateLeft = seconds;
        }

        /// <summary>本帧模拟是否应跳过（冻结，或慢动作的「空帧」）。</summary>
        public static bool SkipFrame()
        {
            if (Frozen) return true;
            if (_rateLeft <= 0f) return false;
            _acc += _rate;
            if (_acc >= 1f)
            {
                _acc -= 1f;
                return false;
            }
            return true;
        }

        /// <summary>暗幕压到 alpha（0..1），fade 秒渐变。</summary>
        public static void Dim(float alpha, Color color, float fade)
        {
            _veilTarget = Mathf.Clamp01(alpha);
            _veilColor = color;
            _veilSpeed = fade <= 0f ? 1000f : Mathf.Abs(_veilTarget - _veilA) / fade;
        }

        public static void Undim(float fade)
        {
            _veilTarget = 0f;
            _veilSpeed = fade <= 0f ? 1000f : Mathf.Max(0.01f, _veilA) / fade;
        }

        /// <summary>每帧驱动：倒计时 + 暗幕渐变。先于粒子 Tick 调用。</summary>
        public static void Tick()
        {
            float real = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            bool paused = World.world == null || World.world.isPaused();
            Dt = paused ? 0f : real;
            if (!paused)
            {
                if (_freezeLeft > 0f) _freezeLeft -= real;
                if (_rateLeft > 0f) _rateLeft -= real;
            }
            _veilA = Mathf.MoveTowards(_veilA, _veilTarget, _veilSpeed * Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            UpdateVeil();
        }

        private static void UpdateVeil()
        {
            Camera cam = Camera.main;
            if (_veilA <= 0.001f || cam == null || !MapBox.isRenderGameplay())
            {
                if (_veil != null) _veil.enabled = false;
                return;
            }
            EnsureVeil();
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            _veil.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
            _veil.transform.localScale = new Vector3(w + 2f, h + 2f, 1f);
            Color c = _veilColor;
            c.a = _veilA;
            _veil.color = c;
            _veil.enabled = true;
        }

        private static void EnsureVeil()
        {
            if (_veil != null) return;
            GameObject go = new GameObject("GeneCultivationVeil");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _veil = go.AddComponent<SpriteRenderer>();
            if (CombatFxArt.White != null) _veil.sprite = CombatFxArt.White;
            _veil.sortingLayerID = 0;
            _veil.sortingOrder = 29000; // 低于 Cine 粒子（30000+），高于一切世界内容
        }

        public static void Clear()
        {
            _freezeLeft = 0f;
            _rateLeft = 0f;
            _acc = 0f;
            _veilA = 0f;
            _veilTarget = 0f;
            if (_veil != null) _veil.enabled = false;
        }
    }
}
