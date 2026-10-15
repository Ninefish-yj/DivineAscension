// ============================================================
// 异能数据 · 星辰系 / 强化系（v0.18.11 重构；原机械系拆分归位）
// 大类：星辰系（星辰/宇宙/陨石/银河）| 强化系（能量/体魄）——机械系已按大众认知拆分归位
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
            new PrototypeDef("ce_star_01", "星光爆裂", "攻击暴击破甲，念力贯穿", "光明系", "光刃",
                PrototypeTag.Crit, PrototypeTag.Telekinesis, 4),
            new PrototypeDef("ce_star_02", "圣辉守护", "受击免伤，受击回血", "光明系", "晨曦",
                PrototypeTag.Dodge, PrototypeTag.Blessing, 4),
            new PrototypeDef("ce_star_03", "星力狂暴", "攻击狂暴当次+25%，溅射波及", "强化系", "能量",
                PrototypeTag.Berserk, PrototypeTag.Chain, 4),
            // ---- 宇宙 ----
            new PrototypeDef("ce_cosmos_01", "心灵之眼", "攻击念力贯穿高伤，恐惧眩晕", "精神系", "心灵",
                PrototypeTag.Telekinesis, PrototypeTag.Fear, 4),
            new PrototypeDef("ce_cosmos_02", "引力掌控", "攻击重力压制目标，减速时缓", "时空系", "引力",
                PrototypeTag.Gravity, PrototypeTag.TimeSlow, 4),
            new PrototypeDef("ce_cosmos_03", "次元护壁", "护盾减伤，受击免伤最高", "时空系", "空间",
                PrototypeTag.Shield, PrototypeTag.Invisibility, 4),
            // ---- 陨石 ----
            new PrototypeDef("ce_meteor_01", "熔岩陨落", "攻击溅射波及，附加灼烧", "元素系", "火",
                PrototypeTag.Chain, PrototypeTag.Burn, 6),
            new PrototypeDef("ce_meteor_02", "重力压制", "攻击重力压制，概率眩晕", "时空系", "引力",
                PrototypeTag.Gravity, PrototypeTag.Stun, 6),
            new PrototypeDef("ce_meteor_03", "磁力钢躯", "护盾减伤，受击反击麻痹攻击者", "元素系", "金属",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 6),
            // ---- 银河 ----
            new PrototypeDef("ce_galaxy_01", "连锁闪电", "攻击暴击破甲，溅射波及", "元素系", "雷",
                PrototypeTag.Crit, PrototypeTag.Chain, 4),
            new PrototypeDef("ce_galaxy_02", "星光治愈", "攻击概率回复生命，受击自愈", "生命系", "治愈",
                PrototypeTag.Heal, PrototypeTag.Regeneration, 4),
            new PrototypeDef("ce_galaxy_03", "幻影迷离", "闪避强化，受击反击震慑攻击者", "精神系", "幻象",
                PrototypeTag.Illusion, PrototypeTag.Sound, 4),
        };
    }

    /// <summary>机械系重构（v0.18.11）：不符合大众认知，拆分归位——
    /// 念力操控→精神系·念力；金属操控→元素系·金属；强化/再生→强化系。</summary>
    public static class PrototypeData_Machine
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 念力（原机械：本质是念力操控，归精神系）----
            new PrototypeDef("ma_machine_01", "念力冲击", "攻击念力贯穿高伤，护盾减伤", "精神系", "念力",
                PrototypeTag.Telekinesis, PrototypeTag.Shield, 0),
            new PrototypeDef("ma_machine_02", "念力洪流", "攻击溅射波及，暴击破甲", "精神系", "念力",
                PrototypeTag.Chain, PrototypeTag.Crit, 0),
            new PrototypeDef("ma_machine_03", "念力护盾", "护盾减伤强化，受击反击眩晕攻击者", "精神系", "念力",
                PrototypeTag.Shield, PrototypeTag.Vibration, 0),
            // ---- 金属（金属操控，归元素系，与既有金属小类合并）----
            new PrototypeDef("ma_metal_01", "钢铁意志", "护盾减伤，攻击暴击", "元素系", "金属",
                PrototypeTag.Shield, PrototypeTag.Crit, 0),
            new PrototypeDef("ma_metal_02", "金属风暴", "攻击溅射波及，念力贯穿", "元素系", "金属",
                PrototypeTag.Chain, PrototypeTag.Telekinesis, 0),
            new PrototypeDef("ma_metal_03", "钛金之躯", "护盾减伤强化，受击反击麻痹攻击者", "元素系", "金属",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 0),
            // ---- 能量（原科技：身体能量强化，归强化系）----
            new PrototypeDef("ma_tech_01", "战意激增", "攻击狂暴当次+25%，护盾减伤", "强化系", "能量",
                PrototypeTag.Berserk, PrototypeTag.Shield, 4),
            new PrototypeDef("ma_tech_02", "能量爆发", "攻击附加等离子灼烧，溅射波及", "强化系", "能量",
                PrototypeTag.Plasma, PrototypeTag.Chain, 4),
            new PrototypeDef("ma_tech_03", "再生之躯", "受击自愈回血，受击免伤", "强化系", "能量",
                PrototypeTag.Regeneration, PrototypeTag.Dodge, 4),
            // ---- 体魄（原改造：身体强化/再生，归强化系）----
            new PrototypeDef("ma_cyber_01", "嗜血狂化", "攻击吸血强化，暴击破甲", "强化系", "体魄",
                PrototypeTag.Blood, PrototypeTag.Crit, 0),
            new PrototypeDef("ma_cyber_02", "金刚不坏", "护盾减伤，攻击念力贯穿", "强化系", "体魄",
                PrototypeTag.Shield, PrototypeTag.Telekinesis, 0),
            new PrototypeDef("ma_cyber_03", "浴血重生", "受击回血强化，受击反击震慑攻击者", "强化系", "体魄",
                PrototypeTag.Blessing, PrototypeTag.Sound, 0),
        };
    }
}
