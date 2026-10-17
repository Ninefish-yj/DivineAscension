// ============================================================
// 异能数据 · 召唤系（v0.18.99 大众认知补齐）
// 大类：召唤系（大众认知核心体系之一）
// 小类：兽灵/元素/亡灵/幻影/虫潮/巨兽/圣灵/龙裔/百鬼
// 机制 = Summon 标签（攻击概率召唤灵兽助战，短暂作战后消散）
//       + 副标签（吸血/溅射/灵魂/闪避/毒蚀/击退/治疗/斩杀/恐惧）。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Summon
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            new PrototypeDef("sum_beast", "兽灵召唤", "召唤灵兽助战，攻击吸血", "召唤系", "兽灵",
                PrototypeTag.Summon, PrototypeTag.Lifesteal, 4, 4),
            new PrototypeDef("sum_element", "元素召唤", "召唤元素体，攻击溅射", "召唤系", "元素",
                PrototypeTag.Summon, PrototypeTag.Chain, 6, 5),
            new PrototypeDef("sum_undead", "亡灵召唤", "召唤亡灵，攻击攫魂", "召唤系", "亡灵",
                PrototypeTag.Summon, PrototypeTag.Soul, 6, 6),
            new PrototypeDef("sum_phantom", "幻影召唤", "召唤幻影，受击闪避", "召唤系", "幻影",
                PrototypeTag.Summon, PrototypeTag.Dodge, 4, 5),
            new PrototypeDef("sum_swarm", "虫潮召唤", "召唤虫群，毒蚀目标", "召唤系", "虫潮",
                PrototypeTag.Summon, PrototypeTag.Poison, 2, 3),
            new PrototypeDef("sum_giant", "巨兽召唤", "召唤巨兽，震退目标", "召唤系", "巨兽",
                PrototypeTag.Summon, PrototypeTag.Knockback, 2, 6),
            new PrototypeDef("sum_holy", "圣灵召唤", "召唤圣灵，治疗友军", "召唤系", "圣灵",
                PrototypeTag.Summon, PrototypeTag.HealAlly, 5, 7),
            new PrototypeDef("sum_dragon", "龙裔召唤", "召唤龙裔，斩杀重伤", "召唤系", "龙裔",
                PrototypeTag.Summon, PrototypeTag.Execute, 0, 9),
            new PrototypeDef("sum_ghost_night", "百鬼夜行", "召唤百鬼，恐惧震慑", "召唤系", "百鬼",
                PrototypeTag.Summon, PrototypeTag.Fear, 6, 8),
        };
    }
}
