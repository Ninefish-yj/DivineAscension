using System;
using Code.Core;
using Code.Data;

namespace Code.Realm
{
    /// <summary>
    /// 原版系统联动桥接：宗教/灾难/经济
    /// 全部用原版公开API，不用反射
    /// </summary>
    public static class VanillaIntegration
    {
        private static bool _initialized = false;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            DSDebug.Verbose("[VanillaIntegration] 原版系统联动桥接初始化完成");
        }

        // === 宗教联动：10阶以上异能者自动创建宗教 ===
        public static void TryCreateReligion(Actor actor)
        {
            try
            {
                if (actor == null || !actor.isAlive()) return;
                int tier = CultivationData.GetRealmTier(actor);
                if (tier < 10) return; // 10阶使徒级以上才创教

                var rm = MapBox.instance.religions;
                if (rm == null) return;

                // 检查是否已有宗教
                if (actor.religion != null) return;

                var religion = rm.newReligion(actor, true);
                if (religion != null)
                {
                    DSDebug.Verbose($"[VanillaIntegration] {actor.getName()} 创建了宗教");
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[VanillaIntegration] 创教失败: {e.Message}");
            }
        }

        // === 灾难联动：9阶天灾级技能调用原版灾难 ===
        public static void SpawnTornado(WorldTile tile)
        {
            try
            {
                var dl = AssetManager.disasters;
                if (dl == null) return;
                var tornadoAsset = AssetManager.disasters.get("tornado");
                if (tornadoAsset != null)
                {
                    dl.spawnTornado(tornadoAsset);
                    DSDebug.Verbose($"[VanillaIntegration] 龙卷风已生成");
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[VanillaIntegration] 生成龙卷风失败: {e.Message}");
            }
        }

        public static void SpawnMeteorite(WorldTile tile)
        {
            try
            {
                if (tile == null) return;
                Meteorite.spawnMeteoriteDisaster(tile, null);
                DSDebug.Verbose($"[VanillaIntegration] 陨石已生成");
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[VanillaIntegration] 生成陨石失败: {e.Message}");
            }
        }

        public static void SpawnEarthquake(WorldTile tile)
        {
            try
            {
                var dl = AssetManager.disasters;
                if (dl == null) return;
                var quakeAsset = AssetManager.disasters.get("small_earthquake");
                if (quakeAsset != null)
                {
                    dl.spawnSmallEarthquake(quakeAsset);
                    DSDebug.Verbose($"[VanillaIntegration] 地震已生成");
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[VanillaIntegration] 生成地震失败: {e.Message}");
            }
        }

        // === 年度信仰结算：登神者收集信徒贡献的信仰能量 ===
        public static void AnnualFaithSettlement()
        {
            try
            {
                if (World.world == null || World.world.units == null) return;

                // 遍历所有11阶登神级单位
                foreach (var actor in World.world.units.units_only_alive)
                {
                    if (actor == null || !actor.isAlive()) continue;
                    int tier = CultivationData.GetRealmTier(actor);
                    if (tier != 11) continue; // 只有登神级收集信仰
                    if (actor.religion == null) continue; // 没创教不收集

                    // 统计宗教信徒数量
                    int followerCount = actor.religion.countUnits();
                    if (followerCount <= 0) continue;

                    // 每个信徒贡献1信仰能量
                    int faithEnergy = followerCount;
                    CultivationData.SetEnergy(actor, CultivationData.GetEnergy(actor) + faithEnergy);

                    DSDebug.Verbose($"[VanillaIntegration] {actor.getName()} 收集信仰能量: +{faithEnergy} ({followerCount}个信徒)");
                }
            }
            catch (Exception e)
            {
                DSDebug.Verbose($"[VanillaIntegration] 年度信仰结算失败: {e.Message}");
            }
        }

        // === 检查登神者是否满足突破真神级的信仰条件 ===
        public static bool HasEnoughFaithForGodhood(Actor actor)
        {
            try
            {
                if (actor == null || actor.religion == null) return false;
                int followerCount = actor.religion.countUnits();
                // 至少有200个信徒才能突破真神级
                return followerCount >= 200;
            }
            catch
            {
                return false;
            }
        }
    }
}
