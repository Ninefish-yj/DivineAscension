// ============================================================
// 战斗表现层·程序化精灵工厂（v0.18.47）
// 运行时用 Texture2D 生成像素美术（FilterMode.Point 保持锐利），
// 零素材文件依赖：星形/火花/碎石/尘土/圆环/冲击波/闪光/光斑。
// 全部白色生成，颜色由粒子的 Grad 渐变控制；alpha 由图元函数给出。
// ============================================================
using System;
using UnityEngine;

namespace Code.Combat.Fx
{
    internal static class CombatFxArt
    {
        /// <summary>命中星形（3 档大小）。</summary>
        internal static Sprite[] HitStars;
        /// <summary>火花/斜条（5 变体）。</summary>
        internal static Sprite[] Sparks;
        /// <summary>碎石块（4 变体）。</summary>
        internal static Sprite[] Debris;
        /// <summary>尘土软斑（4 档）。</summary>
        internal static Sprite[] Dust;
        /// <summary>细圆环（小/中/大）。</summary>
        internal static Sprite[] Rings;
        /// <summary>粗冲击波环（小/中/大）。</summary>
        internal static Sprite[] Shocks;
        /// <summary>十字闪光。</summary>
        internal static Sprite Glint;
        /// <summary>中心光斑（径向渐隐）。</summary>
        internal static Sprite Burst;
        /// <summary>纯白 1×1（暗幕/遮罩用）。</summary>
        internal static Sprite White;

        // ---- v0.18.70 战斗动作图元 ----
        /// <summary>月牙剑弧（御剑横扫）。</summary>
        internal static Sprite Slash;
        /// <summary>飞剑/剑气（长菱形条）。</summary>
        internal static Sprite Blade;
        /// <summary>锯齿闪电（连锁电弧）。</summary>
        internal static Sprite Zap;
        /// <summary>地面放射裂纹（震地重击）。</summary>
        internal static Sprite Crack;
        /// <summary>符文法阵环（领域展开）。</summary>
        internal static Sprite RuneRing;

        internal static void Init()
        {
            try
            {
                HitStars = new[]
                {
                    Make(9, 9, (x, y) => Star5At(x, y, 9, 9, 4.2f)),
                    Make(13, 13, (x, y) => Star5At(x, y, 13, 13, 6.1f)),
                    Make(17, 17, (x, y) => Star5At(x, y, 17, 17, 8.0f))
                };
                Sparks = new[]
                {
                    Make(5, 5, (x, y) => Diamond(x, y, 5, 5, 1.6f)),
                    Make(7, 7, (x, y) => Diamond(x, y, 7, 7, 2.4f)),
                    Make(9, 9, (x, y) => Diamond(x, y, 9, 9, 3.2f)),
                    Make(3, 9, (x, y) => Bar(x, y, 3, 9, 0.9f)),
                    Make(5, 13, (x, y) => Bar(x, y, 5, 13, 1.5f))
                };
                Debris = new[]
                {
                    Make(7, 7, (x, y) => Hash(x, y, 7, 0.45f)),
                    Make(8, 8, (x, y) => Hash(x, y, 8, 0.40f)),
                    Make(9, 9, (x, y) => Hash(x, y, 9, 0.38f)),
                    Make(11, 11, (x, y) => Hash(x, y, 11, 0.34f))
                };
                Dust = new[]
                {
                    Make(9, 9, (x, y) => SoftDot(x, y, 9, 9, 4.2f)),
                    Make(13, 13, (x, y) => SoftDot(x, y, 13, 13, 6.0f)),
                    Make(17, 17, (x, y) => SoftDot(x, y, 17, 17, 8.0f)),
                    Make(23, 23, (x, y) => SoftDot(x, y, 23, 23, 11.0f))
                };
                Rings = new[]
                {
                    Make(17, 17, (x, y) => Ring(x, y, 17, 17, 8.0f, 0.62f, 1.0f)),
                    Make(25, 25, (x, y) => Ring(x, y, 25, 25, 12.0f, 0.66f, 1.0f)),
                    Make(33, 33, (x, y) => Ring(x, y, 33, 33, 16.0f, 0.70f, 1.0f))
                };
                Shocks = new[]
                {
                    Make(25, 25, (x, y) => Ring(x, y, 25, 25, 12.0f, 0.55f, 0.9f)),
                    Make(41, 41, (x, y) => Ring(x, y, 41, 41, 20.0f, 0.58f, 0.9f)),
                    Make(57, 57, (x, y) => Ring(x, y, 57, 57, 28.0f, 0.62f, 0.9f))
                };
                Glint = Make(9, 9, (x, y) => Cross(x, y, 9, 9));
                Burst = Make(21, 21, (x, y) => SoftDot(x, y, 21, 21, 9.5f));
                White = Make(1, 1, (x, y) => 1f);
                // v0.18.70 战斗动作图元
                Slash = Make(33, 33, (x, y) => SlashAt(x, y, 33, 33));
                Blade = Make(29, 9, (x, y) => BladeAt(x, y, 29, 9));
                Zap = Make(33, 13, (x, y) => ZapAt(x, y, 33, 13));
                Crack = Make(41, 41, (x, y) => CrackAt(x, y, 41, 41));
                RuneRing = Make(49, 49, (x, y) => RuneRingAt(x, y, 49, 49));
            }
            catch (Exception e)
            {
                Code.Core.DSDebug.Error("[CombatFxArt] 精灵生成失败: " + e.Message);
            }
        }

        /// <summary>按像素 alpha 生成精灵：PPU 固定 100（世界尺寸 = 像素×px 由粒子的 Px 缩放实现）。</summary>
        private static Sprite Make(int w, int h, Func<int, int, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(alpha(x, y));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---------------------------------------------------------- 图元 --

        /// <summary>五角星：尖角半径 r，谷底半径 0.62r，硬边。</summary>
        private static float Star5At(int x, int y, int w, int h, float r)
        {
            float cx = (w - 1) * 0.5f;
            float cy = (h - 1) * 0.5f;
            float dx = x - cx;
            float dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist > r) return 0f;
            float theta = Mathf.Atan2(dy, dx) + Mathf.PI / 2f; // 尖角朝上
            float sector = theta % (Mathf.PI * 2f / 5f);
            if (sector > Mathf.PI / 5f) sector -= Mathf.PI * 2f / 5f;
            if (sector < -Mathf.PI / 5f) sector += Mathf.PI * 2f / 5f;
            float rad = r * (0.62f + 0.38f * (1f - Mathf.Abs(sector) / (Mathf.PI / 5f)));
            return dist <= rad ? 1f : 0f;
        }

        /// <summary>菱形斜条（火花）：|dx|+|dy| 距离，边缘 1px 软。</summary>
        private static float Diamond(int x, int y, int w, int h, float t)
        {
            float dx = Mathf.Abs(x - (w - 1) * 0.5f);
            float dy = Mathf.Abs(y - (h - 1) * 0.5f);
            return Mathf.Clamp01(t + 0.5f - (dx + dy));
        }

        /// <summary>细长斜条（火花）：绕中心 45° 的窄矩形。</summary>
        private static float Bar(int x, int y, int w, int h, float half)
        {
            float dx = x - (w - 1) * 0.5f;
            float dy = y - (h - 1) * 0.5f;
            float ux = (dx + dy) * 0.7071f; // 旋转 45°
            float uy = (-dx + dy) * 0.7071f;
            float len = (Mathf.Max(w, h) - 1) * 0.5f;
            if (Mathf.Abs(uy) > len * 0.98f) return 0f;
            return Mathf.Clamp01(half + 0.5f - Mathf.Abs(ux));
        }

        /// <summary>确定性随机斑点（碎石）：hash 阈值。</summary>
        private static float Hash(int x, int y, int seed, float threshold)
        {
            float h = Mathf.Sin(x * 12.9898f + y * 78.233f + seed * 37.719f) * 43758.5453f;
            h = h - Mathf.Floor(h);
            return h < threshold ? 1f : 0f;
        }

        /// <summary>软圆斑：径向渐隐。</summary>
        private static float SoftDot(int x, int y, int w, int h, float r)
        {
            float dx = x - (w - 1) * 0.5f;
            float dy = y - (h - 1) * 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(1f - dist / r);
        }

        /// <summary>圆环：d ∈ [r*inner, r*outer]，边缘 1px 软。</summary>
        private static float Ring(int x, int y, int w, int h, float r, float inner, float outer)
        {
            float dx = x - (w - 1) * 0.5f;
            float dy = y - (h - 1) * 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float lo = r * inner;
            float hi = r * outer;
            if (dist < lo - 1f || dist > hi) return 0f;
            if (dist < lo) return dist - (lo - 1f);
            if (dist > hi - 1f) return hi - dist;
            return 1f;
        }

        /// <summary>十字闪光：细十字。</summary>
        private static float Cross(int x, int y, int w, int h)
        {
            float dx = Mathf.Abs(x - (w - 1) * 0.5f);
            float dy = Mathf.Abs(y - (h - 1) * 0.5f);
            if (dx <= 0.8f || dy <= 0.8f) return 1f;
            return 0f;
        }

        // ---------------------------------------------------------- 战斗动作图元（v0.18.70）--

        /// <summary>确定性线性 hash（[0,1)）。</summary>
        private static float LinHash(int seed)
        {
            float h = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
            h = h - Mathf.Floor(h);
            return h;
        }

        /// <summary>点到线段距离。</summary>
        private static float SegDist(int x, int y, float ax, float ay, float bx, float by)
        {
            float px = x, py = y;
            float abx = bx - ax, aby = by - ay;
            float len2 = abx * abx + aby * aby;
            float t = len2 < 0.0001f ? 0f : Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / len2);
            float cx = ax + abx * t, cy = ay + aby * t;
            float dx = px - cx, dy = py - cy;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>月牙剑弧（御剑横扫）：环形段（角度扇区 ±0.55rad），旋转交给粒子。</summary>
        private static float SlashAt(int x, int y, int w, int h)
        {
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            float dx = x - cx, dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float r = (w - 1) * 0.5f * 0.78f;
            float lo = r * 0.42f, hi = r * 0.72f;
            if (dist < lo - 1f || dist > hi) return 0f;
            float a = Mathf.Atan2(dy, dx);
            if (a < -0.55f || a > 0.55f) return 0f;   // 弧段朝右，旋转/翻转由粒子控制
            float alpha = 1f;
            if (dist < lo) alpha = dist - (lo - 1f);
            if (dist > hi - 1f) alpha = hi - dist;
            // 两端渐隐
            float edge = Mathf.Abs(a) > 0.42f ? (0.55f - Mathf.Abs(a)) / 0.13f : 1f;
            return alpha * Mathf.Clamp01(edge);
        }

        /// <summary>飞剑/剑气（长菱形条）：|dx|+3|dy| 窄条，两端尖。</summary>
        private static float BladeAt(int x, int y, int w, int h)
        {
            float dx = Mathf.Abs(x - (w - 1) * 0.5f);
            float dy = Mathf.Abs(y - (h - 1) * 0.5f);
            float t = (w - 1) * 0.5f;
            return Mathf.Clamp01(t + 0.8f - (dx + 3f * dy));
        }

        /// <summary>锯齿闪电（横向折线）：4 段确定性折点，线宽 1.4px。</summary>
        private static float ZapAt(int x, int y, int w, int h)
        {
            float cy = (h - 1) * 0.5f;
            float x0 = 0f, y0 = cy;
            float amp = (h - 1) * 0.5f - 1.5f;
            float seg = (w - 1) / 4f;
            float min = 10f;
            for (int i = 1; i <= 4; i++)
            {
                float nx = seg * i;
                float ny = cy + (LinHash(i * 7 + 13) - 0.5f) * 2f * amp * (i == 4 ? 0.2f : 1f);
                min = Mathf.Min(min, SegDist(x, y, x0, y0, nx, ny));
                x0 = nx; y0 = ny;
            }
            return min <= 1.4f ? 1f : 0f;
        }

        /// <summary>地面放射裂纹：中心 6 条射线（角度 hash 抖动，线长 11~19px）。</summary>
        private static float CrackAt(int x, int y, int w, int h)
        {
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            float min = 10f;
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + (LinHash(i + 3) - 0.5f) * 22f) * Mathf.Deg2Rad;
                float len = 11f + LinHash(i + 9) * 8f;
                float ex = cx + Mathf.Cos(a) * len;
                float ey = cy + Mathf.Sin(a) * len;
                min = Mathf.Min(min, SegDist(x, y, cx, cy, ex, ey));
            }
            // 中心点实心
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return (min <= 1.5f || d <= 2.2f) ? 1f : 0f;
        }

        /// <summary>符文法阵环：粗环 + 圆周 12 刻度（领域展开）。</summary>
        private static float RuneRingAt(int x, int y, int w, int h)
        {
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            float dx = x - cx, dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float r = (w - 1) * 0.5f * 0.88f;
            float lo = r * 0.82f, hi = r * 0.94f;
            if (dist >= lo - 1f && dist <= hi)
            {
                float alpha = 1f;
                if (dist < lo) alpha = dist - (lo - 1f);
                if (dist > hi - 1f) alpha = hi - dist;
                if (alpha > 0f)
                {
                    float a = Mathf.Atan2(dy, dx);
                    // 12 刻度：环上凸起短条
                    float step = Mathf.PI * 2f / 12f;
                    float k = a / step;
                    float kf = k - Mathf.Floor(k);
                    float spread = Mathf.Min(kf, 1f - kf) * step;
                    if (spread < 0.14f) return alpha;
                    return alpha * 0.55f;   // 环体略暗，刻度亮
                }
            }
            return 0f;
        }
    }
}
