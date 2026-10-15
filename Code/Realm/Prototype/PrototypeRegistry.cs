// ============================================================
// 异能注册表（v0.18.0 全量架构）
// 大众认知异能 = 大类（元素系/自然系/精神系…）→ 小类（火/水/风…）→ 异能（火焰操控…）
// 底层机制 = 30 个效果标签组合（PrototypeTags），一次实现、全异能复用。
// 数据按大类拆分在 PrototypeData_*.cs（单一职责：每文件一类数据），本文件负责聚合。
// 旧档兼容：v0.17.0 的 10 个异能 id（fire/water/…）原样保留。
// ============================================================

using System.Collections.Generic;
using System;

namespace Code.Realm.Prototype
{
    /// <summary>异能定义（数据驱动）</summary>
    public sealed class PrototypeDef
    {
        public string Id;                // 异能ID（写入 custom_data_string["ds_prototype"]，全局唯一）
        public string NameZh;            // 中文名（大众认知名）
        public string DescZh;            // 效果描述（真实机制：由标签组合表达的实战效果）
        public string Category;          // 大类（元素系/自然系/精神系/时空系/光明系/黑暗系/生命系/死亡系/强化系/星辰系/血族系/神话系）
        public string SubCategory;       // 小类（火/水/风…按大类内细分）
        public PrototypeTag MainTag;     // 主标签（觉醒时优先表现）
        public PrototypeTag SubTag;      // 副标签
        public int ChromosomePreference; // 染色体偏好（0力量1敏捷2体质3智力4感知5意志6异能7潜能）——桥A觉醒定相的加权依据

        public PrototypeDef(string id, string nameZh, string descZh, string category, string subCategory,
            PrototypeTag mainTag, PrototypeTag subTag, int chromosomePref)
        {
            Id = id; NameZh = nameZh; DescZh = descZh;
            Category = category; SubCategory = subCategory;
            MainTag = mainTag; SubTag = subTag;
            ChromosomePreference = chromosomePref;
        }
    }

    /// <summary>
    /// 异能注册表（单一职责：聚合全部异能数据 + 提供查询）
    /// 数据源：PrototypeData_*.cs 12 个大类文件。
    /// </summary>
    public static class PrototypeRegistry
    {
        /// <summary>全部异能（聚合，觉醒定相/树 UI 都从这里取）</summary>
        public static readonly List<PrototypeDef> All = new List<PrototypeDef>();

        /// <summary>兼容别名：旧代码引用的 Elements = 全量异能</summary>
        public static IList<PrototypeDef> Elements => All;

        /// <summary>分类索引：大类 → 小类 → 异能列表（供异能树层级浏览）</summary>
        public static readonly Dictionary<string, Dictionary<string, List<PrototypeDef>>> Categories =
            new Dictionary<string, Dictionary<string, List<PrototypeDef>>>();

        private static readonly Dictionary<string, PrototypeDef> _byId = new Dictionary<string, PrototypeDef>();

        static PrototypeRegistry()
        {
            RegisterAll(PrototypeData_Elemental.List);
            RegisterAll(PrototypeData_Nature.List);
            RegisterAll(PrototypeData_Psychic.List);
            RegisterAll(PrototypeData_SpaceTime.List);
            RegisterAll(PrototypeData_Light.List);
            RegisterAll(PrototypeData_Dark.List);
            RegisterAll(PrototypeData_Life.List);
            RegisterAll(PrototypeData_Death.List);
            RegisterAll(PrototypeData_Celestial.List);
            RegisterAll(PrototypeData_Machine.List);
            RegisterAll(PrototypeData_Blood.List);
            RegisterAll(PrototypeData_Myth.List);
        }

        private static void RegisterAll(List<PrototypeDef> list)
        {
            if (list == null) return;
            foreach (var def in list)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (_byId.ContainsKey(def.Id)) continue;   // 防重复（旧档兼容：重名以先注册为准）
                _byId[def.Id] = def;
                All.Add(def);
                // 分类索引
                if (string.IsNullOrEmpty(def.Category)) continue;
                if (!Categories.TryGetValue(def.Category, out var subs))
                {
                    subs = new Dictionary<string, List<PrototypeDef>>();
                    Categories[def.Category] = subs;
                }
                string sub = string.IsNullOrEmpty(def.SubCategory) ? "通用" : def.SubCategory;
                if (!subs.TryGetValue(sub, out var subList))
                {
                    subList = new List<PrototypeDef>();
                    subs[sub] = subList;
                }
                subList.Add(def);
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

        /// <summary>大类名称列表（树 UI 第一层）</summary>
        public static List<string> GetCategoryNames()
        {
            return new List<string>(Categories.Keys);
        }

        /// <summary>按大类取全部异能（树 UI 展开用）</summary>
        public static List<PrototypeDef> GetByCategory(string category)
        {
            var result = new List<PrototypeDef>();
            if (string.IsNullOrEmpty(category)) return result;
            if (Categories.TryGetValue(category, out var subs))
            {
                foreach (var subList in subs.Values)
                    result.AddRange(subList);
            }
            return result;
        }
    }
}
