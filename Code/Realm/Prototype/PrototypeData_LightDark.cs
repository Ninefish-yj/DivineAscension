// ============================================================
// 异能数据 · 光明系 / 黑暗系（v0.18.0）
// 大类：光明系（圣光/光刃/净化/晨曦）| 黑暗系（暗影/腐蚀/诅咒/夜幕）
// 描述 = 真实机制（由主/副标签组合表达），不含虚假效果。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Light
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 圣光 ----
            new PrototypeDef("li_holy_01", "辉光", "攻击概率回复生命，护盾减伤", "光明系", "圣光",
                PrototypeTag.Heal, PrototypeTag.Shield, 3, 6),
            new PrototypeDef("li_holy_02", "裁决", "攻击念力贯穿高伤，溅射波及", "光明系", "圣光",
                PrototypeTag.Telekinesis, PrototypeTag.Chain, 3, 8),
            new PrototypeDef("li_holy_03", "圣盾", "受击回血，受击免伤", "光明系", "圣光",
                PrototypeTag.Blessing, PrototypeTag.Dodge, 3, 9),
            new PrototypeDef("li_holy_04", "神恩", "圣光暴击，概率治愈友军", "光明系", "圣光",
                PrototypeTag.Crit, PrototypeTag.HealAlly, 3, 7),
            // ---- 光刃 ----
            new PrototypeDef("li_blade_01", "光刃", "攻击暴击破甲，念力贯穿", "光明系", "光刃",
                PrototypeTag.Crit, PrototypeTag.Telekinesis, 0, 4),
            new PrototypeDef("li_blade_02", "圣剑", "攻击溅射波及，概率眩晕", "光明系", "光刃",
                PrototypeTag.Chain, PrototypeTag.Stun, 0, 7),
            new PrototypeDef("li_blade_03", "速斩", "攻击狂暴当次+25%，暴击", "光明系", "光刃",
                PrototypeTag.Berserk, PrototypeTag.Crit, 0, 9),
            // ---- 净化 ----
            new PrototypeDef("li_purify_01", "圣焰", "攻击附加灼烧（净化烈焰），溅射", "光明系", "净化",
                PrototypeTag.Burn, PrototypeTag.Chain, 3, 4),
            new PrototypeDef("li_purify_02", "驱散", "攻击附加诅咒（灼烧诅咒），减速", "光明系", "净化",
                PrototypeTag.Curse, PrototypeTag.Slow, 3, 6),
            new PrototypeDef("li_purify_03", "圣泉", "攻击概率回复生命，受击自愈", "光明系", "净化",
                PrototypeTag.Soul, PrototypeTag.Regeneration, 3, 9),
            // ---- 晨曦 ----
            new PrototypeDef("li_dawn_01", "晨曦", "攻击治疗强化，护盾减伤", "光明系", "晨曦",
                PrototypeTag.Soul, PrototypeTag.Shield, 3, 3),
            new PrototypeDef("li_dawn_02", "破晓", "攻击暴击高伤，附加灼烧", "光明系", "晨曦",
                PrototypeTag.Crit, PrototypeTag.Burn, 3, 6),
            new PrototypeDef("li_dawn_03", "黎明", "受击免伤强化，攻击回复生命", "光明系", "晨曦",
                PrototypeTag.Invisibility, PrototypeTag.Heal, 3, 9),
        };
    }

    public static class PrototypeData_Dark
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 暗影 ----
            new PrototypeDef("dk_shadow_01", "潜伏", "闪避强化，攻击暴击", "黑暗系", "暗影",
                PrototypeTag.Illusion, PrototypeTag.Crit, 5, 8),
            new PrototypeDef("dk_shadow_02", "突袭", "攻击吸血强化，念力贯穿", "黑暗系", "暗影",
                PrototypeTag.Blood, PrototypeTag.Telekinesis, 5, 9),
            new PrototypeDef("dk_shadow_03", "影遁", "受击免伤最高，受击反击震慑攻击者", "黑暗系", "暗影",
                PrototypeTag.Invisibility, PrototypeTag.Sound, 5, 9),
            // ---- 腐蚀 ----
            new PrototypeDef("dk_corrode_01", "腐蚀", "攻击附加中毒持续伤害，诅咒加深", "黑暗系", "腐蚀",
                PrototypeTag.Poison, PrototypeTag.Curse, 5, 5),
            new PrototypeDef("dk_corrode_02", "溶解", "攻击附加灼烧持续伤害，溅射波及", "黑暗系", "腐蚀",
                PrototypeTag.Burn, PrototypeTag.Chain, 5, 7),
            new PrototypeDef("dk_corrode_03", "蚀甲", "攻击眩晕定身，破开护甲", "黑暗系", "腐蚀",
                PrototypeTag.Stun, PrototypeTag.ArmorBreak, 5, 9),
            new PrototypeDef("dk_corrode_04", "酸蚀", "中毒持续伤害，虚弱目标", "黑暗系", "腐蚀",
                PrototypeTag.Poison, PrototypeTag.Weakness, 5, 2),
            new PrototypeDef("dk_corrode_05", "腐沼", "中毒持续伤害，减速目标", "黑暗系", "腐蚀",
                PrototypeTag.Poison, PrototypeTag.Slow, 5, 4),
            new PrototypeDef("dk_corrode_06", "蚀骨", "中毒持续伤害，衰老目标", "黑暗系", "腐蚀",
                PrototypeTag.Poison, PrototypeTag.Aging, 5, 6),
            new PrototypeDef("dk_corrode_07", "万蚀", "诅咒持续伤害，低血量斩杀", "黑暗系", "腐蚀",
                PrototypeTag.Curse, PrototypeTag.Execute, 5, 8),
            // ---- 诅咒 ----
            new PrototypeDef("dk_curse_01", "恶咒", "攻击附加诅咒持续伤害，恐惧眩晕", "黑暗系", "诅咒",
                PrototypeTag.Curse, PrototypeTag.Fear, 5, 4),
            new PrototypeDef("dk_curse_02", "死印", "攻击吸血，附加中毒", "黑暗系", "诅咒",
                PrototypeTag.Lifesteal, PrototypeTag.Poison, 5, 6),
            new PrototypeDef("dk_curse_03", "咒缚", "攻击减速时缓目标，概率眩晕", "黑暗系", "诅咒",
                PrototypeTag.TimeSlow, PrototypeTag.Stun, 5, 9),
            new PrototypeDef("dk_curse_04", "怨咒", "诅咒持续伤害，虚弱目标", "黑暗系", "诅咒",
                PrototypeTag.Curse, PrototypeTag.Weakness, 5, 3),
            new PrototypeDef("dk_curse_05", "厄运缠身", "虚弱目标，减速目标", "黑暗系", "诅咒",
                PrototypeTag.Weakness, PrototypeTag.Slow, 5, 5),
            new PrototypeDef("dk_curse_06", "死咒", "诅咒持续伤害，低血量斩杀", "黑暗系", "诅咒",
                PrototypeTag.Curse, PrototypeTag.Execute, 5, 7),
            new PrototypeDef("dk_curse_07", "万咒归一", "诅咒持续伤害，破甲", "黑暗系", "诅咒",
                PrototypeTag.Curse, PrototypeTag.ArmorBreak, 5, 8),
            // ---- 夜幕 ----
            new PrototypeDef("dk_night_01", "夜幕", "攻击恐惧眩晕目标，减速", "黑暗系", "夜幕",
                PrototypeTag.Fear, PrototypeTag.Slow, 5, 5),
            new PrototypeDef("dk_night_02", "夜盾", "受击免伤，受击自愈回血", "黑暗系", "夜幕",
                PrototypeTag.Dodge, PrototypeTag.Regeneration, 5, 7),
            new PrototypeDef("dk_night_03", "夜行者", "闪避强化，攻击吸血", "黑暗系", "夜幕",
                PrototypeTag.Illusion, PrototypeTag.Lifesteal, 5, 9),
        };
    }
}
