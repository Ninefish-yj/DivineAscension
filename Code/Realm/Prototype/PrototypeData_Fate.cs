// ============================================================
// 异能数据 · 命运系（v0.18.99 大众认知补齐）
// 大类：命运系（特殊系/命运——操控概率与因果的大众认知体系）
// 小类：命运/因果/气运/灾厄
// 机制 = 主/副标签组合（概率类标签：暴击/斩杀/诅咒/衰老/闪避/回血等）。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Fate
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 命运 ----
            new PrototypeDef("fate_thread", "命运丝线", "攻击暴击，受击概率闪避", "命运系", "命运",
                PrototypeTag.Crit, PrototypeTag.Dodge, 5, 6),
            new PrototypeDef("fate_causality", "因果律", "斩杀重伤，受击概率闪避", "命运系", "命运",
                PrototypeTag.Execute, PrototypeTag.Dodge, 5, 9),
            new PrototypeDef("fate_weave", "命运编织", "攻击暴击，分身免伤", "命运系", "命运",
                PrototypeTag.Crit, PrototypeTag.Clone, 5, 8),
            // ---- 因果 ----
            new PrototypeDef("fate_karma", "业力缠身", "诅咒持续伤害，衰老目标", "命运系", "因果",
                PrototypeTag.Curse, PrototypeTag.Aging, 5, 7),
            new PrototypeDef("fate_reincarnation", "宿命轮回", "灵魂回生，净化祛邪", "命运系", "因果",
                PrototypeTag.Soul, PrototypeTag.Cleanse, 5, 8),
            new PrototypeDef("fate_doom", "命数已尽", "斩杀重伤，衰老目标", "命运系", "因果",
                PrototypeTag.Execute, PrototypeTag.Aging, 5, 9),
            // ---- 气运 ----
            new PrototypeDef("fate_fortune", "气运加身", "攻击回血，治疗友军", "命运系", "气运",
                PrototypeTag.Heal, PrototypeTag.HealAlly, 5, 5),
            new PrototypeDef("fate_opportunity", "时来运转", "攻击暴击，祝福庇护", "命运系", "气运",
                PrototypeTag.Crit, PrototypeTag.Blessing, 5, 6),
            // ---- 灾厄 ----
            new PrototypeDef("fate_misfortune", "厄运缠身", "虚弱目标，衰老目标", "命运系", "灾厄",
                PrototypeTag.Weakness, PrototypeTag.Aging, 5, 7),
        };
    }
}
