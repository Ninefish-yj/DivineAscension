// ============================================================
// 召唤引擎（v0.18.99 召唤体系）
// 单一职责：召唤物生成 + 生命周期管理（与异能数据/效果引擎解耦）。
// 效果入口：PrototypeEffectEngine.TryApplyAttackTag → SummonEngine.TrySummon；
// 帧驱动：CombatFxFramePatch.Postfix → SummonEngine.Tick（与演出层同节奏）。
// 实现：按异能小类映射原版生物 asset（wolf/bear/bee/crab/ghost），
//       在目标相邻格生成野生单位并 addAggro 当前目标，短暂作战后消散，
//       防世界污染与存档膨胀（召唤物不写任何模组数据）。
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Code.Realm.Prototype
{
    public static class SummonEngine
    {
        /// <summary>召唤物同时存活上限（防堆叠刷屏）</summary>
        public const int MAX_ACTIVE = 4;

        /// <summary>召唤物存活时长（秒，现实时间，与特效层同节奏）</summary>
        private const float LIFETIME = 8f;

        private sealed class SummonedUnit
        {
            public Actor Actor;
            public float Remaining;
        }

        private static readonly List<SummonedUnit> _active = new List<SummonedUnit>();

        /// <summary>召唤异能小类 → 原版生物 asset id（大众认知映射：兽灵/虫潮/亡灵/幻影/巨兽/龙裔/元素/圣灵）</summary>
        private static string PickAsset(string subCategory)
        {
            if (string.IsNullOrEmpty(subCategory)) return "wolf";
            switch (subCategory)
            {
                case "兽灵": return RandomUnit("wolf", "bear");
                case "巨兽": return "bear";
                case "龙裔": return "bear";                       // 原版无龙，用熊体量最大
                case "虫潮": return RandomUnit("bee", "crab");
                case "亡灵": return "ghost";
                case "幻影": return RandomUnit("bee", "ghost");
                case "元素": return RandomUnit("bee", "crab");    // 元素体用原版飞行/甲壳生物代形
                case "圣灵": return RandomUnit("ghost", "bee");
                default: return "wolf";
            }
        }

        private static string RandomUnit(string a, string b)
        {
            return UnityEngine.Random.value < 0.5f ? a : b;
        }

        /// <summary>
        /// 召唤入口（攻击标签触发）。
        /// 在目标相邻格生成一只野生召唤物，专注攻击当前目标。
        /// </summary>
        public static void TrySummon(Actor owner, Actor target, PrototypeDef def)
        {
            if (owner == null || target == null || def == null) return;
            if (!owner.isAlive() || !target.isAlive()) return;
            if (World.world == null || World.world.units == null) return;
            if (_active.Count >= MAX_ACTIVE) return;

            WorldTile tile = FindAdjacentTile(target.current_position);
            if (tile == null) return;

            try
            {
                string asset = PickAsset(def.SubCategory);
                Actor summoned = World.world.units.spawnNewUnit(asset, tile, false, false, 0.6f, null);
                if (summoned == null) return;

                // 标记 + 登记生命周期（原版自定义数据容器为 float）
                if (summoned.data == null) summoned.data = new ActorData();
                if (summoned.data.custom_data_float == null)
                    summoned.data.custom_data_float = new CustomDataContainer<float>();
                summoned.data.custom_data_float["ds_summoned"] = 1f;
                _active.Add(new SummonedUnit { Actor = summoned, Remaining = LIFETIME });

                // 专注攻击当前目标（野生生物会与周围卷入战斗，短寿命限幅影响）
                try { summoned.addAggro(target); } catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[Summon] aggro: " + dsEx.Message); }
            }
            catch (System.Exception dsEx)
            {
                Code.Core.DSDebug.Verbose("[Summon] 召唤失败: " + dsEx.Message);
            }
        }

        /// <summary>帧驱动：倒计时，超时消散召唤物（移出世界，不走死亡链）</summary>
        public static void Tick(float dt)
        {
            if (World.world == null)
            {
                if (_active.Count > 0) _active.Clear();
                return;
            }
            if (_active.Count == 0) return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var s = _active[i];
                if (s.Actor == null || !s.Actor.isAlive())
                {
                    _active.RemoveAt(i);
                    continue;
                }
                s.Remaining -= dt;
                if (s.Remaining <= 0f)
                {
                    try { s.Actor.removeByMetamorphosis(); } catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[Summon] 消散: " + dsEx.Message); }
                    _active.RemoveAt(i);
                }
            }
        }

        /// <summary>世界切换/卸载：清空召唤登记（召唤物随世界销毁）</summary>
        public static void ClearAll()
        {
            _active.Clear();
        }

        /// <summary>目标相邻格找可站立点（过滤液体/边界，最多尝试 12 次）</summary>
        private static WorldTile FindAdjacentTile(Vector2 pos)
        {
            try
            {
                for (int i = 0; i < 12; i++)
                {
                    int dx = UnityEngine.Random.Range(-2, 3);
                    int dy = UnityEngine.Random.Range(-2, 3);
                    if (dx == 0 && dy == 0) continue;
                    WorldTile t = World.world.GetTileSimple((int)(pos.x + dx), (int)(pos.y + dy));
                    if (t != null && !t.is_liquid) return t;
                }
            }
            catch (System.Exception) { }
            return null;
        }
    }
}
