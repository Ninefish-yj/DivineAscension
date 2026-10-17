// ============================================================
// 异能数据 · 精神系（v0.18.0）
// 大类：精神系 | 小类：念力/心灵/意志/预知/催眠/操控/幻象
// 描述 = 真实机制（由主/副标签组合表达），不含虚假效果。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Psychic
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 念力 ----
            new PrototypeDef("ps_psycho_01", "念动力", "攻击念力贯穿高伤，概率眩晕", "精神系", "念力",
                PrototypeTag.Telekinesis, PrototypeTag.Stun, 4, 5),
            new PrototypeDef("ps_psycho_02", "念壁", "护盾减伤，受击反击震慑攻击者", "精神系", "念力",
                PrototypeTag.Shield, PrototypeTag.Sound, 4, 7),
            new PrototypeDef("ps_psycho_03", "力场", "攻击重力压制目标，溅射波及", "精神系", "念力",
                PrototypeTag.Gravity, PrototypeTag.Chain, 4, 9),
            // ---- 心灵 ----
            new PrototypeDef("ps_mind_01", "共感", "攻击概率回复生命，闪避免伤", "精神系", "心灵",
                PrototypeTag.Heal, PrototypeTag.Dodge, 3, 3),
            new PrototypeDef("ps_mind_02", "心爆", "攻击恐惧眩晕目标，附加诅咒", "精神系", "心灵",
                PrototypeTag.Fear, PrototypeTag.Curse, 3, 6),
            new PrototypeDef("ps_mind_03", "心链", "受击自愈回血，攻击治疗", "精神系", "心灵",
                PrototypeTag.Regeneration, PrototypeTag.Heal, 3, 9),
            // ---- 意志 ----
            new PrototypeDef("ps_will_01", "钢铁之心", "护盾减伤强化，受击反击眩晕攻击者", "精神系", "意志",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 3, 4),
            new PrototypeDef("ps_will_02", "战意", "攻击狂暴当次+25%，吸血", "精神系", "意志",
                PrototypeTag.Berserk, PrototypeTag.Lifesteal, 3, 6),
            new PrototypeDef("ps_will_03", "心志", "攻击概率回复生命，受击免伤", "精神系", "意志",
                PrototypeTag.Soul, PrototypeTag.Invisibility, 3, 9),
            // ---- 预知 ----
            new PrototypeDef("ps_foresight_01", "警兆", "闪避强化，受击概率免伤", "精神系", "预知",
                PrototypeTag.Illusion, PrototypeTag.Dodge, 4, 1),
            new PrototypeDef("ps_foresight_02", "未来视", "攻击暴击破甲，念力贯穿", "精神系", "预知",
                PrototypeTag.Crit, PrototypeTag.Telekinesis, 4, 5),
            new PrototypeDef("ps_foresight_03", "先读", "受击反击麻痹攻击者，攻击减速", "精神系", "预知",
                PrototypeTag.Vibration, PrototypeTag.Slow, 4, 9),
            // ---- 催眠 ----
            new PrototypeDef("ps_hypno_01", "催眠术", "攻击眩晕定身目标，恐惧加深", "精神系", "催眠",
                PrototypeTag.Stun, PrototypeTag.Fear, 5, 1),
            new PrototypeDef("ps_hypno_02", "沉睡", "攻击时缓目标，概率沉睡", "精神系", "催眠",
                PrototypeTag.TimeSlow, PrototypeTag.Sleep, 5, 5),
            new PrototypeDef("ps_hypno_03", "梦魇", "攻击附加诅咒持续伤害，眩晕", "精神系", "催眠",
                PrototypeTag.Curse, PrototypeTag.Stun, 5, 9),
            // ---- 操控 ----
            new PrototypeDef("ps_control_01", "精神控制", "攻击恐惧眩晕，减速目标", "精神系", "操控",
                PrototypeTag.Fear, PrototypeTag.Slow, 5, 1),
            new PrototypeDef("ps_control_02", "夺心", "攻击眩晕定身，溅射波及", "精神系", "操控",
                PrototypeTag.Stun, PrototypeTag.Chain, 5, 5),
            new PrototypeDef("ps_control_03", "提线", "攻击击退定身，吸血回复", "精神系", "操控",
                PrototypeTag.Knockback, PrototypeTag.Lifesteal, 5, 9),
            // ---- 幻象 ----
            new PrototypeDef("ps_illusion_01", "幻术", "闪避强化，受击概率免伤", "精神系", "幻象",
                PrototypeTag.Illusion, PrototypeTag.Dodge, 4, 3),
            new PrototypeDef("ps_illusion_02", "影分身", "受击免伤最高，分身回敬伤害", "精神系", "幻象",
                PrototypeTag.Invisibility, PrototypeTag.Clone, 4, 6),
            new PrototypeDef("ps_illusion_03", "幻境", "受击反击震慑攻击者，闪避", "精神系", "幻象",
                PrototypeTag.Sound, PrototypeTag.Illusion, 4, 9),
        };
    }
}
