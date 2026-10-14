// ============================================================
// 异能数据 · 星辰系 / 机械系（v0.18.0）
// 大类：星辰系（星辰/宇宙/陨石/银河）| 机械系（机械/金属/科技/改造）
// 描述 = 真实机制（由主/副标签组合表达），不含虚假效果。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Celestial
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 星辰 ----
            new PrototypeDef("ce_star_01", "星光汇聚", "攻击暴击破甲，念力贯穿", "星辰系", "星辰",
                PrototypeTag.Crit, PrototypeTag.Telekinesis, 4),
            new PrototypeDef("ce_star_02", "星辉庇护", "受击免伤，受击回血", "星辰系", "星辰",
                PrototypeTag.Dodge, PrototypeTag.Blessing, 4),
            new PrototypeDef("ce_star_03", "星辰之力", "攻击狂暴当次+25%，溅射波及", "星辰系", "星辰",
                PrototypeTag.Berserk, PrototypeTag.Chain, 4),
            // ---- 宇宙 ----
            new PrototypeDef("ce_cosmos_01", "宇宙之眼", "攻击念力贯穿高伤，恐惧眩晕", "星辰系", "宇宙",
                PrototypeTag.Telekinesis, PrototypeTag.Fear, 4),
            new PrototypeDef("ce_cosmos_02", "虚空引力", "攻击重力压制目标，减速时缓", "星辰系", "宇宙",
                PrototypeTag.Gravity, PrototypeTag.TimeSlow, 4),
            new PrototypeDef("ce_cosmos_03", "宇宙屏障", "护盾减伤，受击免伤最高", "星辰系", "宇宙",
                PrototypeTag.Shield, PrototypeTag.Invisibility, 4),
            // ---- 陨石 ----
            new PrototypeDef("ce_meteor_01", "陨石坠落", "攻击溅射波及，附加灼烧", "星辰系", "陨石",
                PrototypeTag.Chain, PrototypeTag.Burn, 6),
            new PrototypeDef("ce_meteor_02", "陨星重压", "攻击重力压制，概率眩晕", "星辰系", "陨石",
                PrototypeTag.Gravity, PrototypeTag.Stun, 6),
            new PrototypeDef("ce_meteor_03", "陨铁之躯", "护盾减伤，受击反击麻痹攻击者", "星辰系", "陨石",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 6),
            // ---- 银河 ----
            new PrototypeDef("ce_galaxy_01", "银河之力", "攻击暴击破甲，溅射波及", "星辰系", "银河",
                PrototypeTag.Crit, PrototypeTag.Chain, 4),
            new PrototypeDef("ce_galaxy_02", "星河疗愈", "攻击概率回复生命，受击自愈", "星辰系", "银河",
                PrototypeTag.Heal, PrototypeTag.Regeneration, 4),
            new PrototypeDef("ce_galaxy_03", "星海幻影", "闪避强化，受击反击震慑攻击者", "星辰系", "银河",
                PrototypeTag.Illusion, PrototypeTag.Sound, 4),
        };
    }

    public static class PrototypeData_Machine
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 机械 ----
            new PrototypeDef("ma_machine_01", "机械操控", "攻击念力贯穿高伤，护盾减伤", "机械系", "机械",
                PrototypeTag.Telekinesis, PrototypeTag.Shield, 0),
            new PrototypeDef("ma_machine_02", "机械洪流", "攻击溅射波及，暴击破甲", "机械系", "机械",
                PrototypeTag.Chain, PrototypeTag.Crit, 0),
            new PrototypeDef("ma_machine_03", "机械护体", "护盾减伤强化，受击反击眩晕攻击者", "机械系", "机械",
                PrototypeTag.Shield, PrototypeTag.Vibration, 0),
            // ---- 金属 ----
            new PrototypeDef("ma_metal_01", "钢铁意志", "护盾减伤，攻击暴击", "机械系", "金属",
                PrototypeTag.Shield, PrototypeTag.Crit, 0),
            new PrototypeDef("ma_metal_02", "金属风暴", "攻击溅射波及，念力贯穿", "机械系", "金属",
                PrototypeTag.Chain, PrototypeTag.Telekinesis, 0),
            new PrototypeDef("ma_metal_03", "钛金之躯", "护盾减伤强化，受击反击麻痹攻击者", "机械系", "金属",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 0),
            // ---- 科技 ----
            new PrototypeDef("ma_tech_01", "科技强化", "攻击狂暴当次+25%，护盾减伤", "机械系", "科技",
                PrototypeTag.Berserk, PrototypeTag.Shield, 4),
            new PrototypeDef("ma_tech_02", "能量过载", "攻击附加等离子灼烧，溅射波及", "机械系", "科技",
                PrototypeTag.Plasma, PrototypeTag.Chain, 4),
            new PrototypeDef("ma_tech_03", "纳米修复", "受击自愈回血，受击免伤", "机械系", "科技",
                PrototypeTag.Regeneration, PrototypeTag.Dodge, 4),
            // ---- 改造 ----
            new PrototypeDef("ma_cyber_01", "生化改造", "攻击吸血强化，暴击破甲", "机械系", "改造",
                PrototypeTag.Blood, PrototypeTag.Crit, 0),
            new PrototypeDef("ma_cyber_02", "义体强化", "护盾减伤，攻击念力贯穿", "机械系", "改造",
                PrototypeTag.Shield, PrototypeTag.Telekinesis, 0),
            new PrototypeDef("ma_cyber_03", "改造再生", "受击回血强化，受击反击震慑攻击者", "机械系", "改造",
                PrototypeTag.Blessing, PrototypeTag.Sound, 0),
        };
    }
}
