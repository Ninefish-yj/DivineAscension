// ============================================================
// 异能标签定义（v0.17.0 新增系统，不影响现有三系统内部）
// 大众认知异能的底层机制 = 有限的"效果标签"组合：
// 几千个异能名的战斗机制由 ~10 个标签组合生成，一次实现、全异能复用。
// 本文件 = 数据层（标签枚举 + 标签静态表），与效果引擎（逻辑）分离。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    /// <summary>
    /// 异能效果标签（首批 10 个，全部真实生效）
    /// 攻击标签：Burn/Slow/Stun/Poison/Chain/Lifesteal/Crit/Heal（攻击命中时触发）
    /// 防守标签：Shield/Dodge（受击时生效）
    /// </summary>
    public enum PrototypeTag
    {
        Burn,       // 灼烧：附加原版 burning 状态（每2秒 10% 最大生命 DOT）
        Slow,       // 减速：附加原版 frozen 状态（减速/定身行为）
        Stun,       // 麻痹：附加原版 stunned 状态（眩晕）
        Poison,     // 毒蚀：附加原版 poisoned 状态（每秒 DOT）
        Shield,     // 护盾：受击减伤（引擎实现，原版 shield 状态无减伤行为）
        Dodge,      // 闪避：受击概率免伤（引擎实现，原版 dodge 状态无行为）
        Lifesteal,  // 吸血：攻击回复生命
        Crit,       // 暴击：攻击概率伤害×2
        Heal,       // 治疗：攻击概率回复生命
        Chain       // 溅射：攻击波及相邻敌人（30% 溅射伤害，不二次触发标签）
    }

    /// <summary>标签静态定义（数据驱动）</summary>
    public sealed class PrototypeTagDef
    {
        public PrototypeTag Tag;         // 标签枚举
        public string NameZh;            // 中文名
        public string DescZh;            // 效果描述
        public bool IsAttackTag;         // 攻击标签（命中时由攻击方触发）
        public float BaseChance;         // 基础触发概率（攻击标签）
        public float MaxChance;          // 触发概率上限（亲和加成后）
        public float Strength;           // 强度参数（吸血比例/溅射伤害比/减伤比等）

        public PrototypeTagDef(PrototypeTag tag, string nameZh, string descZh,
            bool isAttackTag, float baseChance, float maxChance, float strength)
        {
            Tag = tag; NameZh = nameZh; DescZh = descZh;
            IsAttackTag = isAttackTag;
            BaseChance = baseChance; MaxChance = maxChance; Strength = strength;
        }
    }

    /// <summary>
    /// 标签注册表（单一职责：提供标签静态数据）
    /// </summary>
    public static class PrototypeTags
    {
        private static readonly Dictionary<PrototypeTag, PrototypeTagDef> _defs =
            new Dictionary<PrototypeTag, PrototypeTagDef>();

        static PrototypeTags()
        {
            // 攻击标签
            Add(new PrototypeTagDef(PrototypeTag.Burn, "灼烧", "攻击附加燃烧状态（每2秒 10% 最大生命持续伤害）", true, 0.22f, 0.5f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Slow, "减速", "攻击附加冰冻状态（减速目标行动）", true, 0.18f, 0.45f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Stun, "麻痹", "攻击附加眩晕状态（目标短暂定身）", true, 0.12f, 0.35f, 1.5f));
            Add(new PrototypeTagDef(PrototypeTag.Poison, "毒蚀", "攻击附加中毒状态（每秒持续伤害）", true, 0.18f, 0.45f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Lifesteal, "吸血", "攻击回复生命（按造成伤害比例）", true, 0.5f, 1f, 0.15f));
            Add(new PrototypeTagDef(PrototypeTag.Crit, "暴击", "攻击概率造成双倍伤害", true, 0.10f, 0.4f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Heal, "治疗", "攻击概率回复生命（按伤害比例）", true, 0.5f, 1f, 0.3f));
            Add(new PrototypeTagDef(PrototypeTag.Chain, "溅射", "攻击波及相邻敌人（30% 溅射伤害）", true, 0.5f, 1f, 0.3f));
            // 防守标签
            Add(new PrototypeTagDef(PrototypeTag.Shield, "护盾", "受击减伤（最高 40%）", false, 1f, 1f, 0.25f));
            Add(new PrototypeTagDef(PrototypeTag.Dodge, "闪避", "受击概率完全免伤（最高 40%）", false, 1f, 1f, 0.15f));
        }

        private static void Add(PrototypeTagDef def)
        {
            if (def != null) _defs[def.Tag] = def;
        }

        /// <summary>取标签定义（不存在返回 null）</summary>
        public static PrototypeTagDef Get(PrototypeTag tag)
        {
            _defs.TryGetValue(tag, out var def);
            return def;
        }

        /// <summary>标签中文名（供日志/UI）</summary>
        public static string GetName(PrototypeTag tag)
        {
            var def = Get(tag);
            return def != null ? def.NameZh : tag.ToString();
        }
    }
}
