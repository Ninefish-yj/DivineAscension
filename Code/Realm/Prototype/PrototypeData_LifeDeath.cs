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
            new PrototypeDef("lf_life_01", "生命之息", "攻击概率回复生命，护盾减伤", "生命系", "生命",
                PrototypeTag.Heal, PrototypeTag.Shield, 2),
            new PrototypeDef("lf_life_02", "生命共享", "受击自愈回血，攻击治疗强化", "生命系", "生命",
                PrototypeTag.Regeneration, PrototypeTag.Soul, 2),
            new PrototypeDef("lf_life_03", "生命洪流", "攻击治疗强化，溅射波及", "生命系", "生命",
                PrototypeTag.Soul, PrototypeTag.Chain, 2),
            // ---- 治愈 ----
            new PrototypeDef("lf_heal_01", "圣愈术", "攻击概率回复生命，受击回血", "生命系", "治愈",
                PrototypeTag.Heal, PrototypeTag.Blessing, 2),
            new PrototypeDef("lf_heal_02", "群体治愈", "攻击溅射波及，治疗强化", "生命系", "治愈",
                PrototypeTag.Chain, PrototypeTag.Heal, 2),
            new PrototypeDef("lf_heal_03", "再生之力", "受击自愈回血强化，免伤", "生命系", "治愈",
                PrototypeTag.Regeneration, PrototypeTag.Dodge, 2),
            // ---- 再生 ----
            new PrototypeDef("lf_regrow_01", "断肢再生", "受击自愈回血，护盾减伤", "生命系", "再生",
                PrototypeTag.Regeneration, PrototypeTag.Shield, 2),
            new PrototypeDef("lf_regrow_02", "无限再生", "受击回血强化，受击免伤", "生命系", "再生",
                PrototypeTag.Blessing, PrototypeTag.Illusion, 2),
            new PrototypeDef("lf_regrow_03", "细胞活化", "攻击概率回复生命，狂暴", "生命系", "再生",
                PrototypeTag.Heal, PrototypeTag.Berserk, 2),
            // ---- 生长 ----
            new PrototypeDef("lf_grow_01", "急速生长", "攻击减速缠绕目标，附加中毒", "生命系", "生长",
                PrototypeTag.Slow, PrototypeTag.Poison, 2),
            new PrototypeDef("lf_grow_02", "巨木之躯", "护盾减伤，受击反击眩晕攻击者", "生命系", "生长",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 2),
            new PrototypeDef("lf_grow_03", "藤蔓领域", "攻击眩晕定身，溅射波及", "生命系", "生长",
                PrototypeTag.Stun, PrototypeTag.Chain, 2),
            // ---- 祝福 ----
            new PrototypeDef("lf_bless_01", "圣光祝福", "受击回血，护盾减伤", "生命系", "祝福",
                PrototypeTag.Blessing, PrototypeTag.Shield, 3),
            new PrototypeDef("lf_bless_02", "战斗祝福", "攻击狂暴当次+25%，吸血", "生命系", "祝福",
                PrototypeTag.Berserk, PrototypeTag.Lifesteal, 3),
            new PrototypeDef("lf_bless_03", "祝福光环", "攻击概率回复生命，受击免伤", "生命系", "祝福",
                PrototypeTag.Soul, PrototypeTag.Dodge, 3),
        };
    }

    public static class PrototypeData_Death
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 亡灵 ----
            new PrototypeDef("de_undead_01", "亡灵之触", "攻击附加诅咒持续伤害，吸血", "死亡系", "亡灵",
                PrototypeTag.Curse, PrototypeTag.Lifesteal, 5),
            new PrototypeDef("de_undead_02", "亡者凝视", "攻击恐惧眩晕目标，减速", "死亡系", "亡灵",
                PrototypeTag.Fear, PrototypeTag.Slow, 5),
            new PrototypeDef("de_undead_03", "亡灵大军", "攻击溅射波及，附加持续伤害", "死亡系", "亡灵",
                PrototypeTag.Chain, PrototypeTag.Burn, 5),
            // ---- 死亡 ----
            new PrototypeDef("de_death_01", "死亡宣告", "攻击念力贯穿高伤，诅咒加深", "死亡系", "死亡",
                PrototypeTag.Telekinesis, PrototypeTag.Curse, 5),
            new PrototypeDef("de_death_02", "死亡收割", "攻击暴击破甲，吸血", "死亡系", "死亡",
                PrototypeTag.Crit, PrototypeTag.Lifesteal, 5),
            new PrototypeDef("de_death_03", "死寂领域", "攻击减速时缓目标，概率眩晕", "死亡系", "死亡",
                PrototypeTag.TimeSlow, PrototypeTag.Stun, 5),
            // ---- 腐化 ----
            new PrototypeDef("de_corrupt_01", "腐化之力", "攻击附加中毒持续伤害，溅射", "死亡系", "腐化",
                PrototypeTag.Poison, PrototypeTag.Chain, 5),
            new PrototypeDef("de_corrupt_02", "腐朽之触", "攻击附加灼烧持续伤害，减速", "死亡系", "腐化",
                PrototypeTag.Burn, PrototypeTag.Slow, 5),
            new PrototypeDef("de_corrupt_03", "腐化护体", "受击反击震慑攻击者，受击回血", "死亡系", "腐化",
                PrototypeTag.Sound, PrototypeTag.Regeneration, 5),
            // ---- 灵魂 ----
            new PrototypeDef("de_soul_01", "灵魂汲取", "攻击攫取灵魂回血，吸血", "死亡系", "灵魂",
                PrototypeTag.Soul, PrototypeTag.Lifesteal, 5),
            new PrototypeDef("de_soul_02", "灵魂灼烧", "攻击附加等离子灼烧，念力贯穿", "死亡系", "灵魂",
                PrototypeTag.Plasma, PrototypeTag.Telekinesis, 5),
            new PrototypeDef("de_soul_03", "灵魂守护", "受击免伤最高，受击回血", "死亡系", "灵魂",
                PrototypeTag.Invisibility, PrototypeTag.Blessing, 5),
            // ---- 凋零 ----
            new PrototypeDef("de_wilt_01", "凋零之息", "攻击附加诅咒持续伤害，减速", "死亡系", "凋零",
                PrototypeTag.Curse, PrototypeTag.Slow, 5),
            new PrototypeDef("de_wilt_02", "枯萎领域", "攻击溅射波及，附加持续伤害", "死亡系", "凋零",
                PrototypeTag.Chain, PrototypeTag.Poison, 5),
            new PrototypeDef("de_wilt_03", "万物寂灭", "攻击恐惧眩晕，念力贯穿", "死亡系", "凋零",
                PrototypeTag.Fear, PrototypeTag.Telekinesis, 5),
        };
    }
}
