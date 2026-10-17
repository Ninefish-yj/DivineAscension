// ============================================================
// 异能树 · 数据与布局层（v0.18.93 辐射式）
// 职责（单一职责）：
//   1. 从 PrototypeRegistry（异能全量库）构建树节点模型：异能体系 → 9 大类 → 小类 → 异能
//   2. 辐射树布局：根"异能"居中，大类按系内异能数占比分配圆周扇区向四周辐射，
//      小类在其系扇区内按异能数细分角，异能叶在小类中心角 ±30° 扇区内均布。
//   3. 输出可见层包围盒（根+系+小类，异能叶默认折叠由渲染层展开），供 Fit 聚焦
// 纯数据，不含任何 GUI 调用（UI 与业务/数据逻辑分离）。
// 自适应：大类顺序动态枚举（新增大类自动追加扇区），异能增减只改变所属扇区角度，
//         无需重新设计布局——拓展异能不用改树。
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
        public Rect Bounds;        // 可见层内容包围盒（根+系+小类，渲染层 Fit 用；异能叶按需展开）

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
    /// 异能树辐射布局构建器（单一职责：输入异能全量库 → 输出辐射树布局）
    /// 纯数据计算，无 GUI / 无业务副作用。
    /// </summary>
    public static class ProtoTreeLayoutBuilder
    {
        // ---- 辐射布局常量 ----
        public const float RADIUS_CAT = 300f;    // 大类环半径（根中心起）
        public const float RADIUS_SUB = 620f;    // 小类环半径
        public const float RADIUS_LEAF = 900f;   // 异能叶环半径（展开扇区按叶数自适应，半径固定）
        public const float NODE_ROOT_W = 160f, NODE_ROOT_H = 54f;
        public const float NODE_CAT_W = 190f, NODE_CAT_H = 46f;
        public const float NODE_SUB_W = 120f, NODE_SUB_H = 40f;
        public const float NODE_LEAF_W = 172f, NODE_LEAF_H = 68f;

        // 大类固定展示顺序（Categories 字典无序，用顺序数组保证树 UI 稳定；大众认知 9 系）。
        // 数据中出现的新大类自动追加到末尾，无需改此数组——拓展异能/体系不用重新设计树。
        private static readonly string[] CategoryOrder =
        {
            "元素系", "自然系", "精神系", "时空系", "光明系", "黑暗系",
            "生命系", "死亡系", "强化系"
        };

        /// <summary>构建全量异能树布局（根居中，大类/小类按异能数占比分配扇区向四周辐射）</summary>
        public static ProtoTreeLayout Build()
        {
            var layout = new ProtoTreeLayout();
            var registry = PrototypeRegistry.Categories;

            // ---- 阶段0：大类动态枚举（固定顺序优先，新系自动追加）----
            var cats = new List<string>();
            foreach (string c in CategoryOrder)
                if (registry.ContainsKey(c)) cats.Add(c);
            foreach (var kv in registry)
                if (!cats.Contains(kv.Key)) cats.Add(kv.Key);

            // ---- 阶段1：按系/小类异能数计算扇区角并生成节点 ----
            // 总异能数（角度分配权重）
            int totalLeaves = 0;
            foreach (string cat in cats)
            {
                if (!registry.TryGetValue(cat, out var subs)) continue;
                foreach (var kv in subs)
                    if (kv.Value != null) totalLeaves += kv.Value.Count;
            }
            if (totalLeaves == 0)
            {
                layout.Bounds = new Rect(-100f, -100f, 200f, 200f);
                return layout;
            }

            // 根节点（异能）：世界中心
            var root = new ProtoTreeNode
            {
                Id = ROOT_ID, Name = "异能", Level = -1, ParentId = null,
                X = -NODE_ROOT_W / 2f, Y = -NODE_ROOT_H / 2f,
                W = NODE_ROOT_W, H = NODE_ROOT_H,
                Color = new Color(0.95f, 0.85f, 0.55f, 1f)
            };
            layout.Nodes.Add(root);

            float ang = 0f;   // 当前扇区起始角（度，0=正右，顺时针；绘制时 -90° 偏移使顶部为起点）
            foreach (string cat in cats)
            {
                if (!registry.TryGetValue(cat, out var subs)) continue;
                int catLeaves = 0;
                foreach (var kv in subs)
                    if (kv.Value != null) catLeaves += kv.Value.Count;
                if (catLeaves == 0) continue;

                float catSector = catLeaves / (float)totalLeaves * 360f;
                float catCenter = ang + catSector / 2f;

                // 大类节点（环 RADIUS_CAT）
                var catNode = new ProtoTreeNode
                {
                    Id = cat, Name = cat, Level = 0, ParentId = ROOT_ID,
                    W = NODE_CAT_W, H = NODE_CAT_H,
                    Color = GetCategoryColor(cat)
                };
                SetRadialPos(catNode, catCenter, RADIUS_CAT);
                layout.Nodes.Add(catNode);

                // 系内小类：在系扇区内按小类异能数占比细分角
                float subAng = ang;
                foreach (var kv in subs)
                {
                    var subList = kv.Value;
                    if (subList == null || subList.Count == 0) continue;
                    string subName = kv.Key;
                    float subSector = subList.Count / (float)catLeaves * catSector;
                    float subCenter = subAng + subSector / 2f;

                    // 小类节点（环 RADIUS_SUB）
                    var subNode = new ProtoTreeNode
                    {
                        Id = cat + "::" + subName, Name = subName, Level = 1, ParentId = cat,
                        W = NODE_SUB_W, H = NODE_SUB_H,
                        Color = GetCategoryColor(cat) * new Color(0.85f, 0.85f, 0.85f, 1f)
                    };
                    SetRadialPos(subNode, subCenter, RADIUS_SUB);
                    layout.Nodes.Add(subNode);

                    // 异能叶：以小类中心角为中心、扇区随叶数自适应（叶数×12°，夹 50°~110°）均布
                    // （环 RADIUS_LEAF；默认折叠由渲染层按展开状态显隐，坐标始终预计算——
                    //   新增异能自动并入扇区，无需重新设计布局）
                    float leafSpan = Mathf.Clamp(subList.Count * 12f, 50f, 110f);
                    for (int i = 0; i < subList.Count; i++)
                    {
                        var def = subList[i];
                        if (def == null) continue;
                        float leafAngle = subCenter - leafSpan / 2f
                                        + (i + 0.5f) / subList.Count * leafSpan;
                        var leaf = new ProtoTreeNode
                        {
                            Id = def.Id, Name = def.NameZh, Level = 2,
                            ParentId = cat + "::" + subName,
                            W = NODE_LEAF_W, H = NODE_LEAF_H,
                            Def = def, Color = GetCategoryColor(cat)
                        };
                        SetRadialPos(leaf, leafAngle, RADIUS_LEAF);
                        layout.Nodes.Add(leaf);
                    }
                    subAng += subSector;
                }
                ang += catSector;
            }

            // ---- 阶段2：可见层包围盒（根+系+小类；异能叶折叠不计入，避免全树 Fit 过小）----
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var n in layout.Nodes)
            {
                if (n.Level == 2) continue;   // 异能叶默认折叠
                x0 = Mathf.Min(x0, n.X); y0 = Mathf.Min(y0, n.Y);
                x1 = Mathf.Max(x1, n.X + n.W); y1 = Mathf.Max(y1, n.Y + n.H);
            }
            if (layout.Nodes.Count == 0) { layout.Bounds = new Rect(0, 0, 1, 1); return layout; }
            layout.Bounds = Rect.MinMaxRect(x0 - 40f, y0 - 40f, x1 + 40f, y1 + 40f);
            return layout;
        }

        /// <summary>把节点中心放到极坐标 (radius, angleDeg) 处（左上角对齐）</summary>
        private static void SetRadialPos(ProtoTreeNode node, float angleDeg, float radius)
        {
            float rad = (angleDeg - 90f) * Mathf.Deg2Rad;   // -90°：顶部为 0 起点，顺时针绕
            float cx = Mathf.Cos(rad) * radius;
            float cy = Mathf.Sin(rad) * radius;
            node.X = cx - node.W / 2f;
            node.Y = cy - node.H / 2f;
        }

        /// <summary>根节点 ID（异能）</summary>
        public const string ROOT_ID = "__root__";

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
                case "星辰系": return new Color(0.50f, 0.55f, 0.90f, 1f);   // 星辰蓝紫
                case "血族系": return new Color(0.80f, 0.25f, 0.30f, 1f);   // 血族血红
                case "神话系": return new Color(0.90f, 0.72f, 0.35f, 1f);   // 神话金橙
                default: return new Color(0.6f, 0.6f, 0.6f, 1f);
            }
        }
    }
}
