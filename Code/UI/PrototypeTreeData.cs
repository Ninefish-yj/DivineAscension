// ============================================================
// 异能树 · 数据与布局层（v0.18.0）
// 职责（单一职责）：
//   1. 从 PrototypeRegistry（异能全量库）构建树节点模型：异能体系 → 12 大类 → 小类 → 异能
//   2. 分层簇状布局：异能叶子横向排布 → 小类居中于其异能群 → 大类居中于小类群 → 根居中于大类群
//   3. 输出内容包围盒（Bounds），供渲染层 Fit（聚焦）计算
// 纯数据，不含任何 GUI 调用（UI 与业务/数据逻辑分离）。
// 布局思想参考诸天神座 DaoTuLayout：分层 + 槽位 + 上层节点对下层子群居中。
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Code.Realm.Prototype;

namespace Code.UI
{
    /// <summary>异能树节点（数据模型）</summary>
    public sealed class ProtoTreeNode
    {
        public string Id;          // 异能id / 大类名 / 小类名
        public string Name;        // 显示名
        public int Level;          // 0=大类 1=小类 2=异能叶子
        public string ParentId;    // 父节点 Id（连线用）
        public float X, Y;         // 布局坐标（左上角）
        public float W, H;         // 节点尺寸
        public PrototypeDef Def;   // 异能定义（根/大类/小类为 null）
        public Color Color;        // 节点主色（大类统一色系，异能随大类）
    }

    /// <summary>异能树布局结果（数据模型）</summary>
    public sealed class ProtoTreeLayout
    {
        public readonly List<ProtoTreeNode> Nodes = new List<ProtoTreeNode>();
        public Rect Bounds;        // 内容包围盒（渲染层 Fit 用）

        private Dictionary<string, ProtoTreeNode> _byId;

        public ProtoTreeNode GetNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, ProtoTreeNode>();
                foreach (var n in Nodes) _byId[n.Id] = n;
            }
            _byId.TryGetValue(id, out var node);
            return node;
        }
    }

    /// <summary>
    /// 异能树布局构建器（单一职责：输入异能全量库 → 输出分层簇状布局）
    /// 纯数据计算，无 GUI / 无业务副作用。
    /// </summary>
    public static class ProtoTreeLayoutBuilder
    {
        // 布局常量（参照诸天神座 DaoTuLayout 的量级，异能树更深更宽故槽位紧凑）
        public const float LAYER_PITCH = 240f;    // 层间距（Y）
        public const float NODE_ROOT_W = 160f, NODE_ROOT_H = 54f;
        public const float NODE_CAT_W = 180f, NODE_CAT_H = 46f;
        public const float NODE_SUB_W = 110f, NODE_SUB_H = 40f;
        public const float NODE_LEAF_W = 172f, NODE_LEAF_H = 68f;
        public const float LEAF_SLOT_X = 188f;    // 异能槽位宽（节点宽+间隔）
        public const float GROUP_GAP = 160f;      // 不同小类异能群之间的最小间隔
        public const float CAT_GAP = 240f;        // 不同大类之间的最小间隔

        // 大类固定展示顺序（Categories 字典无序，用顺序数组保证树 UI 稳定；大众认知 10 系）
        private static readonly string[] CategoryOrder =
        {
            "元素系", "自然系", "精神系", "时空系", "光明系", "黑暗系",
            "生命系", "死亡系", "强化系"
        };

        /// <summary>构建全量异能树布局（根：异能 → 大类 → 小类 → 异能叶，根居中簇状分层树）</summary>
        public static ProtoTreeLayout Build()
        {
            var layout = new ProtoTreeLayout();

            // 层间距（Y）：根 → 大类 → 小类 → 异能叶
            float rootY = 0f;
            float catY = 1f * LAYER_PITCH;
            float subY = 2f * LAYER_PITCH;
            float leafY = 3f * LAYER_PITCH;

            // ---- 阶段1：每系内部布局（异能叶横排、跨小类累积 X、小类居中于其群）----
            // 记录每系：系名、系内总宽、小类群中心均值（相对系起点 0）、系内全部节点（相对坐标）
            var catInfo = new List<CatLayoutInfo>();
            foreach (string category in CategoryOrder)
            {
                if (!PrototypeRegistry.Categories.TryGetValue(category, out var subs)) continue;

                var catNodes = new List<ProtoTreeNode>();
                var subCenters = new List<float>();
                float groupX0 = 0f;   // ← 跨小类累积（旧版在此循环内重置导致全系异能重叠）

                foreach (var kv in subs)
                {
                    string subName = kv.Key;
                    var subList = kv.Value;
                    if (subList == null || subList.Count == 0) continue;

                    // 异能叶子（横排）
                    var leafRefs = new List<ProtoTreeNode>();
                    for (int i = 0; i < subList.Count; i++)
                    {
                        var def = subList[i];
                        if (def == null) continue;
                        var leaf = new ProtoTreeNode
                        {
                            Id = def.Id, Name = def.NameZh, Level = 2,
                            ParentId = category + "::" + subName,
                            X = groupX0 + i * LEAF_SLOT_X, Y = leafY,
                            W = NODE_LEAF_W, H = NODE_LEAF_H,
                            Def = def, Color = GetCategoryColor(category)
                        };
                        catNodes.Add(leaf);
                        leafRefs.Add(leaf);
                    }
                    if (leafRefs.Count == 0) continue;
                    float groupW = (leafRefs.Count - 1) * LEAF_SLOT_X + NODE_LEAF_W;
                    float groupCenter = groupX0 + groupW / 2f;

                    // 小类节点：居中于其异能群
                    var subNode = new ProtoTreeNode
                    {
                        Id = category + "::" + subName, Name = subName, Level = 1,
                        ParentId = category, X = groupCenter - NODE_SUB_W / 2f, Y = subY,
                        W = NODE_SUB_W, H = NODE_SUB_H,
                        Color = GetCategoryColor(category) * new Color(0.85f, 0.85f, 0.85f, 1f)
                    };
                    catNodes.Add(subNode);
                    subCenters.Add(groupCenter);

                    // 下一组异能群的起始 X（当前群末尾 + 群间隔）
                    groupX0 = groupX0 + groupW + GROUP_GAP;
                }
                if (subCenters.Count == 0) continue;

                // 系内总宽 = 最后一个异能群右端（去掉尾间隔）
                float catWidth = groupX0 - GROUP_GAP;
                float catCenterOfs = 0f;
                foreach (float c in subCenters) catCenterOfs += c;
                catCenterOfs /= subCenters.Count;
                catInfo.Add(new CatLayoutInfo(category, catWidth, catCenterOfs, catNodes));
            }

            // ---- 阶段2：横向排系 + 根节点居中 ----
            // 总宽 = Σ(系宽 + 系间隔)；以根中心 x=0 整体居中
            float totalW = 0f;
            foreach (var ci in catInfo) totalW += ci.Width + CAT_GAP;
            totalW -= CAT_GAP;
            float cursorX = -totalW / 2f;

            // 根节点（异能）：顶部居中
            var root = new ProtoTreeNode
            {
                Id = ROOT_ID, Name = "异能", Level = -1, ParentId = null,
                X = -NODE_ROOT_W / 2f, Y = rootY,
                W = NODE_ROOT_W, H = NODE_ROOT_H,
                Color = new Color(0.95f, 0.85f, 0.55f, 1f)
            };
            layout.Nodes.Add(root);

            foreach (var ci in catInfo)
            {
                // 大类节点：居中于其小类群（相对坐标 + 系起点）
                var catNode = new ProtoTreeNode
                {
                    Id = ci.Name, Name = ci.Name, Level = 0, ParentId = ROOT_ID,
                    X = cursorX + ci.CenterOfs - NODE_CAT_W / 2f, Y = catY,
                    W = NODE_CAT_W, H = NODE_CAT_H,
                    Color = GetCategoryColor(ci.Name)
                };
                layout.Nodes.Add(catNode);
                foreach (var n in ci.Nodes)
                {
                    n.X += cursorX;   // 系内节点平移到该系起点
                    layout.Nodes.Add(n);
                }
                cursorX += ci.Width + CAT_GAP;
            }

            // 内容包围盒（含边预留）
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var n in layout.Nodes)
            {
                x0 = Mathf.Min(x0, n.X); y0 = Mathf.Min(y0, n.Y);
                x1 = Mathf.Max(x1, n.X + n.W); y1 = Mathf.Max(y1, n.Y + n.H);
            }
            if (layout.Nodes.Count == 0) { layout.Bounds = new Rect(0, 0, 1, 1); return layout; }
            layout.Bounds = Rect.MinMaxRect(x0 - 40f, y0 - 40f, x1 + 40f, y1 + 40f);
            return layout;
        }

        /// <summary>根节点 ID（异能）</summary>
        public const string ROOT_ID = "__root__";

        /// <summary>每系的布局信息（阶段1 → 阶段2 传递）</summary>
        private sealed class CatLayoutInfo
        {
            public readonly string Name;
            public readonly float Width;        // 系内异能群总宽（相对坐标跨度）
            public readonly float CenterOfs;    // 小类群中心均值（相对系起点 0）
            public readonly List<ProtoTreeNode> Nodes;

            public CatLayoutInfo(string name, float width, float centerOfs, List<ProtoTreeNode> nodes)
            {
                Name = name; Width = width; CenterOfs = centerOfs; Nodes = nodes;
            }
        }

        /// <summary>大类主色（元素系红蓝系 / 自然系绿 / 精神系紫 / 时空系青 / 光明金 / 黑暗紫灰 / 生命翠 / 死亡灰绿 / 强化赤橙 / 星辰蓝紫 / 血族血红 / 神话金橙）</summary>
        public static Color GetCategoryColor(string category)
        {
            switch (category)
            {
                case "元素系": return new Color(0.85f, 0.55f, 0.35f, 1f);   // 火橙
                case "自然系": return new Color(0.45f, 0.75f, 0.45f, 1f);   // 草木绿
                case "精神系": return new Color(0.65f, 0.50f, 0.85f, 1f);   // 幻紫
                case "时空系": return new Color(0.45f, 0.80f, 0.80f, 1f);   // 时空青
                case "光明系": return new Color(0.95f, 0.82f, 0.45f, 1f);   // 圣光金
                case "黑暗系": return new Color(0.55f, 0.45f, 0.70f, 1f);   // 暗影紫
                case "生命系": return new Color(0.40f, 0.85f, 0.55f, 1f);   // 生命翠
                case "死亡系": return new Color(0.60f, 0.60f, 0.55f, 1f);   // 死寂灰绿
                case "强化系": return new Color(0.88f, 0.55f, 0.40f, 1f);   // 强化赤橙（力量感）
                default: return new Color(0.6f, 0.6f, 0.6f, 1f);
            }
        }
    }
}
