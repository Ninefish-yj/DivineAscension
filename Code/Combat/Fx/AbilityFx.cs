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
            _tagPool[PrototypeTag.Curse]      = new[] { "magic_darkbolt", "bub_purple", "bub_black" };
            _tagPool[PrototypeTag.Fear]       = new[] { "vfx_vortex", "bub_black" };
            _tagPool[PrototypeTag.Slow]       = new[] { "bub_ice", "bub_water1", "bub_water2" };
            _tagPool[PrototypeTag.Lifesteal]  = new[] { "blood_vfx1", "blood_vfx3", "bub_blood" };
            _tagPool[PrototypeTag.Drain]      = new[] { "blood_vfx2", "blood_vfx3" };
            _tagPool[PrototypeTag.Blood]      = new[] { "blood_vfx1", "blood_vfx3" };
            _tagPool[PrototypeTag.Soul]       = new[] { "art_soulflame", "bub_white", "bub_gold" };
            _tagPool[PrototypeTag.Heal]       = new[] { "bub_green1", "vfx_puffstars", "bub_gold" };
            _tagPool[PrototypeTag.Chain]      = new[] { "magic_lightning", "vfx_elecshield", "magic_bolt" };
            _tagPool[PrototypeTag.Knockback]  = new[] { "vfx_smallhit", "magic_spark", "magic_dust" };
            _tagPool[PrototypeTag.Gravity]    = new[] { "vfx_vortex", "bub_black", "magic_dust" };
            _tagPool[PrototypeTag.Berserk]    = new[] { "vfx_magma", "vfx_fastfire" };
            _tagPool[PrototypeTag.Entangle]   = new[] { "art_woodvine", "vfx_tentacles" };
            _tagPool[PrototypeTag.Petrify]    = new[] { "magic_dust", "magic_smoke" };
            _tagPool[PrototypeTag.Aging]      = new[] { "bub_purple2", "bub_black" };
            _tagPool[PrototypeTag.Weakness]   = new[] { "bub_purple", "bub_purple2" };
            _tagPool[PrototypeTag.ArmorBreak] = new[] { "magic_spark", "magic_spark2", "magic_spark4" };
            _tagPool[PrototypeTag.Execute]    = new[] { "vfx_kabooms", "vfx_bighit", "blood_vfx1" };
            _tagPool[PrototypeTag.Sleep]      = new[] { "bub_white", "bub_mana" };
            _tagPool[PrototypeTag.MultiShot]  = new[] { "magic_spark2", "magic_spark4", "spx_splatter" };
            _tagPool[PrototypeTag.HealAlly]   = new[] { "bub_green2", "bub_gold", "vfx_puffstars" };
            _tagPool[PrototypeTag.Summon]     = new[] { "fx_teleport_singularity", "bub_mana", "magic_darkbolt" };

            // ---- 防守标签池 ----
            _tagPool[PrototypeTag.Shield]         = new[] { "shield_blue_loop", "shield_green_loop", "shield_bw_loop" };
            _tagPool[PrototypeTag.Dodge]          = new[] { "fx_dodge", "art_windarc", "bub_white" };
            _tagPool[PrototypeTag.Illusion]       = new[] { "bub_rainbow", "bub_white" };
            _tagPool[PrototypeTag.Invisibility]   = new[] { "bub_white", "bub_rainbow", "bub_mana" };
            _tagPool[PrototypeTag.Teleport]       = new[] { "fx_teleport_singularity", "fx_teleport_blue", "fx_teleport_red" };
            _tagPool[PrototypeTag.Clone]          = new[] { "vfx_elecshield", "art_windarc" };
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
            _family["元素系/风"] = "art_windarc";      _family["元素系/雷"] = "magic_lightning";
            _family["元素系/冰"] = "magic_ice_a";      _family["元素系/土"] = "magic_dust";
            _family["元素系/木"] = "art_woodvine";     _family["元素系/金属"] = "vfx_impact";
            _family["元素系/光"] = "art_rune";         _family["元素系/暗"] = "vfx_vortex";
            _family["光明系/圣光"] = "art_rune";        _family["光明系/光刃"] = "spx_shock";
            _family["光明系/净化"] = "bub_white";       _family["光明系/晨曦"] = "art_rune";
            _family["黑暗系/暗影"] = "vfx_vortex";      _family["黑暗系/腐蚀"] = "bub_toxic";
            _family["黑暗系/诅咒"] = "bub_purple";      _family["黑暗系/夜幕"] = "fx_teleport_singularity";
            _family["生命系/生命"] = "art_soulflame";   _family["生命系/治愈"] = "vfx_puffstars";
            _family["生命系/再生"] = "bub_aqua";        _family["生命系/生长"] = "art_woodvine";
            _family["生命系/祝福"] = "bub_gold";
            _family["死亡系/亡灵"] = "vfx_bloodimpact"; _family["死亡系/凋零"] = "bub_black";
            _family["死亡系/死亡"] = "art_soulflame";   _family["死亡系/灵魂"] = "art_soulflame";
            _family["死亡系/腐化"] = "vfx_magma";
            _family["精神系/催眠"] = "bub_mana";        _family["精神系/幻象"] = "bub_rainbow";
            _family["精神系/心灵"] = "vfx_constellation"; _family["精神系/念力"] = "vfx_constellation";
            _family["精神系/意志"] = "art_rune";         _family["精神系/操控"] = "vfx_constellation";
            _family["精神系/预知"] = "bub_rainbow";
            _family["时空系/时间"] = "bub_mana";        _family["时空系/空间"] = "fx_teleport_singularity";
            _family["时空系/次元"] = "vfx_vortex";      _family["时空系/引力"] = "vfx_vortex";
            _family["时空系/速度"] = "fx_plasma_trail";
            _family["自然系/动物"] = "vfx_puffstars";   _family["自然系/植物"] = "art_woodvine";
            _family["自然系/毒"] = "bub_toxic";         _family["自然系/气象"] = "vfx_elecshield";
            _family["自然系/藤蔓"] = "art_woodvine";    _family["自然系/野兽"] = "spx_shock";
            _family["强化系/体魄"] = "vfx_bighit";      _family["强化系/能量"] = "bub_gold";
            // ---- v0.18.99 新体系家族氛围 ----
            _family["兽化系/猛兽"] = "vfx_bighit";      _family["兽化系/飞禽"] = "art_windarc";
            _family["兽化系/爬行"] = "art_woodvine";    _family["兽化系/虫豸"] = "spx_shock";
            _family["兽化系/神兽"] = "vfx_powerchords";
            _family["命运系/命运"] = "vfx_constellation"; _family["命运系/因果"] = "vfx_constellation";
            _family["命运系/气运"] = "bub_gold";         _family["命运系/灾厄"] = "vfx_vortex";
            _family["召唤系/兽灵"] = "vfx_puffstars";    _family["召唤系/元素"] = "magic_lightning";
            _family["召唤系/亡灵"] = "art_soulflame";    _family["召唤系/幻影"] = "bub_rainbow";
            _family["召唤系/虫潮"] = "spx_shock";        _family["召唤系/巨兽"] = "vfx_bighit";
            _family["召唤系/圣灵"] = "bub_gold";         _family["召唤系/龙裔"] = "vfx_powerchords";
            _family["召唤系/百鬼"] = "vfx_vortex";
            _family["强化系/力量"] = "vfx_bighit";       _family["强化系/速度"] = "fx_plasma_trail";
            _family["强化系/防御"] = "shield_blue_loop"; _family["强化系/变形"] = "art_windarc";
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
                // art_ 前缀：程序化图元（v0.18.74 中式重生成，零素材文件依赖）
                if (effectId.StartsWith("art_"))
                {
                    CombatFxArt.EnsureInit();
                    var art = CombatFxArt.Get(effectId);
                    if (art == null)
                    {
                        _cache[effectId] = new Sprite[0];
                        return null;
                    }
                    _cache[effectId] = new[] { art };
                    return _cache[effectId];
                }
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
            // 尺寸基准：px=1/帧宽 → 世界宽恒等于 size（size 即期望世界宽）。
            // 人类 actor_scale≈0.25 → 单位视觉宽≈0.22~0.28 世界单位（v0.18.91 反编译复核；
            // 西幻奥术护盾 0.24 世界单位=罩全身的标准）：
            // 护盾≈1.3 倍单位宽罩全身、投射≈单位宽、爆炸类扩散到≈2 倍单位宽，不遮地图人物。
            if (id == null) { size = 0.4f; life = 0.6f; return; }
            if (id.StartsWith("art_"))
            {
                // v0.18.74 程序化中式图元：魂焰/风痕/木藤≈单位宽，符环≈领域环
                if (id == "art_rune") { size = 0.5f; life = 0.7f; return; }
                size = 0.42f; life = 0.55f; return;
            }
            if (id.StartsWith("shield_")) { size = 0.28f; life = 2.0f; return; }      // v0.18.75 对齐西幻奥术护盾：半单位宽（0.28/0.55≈51%）只罩躯干
            if (id.StartsWith("bub_")) { size = 0.35f; life = 0.55f; return; }
            if (id.StartsWith("spx_") || id.StartsWith("magic_spark") || id == "magic_flash"
                || id == "magic_dust" || id == "vfx_impact" || id == "vfx_smallhit"
                || id == "vfx_ditheredfire" || id == "vfx_fastfire" || id == "vfx_bloodimpact"
                || id == "vfx_puffstars" || id == "vfx_elecshield" || id == "vfx_anima")
            {
                size = 0.38f; life = 0.5f; return;
            }
            if (id.StartsWith("vfx_") && (id.Contains("explosion") || id.Contains("kabooms")
                || id.Contains("bighit") || id.Contains("charged") || id.Contains("magma")
                || id.Contains("tentacles") || id.Contains("vortex") || id.Contains("worm")
                || id.Contains("wheel") || id.Contains("eldenring") || id.Contains("constellation")
                || id.Contains("powerchords") || id.Contains("hyperspeed")))
            {
                size = 0.5f; life = 0.75f; return;                                    // 命中爆发类≈单位宽，不遮地图人物
            }
            size = 0.4f; life = 0.6f;
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
        // v0.18.92 异能专属视觉：44 标签基准色 × 异能 id 稳定哈希派生
        // 每个异能都有稳定可辨识的专属色调（同标签异能互不撞色），
        // 且不破坏大众认知（火系仍是暖色、冰系仍是冷色、毒仍是绿）。
        // 同一异能每次播放色调一致（哈希稳定，非随机）。
        // ============================================================

        private static Color TagBaseColor(PrototypeTag tag)
        {
            switch (tag)
            {
                case PrototypeTag.Burn:          return new Color(1f, 0.42f, 0.18f);  // 橙红
                case PrototypeTag.Plasma:        return new Color(1f, 0.28f, 0.15f);  // 炽红
                case PrototypeTag.Frost:         return new Color(0.55f, 0.82f, 1f);   // 淡蓝
                case PrototypeTag.Frostbite:     return new Color(0.62f, 0.9f, 1f);    // 冰蓝白
                case PrototypeTag.TimeSlow:      return new Color(0.45f, 0.6f, 0.95f); // 蓝紫
                case PrototypeTag.Stun:          return new Color(1f, 0.82f, 0.2f);   // 金黄
                case PrototypeTag.Crit:          return new Color(1f, 0.65f, 0.15f);  // 橙
                case PrototypeTag.Telekinesis:   return new Color(0.68f, 0.5f, 0.95f); // 紫
                case PrototypeTag.Poison:        return new Color(0.45f, 0.85f, 0.35f);// 绿
                case PrototypeTag.Curse:         return new Color(0.5f, 0.3f, 0.65f);  // 暗紫
                case PrototypeTag.Fear:          return new Color(0.4f, 0.25f, 0.55f); // 暗紫
                case PrototypeTag.Slow:          return new Color(0.4f, 0.7f, 0.9f);   // 水蓝
                case PrototypeTag.Lifesteal:     return new Color(0.85f, 0.2f, 0.22f);// 血红
                case PrototypeTag.Drain:         return new Color(0.6f, 0.15f, 0.25f);// 暗红
                case PrototypeTag.Blood:         return new Color(0.75f, 0.12f, 0.15f);// 血红
                case PrototypeTag.Soul:          return new Color(0.45f, 0.75f, 0.95f);// 幽蓝
                case PrototypeTag.Heal:          return new Color(0.4f, 0.85f, 0.5f); // 翠绿
                case PrototypeTag.Chain:         return new Color(0.45f, 0.72f, 1f);  // 电蓝
                case PrototypeTag.Knockback:     return new Color(1f, 0.7f, 0.3f);    // 橙
                case PrototypeTag.Gravity:       return new Color(0.42f, 0.3f, 0.55f);// 暗紫
                case PrototypeTag.Berserk:       return new Color(0.9f, 0.25f, 0.12f);// 血红
                case PrototypeTag.Entangle:      return new Color(0.4f, 0.75f, 0.3f); // 藤绿
                case PrototypeTag.Petrify:       return new Color(0.75f, 0.72f, 0.68f);// 灰
                case PrototypeTag.Aging:         return new Color(0.62f, 0.55f, 0.5f);// 灰褐
                case PrototypeTag.Weakness:      return new Color(0.55f, 0.5f, 0.3f); // 暗黄
                case PrototypeTag.ArmorBreak:    return new Color(1f, 0.72f, 0.2f);   // 橙黄
                case PrototypeTag.Execute:       return new Color(0.85f, 0.1f, 0.08f);// 深红
                case PrototypeTag.Sleep:         return new Color(0.6f, 0.75f, 0.9f); // 淡蓝白
                case PrototypeTag.MultiShot:     return new Color(1f, 0.85f, 0.35f);  // 金黄
                case PrototypeTag.HealAlly:      return new Color(0.45f, 0.85f, 0.55f);// 翠绿
                case PrototypeTag.Shield:        return new Color(0.45f, 0.65f, 1f);  // 蓝
                case PrototypeTag.Dodge:         return new Color(0.4f, 0.8f, 0.85f); // 青
                case PrototypeTag.Blessing:      return new Color(1f, 0.85f, 0.4f);   // 金
                case PrototypeTag.Magnetism:     return new Color(0.4f, 0.6f, 0.95f); // 电蓝
                case PrototypeTag.Vibration:     return new Color(1f, 0.6f, 0.25f);   // 橙
                case PrototypeTag.Sound:         return new Color(0.4f, 0.8f, 0.7f);  // 青绿
                case PrototypeTag.Illusion:      return new Color(0.85f, 0.75f, 0.95f);// 浅紫
                case PrototypeTag.Invisibility:  return new Color(0.8f, 0.85f, 0.95f);// 淡蓝白
                case PrototypeTag.Regeneration:  return new Color(0.45f, 0.9f, 0.6f); // 翠绿
                case PrototypeTag.Thorns:        return new Color(0.5f, 0.8f, 0.35f); // 绿
                case PrototypeTag.Clone:         return new Color(0.55f, 0.8f, 0.85f);// 青
                case PrototypeTag.ElementResist: return new Color(0.5f, 0.7f, 1f);    // 蓝
                case PrototypeTag.Cleanse:       return new Color(0.75f, 0.9f, 0.95f);// 淡青白
                case PrototypeTag.Teleport:      return new Color(0.6f, 0.45f, 0.9f); // 紫
                case PrototypeTag.Summon:        return new Color(0.55f, 0.4f, 0.85f);// 召唤紫（v0.18.99）
                default:                         return new Color(1f, 1f, 1f);
            }
        }

        /// <summary>异能专属派生色：基准标签色 + 异能 id 稳定哈希（色相 ±12°、明度 ±0.08）。
        /// 233 个异能各自稳定可辨识；同标签下不同异能互不撞色；不破坏标签大众认知。</summary>
        private static Color DeriveTint(PrototypeDef def)
        {
            Color c = TagBaseColor(def.MainTag);
            if (def == null || string.IsNullOrEmpty(def.Id)) return c;
            unchecked
            {
                int h = 17;
                foreach (char ch in def.Id) h = h * 31 + ch;
                if (h < 0) h = -h;
                float hueShift = ((h % 240) - 120) / 10f;      // -12° ~ +12°
                float bright = 1f + (((h / 240) % 160) - 80) / 1000f; // ±0.08
                Color.RGBToHSV(c, out float H, out float S, out float V);
                H = (H + hueShift / 360f) % 1f;
                if (H < 0f) H += 1f;
                V = Mathf.Clamp01(V * bright);
                return Color.HSVToRGB(H, S, V);
            }
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
            Color tint = DeriveTint(def);   // v0.18.92 异能专属派生色（基准标签色×异能id哈希）

            if (attack)
            {
                // ---- 攻击：投射飞行 or 目标爆发 ----
                if (!string.IsNullOrEmpty(mainFx) && IsProjectile(mainFx))
                {
                    SpawnProjectile(mainFx, self.current_position, pos, scale, tint);
                    // 命中爆（延迟一段，投射物到点后炸开）
                    SpawnFx(mainFx, pos, FxLayer.Top, 36, true, scale, delay: 0.13f, follow: null, tint: tint);
                }
                else if (!string.IsNullOrEmpty(mainFx))
                {
                    // v0.18.69：护盾环绕类特效必须绑定单位跟随——护盾是"套在身上环绕"，
                    // 不是命中贴图。攻击路径也跟随被攻击目标（给目标上盾的视觉），
                    // 防守路径（else 大分支）已 follow:self。
                    bool shieldFx = mainFx.StartsWith("shield_");
                    SpawnFx(mainFx, pos, FxLayer.Top, shieldFx ? 25 : 35, true, scale, 0f,
                        shieldFx ? anchor : null, tint: tint);
                }
                // 命中冲击环（扩张淡出，贴合目标体型，细环不遮视野）
                var ring = CombatFxCore.Spawn(CombatFxArt.Rings[0], pos, 0.09f, 0.35f, FxLayer.Top, 30);
                if (ring != null)
                {
                    // v0.18.91：命中冲击环按帧宽归一化（世界宽=px×帧宽×size，px=0.09/帧宽17→1.53×size），
                    // 目标 0.9→0.35 世界单位（≈1.5 倍单位宽扩散），原 1.7 世界单位偏大
                    // v0.18.92：环色跟随异能专属派生色
                    ring.Color(new Color(tint.r, tint.g, tint.b, 0.85f), new Color(tint.r, tint.g, tint.b, 0f))
                        .Sized(0.59f * scale, 0.23f * scale);
                }
            }
            else
            {
                // ---- 防守：特效绑定单位跟随（护盾/再生/祝福/净化环绕）----
                if (!string.IsNullOrEmpty(mainFx))
                {
                    SpawnFx(mainFx, pos, FxLayer.Over, 25, true, scale, 0f, self, tint: tint);
                }
            }

            // 家族氛围层（与主特效不同才叠；低透明背景）
            // 攻击时不叠——避免战斗时每下都有背景光效堆叠成"一片条纹"；仅防守/强效演出叠
            string famFx = PickFamilyFx(def.Category, def.SubCategory);
            if (!string.IsNullOrEmpty(famFx) && famFx != mainFx && (!attack || force))
            {
                SpawnFx(famFx, pos, FxLayer.Behind, 5, false, scale * 0.8f, 0f, attack ? null : self, tint: tint);
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

        /// <summary>投射物：自 from 飞向 to（FaceVel 朝向 + 自旋），飞行 0.26s 恰好到点。
        /// v0.18.69：px=1/帧宽归一化（世界尺寸=Size 参数，不再随素材帧宽漂移）；起点从单位身体中部飞出。
        /// v0.18.92：投射体颜色跟随异能专属派生色。</summary>
        private static void SpawnProjectile(string id, Vector2 from, Vector2 to, float scale, Color? tint = null)
        {
            var frames = Load(id);
            if (frames == null || frames.Length == 0) return;
            Vector2 dir = to - from;
            float dist = Mathf.Max(0.5f, dir.magnitude);
            dir /= dist;
            float fly = 0.26f;
            float frameW = frames[0].rect.width;
            if (frameW <= 0f) frameW = 128f;
            float px = 1f / frameW;
            // v0.18.91：起点从单位身体中心飞出（脚底+0.17×体型），原 0.3×scale 在头顶
            var p = CombatFxCore.Spawn(frames, from + new Vector2(0f, 0.17f * scale), px, fly, FxLayer.Top, 34);
            if (p == null) return;
            Color tc = tint ?? Color.white;
            p.Sized(0.45f * scale, 0.28f * scale)
             .Color(new Color(tc.r, tc.g, tc.b, 0.7f), new Color(tc.r, tc.g, tc.b, 0f))
             .Moving(dir * (dist / fly), 0f, 0.5f, 0f);
            p.FaceVel = true;
            p.Spin = UnityEngine.Random.Range(4f, 8f);
        }

        /// <summary>
        /// 单层特效播放（px 0.01 基准；尺寸=样式组×单位缩放；alpha 渐隐；可延迟、可绑定单位）。
        /// fx_* 前缀走原版 EffectsLibrary（原版内置图，自动回池）。
        /// v0.18.92：tint 为异能专属派生色（null=原白色），应用于特效主体着色。
        /// </summary>
        private static void SpawnFx(string id, Vector2 pos, FxLayer layer, int order, bool strong,
            float scale = 1f, float delay = 0f, Actor follow = null, Color? tint = null)
        {
            // 原版特效（fx_* 前缀）：走 EffectsLibrary（原版内置图，自动回池）。
            // v0.18.67：spawn 后按单位缩放 prepare（原版资产默认 1.0 偏大，压到 0.5×单位缩放）。
            if (id.StartsWith("fx_"))
            {
                try
                {
                    WorldTile tile = World.world.GetTileSimple((int)pos.x, (int)pos.y);
                    if (tile != null)
                    {
                        var fx = EffectsLibrary.spawn(id, tile, null, null, 0f, -1f, -1f, null);
                        if (fx != null)
                        {
                            // v0.18.91：特效中心对准单位身体中心（脚底+0.17×体型），原 0.3×scale 悬在头顶
                            fx.prepare(pos + new Vector2(0f, 0.17f * scale), 1f);
                            // v0.18.91：帧宽归一化——目标世界宽 = 0.25×体型（人类 actor_scale≈0.25，
                            // 1 倍单位宽≈0.25 世界单位，与西幻奥术护盾 0.24 对齐）
                            CombatFxCore.ScaleFxToWorldWidth(fx, 0.25f * scale);
                            // v0.18.92：原版特效同样跟随异能专属派生色
                            if (tint.HasValue && fx.sprite_renderer != null)
                            {
                                Color fc = fx.sprite_renderer.color;
                                fx.sprite_renderer.color = new Color(tint.Value.r, tint.Value.g, tint.Value.b, fc.a);
                            }
                        }
                    }
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[AbilityFx] 原版特效 " + id + ": " + dsEx.Message); }
                return;
            }
            var frames = Load(id);
            if (frames == null || frames.Length == 0) return;
            Style(id, out float size, out float life);
            // v0.18.69：尺寸按帧宽归一化——世界宽 = px×帧宽×s，素材帧宽 55~140px 差异巨大，
            // 固定 px 会导致同 size 的特效世界尺寸漂移近一倍（"好多大小不对"根因）。
            // px = 1/帧宽 → 世界宽恒等于 s（s 即期望世界宽，≈人类单位 0.55）。
            float frameW = frames[0].rect.width;
            if (frameW <= 0f) frameW = 128f;
            float px = 1f / frameW;
            // 特效中心对准单位身体中心（单位 current_position 是脚底；v0.18.91 由 0.3×scale 修正——
            // 0.3 相对单位视觉高≈0.33 已到头顶，大众认知特效应作用在身体上）
            Vector2 center = pos + new Vector2(0f, 0.17f * scale);
            var p = CombatFxCore.Spawn(frames, center, px, life, layer, order);
            if (p == null) return;
            p.Delay = delay;
            p.FollowActor = follow;
            p.FollowOffset = new Vector2(0f, 0.17f * scale);   // 跟随单位时同样对准身体中心，护盾/光环套在身上
            float s = (strong ? size : size * 0.55f) * scale;
            p.Sized(s, s * 0.72f);
            // v0.18.72：竖素材（96x128 护盾）默认高=1.33×宽会显大——护盾压扁到 0.75（高≈0.32，只罩躯干）
            // v0.18.75：对齐西幻奥术护盾 alpha≈0.03 的"淡罩"观感——护盾透明度压到 0.2（淡可见不遮人）
            // v0.18.92：着色跟随异能专属派生色
            Color tc = tint ?? Color.white;
            if (id.StartsWith("shield_"))
            {
                p.YRatio = 0.8f;
                p.Color(new Color(tc.r, tc.g, tc.b, 0.2f), new Color(tc.r, tc.g, tc.b, 0.05f));
                return;
            }
            if (strong)
            {
                // v0.18.69：非半透明命中特效不透明度从 0.92 压到 0.55——
                // 大块不透明会遮住地图和人物，半透明既能看清特效又不挡视野
                p.Color(new Color(tc.r, tc.g, tc.b, 0.55f), new Color(tc.r, tc.g, tc.b, 0f));
            }
            else
            {
                p.Color(new Color(tc.r, tc.g, tc.b, 0.34f), new Color(tc.r, tc.g, tc.b, 0f));
            }
        }
    }
}
