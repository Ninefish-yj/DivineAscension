// ============================================================
// 异能标签定义（v0.18.0 扩展：10 → 30 标签）
// 大众认知异能的底层机制 = 有限的"效果标签"组合：
// 几百上千个异能名的战斗机制由 ~30 个标签组合生成，一次实现、全异能复用。
// 本文件 = 数据层（标签枚举 + 标签静态表），与效果引擎（逻辑）分离。
// 全部 30 标签均有真实机制归宿（新标签 = 已有机制的变体/组合）：
//   灼烧组：Burn/Plasma/Frost
//   减速组：Slow/Frostbite/TimeSlow/Frost
//   眩晕组：Stun/Knockback/Gravity/Fear
//   毒蚀组：Poison/Curse
//   吸血组：Lifesteal/Drain/Blood
//   暴击组：Crit/Telekinesis
//   治疗组：Heal/Soul
//   溅射组：Chain
//   狂暴组：Berserk（当次攻击伤害加成）
//   防守-减伤：Shield
//   防守-闪避：Dodge/Illusion/Invisibility
//   防守-反击：Magnetism/Vibration/Sound（受击麻痹攻击者）
//   防守-回复：Blessing/Regeneration（受击回血）
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    /// <summary>
    /// 异能效果标签（44 个，全部真实生效）
    /// 攻击标签（命中时由攻击方触发）：Burn/Slow/Stun/Poison/Chain/Lifesteal/Crit/Heal/
    ///   Frostbite/Knockback/Gravity/TimeSlow/Fear/Berserk/Drain/Blood/Soul/Curse/Frost/Plasma/Telekinesis/
    ///   Entangle/Petrify/Aging/Weakness/ArmorBreak/Execute/Sleep/MultiShot/HealAlly（v0.18.24 大众认知补充）
    /// 防守标签（受击时生效）：Shield/Dodge/Blessing/Magnetism/Vibration/Sound/Illusion/Invisibility/Regeneration/
    ///   Thorns/Clone/ElementResist/Cleanse/Teleport（v0.18.24 大众认知补充）
    /// </summary>
    public enum PrototypeTag
    {
        // ---- 攻击标签 ----
        Burn,       // 灼烧：附加原版 burning 状态（每2秒 10% 最大生命 DOT）
        Slow,       // 减速：附加原版 frozen 状态（减速/定身行为）
        Stun,       // 麻痹：附加原版 stunned 状态（眩晕）
        Poison,     // 毒蚀：附加原版 poisoned 状态（每秒 DOT）
        Lifesteal,  // 吸血：攻击回复生命（按造成伤害比例）
        Crit,       // 暴击：攻击概率伤害×2
        Heal,       // 治疗：攻击概率回复生命（按伤害比例）
        Chain,      // 溅射：攻击波及相邻敌人（30% 溅射伤害，不二次触发标签）
        // ---- 攻击变体（v0.18.0 新增）----
        Frostbite,  // 霜冻：减速强化（frozen 概率更高、冻结更久）
        Knockback,  // 击退：攻击将目标震退（眩晕短暂定身）
        Gravity,    // 重力：攻击以重力压制目标（眩晕）
        TimeSlow,   // 时缓：攻击扭曲时间减速目标（强减速）
        Fear,       // 恐惧：攻击使目标恐惧（眩晕+怯战减速）
        Berserk,    // 狂暴：攻击进入狂暴状态（当次攻击伤害提升）
        Drain,      // 汲取：攻击汲取生命（吸血弱化版）
        Blood,      // 血祭：攻击血祭生命（吸血强化版）
        Soul,       // 灵魂：攻击攫取灵魂（概率回复生命）
        Curse,      // 诅咒：攻击附加诅咒（中毒持续伤害）
        Frost,      // 寒冰：攻击附加寒霜（减速+微伤）
        Plasma,     // 等离子：攻击附加等离子灼烧（强化燃烧）
        Telekinesis,// 念力：攻击以念力贯穿（破甲高伤=强化暴击）
        // ---- 防守标签 ----
        Shield,     // 护盾：受击减伤（引擎实现，最高 40%）
        Dodge,      // 闪避：受击概率完全免伤（引擎实现，最高 40%）
        // ---- 防守变体（v0.18.0 新增）----
        Blessing,   // 祝福：受击概率回血（神圣庇护）
        Magnetism,  // 磁力：受击概率麻痹攻击者（电磁反击）
        Vibration,  // 震动：受击概率震晕攻击者（冲击反震）
        Sound,      // 声波：受击概率震慑攻击者（音爆反震）
        Illusion,   // 幻象：受击概率制造幻象闪避（闪避强化）
        Invisibility, // 隐身：受击概率隐遁免伤（闪避最高）
        Regeneration, // 再生：受击概率自愈回血（战斗再生）
        // ---- 大众认知补充标签（v0.18.24 新增 14 个，共 44 标签）----
        // 攻击标签：Entangle/Petrify/Aging/Weakness/ArmorBreak/Execute/Sleep/MultiShot/HealAlly
        // 防守标签：Thorns/Clone/ElementResist/Cleanse/Teleport
        Entangle,     // 束缚：缠绕定身（深度冰冻，比减速更牢）
        Petrify,      // 石化：定身+易碎（更长眩晕+当次增伤）
        Aging,        // 衰老：加速老化（虚弱+减速双状态）
        Weakness,     // 虚弱：削弱目标（长时虚弱）
        ArmorBreak,   // 破甲：穿透减伤+后续虚弱
        Execute,      // 致死：低血量概率斩杀
        Sleep,        // 睡眠：陷入沉睡（长眩晕）
        MultiShot,    // 多重：连击爆发（当次伤害+50%）
        HealAlly,     // 治疗：攻击概率治愈友军（含自身）
        Thorns,       // 反弹：受击反弹伤害给攻击者
        Clone,        // 分身：受击免伤+回敬伤害
        ElementResist,// 元素抗性：受击减伤（元素/异常攻击抗性）
        Cleanse,      // 净化：受击概率清除自身负面状态
        Teleport      // 瞬移：受击概率遁走免伤（闪避最高）
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
        public float Strength;           // 强度参数（吸血比例/溅射伤害比/减伤比/伤害加成等）

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
            // ---- 攻击标签（10 个，v0.17.0）----
            Add(new PrototypeTagDef(PrototypeTag.Burn, "灼烧", "攻击附加燃烧状态（每2秒 10% 最大生命持续伤害）", true, 0.22f, 0.5f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Slow, "减速", "攻击附加冰冻状态（减速目标行动）", true, 0.18f, 0.45f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Stun, "麻痹", "攻击附加眩晕状态（目标短暂定身）", true, 0.12f, 0.35f, 1.5f));
            Add(new PrototypeTagDef(PrototypeTag.Poison, "毒蚀", "攻击附加中毒状态（每秒持续伤害）", true, 0.18f, 0.45f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Lifesteal, "吸血", "攻击回复生命（按造成伤害比例）", true, 0.5f, 1f, 0.15f));
            Add(new PrototypeTagDef(PrototypeTag.Crit, "暴击", "攻击概率造成双倍伤害", true, 0.10f, 0.4f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Heal, "治疗", "攻击概率回复生命（按伤害比例）", true, 0.5f, 1f, 0.3f));
            Add(new PrototypeTagDef(PrototypeTag.Chain, "溅射", "攻击波及相邻敌人（30% 溅射伤害）", true, 0.5f, 1f, 0.3f));
            // ---- 攻击变体（13 个，v0.18.0）----
            Add(new PrototypeTagDef(PrototypeTag.Frostbite, "霜冻", "攻击附加深度冰冻（强减速目标行动）", true, 0.18f, 0.45f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Knockback, "击退", "攻击将目标震退（短暂定身）", true, 0.12f, 0.35f, 1.5f));
            Add(new PrototypeTagDef(PrototypeTag.Gravity, "重力", "攻击以重力压制目标（短暂定身）", true, 0.12f, 0.35f, 1.5f));
            Add(new PrototypeTagDef(PrototypeTag.TimeSlow, "时缓", "攻击扭曲时间减速目标（强减速）", true, 0.18f, 0.45f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Fear, "恐惧", "攻击使目标恐惧（眩晕+怯战）", true, 0.12f, 0.35f, 1.5f));
            Add(new PrototypeTagDef(PrototypeTag.Berserk, "狂暴", "攻击进入狂暴（当次伤害提升25%）", true, 0.5f, 1f, 0.25f));
            Add(new PrototypeTagDef(PrototypeTag.Drain, "汲取", "攻击汲取生命（吸血弱化版）", true, 0.5f, 1f, 0.12f));
            Add(new PrototypeTagDef(PrototypeTag.Blood, "血祭", "攻击以血祭回生（吸血强化版）", true, 0.5f, 1f, 0.35f));
            Add(new PrototypeTagDef(PrototypeTag.Soul, "灵魂", "攻击攫取灵魂（概率回复生命）", true, 0.3f, 0.8f, 0.2f));
            Add(new PrototypeTagDef(PrototypeTag.Curse, "诅咒", "攻击附加诅咒（中毒持续伤害）", true, 0.2f, 0.5f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Frost, "寒冰", "攻击附加寒霜（减速+微伤）", true, 0.2f, 0.5f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Plasma, "等离子", "攻击附加等离子灼烧（强化燃烧）", true, 0.25f, 0.55f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Telekinesis, "念力", "攻击以念力贯穿（破甲高伤）", true, 0.12f, 0.4f, 1f));
            // ---- 防守标签（2 个，v0.17.0）----
            Add(new PrototypeTagDef(PrototypeTag.Shield, "护盾", "受击减伤（最高 40%）", false, 1f, 1f, 0.25f));
            Add(new PrototypeTagDef(PrototypeTag.Dodge, "闪避", "受击概率完全免伤（最高 40%）", false, 1f, 1f, 0.15f));
            // ---- 防守变体（7 个，v0.18.0）----
            Add(new PrototypeTagDef(PrototypeTag.Blessing, "祝福", "受击概率回血（神圣庇护）", false, 0.5f, 1f, 0.2f));
            Add(new PrototypeTagDef(PrototypeTag.Magnetism, "磁力", "受击概率麻痹攻击者（电磁反击）", false, 0.15f, 0.4f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Vibration, "震动", "受击概率震晕攻击者（冲击反震）", false, 0.15f, 0.4f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Sound, "声波", "受击概率震慑攻击者（音爆反震）", false, 0.18f, 0.45f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Illusion, "幻象", "受击概率制造幻象闪避（闪避强化）", false, 1f, 1f, 0.22f));
            Add(new PrototypeTagDef(PrototypeTag.Invisibility, "隐身", "受击概率隐遁免伤（闪避最高）", false, 1f, 1f, 0.3f));
            Add(new PrototypeTagDef(PrototypeTag.Regeneration, "再生", "受击概率自愈回血（战斗再生）", false, 0.5f, 1f, 0.25f));
            // ---- 大众认知补充标签（14 个，v0.18.24）----
            Add(new PrototypeTagDef(PrototypeTag.Entangle, "束缚", "攻击缠绕定身目标（深度冰冻）", true, 0.20f, 0.45f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Petrify, "石化", "攻击将目标石化（长眩晕+易碎）", true, 0.12f, 0.35f, 1.1f));
            Add(new PrototypeTagDef(PrototypeTag.Aging, "衰老", "攻击使目标衰老（虚弱+减速）", true, 0.18f, 0.45f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Weakness, "虚弱", "攻击削弱目标（长时虚弱）", true, 0.20f, 0.50f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.ArmorBreak, "破甲", "攻击破开护甲（穿透减伤+虚弱）", true, 0.25f, 0.55f, 1.3f));
            Add(new PrototypeTagDef(PrototypeTag.Execute, "致死", "攻击斩杀重伤目标（低血量）", true, 0.05f, 0.15f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Sleep, "睡眠", "攻击使目标陷入沉睡（长眩晕）", true, 0.12f, 0.35f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.MultiShot, "多重", "攻击连击爆发（当次伤害+50%）", true, 0.30f, 0.70f, 0.5f));
            Add(new PrototypeTagDef(PrototypeTag.HealAlly, "治疗", "攻击概率治愈友军（含自身）", true, 0.30f, 0.60f, 0.3f));
            Add(new PrototypeTagDef(PrototypeTag.Thorns, "反弹", "受击反弹伤害给攻击者", false, 0.25f, 0.60f, 0.3f));
            Add(new PrototypeTagDef(PrototypeTag.Clone, "分身", "受击概率完全免伤并回敬伤害", false, 1f, 1f, 0.22f));
            Add(new PrototypeTagDef(PrototypeTag.ElementResist, "元素抗性", "受击减伤（元素/异常攻击抗性）", false, 1f, 1f, 0.2f));
            Add(new PrototypeTagDef(PrototypeTag.Cleanse, "净化", "受击概率清除自身负面状态", false, 0.30f, 0.70f, 1f));
            Add(new PrototypeTagDef(PrototypeTag.Teleport, "瞬移", "受击概率遁走免伤（闪避最高）", false, 1f, 1f, 0.35f));
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
