// ============================================================
// 异能数据 · 血族系 / 神话系（v0.18.0）
// 大类：血族系（血/暗夜/獠牙/转化）| 神话系（龙/神/恶魔/天使/凤凰/麒麟）
// 描述 = 真实机制（由主/副标签组合表达），不含虚假效果。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Blood
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 血 ----
            new PrototypeDef("bl_blood_01", "血噬", "攻击吸血强化，护盾减伤", "黑暗系", "暗影",
                PrototypeTag.Blood, PrototypeTag.Shield, 5),
            new PrototypeDef("bl_blood_02", "嗜血", "攻击吸血，暴击破甲", "黑暗系", "暗影",
                PrototypeTag.Lifesteal, PrototypeTag.Crit, 5),
            new PrototypeDef("bl_blood_03", "血生", "受击自愈回血，受击反击震慑攻击者", "黑暗系", "暗影",
                PrototypeTag.Regeneration, PrototypeTag.Sound, 5),
            // ---- 暗夜 ----
            new PrototypeDef("bl_night_01", "影舞", "闪避强化，攻击吸血", "黑暗系", "暗影",
                PrototypeTag.Illusion, PrototypeTag.Lifesteal, 5),
            new PrototypeDef("bl_night_02", "永夜", "受击免伤最高，攻击暴击", "黑暗系", "夜幕",
                PrototypeTag.Invisibility, PrototypeTag.Crit, 5),
            new PrototypeDef("bl_night_03", "惊魂", "攻击恐惧眩晕，念力贯穿", "黑暗系", "夜幕",
                PrototypeTag.Fear, PrototypeTag.Telekinesis, 5),
            // ---- 獠牙 ----
            new PrototypeDef("bl_fang_01", "血咒", "攻击吸血强化，附加诅咒", "黑暗系", "诅咒",
                PrototypeTag.Blood, PrototypeTag.Curse, 0),
            new PrototypeDef("bl_fang_02", "本能", "攻击暴击破甲，击退眩晕", "强化系", "体魄",
                PrototypeTag.Crit, PrototypeTag.Knockback, 0),
            new PrototypeDef("bl_fang_03", "狂暴", "攻击狂暴当次+25%，吸血", "强化系", "体魄",
                PrototypeTag.Berserk, PrototypeTag.Lifesteal, 0),
            // ---- 转化 ----
            new PrototypeDef("bl_turn_01", "血祭", "攻击概率回复生命，吸血", "黑暗系", "暗影",
                PrototypeTag.Soul, PrototypeTag.Lifesteal, 5),
            new PrototypeDef("bl_turn_02", "觉醒", "受击回血强化，护盾减伤", "强化系", "体魄",
                PrototypeTag.Blessing, PrototypeTag.Shield, 5),
            new PrototypeDef("bl_turn_03", "血威", "攻击恐惧眩晕目标，溅射波及", "黑暗系", "暗影",
                PrototypeTag.Fear, PrototypeTag.Chain, 5),
        };
    }

    public static class PrototypeData_Myth
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 龙 ----
            new PrototypeDef("my_dragon_01", "狱火", "攻击附加等离子灼烧，溅射波及", "元素系", "火",
                PrototypeTag.Plasma, PrototypeTag.Chain, 6),
            new PrototypeDef("my_dragon_02", "震慑", "攻击恐惧眩晕目标，念力贯穿", "精神系", "意志",
                PrototypeTag.Fear, PrototypeTag.Telekinesis, 6),
            new PrototypeDef("my_dragon_03", "钢躯", "护盾减伤强化，受击反击麻痹攻击者", "强化系", "体魄",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 6),
            // ---- 神 ----
            new PrototypeDef("my_god_01", "天罚", "攻击念力贯穿高伤，暴击破甲", "光明系", "圣光",
                PrototypeTag.Telekinesis, PrototypeTag.Crit, 3),
            new PrototypeDef("my_god_02", "圣佑", "受击免伤最高，受击回血", "光明系", "圣光",
                PrototypeTag.Invisibility, PrototypeTag.Blessing, 3),
            new PrototypeDef("my_god_03", "压迫", "攻击恐惧眩晕，重力压制", "精神系", "意志",
                PrototypeTag.Fear, PrototypeTag.Gravity, 3),
            // ---- 恶魔 ----
            new PrototypeDef("my_demon_01", "地狱火", "攻击附加灼烧持续伤害，吸血", "黑暗系", "腐蚀",
                PrototypeTag.Burn, PrototypeTag.Lifesteal, 5),
            new PrototypeDef("my_demon_02", "血契", "攻击吸血强化，诅咒加深", "黑暗系", "诅咒",
                PrototypeTag.Blood, PrototypeTag.Curse, 5),
            new PrototypeDef("my_demon_03", "深渊", "受击反击震慑攻击者，受击自愈", "黑暗系", "腐蚀",
                PrototypeTag.Sound, PrototypeTag.Regeneration, 5),
            // ---- 天使 ----
            new PrototypeDef("my_angel_01", "光愈", "攻击概率回复生命，护盾减伤", "光明系", "圣光",
                PrototypeTag.Heal, PrototypeTag.Shield, 3),
            new PrototypeDef("my_angel_02", "光闪", "闪避强化，攻击暴击", "光明系", "光刃",
                PrototypeTag.Illusion, PrototypeTag.Crit, 3),
            new PrototypeDef("my_angel_03", "圣临", "攻击溅射波及，治疗强化", "光明系", "净化",
                PrototypeTag.Chain, PrototypeTag.Soul, 3),
            // ---- 凤凰 ----
            new PrototypeDef("my_phoenix_01", "重生", "受击自愈回血，攻击附加灼烧", "生命系", "再生",
                PrototypeTag.Regeneration, PrototypeTag.Burn, 6),
            new PrototypeDef("my_phoenix_02", "焚天", "攻击附加等离子灼烧，溅射波及", "元素系", "火",
                PrototypeTag.Plasma, PrototypeTag.Chain, 6),
            new PrototypeDef("my_phoenix_03", "燃尽", "攻击狂暴当次+25%，灼烧", "元素系", "火",
                PrototypeTag.Berserk, PrototypeTag.Plasma, 6),
            // ---- 麒麟 ----
            new PrototypeDef("my_qilin_01", "祝福", "攻击概率回复生命，受击免伤", "生命系", "祝福",
                PrototypeTag.Heal, PrototypeTag.Dodge, 3),
            new PrototypeDef("my_qilin_02", "千斤坠", "攻击重力压制，溅射波及", "时空系", "引力",
                PrototypeTag.Gravity, PrototypeTag.Chain, 3),
            new PrototypeDef("my_qilin_03", "守护", "护盾减伤，受击反击震慑攻击者", "生命系", "祝福",
                PrototypeTag.Shield, PrototypeTag.Sound, 3),
        };
    }
}
