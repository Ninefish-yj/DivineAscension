// ============================================================
// 异能数据 · 生命系 / 死亡系（v0.18.0）
// 大类：生命系（生命/治愈/再生/生长/祝福）| 死亡系（亡灵/死亡/腐化/灵魂/凋零）
// 描述 = 真实机制（由主/副标签组合表达），不含虚假效果。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Life
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 生命 ----
            new PrototypeDef("lf_life_01", "生息", "攻击概率回复生命，护盾减伤", "生命系", "生命",
                PrototypeTag.Heal, PrototypeTag.Shield, 2, 1),
            new PrototypeDef("lf_life_02", "共生", "受击自愈回血，攻击治疗强化", "生命系", "生命",
                PrototypeTag.Regeneration, PrototypeTag.Soul, 2, 5),
            new PrototypeDef("lf_life_03", "命泉", "攻击治疗强化，溅射波及", "生命系", "生命",
                PrototypeTag.Soul, PrototypeTag.Chain, 2, 9),
            // ---- 治愈 ----
            new PrototypeDef("lf_heal_01", "圣愈术", "攻击概率回复生命，受击回血", "生命系", "治愈",
                PrototypeTag.Heal, PrototypeTag.Blessing, 2, 2),
            new PrototypeDef("lf_heal_02", "群疗", "攻击溅射波及，治疗强化", "生命系", "治愈",
                PrototypeTag.Chain, PrototypeTag.HealAlly, 2, 4),
            new PrototypeDef("lf_heal_03", "愈合", "受击自愈回血强化，免伤", "生命系", "治愈",
                PrototypeTag.Regeneration, PrototypeTag.Dodge, 2, 5),
            // ---- 再生 ----
            new PrototypeDef("lf_regrow_01", "复生", "受击自愈回血，护盾减伤", "生命系", "再生",
                PrototypeTag.Regeneration, PrototypeTag.Shield, 2, 3),
            new PrototypeDef("lf_regrow_02", "不死", "受击回血强化，受击免伤", "生命系", "再生",
                PrototypeTag.Blessing, PrototypeTag.Illusion, 2, 6),
            new PrototypeDef("lf_regrow_03", "活化", "攻击概率回复生命，狂暴", "生命系", "再生",
                PrototypeTag.Heal, PrototypeTag.Berserk, 2, 9),
            // ---- 生长 ----
            new PrototypeDef("lf_grow_01", "滋生", "攻击减速缠绕目标，附加中毒", "生命系", "生长",
                PrototypeTag.Slow, PrototypeTag.Poison, 2, 1),
            new PrototypeDef("lf_grow_02", "树人", "护盾减伤，受击反击眩晕攻击者", "生命系", "生长",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 2, 5),
            new PrototypeDef("lf_grow_03", "蔓生", "攻击眩晕定身，溅射波及", "生命系", "生长",
                PrototypeTag.Stun, PrototypeTag.Chain, 2, 9),
            // ---- 祝福 ----
            new PrototypeDef("lf_bless_01", "祷言", "受击回血，护盾减伤", "生命系", "祝福",
                PrototypeTag.Blessing, PrototypeTag.Shield, 3, 4),
            new PrototypeDef("lf_bless_02", "战歌", "攻击狂暴当次+25%，吸血", "生命系", "祝福",
                PrototypeTag.Berserk, PrototypeTag.Lifesteal, 3, 6),
            new PrototypeDef("lf_bless_03", "光环", "攻击概率回复生命，受击免伤", "生命系", "祝福",
                PrototypeTag.Soul, PrototypeTag.Dodge, 3, 9),
        };
    }

    public static class PrototypeData_Death
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 亡灵 ----
            new PrototypeDef("de_undead_01", "亡灵", "攻击附加诅咒持续伤害，吸血", "死亡系", "亡灵",
                PrototypeTag.Curse, PrototypeTag.Lifesteal, 5, 1),
            new PrototypeDef("de_undead_02", "亡视", "攻击恐惧眩晕目标，减速", "死亡系", "亡灵",
                PrototypeTag.Fear, PrototypeTag.Slow, 5, 5),
            new PrototypeDef("de_undead_03", "招魂", "攻击溅射波及，附加持续伤害", "死亡系", "亡灵",
                PrototypeTag.Chain, PrototypeTag.Burn, 5, 9),
            // ---- 死亡 ----
            new PrototypeDef("de_death_01", "死亡宣告", "攻击念力贯穿高伤，诅咒加深", "死亡系", "死亡",
                PrototypeTag.Telekinesis, PrototypeTag.Curse, 5, 1),
            new PrototypeDef("de_death_02", "收割", "攻击暴击破甲，吸血", "死亡系", "死亡",
                PrototypeTag.Crit, PrototypeTag.Execute, 5, 5),
            new PrototypeDef("de_death_03", "死域", "攻击减速时缓目标，概率眩晕", "死亡系", "死亡",
                PrototypeTag.TimeSlow, PrototypeTag.Stun, 5, 9),
            // ---- 腐化 ----
            new PrototypeDef("de_corrupt_01", "腐化", "攻击附加中毒持续伤害，溅射", "死亡系", "腐化",
                PrototypeTag.Poison, PrototypeTag.Chain, 5, 1),
            new PrototypeDef("de_corrupt_02", "衰败", "攻击附加灼烧持续伤害，减速", "死亡系", "腐化",
                PrototypeTag.Burn, PrototypeTag.Aging, 5, 5),
            new PrototypeDef("de_corrupt_03", "腐甲", "受击反击震慑攻击者，受击回血", "死亡系", "腐化",
                PrototypeTag.Sound, PrototypeTag.Regeneration, 5, 9),
            // ---- 灵魂 ----
            new PrototypeDef("de_soul_01", "噬魂", "攻击攫取灵魂回血，吸血", "死亡系", "灵魂",
                PrototypeTag.Soul, PrototypeTag.Lifesteal, 5, 1),
            new PrototypeDef("de_soul_02", "魂火", "攻击附加等离子灼烧，念力贯穿", "死亡系", "灵魂",
                PrototypeTag.Plasma, PrototypeTag.Telekinesis, 5, 5),
            new PrototypeDef("de_soul_03", "镇魂", "受击免伤最高，受击回血", "死亡系", "灵魂",
                PrototypeTag.Invisibility, PrototypeTag.Blessing, 5, 9),
            // ---- 凋零 ----
            new PrototypeDef("de_wilt_01", "凋零", "攻击附加诅咒持续伤害，减速", "死亡系", "凋零",
                PrototypeTag.Curse, PrototypeTag.Slow, 5, 1),
            new PrototypeDef("de_wilt_02", "枯萎", "攻击溅射波及，附加持续伤害", "死亡系", "凋零",
                PrototypeTag.Chain, PrototypeTag.Poison, 5, 5),
            new PrototypeDef("de_wilt_03", "终焉", "攻击恐惧眩晕，念力贯穿", "死亡系", "凋零",
                PrototypeTag.Fear, PrototypeTag.Telekinesis, 5, 9),
        };
    }
}
