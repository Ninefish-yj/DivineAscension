// ============================================================
// 元素系异能注册表（v0.17.0 新增系统）
// 大众认知异能 = 大类（元素系）→ 小类（火/水/风…）→ 异能（火焰操控…）
// 本文件 = 数据层：10 个元素异能定义（名称 + 主/副标签组合 + 染色体偏好 + 原版基因亲和偏好）
// 效果机制全部由 PrototypeEffectEngine 按标签组合执行，异能本身零逻辑（数据驱动）。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    /// <summary>异能定义（数据驱动）</summary>
    public sealed class PrototypeDef
    {
        public string Id;                // 异能ID（写入 custom_data_string["ds_prototype"]）
        public string NameZh;            // 中文名（大众认知名）
        public string DescZh;            // 效果描述
        public PrototypeTag MainTag;     // 主标签（觉醒时优先表现）
        public PrototypeTag SubTag;      // 副标签
        public int ChromosomePreference; // 染色体偏好（0力量1敏捷2体质3智力4感知5意志6异能7潜能）——桥A觉醒定相的加权依据
        public string VanillaAffinity;   // 原版基因亲和偏好（桥C：原版基因关键词命中则加权重）

        public PrototypeDef(string id, string nameZh, string descZh,
            PrototypeTag mainTag, PrototypeTag subTag, int chromosomePref, string vanillaAffinity)
        {
            Id = id; NameZh = nameZh; DescZh = descZh;
            MainTag = mainTag; SubTag = subTag;
            ChromosomePreference = chromosomePref; VanillaAffinity = vanillaAffinity;
        }
    }

    /// <summary>
    /// 异能注册表（单一职责：提供异能静态数据）
    /// </summary>
    public static class PrototypeRegistry
    {
        /// <summary>元素系 10 异能（首期全量）</summary>
        public static readonly List<PrototypeDef> Elements = new List<PrototypeDef>
        {
            new PrototypeDef("fire",    "火焰操控", "灼烧敌人，攻击波及周围（火元素基因表达）",
                PrototypeTag.Burn, PrototypeTag.Chain, 6, "damage"),
            new PrototypeDef("water",   "水流操控", "治疗自身，护盾减伤（水元素基因表达）",
                PrototypeTag.Heal, PrototypeTag.Shield, 2, "health"),
            new PrototypeDef("wind",    "疾风", "身法闪避，攻击减速目标（风元素基因表达）",
                PrototypeTag.Dodge, PrototypeTag.Slow, 1, "speed"),
            new PrototypeDef("lightning","雷电掌控", "攻击溅射连锁，概率麻痹眩晕（雷元素基因表达）",
                PrototypeTag.Chain, PrototypeTag.Stun, 6, "warfare"),
            new PrototypeDef("ice",     "寒冰操控", "减速冻结目标，附加霜冻持续伤害（冰元素基因表达）",
                PrototypeTag.Slow, PrototypeTag.Burn, 4, "intelligence"),
            new PrototypeDef("earth",   "大地之力", "护盾减伤，攻击波及周围（土元素基因表达）",
                PrototypeTag.Shield, PrototypeTag.Chain, 2, "armor"),
            new PrototypeDef("wood",    "生命操控", "治疗自身，攻击减速缠绕目标（木元素基因表达）",
                PrototypeTag.Heal, PrototypeTag.Slow, 2, "health"),
            new PrototypeDef("light",   "圣光", "治疗自身，护盾减伤（光元素基因表达）",
                PrototypeTag.Heal, PrototypeTag.Shield, 3, "diplomacy"),
            new PrototypeDef("dark",    "暗影", "攻击吸血，附加暗蚀持续伤害（暗元素基因表达）",
                PrototypeTag.Lifesteal, PrototypeTag.Burn, 5, "warfare"),
            new PrototypeDef("metal",   "金属操控", "攻击暴击破甲，护盾硬化（金属元素基因表达）",
                PrototypeTag.Crit, PrototypeTag.Shield, 0, "damage")
        };

        private static readonly Dictionary<string, PrototypeDef> _byId = new Dictionary<string, PrototypeDef>();

        static PrototypeRegistry()
        {
            foreach (var def in Elements)
            {
                if (def != null && !string.IsNullOrEmpty(def.Id) && !_byId.ContainsKey(def.Id))
                    _byId[def.Id] = def;
            }
        }

        /// <summary>按异能ID取定义（不存在返回 null）</summary>
        public static PrototypeDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _byId.TryGetValue(id, out var def);
            return def;
        }

        /// <summary>单位当前异能定义（读取 custom_data 中的 ds_prototype，无则 null）</summary>
        public static PrototypeDef GetForActor(Actor actor)
        {
            if (actor == null) return null;
            var data = Code.Data.ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_string == null) return null;
            if (data.custom_data_string.TryGetValue("ds_prototype", out string id) && !string.IsNullOrEmpty(id))
                return Get(id);
            return null;
        }

        /// <summary>单位是否已定相异能（补发判定用）</summary>
        public static bool HasPrototype(Actor actor)
        {
            if (actor == null) return false;
            var data = Code.Data.ActorDataAccessor.GetData(actor);
            if (data == null || data.custom_data_string == null) return false;
            return data.custom_data_string.TryGetValue("ds_prototype", out string id) && !string.IsNullOrEmpty(id);
        }
    }
}
