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
            new PrototypeDef("bl_blood_01", "血之掌控", "攻击吸血强化，护盾减伤", "血族系", "血",
                PrototypeTag.Blood, PrototypeTag.Shield, 5),
            new PrototypeDef("bl_blood_02", "血之盛宴", "攻击吸血，暴击破甲", "血族系", "血",
                PrototypeTag.Lifesteal, PrototypeTag.Crit, 5),
            new PrototypeDef("bl_blood_03", "血之护体", "受击自愈回血，受击反击震慑攻击者", "血族系", "血",
                PrototypeTag.Regeneration, PrototypeTag.Sound, 5),
            // ---- 暗夜 ----
            new PrototypeDef("bl_night_01", "暗夜之影", "闪避强化，攻击吸血", "血族系", "暗夜",
                PrototypeTag.Illusion, PrototypeTag.Lifesteal, 5),
            new PrototypeDef("bl_night_02", "夜之庇护", "受击免伤最高，攻击暴击", "血族系", "暗夜",
                PrototypeTag.Invisibility, PrototypeTag.Crit, 5),
            new PrototypeDef("bl_night_03", "暗夜突袭", "攻击恐惧眩晕，念力贯穿", "血族系", "暗夜",
                PrototypeTag.Fear, PrototypeTag.Telekinesis, 5),
            // ---- 獠牙 ----
            new PrototypeDef("bl_fang_01", "獠牙之咬", "攻击吸血强化，附加诅咒", "血族系", "獠牙",
                PrototypeTag.Blood, PrototypeTag.Curse, 0),
            new PrototypeDef("bl_fang_02", "猎杀本能", "攻击暴击破甲，击退眩晕", "血族系", "獠牙",
                PrototypeTag.Crit, PrototypeTag.Knockback, 0),
            new PrototypeDef("bl_fang_03", "血牙之怒", "攻击狂暴当次+25%，吸血", "血族系", "獠牙",
                PrototypeTag.Berserk, PrototypeTag.Lifesteal, 0),
            // ---- 转化 ----
            new PrototypeDef("bl_turn_01", "血裔转化", "攻击概率回复生命，吸血", "血族系", "转化",
                PrototypeTag.Soul, PrototypeTag.Lifesteal, 5),
            new PrototypeDef("bl_turn_02", "血脉觉醒", "受击回血强化，护盾减伤", "血族系", "转化",
                PrototypeTag.Blessing, PrototypeTag.Shield, 5),
            new PrototypeDef("bl_turn_03", "血统威压", "攻击恐惧眩晕目标，溅射波及", "血族系", "转化",
                PrototypeTag.Fear, PrototypeTag.Chain, 5),
        };
    }

    public static class PrototypeData_Myth
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 龙 ----
            new PrototypeDef("my_dragon_01", "龙之吐息", "攻击附加等离子灼烧，溅射波及", "神话系", "龙",
                PrototypeTag.Plasma, PrototypeTag.Chain, 6),
            new PrototypeDef("my_dragon_02", "龙威", "攻击恐惧眩晕目标，念力贯穿", "神话系", "龙",
                PrototypeTag.Fear, PrototypeTag.Telekinesis, 6),
            new PrototypeDef("my_dragon_03", "龙鳞护体", "护盾减伤强化，受击反击麻痹攻击者", "神话系", "龙",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 6),
            // ---- 神 ----
            new PrototypeDef("my_god_01", "神之审判", "攻击念力贯穿高伤，暴击破甲", "神话系", "神",
                PrototypeTag.Telekinesis, PrototypeTag.Crit, 3),
            new PrototypeDef("my_god_02", "神之庇护", "受击免伤最高，受击回血", "神话系", "神",
                PrototypeTag.Invisibility, PrototypeTag.Blessing, 3),
            new PrototypeDef("my_god_03", "神威如狱", "攻击恐惧眩晕，重力压制", "神话系", "神",
                PrototypeTag.Fear, PrototypeTag.Gravity, 3),
            // ---- 恶魔 ----
            new PrototypeDef("my_demon_01", "恶魔之焰", "攻击附加灼烧持续伤害，吸血", "神话系", "恶魔",
                PrototypeTag.Burn, PrototypeTag.Lifesteal, 5),
            new PrototypeDef("my_demon_02", "恶魔契约", "攻击吸血强化，诅咒加深", "神话系", "恶魔",
                PrototypeTag.Blood, PrototypeTag.Curse, 5),
            new PrototypeDef("my_demon_03", "恶魔之躯", "受击反击震慑攻击者，受击自愈", "神话系", "恶魔",
                PrototypeTag.Sound, PrototypeTag.Regeneration, 5),
            // ---- 天使 ----
            new PrototypeDef("my_angel_01", "天使圣光", "攻击概率回复生命，护盾减伤", "神话系", "天使",
                PrototypeTag.Heal, PrototypeTag.Shield, 3),
            new PrototypeDef("my_angel_02", "天使之翼", "闪避强化，攻击暴击", "神话系", "天使",
                PrototypeTag.Illusion, PrototypeTag.Crit, 3),
            new PrototypeDef("my_angel_03", "天使降临", "攻击溅射波及，治疗强化", "神话系", "天使",
                PrototypeTag.Chain, PrototypeTag.Soul, 3),
            // ---- 凤凰 ----
            new PrototypeDef("my_phoenix_01", "凤凰涅槃", "受击自愈回血，攻击附加灼烧", "神话系", "凤凰",
                PrototypeTag.Regeneration, PrototypeTag.Burn, 6),
            new PrototypeDef("my_phoenix_02", "凤凰之炎", "攻击附加等离子灼烧，溅射波及", "神话系", "凤凰",
                PrototypeTag.Plasma, PrototypeTag.Chain, 6),
            new PrototypeDef("my_phoenix_03", "不灭之焰", "攻击狂暴当次+25%，灼烧", "神话系", "凤凰",
                PrototypeTag.Berserk, PrototypeTag.Plasma, 6),
            // ---- 麒麟 ----
            new PrototypeDef("my_qilin_01", "麒麟祥瑞", "攻击概率回复生命，受击免伤", "神话系", "麒麟",
                PrototypeTag.Heal, PrototypeTag.Dodge, 3),
            new PrototypeDef("my_qilin_02", "麒麟踏天", "攻击重力压制，溅射波及", "神话系", "麒麟",
                PrototypeTag.Gravity, PrototypeTag.Chain, 3),
            new PrototypeDef("my_qilin_03", "麒麟守护", "护盾减伤，受击反击震慑攻击者", "神话系", "麒麟",
                PrototypeTag.Shield, PrototypeTag.Sound, 3),
        };
    }
}
