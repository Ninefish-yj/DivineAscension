// ============================================================
// 异能数据 · 强化系细分（v0.18.99 大众认知补齐）
// 大类：强化系（原有：体魄 9 + 能量 4）
// 新增小类：力量/速度/防御/变形（大众认知强化四大方向）
// 机制 = 主/副标签组合（狂暴/斩杀/破甲/多重/闪避/护盾/反弹/变形等）。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Fortify
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 力量 ----
            new PrototypeDef("fort_giant", "巨力", "狂暴强化当次攻击，破甲穿透", "强化系", "力量",
                PrototypeTag.Berserk, PrototypeTag.ArmorBreak, 0, 2),
            new PrototypeDef("fort_rock_shatter", "碎岩", "斩杀重伤，破甲穿透", "强化系", "力量",
                PrototypeTag.Execute, PrototypeTag.ArmorBreak, 0, 4),
            new PrototypeDef("fort_mountain_break", "破岳", "斩杀重伤，狂暴强化", "强化系", "力量",
                PrototypeTag.Execute, PrototypeTag.Berserk, 0, 6),
            new PrototypeDef("fort_sky_hold", "擎天", "狂暴强化，重力压制", "强化系", "力量",
                PrototypeTag.Berserk, PrototypeTag.Gravity, 0, 7),
            // ---- 速度 ----
            new PrototypeDef("fort_shadow", "疾影", "多重连击，受击闪避", "强化系", "速度",
                PrototypeTag.MultiShot, PrototypeTag.Dodge, 1, 3),
            new PrototypeDef("fort_blink", "瞬步", "瞬移遁走，受击闪避", "强化系", "速度",
                PrototypeTag.Dodge, PrototypeTag.Teleport, 1, 5),
            new PrototypeDef("fort_light_stream", "流光", "多重连击，时缓强减速", "强化系", "速度",
                PrototypeTag.MultiShot, PrototypeTag.TimeSlow, 1, 6),
            new PrototypeDef("fort_quick_step", "疾走", "受击闪避，时缓强减速", "强化系", "速度",
                PrototypeTag.Dodge, PrototypeTag.TimeSlow, 1, 2),
            // ---- 防御 ----
            new PrototypeDef("fort_iron_wall", "铁壁", "护盾减伤，石化反击", "强化系", "防御",
                PrototypeTag.Shield, PrototypeTag.Petrify, 2, 3),
            new PrototypeDef("fort_vajra", "金刚", "护盾减伤，反弹伤害", "强化系", "防御",
                PrototypeTag.Shield, PrototypeTag.Thorns, 2, 5),
            new PrototypeDef("fort_boulder", "磐石", "石化反击，反弹伤害", "强化系", "防御",
                PrototypeTag.Petrify, PrototypeTag.Thorns, 2, 4),
            new PrototypeDef("fort_indestructible", "不坏", "护盾减伤，受击净化", "强化系", "防御",
                PrototypeTag.Shield, PrototypeTag.Cleanse, 2, 7),
            // ---- 变形 ----
            new PrototypeDef("fort_steelize", "钢化", "反弹伤害，元素抗性", "强化系", "变形",
                PrototypeTag.Thorns, PrototypeTag.ElementResist, 2, 5),
            new PrototypeDef("fort_beast_morph", "兽化", "狂暴强化，再生自愈", "强化系", "变形",
                PrototypeTag.Berserk, PrototypeTag.Regeneration, 2, 6),
            new PrototypeDef("fort_liquify", "液化", "瞬移遁形，分身免伤", "强化系", "变形",
                PrototypeTag.Clone, PrototypeTag.Teleport, 2, 6),
            new PrototypeDef("fort_mimic", "拟态", "隐身遁形，幻象迷惑", "强化系", "变形",
                PrototypeTag.Invisibility, PrototypeTag.Illusion, 2, 7),
        };
    }
}
