// ============================================================
// 战斗表现层·粒子核心（v0.18.47）
// 参考「战斗之神」模组的 Fx 体系精简移植：
//   - SpriteRenderer 池化粒子（无 GC 压力）
//   - 五层渲染（地面/单位后/单位前/顶层/电影幕上）
//   - 简单物理（重力/拖拽/弹跳）+ 颜色渐变 + 帧动画 + 旋转
// 独立模块，不依赖业务逻辑；业务侧通过 CombatFxDirector 调用。
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Combat.Fx
{
    /// <summary>特效渲染层级：地面（划痕/圆环）/单位后（残影）/单位前（光晕）/顶层（火花/闪光）/电影幕上（暗幕中仍亮的演出）</summary>
    public enum FxLayer
    {
        Ground,
        Behind,
        Over,
        Top,
        Cine
    }

    /// <summary>
    /// 一个可复用粒子：世界坐标 + 高度（Z，画成 +y 偏移）+ 简单物理。
    /// 位置为世界单位；Px 为「每像素世界单位」。
    /// </summary>
    public sealed class CombatFxParticle
    {
        internal GameObject Go;
        internal Transform Tr;
        internal SpriteRenderer R;

        /// <summary>帧动画（多帧）；单帧时用 One。</summary>
        internal Sprite[] Frames;
        /// <summary>每秒帧数；0 = 整段生命均匀摊帧。</summary>
        public float Fps;
        public bool Loop;

        public Vector2 Pos;
        public float Z;
        public Vector2 Vel;
        public float VZ;
        public float Gravity;
        /// <summary>每秒速度衰减（指数）。</summary>
        public float Drag;
        /// <summary>落地弹性；负数 = 不落地（原地保持）。</summary>
        public float Bounce = -1f;
        public int Bounces;

        public float Age, Life, Delay;
        /// <summary>生命期颜色渐变（均匀分布，含 alpha）。</summary>
        public Color[] Grad;
        public float Size0 = 1f, Size1 = 1f;
        public float Rot, Spin;
        /// <summary>素材宽高比（高/宽，来自 sprite.rect，v0.18.72 竖素材防竖拉）。</summary>
        public float Aspect = 1f;
        /// <summary>目标高宽比（Y/X 世界尺寸比），默认=素材原始比；护盾等需压扁时单独调。</summary>
        public float YRatio = 1f;
        /// <summary>sprite 的 +y 轴朝向运动方向。</summary>
        public bool FaceVel;
        /// <summary>对齐到 texel 网格（像素锐利的贴地美术）。</summary>
        public bool Snap;
        /// <summary>电影冻结世界时仍继续运动（暗幕上演出）。</summary>
        public bool Cine;
        /// <summary>绑定单位（非空时每帧跟随单位位置，护盾/光环/神形类环绕演出用）。</summary>
        public Actor FollowActor;
        public Vector2 FollowOffset;
        public float Px = 0.1f;
        internal bool Alive;

        internal readonly Sprite[] One = new Sprite[1];

        /// <summary>生命进度 0..1。</summary>
        public float K => Life > 0f ? Mathf.Clamp01(Age / Life) : 1f;

        public void Kill() { Life = 0f; }

        private Color[] _g1, _g2, _g3, _g4, _g5;

        public CombatFxParticle Color(params Color[] stops) { Grad = stops; return this; }
        public CombatFxParticle Color(Color a)
        {
            Color[] g = _g1 ??= new Color[1];
            g[0] = a;
            Grad = g;
            return this;
        }
        public CombatFxParticle Color(Color a, Color b)
        {
            Color[] g = _g2 ??= new Color[2];
            g[0] = a; g[1] = b;
            Grad = g;
            return this;
        }
        public CombatFxParticle Color(Color a, Color b, Color c)
        {
            Color[] g = _g3 ??= new Color[3];
            g[0] = a; g[1] = b; g[2] = c;
            Grad = g;
            return this;
        }
        public CombatFxParticle Color(Color a, Color b, Color c, Color d)
        {
            Color[] g = _g4 ??= new Color[4];
            g[0] = a; g[1] = b; g[2] = c; g[3] = d;
            Grad = g;
            return this;
        }
        public CombatFxParticle Color(Color a, Color b, Color c, Color d, Color e)
        {
            Color[] g = _g5 ??= new Color[5];
            g[0] = a; g[1] = b; g[2] = c; g[3] = d; g[4] = e;
            Grad = g;
            return this;
        }

        public CombatFxParticle Sized(float from, float to) { Size0 = from; Size1 = to; return this; }

        public CombatFxParticle Moving(Vector2 vel, float vz = 0f, float drag = 0f, float gravity = 0f)
        {
            Vel = vel;
            VZ = vz;
            Drag = drag;
            Gravity = gravity;
            return this;
        }
    }

    /// <summary>粒子池 + 渲染核心。单例静态，游戏世界生命周期由 CombatFxDirector.ClearAll 管理。</summary>
    public static class CombatFxCore
    {
        private const int MaxLive = 1500;

        private static readonly List<CombatFxParticle> _live = new List<CombatFxParticle>();
        private static readonly Stack<CombatFxParticle> _free = new Stack<CombatFxParticle>();
        private static Transform _root;
        private static Material _normal;
        private static int _layerGround, _layerUnits, _layerTop;

        public static int Live => _live.Count;

        /// <summary>当前是否在屏幕内（世界坐标 → 屏幕，宽松边界）。</summary>
        public static bool OnScreen(Vector2 at, float pad)
        {
            Camera cam = Camera.main;
            if (cam == null) return true;
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(at.x, at.y, 0f));
            if (sp.z < 0f) return false;
            float p = pad * 100f;
            return sp.x >= -p && sp.x <= Screen.width + p && sp.y >= -p && sp.y <= Screen.height + p;
        }

        private static void EnsureInit()
        {
            if (_root != null) return;
            GameObject root = new GameObject("GeneCultivationFx");
            UnityEngine.Object.DontDestroyOnLoad(root);
            _root = root.transform;
            Shader sprite = Shader.Find("Sprites/Default");
            _normal = sprite != null ? new Material(sprite) : null;
            _layerGround = Layer("EffectsBack");
            _layerUnits = Layer("Objects");
            _layerTop = Layer("EffectsTop");
        }

        private static int Layer(string name)
        {
            int id = SortingLayer.NameToID(name);
            return SortingLayer.IsValid(id) ? id : 0;
        }

        // ----------------------------------------------------------- 生成 --

        /// <summary>新建特效精灵；预算耗尽返回 null（调用方直接跳过）。</summary>
        public static CombatFxParticle Spawn(Sprite[] frames, Vector2 pos, float px, float life, FxLayer layer, int order = 0)
        {
            return frames == null || frames.Length == 0 ? null : Take(frames, pos, px, life, layer, order);
        }

        public static CombatFxParticle Spawn(Sprite sprite, Vector2 pos, float px, float life, FxLayer layer, int order = 0)
        {
            return sprite == null ? null : Take(sprite, pos, px, life, layer, order);
        }

        private static CombatFxParticle Take(Sprite[] frames, Vector2 pos, float px, float life, FxLayer layer, int order)
        {
            EnsureInit();
            if (_live.Count >= MaxLive) return null;
            CombatFxParticle p = _free.Count > 0 ? _free.Pop() : Create();
            p.Frames = frames;
            p.Fps = 0f;
            p.Loop = false;
            p.Pos = pos;
            p.Z = 0f;
            p.Vel = Vector2.zero;
            p.VZ = 0f;
            p.Gravity = 0f;
            p.Drag = 0f;
            p.Bounce = -1f;
            p.Bounces = 0;
            p.Age = 0f;
            p.Life = life;
            p.Delay = 0f;
            p.Grad = null;
            p.Size0 = p.Size1 = 1f;
            p.Rot = 0f;
            p.Spin = 0f;
            p.FaceVel = false;
            p.Snap = false;
            p.Cine = layer == FxLayer.Cine;
            p.Px = px;
            p.Alive = true;
            // v0.18.72：素材宽高比（取首帧像素 rect，无需 bounds 开销）
            p.Aspect = (frames != null && frames.Length > 0 && frames[0] != null)
                ? frames[0].rect.height / Mathf.Max(0.0001f, frames[0].rect.width) : 1f;
            p.YRatio = p.Aspect;
            if (_normal != null) p.R.sharedMaterial = _normal;
            switch (layer)
            {
                case FxLayer.Ground:
                    p.R.sortingLayerID = _layerGround;
                    p.R.sortingOrder = order;
                    break;
                case FxLayer.Behind:
                    p.R.sortingLayerID = _layerUnits;
                    p.R.sortingOrder = -20 + order;
                    break;
                case FxLayer.Over:
                    p.R.sortingLayerID = _layerUnits;
                    p.R.sortingOrder = 20 + order;
                    break;
                case FxLayer.Top:
                    p.R.sortingLayerID = _layerTop;
                    p.R.sortingOrder = 50 + order;
                    break;
                default: // Cine：压过一切（含暗幕）
                    p.R.sortingLayerID = _layerTop;
                    p.R.sortingOrder = 30000 + order;
                    break;
            }
            if (p.Frames != null && p.Frames.Length > 0) p.R.sprite = p.Frames[0];
            p.R.color = Color.white;
            p.R.enabled = false; // 首次 Update 到位后再显示
            p.Go.SetActive(true);
            _live.Add(p);
            return p;
        }

        private static CombatFxParticle Take(Sprite sprite, Vector2 pos, float px, float life, FxLayer layer, int order)
        {
            CombatFxParticle p = _free.Count > 0 ? _free.Pop() : Create();
            EnsureInit();
            if (_live.Count >= MaxLive) { _free.Push(p); return null; }
            p.One[0] = sprite;
            p.Frames = p.One;
            p.Fps = 0f;
            p.Loop = false;
            p.Pos = pos;
            p.Z = 0f;
            p.Vel = Vector2.zero;
            p.VZ = 0f;
            p.Gravity = 0f;
            p.Drag = 0f;
            p.Bounce = -1f;
            p.Bounces = 0;
            p.Age = 0f;
            p.Life = life;
            p.Delay = 0f;
            p.Grad = null;
            p.Size0 = p.Size1 = 1f;
            p.Rot = 0f;
            p.Spin = 0f;
            p.FaceVel = false;
            p.Snap = false;
            p.Cine = layer == FxLayer.Cine;
            p.Px = px;
            p.Alive = true;
            // v0.18.72：素材宽高比（取 sprite 像素 rect，无需 bounds 开销）
            p.Aspect = (sprite != null)
                ? sprite.rect.height / Mathf.Max(0.0001f, sprite.rect.width) : 1f;
            p.YRatio = p.Aspect;
            if (_normal != null) p.R.sharedMaterial = _normal;
            switch (layer)
            {
                case FxLayer.Ground:
                    p.R.sortingLayerID = _layerGround;
                    p.R.sortingOrder = order;
                    break;
                case FxLayer.Behind:
                    p.R.sortingLayerID = _layerUnits;
                    p.R.sortingOrder = -20 + order;
                    break;
                case FxLayer.Over:
                    p.R.sortingLayerID = _layerUnits;
                    p.R.sortingOrder = 20 + order;
                    break;
                case FxLayer.Top:
                    p.R.sortingLayerID = _layerTop;
                    p.R.sortingOrder = 50 + order;
                    break;
                default:
                    p.R.sortingLayerID = _layerTop;
                    p.R.sortingOrder = 30000 + order;
                    break;
            }
            p.R.sprite = sprite;
            p.R.color = Color.white;
            p.R.enabled = false;
            p.Go.SetActive(true);
            _live.Add(p);
            return p;
        }

        private static CombatFxParticle Create()
        {
            GameObject go = new GameObject("genefx");
            go.transform.SetParent(_root, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            return new CombatFxParticle { Go = go, Tr = go.transform, R = r };
        }

        // ------------------------------------------------------------- 更新 --

        /// <summary>
        /// 每帧驱动：dt 为模拟时间步（击停/冻结时为 0），cineDt 为电影实时步（冻结时继续）。
        /// </summary>
        public static void Update(float dt, float cineDt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                CombatFxParticle p = _live[i];
                float step = p.Cine ? cineDt : dt;
                if (p.Delay > 0f)
                {
                    p.Delay -= step;
                    if (p.Delay > 0f) continue;
                }
                p.Age += step;
                if (p.Life <= 0f || p.Age >= p.Life)
                {
                    Recycle(p, i);
                    continue;
                }
                // 物理积分
                if (step > 0f)
                {
                    p.Pos += p.Vel * step;
                    p.Z += p.VZ * step;
                    p.VZ -= p.Gravity * step;
                    if (p.Drag > 0f)
                    {
                        float d = Mathf.Exp(-p.Drag * step);
                        p.Vel *= d;
                        p.VZ *= d;
                    }
                    if (p.Bounce >= 0f && p.Z <= 0f)
                    {
                        p.Z = 0f;
                        p.VZ = -p.VZ * p.Bounce;
                        if (p.Bounces > 0)
                        {
                            p.Bounces--;
                            if (p.Bounces == 0) p.VZ = 0f;
                        }
                    }
                }
                float k = p.K;
                // 绑定单位跟随（护盾/光环/神形环绕：位置每帧贴合单位，单位死亡即结束）
                if (p.FollowActor != null)
                {
                    if (p.FollowActor.isAlive())
                    {
                        p.Pos = p.FollowActor.current_position + p.FollowOffset;
                    }
                    else
                    {
                        Recycle(p, i);
                        continue;
                    }
                }
                Vector3 pos = new Vector3(p.Pos.x, p.Pos.y + p.Z, 0f);
                if (p.Snap)
                {
                    pos.x = Mathf.Round(pos.x / p.Px) * p.Px;
                    pos.y = Mathf.Round(pos.y / p.Px) * p.Px;
                }
                p.Tr.position = pos;
                // 帧动画
                if (p.Frames != null && p.Frames.Length > 0)
                {
                    Sprite spr;
                    if (p.Frames.Length > 1)
                    {
                        if (p.Fps > 0f)
                        {
                            int f = (int)(p.Age * p.Fps);
                            spr = p.Loop ? p.Frames[f % p.Frames.Length] : p.Frames[Mathf.Min(f, p.Frames.Length - 1)];
                        }
                        else
                        {
                            int f = Mathf.Min((int)(k * p.Frames.Length), p.Frames.Length - 1);
                            spr = p.Frames[f];
                        }
                    }
                    else spr = p.Frames[0];
                    p.R.sprite = spr;
                }
                // 颜色渐变
                if (p.Grad != null && p.Grad.Length > 0)
                {
                    if (p.Grad.Length == 1) p.R.color = p.Grad[0];
                    else
                    {
                        float t = k * (p.Grad.Length - 1);
                        int i0 = Mathf.Min((int)t, p.Grad.Length - 2);
                        int i1 = i0 + 1;
                        p.R.color = Color.Lerp(p.Grad[i0], p.Grad[i1], t - i0);
                    }
                }
                // 尺寸（Px 语义=每像素世界单位；精灵 PPU 固定 100 → 缩放 = px*100）
                // v0.18.72：Y 按目标高宽比缩放——默认 YRatio=Aspect（=素材原始比，行为不变）；
                // 护盾等竖素材可单独压扁（96x128 护盾原高=1.33×宽会显大，YRatio=0.75 → 高≈0.32）
                float size = Mathf.Lerp(p.Size0, p.Size1, k);
                float sx = p.Px * 100f * size;
                p.Tr.localScale = new Vector3(sx, sx * (p.YRatio / Mathf.Max(0.0001f, p.Aspect)), 1f);
                // 旋转
                if (p.FaceVel && p.Vel.sqrMagnitude > 0.0001f)
                {
                    p.Rot = Mathf.Atan2(p.Vel.y, p.Vel.x) * Mathf.Rad2Deg - 90f;
                }
                p.Rot += p.Spin * step;
                p.Tr.localRotation = Quaternion.Euler(0f, 0f, p.Rot);
                p.R.enabled = true;
            }
        }

        private static void Recycle(CombatFxParticle p, int index)
        {
            _live.RemoveAt(index);
            p.Go.SetActive(false);
            p.Alive = false;
            p.Frames = null;
            p.Grad = null;
            p.FollowActor = null;
            _free.Push(p);
        }

        /// <summary>世界销毁/存档切换时清空全部演出。</summary>
        public static void Clear()
        {
            for (int i = 0; i < _live.Count; i++) _live[i].Go.SetActive(false);
            _live.Clear();
            _free.Clear();
        }
    }
}
