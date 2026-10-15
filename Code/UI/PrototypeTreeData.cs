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
        public string Id;          // 异能id / 大类名 / 小类名 / "root"
        public string Name;        // 显示名
        public int Level;          // 0=异能体系根 1=大类 2=小类 3=异能叶子
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
        public const float NODE_ROOT_W = 200f, NODE_ROOT_H = 50f;
        public const float NODE_CAT_W = 180f, NODE_CAT_H = 46f;
        public const float NODE_SUB_W = 110f, NODE_SUB_H = 40f;
        public const float NODE_LEAF_W = 172f, NODE_LEAF_H = 68f;
        public const float LEAF_SLOT_X = 188f;    // 异能槽位宽（节点宽+间隔）
        public const float GROUP_GAP = 160f;      // 不同小类异能群之间的最小间隔

        // 大类固定展示顺序（Categories 字典无序，用顺序数组保证树 UI 稳定）
        private static readonly string[] CategoryOrder =
        {
            "元素系", "自然系", "精神系", "时空系", "光明系", "黑暗系",
            "生命系", "死亡系", "强化系", "星辰系", "血族系", "神话系"
        };

        /// <summary>构建全量异能树布局</summary>
        public static ProtoTreeLayout Build()
        {
            var layout = new ProtoTreeLayout();
            // 根
            var root = new ProtoTreeNode
            {
                Id = "root", Name = "异能体系", Level = 0, ParentId = null,
                Color = new Color(0.45f, 0.80f, 0.85f, 1f)
            };
            layout.Nodes.Add(root);

            float leafY = 3f * LAYER_PITCH;
            float subY = 2f * LAYER_PITCH;
            float catY = 1f * LAYER_PITCH;
            float rootY = 0f;

            // 遍历大类（固定顺序）
            var catCenters = new List<float>();
            var catRefs = new List<ProtoTreeNode>();
            foreach (string category in CategoryOrder)
            {
                if (!PrototypeRegistry.Categories.TryGetValue(category, out var subs)) continue;

                // 大类节点
                var catNode = new ProtoTreeNode
                {
                    Id = category, Name = category, Level = 1, ParentId = "root",
                    W = NODE_CAT_W, H = NODE_CAT_H,
                    Color = GetCategoryColor(category)
                };
                layout.Nodes.Add(catNode);
                catRefs.Add(catNode);

                // 遍历小类
                var subCenters = new List<float>();
                var subRefs = new List<ProtoTreeNode>();
                foreach (var kv in subs)
                {
                    string subName = kv.Key;
                    var subList = kv.Value;
                    if (subList == null || subList.Count == 0) continue;

                    // 异能叶子（横排）
                    float groupX0 = 0f;
                    var leafRefs = new List<ProtoTreeNode>();
                    for (int i = 0; i < subList.Count; i++)
                    {
                        var def = subList[i];
                        if (def == null) continue;
                        var leaf = new ProtoTreeNode
                        {
                            Id = def.Id, Name = def.NameZh, Level = 3,
                            ParentId = category + "::" + subName,
                            X = groupX0 + i * LEAF_SLOT_X, Y = leafY,
                            W = NODE_LEAF_W, H = NODE_LEAF_H,
                            Def = def, Color = catNode.Color
                        };
                        layout.Nodes.Add(leaf);
                        leafRefs.Add(leaf);
                    }
                    if (leafRefs.Count == 0) continue;
                    float groupW = (leafRefs.Count - 1) * LEAF_SLOT_X + NODE_LEAF_W;
                    float groupCenter = groupX0 + groupW / 2f;

                    // 小类节点：居中于其异能群
                    var subNode = new ProtoTreeNode
                    {
                        Id = category + "::" + subName, Name = subName, Level = 2,
                        ParentId = category, X = groupCenter - NODE_SUB_W / 2f, Y = subY,
                        W = NODE_SUB_W, H = NODE_SUB_H,
                        Color = catNode.Color * new Color(0.85f, 0.85f, 0.85f, 1f)
                    };
                    layout.Nodes.Add(subNode);
                    subRefs.Add(subNode);
                    subCenters.Add(groupCenter);

                    // 下一组异能群的起始 X（当前群末尾 + 群间隔）
                    groupX0 = groupX0 + groupW + GROUP_GAP;
                }
                if (subRefs.Count == 0) continue;

                // 大类节点：居中于其小类群
                float catCenter = 0f;
                foreach (float c in subCenters) catCenter += c;
                catCenter /= subCenters.Count;
                catNode.X = catCenter - NODE_CAT_W / 2f;
                catNode.Y = catY;
                catCenters.Add(catCenter);
            }

            // 根节点：居中于全部大类群
            if (catCenters.Count > 0)
            {
                float rootCenter = 0f;
                foreach (float c in catCenters) rootCenter += c;
                rootCenter /= catCenters.Count;
                root.X = rootCenter - NODE_ROOT_W / 2f;
                root.Y = rootY;
                root.W = NODE_ROOT_W; root.H = NODE_ROOT_H;
            }
            else
            {
                root.X = 0f; root.Y = rootY; root.W = NODE_ROOT_W; root.H = NODE_ROOT_H;
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
                case "星辰系": return new Color(0.45f, 0.60f, 0.90f, 1f);   // 星辰蓝
                case "强化系": return new Color(0.88f, 0.55f, 0.40f, 1f);   // 强化赤橙（力量感）
                case "血族系": return new Color(0.80f, 0.30f, 0.35f, 1f);   // 血族红
                case "神话系": return new Color(0.95f, 0.70f, 0.35f, 1f);   // 神话金橙
                default: return new Color(0.6f, 0.6f, 0.6f, 1f);
            }
        }
    }
}
