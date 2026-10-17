// ============================================================
// 异能级特效库（v0.18.66）
// 全量素材：GameResources/effects/<id>/frame_NN.png（VFX 免费包精筛 100 特效单元）
// 每个异能 = 标签主体特效（按 主+副标签组合 选池内变体，保证 233 异能组合唯一）
//          + 家族氛围特效（低透明背景层，贴合小类意象）
// 播放走 CombatFxCore 粒子（自有渲染器，不依赖原版 EffectsLibrary）；
// 加载失败静默回退（调用方保留原版标签特效兜底）。
// 纯展示层，不写业务数据；出镜率 + 粒子预算双重门禁防刷屏。
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Realm.Prototype;

namespace Code.Combat.Fx
{
    public static class AbilityFx
    {
        /// <summary>特效总开关（默认开；卡顿时可关）</summary>
        public static bool Enabled = true;

        private static readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();

        // 主标签 → 特效池（多个变体；按副标签哈希取用，保证 (主,副) 组合唯一）
        private static readonly Dictionary<PrototypeTag, string[]> _tagPool = new Dictionary<PrototypeTag, string[]>();
        // 家族（大类/小类）→ 氛围特效
        private static readonly Dictionary<string, string> _family = new Dictionary<string, string>();

        static AbilityFx()
        {
            // ---- 攻击标签池 ----
            _tagPool[PrototypeTag.Burn]       = new[] { "vfx_ditheredfire", "vfx_fastfire", "magic_fireball" };
            _tagPool[PrototypeTag.Plasma]     = new[] { "vfx_explosion", "vfx_explosion2", "spx_explosion" };
            _tagPool[PrototypeTag.Frost]      = new[] { "magic_ice_b", "magic_ice_a", "magic_ice_c" };
            _tagPool[PrototypeTag.Frostbite]  = new[] { "magic_ice_c", "magic_ice_b", "magic_ice_a" };
            _tagPool[PrototypeTag.TimeSlow]   = new[] { "bub_mana", "bub_ice" };
            _tagPool[PrototypeTag.Stun]       = new[] { "vfx_impact", "vfx_smallhit", "magic_spark" };
            _tagPool[PrototypeTag.Crit]       = new[] { "vfx_bighit", "vfx_kabooms", "magic_spark4" };
            _tagPool[PrototypeTag.Telekinesis]= new[] { "fx_plasma_trail", "vfx_constellation", "magic_spark4" };
            _tagPool[PrototypeTag.Poison]     = new[] { "bub_toxic", "magic_smoke", "bub_green2" };
            _tagPool[PrototypeTag.Curse]      = new[] { "vfx_tentacles", "magic_darkbolt", "bub_purple" };
            _tagPool[PrototypeTag.Fear]       = new[] { "vfx_vortex", "bub_black" };
            _tagPool[PrototypeTag.Slow]       = new[] { "bub_ice", "bub_water1", "bub_water2" };
            _tagPool[PrototypeTag.Lifesteal]  = new[] { "blood_vfx1", "blood_vfx3", "bub_blood" };
            _tagPool[PrototypeTag.Drain]      = new[] { "blood_vfx2", "soul_yin" };
            _tagPool[PrototypeTag.Blood]      = new[] { "blood_vfx1", "soul_yin" };
            _tagPool[PrototypeTag.Soul]       = new[] { "soul_yin", "soul_yang", "bub_white" };
            _tagPool[PrototypeTag.Heal]       = new[] { "bub_green1", "vfx_puffstars", "bub_gold" };
            _tagPool[PrototypeTag.Chain]      = new[] { "magic_lightning", "vfx_elecshield", "magic_bolt" };
            _tagPool[PrototypeTag.Knockback]  = new[] { "vfx_smallhit", "magic_spark", "magic_dust" };
            _tagPool[PrototypeTag.Gravity]    = new[] { "soul_earth", "vfx_vortex", "magic_dust" };
            _tagPool[PrototypeTag.Berserk]    = new[] { "vfx_magma", "vfx_fastfire" };
            _tagPool[PrototypeTag.Entangle]   = new[] { "soul_wood", "vfx_tentacles" };
            _tagPool[PrototypeTag.Petrify]    = new[] { "soul_earth", "magic_dust" };
            _tagPool[PrototypeTag.Aging]      = new[] { "bub_purple2", "bub_black" };
            _tagPool[PrototypeTag.Weakness]   = new[] { "bub_purple", "bub_purple2" };
            _tagPool[PrototypeTag.ArmorBreak] = new[] { "magic_spark", "magic_spark2", "magic_spark4" };
            _tagPool[PrototypeTag.Execute]    = new[] { "vfx_kabooms", "vfx_bighit", "blood_vfx1" };
            _tagPool[PrototypeTag.Sleep]      = new[] { "bub_white", "bub_mana" };
            _tagPool[PrototypeTag.MultiShot]  = new[] { "magic_spark2", "magic_spark4", "spx_splatter" };
            _tagPool[PrototypeTag.HealAlly]   = new[] { "bub_green2", "bub_gold", "vfx_puffstars" };

            // ---- 防守标签池 ----
            _tagPool[PrototypeTag.Shield]         = new[] { "shield_blue_loop", "shield_green_loop", "shield_bw_loop" };
            _tagPool[PrototypeTag.Dodge]          = new[] { "fx_dodge", "soul_wind", "bub_white" };
            _tagPool[PrototypeTag.Illusion]       = new[] { "soul_wind", "bub_white" };
            _tagPool[PrototypeTag.Invisibility]   = new[] { "bub_white", "bub_rainbow", "bub_mana" };
            _tagPool[PrototypeTag.Teleport]       = new[] { "fx_teleport_singularity", "fx_teleport_blue", "fx_teleport_red" };
            _tagPool[PrototypeTag.Clone]          = new[] { "soul_kong", "vfx_elecshield" };
            _tagPool[PrototypeTag.Blessing]       = new[] { "bub_gold", "bub_white", "vfx_puffstars" };
            _tagPool[PrototypeTag.Regeneration]   = new[] { "bub_aqua", "bub_green1", "vfx_puffstars" };
            _tagPool[PrototypeTag.Magnetism]      = new[] { "vfx_elecshield", "magic_bolt", "magic_spark4" };
            _tagPool[PrototypeTag.Vibration]      = new[] { "vfx_smallhit", "vfx_impact" };
            _tagPool[PrototypeTag.Sound]          = new[] { "fx_explosion_wave", "vfx_powerchords", "bub_white" };
            _tagPool[PrototypeTag.Thorns]         = new[] { "magic_spark", "spx_splatter", "vfx_impact" };
            _tagPool[PrototypeTag.ElementResist]  = new[] { "shield_green_loop", "shield_blue_loop", "shield_bw_loop" };
            _tagPool[PrototypeTag.Cleanse]        = new[] { "bub_white", "bub_gold" };

            // ---- 家族氛围（大类/小类 → 特效）----
            _family["元素系/火"] = "vfx_fastfire";     _family["元素系/水"] = "magic_splash";
            _family["元素系/风"] = "soul_wind";        _family["元素系/雷"] = "magic_lightning";
            _family["元素系/冰"] = "magic_ice_a";      _family["元素系/土"] = "soul_earth";
            _family["元素系/木"] = "soul_wood";        _family["元素系/金属"] = "vfx_impact";
            _family["元素系/光"] = "vfx_eldenring";    _family["元素系/暗"] = "vfx_tentacles";
            _family["光明系/圣光"] = "vfx_powerchords"; _family["光明系/光刃"] = "spx_shock";
            _family["光明系/净化"] = "bub_white";       _family["光明系/晨曦"] = "vfx_eldenring";
            _family["黑暗系/暗影"] = "vfx_vortex";      _family["黑暗系/腐蚀"] = "vfx_magma";
            _family["黑暗系/诅咒"] = "vfx_tentacles";   _family["黑暗系/夜幕"] = "fx_teleport_singularity";
            _family["生命系/生命"] = "soul_yang";       _family["生命系/治愈"] = "vfx_puffstars";
            _family["生命系/再生"] = "bub_aqua";        _family["生命系/生长"] = "soul_wood";
            _family["生命系/祝福"] = "bub_gold";
            _family["死亡系/亡灵"] = "vfx_bloodimpact"; _family["死亡系/凋零"] = "bub_black";
            _family["死亡系/死亡"] = "soul_yin";        _family["死亡系/灵魂"] = "soul_yin";
            _family["死亡系/腐化"] = "vfx_magma";
            _family["精神系/催眠"] = "bub_mana";        _family["精神系/幻象"] = "soul_wind";
            _family["精神系/心灵"] = "vfx_constellation"; _family["精神系/念力"] = "vfx_constellation";
            _family["精神系/意志"] = "vfx_powerchords";  _family["精神系/操控"] = "vfx_constellation";
            _family["精神系/预知"] = "bub_rainbow";
            _family["时空系/时间"] = "bub_mana";        _family["时空系/空间"] = "fx_teleport_singularity";
            _family["时空系/次元"] = "vfx_vortex";      _family["时空系/引力"] = "soul_earth";
            _family["时空系/速度"] = "fx_plasma_trail";
            _family["自然系/动物"] = "soul_kong";       _family["自然系/植物"] = "soul_wood";
            _family["自然系/毒"] = "bub_toxic";         _family["自然系/气象"] = "vfx_elecshield";
            _family["自然系/藤蔓"] = "vfx_tentacles";   _family["自然系/野兽"] = "soul_jin";
            _family["强化系/体魄"] = "vfx_bighit";      _family["强化系/能量"] = "bub_gold";
        }

        // ============================================================
        // 资源加载（带缓存）
        // ============================================================
        public static Sprite[] Load(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return null;
            if (_cache.TryGetValue(effectId, out var cached)) return cached;
            try
            {
                var sprites = Resources.LoadAll<Sprite>("effects/" + effectId);
                if (sprites == null || sprites.Length == 0)
                {
                    _cache[effectId] = new Sprite[0];
                    return null;
                }
                Array.Sort(sprites, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                _cache[effectId] = sprites;
                return sprites;
            }
            catch (Exception dsEx)
            {
                Code.Core.DSDebug.Verbose("[AbilityFx] 加载失败 effects/" + effectId + ": " + dsEx.Message);
                _cache[effectId] = new Sprite[0];
                return null;
            }
        }

        // ============================================================
        // 特效样式（按 id 前缀分组：大/中/小/护盾/气泡）
        // ============================================================
        private static void Style(string id, out float size, out float life)
        {
            // 尺寸基准：特效世界宽 = Px(0.01)×帧宽(128)×size ≈ 1.28×size。
            // 人类单位约 1 世界单位：护盾/光环≈单位、投射≈0.7、爆炸类扩散到≈1.5 倍单位。
            if (id == null) { size = 0.7f; life = 0.6f; return; }
            if (id.StartsWith("shield_")) { size = 0.95f; life = 1.4f; return; }      // 护盾环绕≈单位宽
            if (id.StartsWith("bub_")) { size = 0.6f; life = 0.55f; return; }
            if (id.StartsWith("spx_") || id.StartsWith("magic_spark") || id == "magic_flash"
                || id == "magic_dust" || id == "vfx_impact" || id == "vfx_smallhit"
                || id == "vfx_ditheredfire" || id == "vfx_fastfire" || id == "vfx_bloodimpact"
                || id == "vfx_puffstars" || id == "vfx_elecshield" || id == "vfx_anima")
            {
                size = 0.75f; life = 0.5f; return;
            }
            if (id.StartsWith("vfx_") && (id.Contains("explosion") || id.Contains("kabooms")
                || id.Contains("bighit") || id.Contains("charged") || id.Contains("magma")
                || id.Contains("tentacles") || id.Contains("vortex") || id.Contains("worm")
                || id.Contains("wheel") || id.Contains("eldenring") || id.Contains("constellation")
                || id.Contains("powerchords") || id.Contains("hyperspeed")))
            {
                size = 1.2f; life = 0.75f; return;                                   // 爆发类扩散≈1.5倍单位
            }
            size = 0.85f; life = 0.6f;
        }

        // ============================================================
        // 主标签特效（按副标签哈希从池内取变体 → 组合唯一）
        // ============================================================
        private static string PickTagFx(PrototypeTag main, PrototypeTag sub)
        {
            if (!_tagPool.TryGetValue(main, out var pool) || pool == null || pool.Length == 0) return null;
            if (pool.Length == 1) return pool[0];
            int idx = Math.Abs((int)sub) % pool.Length;
            return pool[idx];
        }

        private static string PickFamilyFx(string category, string sub)
        {
            if (string.IsNullOrEmpty(category)) return null;
            string key = category + "/" + (string.IsNullOrEmpty(sub) ? "通用" : sub);
            _family.TryGetValue(key, out var fx);
            return fx;
        }

        // ============================================================
        // 神形显现（v0.18.66 unit_gifs 登神动画）
        // 帧序列在 GameResources/effects/unit_anim_<name>/ 下，清单在
        // GameResources/effects_unit_anims.txt（GIF 转换脚本生成，运行期只读清单）
        // ============================================================
        private static string[] _unitAnimNames;

        public static Sprite[] PickUnitAnim()
        {
            if (_unitAnimNames == null)
            {
                _unitAnimNames = new string[0];
                try
                {
                    var ta = Resources.Load<TextAsset>("effects_unit_anims");
                    if (ta != null)
                    {
                        var lines = ta.text.Split('\n');
                        var list = new List<string>();
                        foreach (var ln in lines)
                        {
                            string s = ln.Trim();
                            if (s.Length > 0) list.Add(s);
                        }
                        _unitAnimNames = list.ToArray();
                    }
                }
                catch (Exception) { }
            }
            if (_unitAnimNames.Length == 0) return null;
            string pick = _unitAnimNames[UnityEngine.Random.Range(0, _unitAnimNames.Length)];
            return Load("unit_anim_" + pick);
        }

        // ============================================================
        // 播放（异能级：投射物飞行/原地爆发 + 家族氛围 + 命中冲击环）
        // attack=true：self=攻击者，other=目标——投射类特效从攻击者飞向目标并命中爆发；
        //             非投射类直接在目标位置爆发 + 扩张冲击环（战斗之神 SpellBurst 思路）。
        // attack=false：self=防守者，特效绑定单位跟随（护盾/再生/祝福环绕演出）。
        // 全部尺寸按目标单位体型缩放（actor_size/13，Human=1.0）。
        // ============================================================
        public static void PlayAbilityFx(Actor self, Actor other, PrototypeDef def, bool attack, bool force = false)
        {
            if (!Enabled || def == null || self == null) return;
            if (!attack && !self.isAlive()) return;
            Actor anchor = attack ? (other != null && other.isAlive() ? other : self) : self;
            Vector2 pos = anchor.current_position;
            if (World.world == null || World.world.GetTileSimple((int)pos.x, (int)pos.y) == null) return;
            if (!force && UnityEngine.Random.value > 0.5f) return;   // 50% 出镜率

            float scale = UnitScale(anchor);
            string mainFx = PickTagFx(def.MainTag, def.SubTag);

            if (attack)
            {
                // ---- 攻击：投射飞行 or 目标爆发 ----
                if (!string.IsNullOrEmpty(mainFx) && IsProjectile(mainFx))
                {
                    SpawnProjectile(mainFx, self.current_position, pos, scale);
                    // 命中爆（延迟一段，投射物到点后炸开）
                    SpawnFx(mainFx, pos, FxLayer.Top, 36, true, scale, delay: 0.13f, follow: null);
                }
                else if (!string.IsNullOrEmpty(mainFx))
                {
                    SpawnFx(mainFx, pos, FxLayer.Top, 35, true, scale, 0f, null);
                }
                // 命中冲击环（扩张淡出，贴合目标体型）
                var ring = CombatFxCore.Spawn(CombatFxArt.Rings[0], pos, 0.09f, 0.35f, FxLayer.Top, 30);
                if (ring != null)
                {
                    ring.Color(new Color(1f, 0.95f, 0.75f, 0.85f), new Color(1f, 0.85f, 0.55f, 0f))
                        .Sized(0.35f * scale, 1.5f * scale);
                }
            }
            else
            {
                // ---- 防守：特效绑定单位跟随（护盾/再生/祝福/净化环绕）----
                if (!string.IsNullOrEmpty(mainFx))
                {
                    SpawnFx(mainFx, pos, FxLayer.Over, 25, true, scale, 0f, self);
                }
            }

            // 家族氛围层（与主特效不同才叠；低透明背景）
            // 攻击时不叠——避免战斗时每下都有背景光效堆叠成"一片条纹"；仅防守/强效演出叠
            string famFx = PickFamilyFx(def.Category, def.SubCategory);
            if (!string.IsNullOrEmpty(famFx) && famFx != mainFx && (!attack || force))
            {
                SpawnFx(famFx, pos, FxLayer.Behind, 5, false, scale * 0.8f, 0f, attack ? null : self);
            }
        }

        /// <summary>单位体型缩放系数（ActorSize 0-17 / 13，Human=1.0，下限 0.3 防虫级过小）</summary>
        private static float UnitScale(Actor a)
        {
            if (a == null || a.asset == null) return 1f;
            return Mathf.Max(0.3f, (float)a.asset.actor_size / 13f);
        }

        /// <summary>投射类特效（球/弹/泡/星/飞火）：可做飞行弹道。
        /// 排除窄长条素材（ray/lightning 等）——竖长条旋转 90° 横飞会变成"横着的光条"，观感违和。</summary>
        private static bool IsProjectile(string id)
        {
            if (id == null) return false;
            if (id.Contains("ray") || id.Contains("lightning") || id.Contains("flash")
                || id.Contains("dust") || id.Contains("splash") || id.Contains("smoke")
                || id.Contains("puff") || id.Contains("spark"))
            {
                return false;
            }
            return id.StartsWith("magic_") || id.StartsWith("spx_") || id.StartsWith("bub_")
                || id == "vfx_fastfire" || id == "vfx_ditheredfire";
        }

        /// <summary>投射物：自 from 飞向 to（FaceVel 朝向 + 自旋），飞行 0.26s 恰好到点</summary>
        private static void SpawnProjectile(string id, Vector2 from, Vector2 to, float scale)
        {
            var frames = Load(id);
            if (frames == null || frames.Length == 0) return;
            Vector2 dir = to - from;
            float dist = Mathf.Max(0.5f, dir.magnitude);
            dir /= dist;
            float fly = 0.26f;
            var p = CombatFxCore.Spawn(frames, from, 0.012f, fly, FxLayer.Top, 34);
            if (p == null) return;
            p.Sized(0.7f * scale, 0.45f * scale)
             .Color(new Color(1f, 1f, 1f, 0.95f), new Color(1f, 1f, 1f, 0f))
             .Moving(dir * (dist / fly), 0f, 0.5f, 0f);
            p.FaceVel = true;
            p.Spin = UnityEngine.Random.Range(4f, 8f);
        }

        /// <summary>
        /// 单层特效播放（px 0.01 基准；尺寸=样式组×单位缩放；alpha 渐隐；可延迟、可绑定单位）。
        /// fx_* 前缀走原版 EffectsLibrary（原版内置图，自动回池）。
        /// </summary>
        private static void SpawnFx(string id, Vector2 pos, FxLayer layer, int order, bool strong,
            float scale = 1f, float delay = 0f, Actor follow = null)
        {
            // 原版特效（fx_* 前缀）：走 EffectsLibrary（原版内置图，自动回池）
            if (id.StartsWith("fx_"))
            {
                try
                {
                    WorldTile tile = World.world.GetTileSimple((int)pos.x, (int)pos.y);
                    if (tile != null) EffectsLibrary.spawn(id, tile, null, null, 0f, -1f, -1f, null);
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[AbilityFx] 原版特效 " + id + ": " + dsEx.Message); }
                return;
            }
            var frames = Load(id);
            if (frames == null || frames.Length == 0) return;
            Style(id, out float size, out float life);
            var p = CombatFxCore.Spawn(frames, pos, 0.01f, life, layer, order);
            if (p == null) return;
            p.Delay = delay;
            p.FollowActor = follow;
            p.FollowOffset = Vector2.zero;
            float s = (strong ? size : size * 0.55f) * scale;
            p.Sized(s, s * 0.72f);
            if (strong)
            {
                p.Color(new Color(1f, 1f, 1f, 0.92f), new Color(1f, 1f, 1f, 0f));
            }
            else
            {
                p.Color(new Color(1f, 1f, 1f, 0.34f), new Color(1f, 1f, 1f, 0f));
            }
        }
    }
}
