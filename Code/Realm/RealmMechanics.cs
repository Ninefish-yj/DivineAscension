// ============================================================

using System;
using System.Collections.Generic;
using Code.UI;
using Code.Core;
using Code.Data;
using UnityEngine;
using ai;

namespace Code.Realm
{
    /// <summary>
    /// 境界独特机制管理器
    /// 提供11阶基因化身召唤和13阶神眷等交互型机制
    /// </summary>
    public static class RealmMechanics
    {
        // ============================================================
        //  基因化身系统（11阶 登神级）
        // ============================================================

        /// <summary>基因化身数据记录</summary>
        private class ElementAvatarRecord
        {
            public long AvatarId;       // 化身单位ID
            public long OwnerId;        // 召唤者ID
            public float SpawnTime;     // 召唤时的世界时间（秒）
            public string ElementName;  // 化身基因名称
        }

        /// <summary>活跃的基因化身列表（用于回收追踪）</summary>
        private static readonly List<ElementAvatarRecord> _activeAvatars = new List<ElementAvatarRecord>();

        /// <summary>基因化身持续时间（游戏秒，30秒=0.5年）</summary>
        private const float AVATAR_DURATION = 30f;

        /// <summary>每个登神级同时最多存在的化身数量</summary>
        private const int MAX_AVATARS_PER_OWNER = 1;

        /// <summary>
        /// 11阶登神级：召唤基因化身独立作战
        /// 在召唤者位置生成一个强力单位，添加strong/immortal等特质，持续30秒后自动回收。
        /// 注意：使用反射调用原版单位生成API，失败时静默处理。
        /// </summary>
        public static bool SummonElementAvatar(Actor owner)
        {
            if (owner == null || !owner.isAlive()) return false;
            int tier = CultivationData.GetRealmTier(owner);
            if (tier < 11)
            {
                DSDebug.Warning($"[GeneCultivation] 基因化身召唤失败：{owner.getName()} 境界不足（需要11阶）");
                return false;
            }

            try
            {
                // 检查该召唤者是否已有活跃化身（限制同时存在数量）
                int ownerAvatarCount = 0;
                foreach (var rec in _activeAvatars)
                {
                    if (rec.OwnerId == owner.getID()) ownerAvatarCount++;
                }
                if (ownerAvatarCount >= MAX_AVATARS_PER_OWNER)
                {
                    DSDebug.Verbose($"[GeneCultivation] {owner.getName()} 已存在基因化身，无法重复召唤");
                    return false;
                }

                // 检查能量消耗
                float energyCost = 500f;
                float currentEnergy = CultivationData.GetEnergy(owner);
                if (currentEnergy < energyCost)
                {
                    DSDebug.Verbose($"[GeneCultivation] {owner.getName()} 能量不足，无法召唤基因化身（需要{energyCost}）");
                    return false;
                }
                CultivationData.SetEnergy(owner, currentEnergy - energyCost);

                // 获取召唤者的主要基因（用于化身命名）
                string elementName = GetOwnerPrimaryElement(owner);

                // 在召唤者附近生成化身单位
                Actor avatar = SpawnAvatarUnit(owner, elementName);
                if (avatar == null)
                {
                    DSDebug.Verbose($"[GeneCultivation] 基因化身生成失败：{owner.getName()}");
                    return false;
                }

                // 给化身添加强力特质
                ApplyAvatarTraits(avatar, tier);

                // 记录化身数据用于回收
                var record = new ElementAvatarRecord
                {
                    AvatarId = avatar.getID(),
                    OwnerId = owner.getID(),
                    SpawnTime = World.world != null ? (float)World.world.getCurWorldTime() : 0f,
                    ElementName = elementName
                };
                _activeAvatars.Add(record);

                // 通知
                DSNotificationManager.NotifyInfo(string.Format(UILocalization.Get("realm_element_manifest"), owner.getName(), elementName));
                DSDebug.Verbose($"[GeneCultivation] {owner.getName()} 召唤基因化身【{elementName}】成功，化身ID={avatar.getID()}");
                return true;
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 基因化身召唤异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 生成化身单位（使用反射调用原版API）
        /// 优先使用createNewUnit，失败则尝试spawnActor等备选方法
        /// </summary>
        private static Actor SpawnAvatarUnit(Actor owner, string elementName)
        {
            if (World.world == null || World.world.units == null) return null;

            try
            {
                float x = owner.current_position.x + UnityEngine.Random.Range(-2f, 2f);
                float y = owner.current_position.y + UnityEngine.Random.Range(-2f, 2f);
                WorldTile tile = World.world.GetTileSimple((int)x, (int)y);
                if (tile == null) return null;

                // 正确API：units.createNewUnit(unitAssetId, tile) —— 参考西幻世界 SummonerSkill.cs
                string raceId = owner.asset != null ? owner.asset.id : "human";
                Actor result = World.world.units.createNewUnit(raceId, tile);
                if (result != null) return result;

                DSDebug.Warning("[GeneCultivation] 基因化身生成：createNewUnit返回null");
                return null;
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 基因化身生成异常: {e.Message}");
                return null;
            }
        }

        /// <summary>给化身单位添加强力特质（基于召唤者境界）</summary>
        private static void ApplyAvatarTraits(Actor avatar, int ownerTier)
        {
            try
            {
                // 基础强力特质
                if (!avatar.hasTrait("strong")) avatar.addTrait("strong", false);
                if (!avatar.hasTrait("fast")) avatar.addTrait("fast", false);
                if (!avatar.hasTrait("immortal")) avatar.addTrait("immortal", false);
                if (!avatar.hasTrait("blessed")) avatar.addTrait("blessed", false);

                // 高境界化身额外特质
                if (ownerTier >= 12)
                {
                    if (!avatar.hasTrait("genius")) try { avatar.addTrait("genius", false); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] ApplyAvatarTraits 异常: " + dsEx.Message); }
                }

                // 标记化身为临时单位（用于识别和回收）
                CultivationData.SetCooldown(avatar, "element_avatar", 1);
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 化身特质添加失败: {e.Message}");
            }
        }

        /// <summary>获取召唤者的主要基因名称（用于化身命名）</summary>
        private static string GetOwnerPrimaryElement(Actor owner)
        {
            try
            {
                var elements = CultivationData.GetUnlockedElements(owner);
                if (elements != null && elements.Count > 0)
                {
                    // 取最后解锁的基因作为主要基因
                    string lastElem = elements[elements.Count - 1];
                    var elemDef = ElementDef.GetById(lastElem);
                    if (elemDef != null) return elemDef.NameZh;
                }
            }
            catch (System.Exception dsEx) { Code.Core.DSDebug.Verbose("[GeneCultivation] GetOwnerPrimaryElement 异常: " + dsEx.Message); }
            return UILocalization.Get("graph_attr_energy");
        }

        /// <summary>
        /// 年度回收过期的基因化身（由RealmAbilities.OnAnnualTick或AnnualTickManager调用）
        /// 检查每个活跃化身是否超过持续时间，超过则移除单位并清理记录
        /// </summary>
        public static void CleanupExpiredAvatars()
        {
            if (_activeAvatars.Count == 0) return;
            if (World.world == null) return;

            try
            {
                float currentTime = (float)World.world.getCurWorldTime();
                var toRemove = new List<ElementAvatarRecord>();

                foreach (var record in _activeAvatars)
                {
                    // 超过持续时间则回收
                    if (currentTime - record.SpawnTime >= AVATAR_DURATION)
                    {
                        // 查找并移除化身单位
                        Actor avatar = SystemManagerExtensions.FindActorById(record.AvatarId);
                        if (avatar != null && avatar.isAlive())
                        {
                            try
                            {
                                // 移除化身（使用反射调用removeUnit或直接kill）
                                RemoveAvatarUnit(avatar);
                            }
                            catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] CleanupExpiredAvatars 异常: " + dsEx.Message); }
                        }
                        toRemove.Add(record);
                    }
                }

                // 清理记录
                foreach (var rec in toRemove)
                {
                    _activeAvatars.Remove(rec);
                }

                if (toRemove.Count > 0)
                {
                    DSDebug.Verbose($"[GeneCultivation] 基因化身回收：{toRemove.Count}个化身已到期回收");
                }
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 基因化身回收异常: {e.Message}");
            }
        }

        /// <summary>移除化身单位（使用反射调用原版API，失败则kill）</summary>
        private static void RemoveAvatarUnit(Actor avatar)
        {
            try
            {
                // 方法1：尝试 units.removeUnit
                var units = World.world.units;
                var removeMethod = units.GetType().GetMethod("removeUnit",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (removeMethod != null)
                {
                    removeMethod.Invoke(units, new object[] { avatar });
                    return;
                }

                // 方法2：直接kill单位
                avatar.getHit(99999f, false, AttackType.Other, null, false, false, true);
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 化身单位移除失败: {e.Message}");
            }
        }

        // ============================================================
        //  神眷系统（13阶 超神级）
        // ============================================================

        /// <summary>神眷者数据记录</summary>
        private class DivineBlessingRecord
        {
            public long BlessedId;     // 神眷者单位ID
            public long GodId;         // 赐予神眷的真神ID
            public float GrantedTime;  // 赐予时间
        }

        /// <summary>活跃神眷者列表</summary>
        private static readonly List<DivineBlessingRecord> _divineBlessings = new List<DivineBlessingRecord>();

        /// <summary>全局神眷者上限（避免神眷泛滥）</summary>
        private const int MAX_DIVINE_BLESSINGS = 5;

        /// <summary>
        /// 13阶超神级：赐予神眷
        /// 指定一个单位获得"神眷者"状态：全属性+30%（strong/fast/genius/blessed），不朽。
        /// 可通过调试面板或单位窗口调用。
        /// </summary>
        public static bool GrantDivineBlessing(Actor god, Actor target)
        {
            if (god == null || !god.isAlive()) return false;
            if (target == null || !target.isAlive()) return false;

            int godTier = CultivationData.GetRealmTier(god);
            if (godTier < 13)
            {
                DSDebug.Warning($"[GeneCultivation] 神眷赐予失败：{god.getName()} 不是真神（需要13阶）");
                return false;
            }

            try
            {
                // 检查目标是否已是神眷者
                if (IsDivineBlessed(target))
                {
                    DSDebug.Verbose($"[GeneCultivation] {target.getName()} 已经是神眷者");
                    return false;
                }

                // 检查全局神眷上限
                if (_divineBlessings.Count >= MAX_DIVINE_BLESSINGS)
                {
                    DSNotificationManager.NotifyWarning(string.Format(UILocalization.Get("divine_blessing_full"), MAX_DIVINE_BLESSINGS));
                    return false;
                }

                // 赐予神眷特质（全属性+30%的近似体现）
                if (!target.hasTrait("strong")) target.addTrait("strong", false);
                if (!target.hasTrait("fast")) target.addTrait("fast", false);
                if (!target.hasTrait("genius")) try { target.addTrait("genius", false); } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] GrantDivineBlessing 异常: " + dsEx.Message); }
                if (!target.hasTrait("blessed")) target.addTrait("blessed", false);
                if (!target.hasTrait("immortal")) target.addTrait("immortal", false);

                // 标记神眷状态
                CultivationData.SetCooldown(target, "divine_blessed", 1);

                // v0.18.31 持久化所属神（供神眷者单位窗显示"所属神"；神名优先）
                try
                {
                    string godName = GetStoredDivineName(god);
                    if (string.IsNullOrEmpty(godName)) godName = god.getName();
                    CultivationData.EnsureCustomDataInitialized(target);
                    ActorDataAccessor.GetData(target).custom_data_string["divine_blessed_by"] = godName;
                }
                catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] 神眷归属记录异常: " + dsEx.Message); }

                // 记录
                var record = new DivineBlessingRecord
                {
                    BlessedId = target.getID(),
                    GodId = god.getID(),
                    GrantedTime = World.world != null ? (float)World.world.getCurWorldTime() : 0f
                };
                _divineBlessings.Add(record);

                // 全局通知 + 历史记录（宏大叙事+个人史诗风格）
                DSNotificationManager.NotifyMajor(UILocalization.Get("divine_blessing_link_title"), string.Format(UILocalization.Get("realm_divine_blessing_link"), god.getName(), target.getName()));
                DSEventManager.RecordEvent(
                    DSEventType.Divine,
                    UILocalization.Get("divine_blessing_grant_title"),
                    string.Format(UILocalization.Get("realm_divine_blessing_grant"), god.getName(), target.getName()),
                    target.getName(),
                    target.getID(), 0, target);

                DSDebug.Verbose($"[GeneCultivation] {god.getName()} 赐予 {target.getName()} 神眷！");
                return true;
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 神眷赐予异常: {e.Message}");
                return false;
            }
        }

        /// <summary>检查单位是否为神眷者</summary>
        public static bool IsDivineBlessed(Actor actor)
        {
            if (actor == null) return false;
            return CultivationData.GetCooldown(actor, "divine_blessed") > 0;
        }

        /// <summary>读取单位已存的神名（custom_data_string["divine_name"]，无则空串）（v0.18.31 称号/单位窗共用）</summary>
        public static string GetStoredDivineName(Actor actor)
        {
            try
            {
                if (actor == null) return "";
                var data = ActorDataAccessor.GetData(actor);
                if (data == null || data.custom_data_string == null) return "";
                string name;
                if (data.custom_data_string.TryGetValue("divine_name", out name)) return name ?? "";
                return "";
            }
            catch { return ""; }
        }

        /// <summary>读取神眷者所属神（神名；旧存档未记录则空串）（v0.18.31）</summary>
        public static string GetBlessedByGod(Actor actor)
        {
            try
            {
                if (actor == null || !IsDivineBlessed(actor)) return "";
                var data = ActorDataAccessor.GetData(actor);
                if (data == null || data.custom_data_string == null) return "";
                string god;
                if (data.custom_data_string.TryGetValue("divine_blessed_by", out god)) return god ?? "";
                return "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// 年度清理：检查神眷者是否存活，死亡则移除记录
        /// </summary>
        public static void CleanupDivineBlessings()
        {
            if (_divineBlessings.Count == 0) return;

            try
            {
                _divineBlessings.RemoveAll(record =>
                {
                    Actor blessed = SystemManagerExtensions.FindActorById(record.BlessedId);
                    return blessed == null || !blessed.isAlive();
                });
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 神眷清理异常: {e.Message}");
            }
        }

        // ============================================================
        //  门徒/改造者系统（13阶 超神级）
        //  基因层面的延伸：真神主动基因改造特定单位，获得基因亲和+境界加速
        //  与神眷的区别：神眷是"点化"（全属性+不朽），门徒是"基因改造"（基因亲和+修炼加速）
        // ============================================================

        /// <summary>

        // ============================================================
        //  统一年度清理入口
        // ============================================================

        /// <summary>
        /// 年度机制清理（由AnnualTickManager或RealmAbilities.OnAnnualTick调用）
        /// 统一回收过期化身和清理死亡神眷者记录
        /// </summary>
        public static void OnAnnualTick()
        {
            try
            {
                CleanupExpiredAvatars();
                CleanupDivineBlessings();
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 年度机制清理异常: {e.Message}");
            }
        }
        // ============================================================
        //  真神系统（13阶 超神级）：神名+氏族+神眷+宗教
        // ============================================================

        /// <summary>神名词库（96个，按主要基因染色体分类，每条染色体12个）</summary>
        private static readonly string[] DivineNames = new string[]
        {
            // 物质染色体（0-11）
            UILocalization.Get("god_name_matter_1"), UILocalization.Get("god_name_matter_2"), UILocalization.Get("god_name_matter_3"), UILocalization.Get("god_name_matter_4"), UILocalization.Get("god_name_matter_5"), UILocalization.Get("god_name_matter_6"),
            UILocalization.Get("god_name_matter_7"), UILocalization.Get("god_name_matter_8"), UILocalization.Get("god_name_matter_9"), UILocalization.Get("god_name_matter_10"), UILocalization.Get("god_name_matter_11"), UILocalization.Get("god_name_matter_12"),
            // 生命染色体（12-23）
            UILocalization.Get("god_name_life_1"), UILocalization.Get("god_name_life_2"), UILocalization.Get("god_name_life_3"), UILocalization.Get("god_name_life_4"), UILocalization.Get("god_name_life_5"), UILocalization.Get("god_name_life_6"),
            UILocalization.Get("god_name_life_7"), UILocalization.Get("god_name_life_8"), UILocalization.Get("god_name_life_9"), UILocalization.Get("god_name_life_10"), UILocalization.Get("god_name_life_11"), UILocalization.Get("god_name_life_12"),
            // 能量染色体（24-35）
            UILocalization.Get("god_name_energy_1"), UILocalization.Get("god_name_energy_2"), UILocalization.Get("god_name_energy_3"), UILocalization.Get("god_name_energy_4"), UILocalization.Get("god_name_energy_5"), UILocalization.Get("god_name_energy_6"),
            UILocalization.Get("god_name_energy_7"), UILocalization.Get("god_name_energy_8"), UILocalization.Get("god_name_energy_9"), UILocalization.Get("god_name_energy_10"), UILocalization.Get("god_name_energy_11"), UILocalization.Get("god_name_energy_12"),
            // 力场染色体（36-47）
            UILocalization.Get("god_name_force_1"), UILocalization.Get("god_name_force_2"), UILocalization.Get("god_name_force_3"), UILocalization.Get("god_name_force_4"), UILocalization.Get("god_name_force_5"), UILocalization.Get("god_name_force_6"),
            UILocalization.Get("god_name_force_7"), UILocalization.Get("god_name_force_8"), UILocalization.Get("god_name_force_9"), UILocalization.Get("god_name_force_10"), UILocalization.Get("god_name_force_11"), UILocalization.Get("god_name_force_12"),
            // 时空染色体（48-59）
            UILocalization.Get("god_name_time_1"), UILocalization.Get("god_name_time_2"), UILocalization.Get("god_name_time_3"), UILocalization.Get("god_name_time_4"), UILocalization.Get("god_name_time_5"), UILocalization.Get("god_name_time_6"),
            UILocalization.Get("god_name_time_7"), UILocalization.Get("god_name_time_8"), UILocalization.Get("god_name_time_9"), UILocalization.Get("god_name_time_10"), UILocalization.Get("god_name_time_11"), UILocalization.Get("god_name_time_12"),
            // 精神染色体（60-71）
            UILocalization.Get("god_name_spirit_1"), UILocalization.Get("god_name_spirit_2"), UILocalization.Get("god_name_spirit_3"), UILocalization.Get("god_name_spirit_4"), UILocalization.Get("god_name_spirit_5"), UILocalization.Get("god_name_spirit_6"),
            UILocalization.Get("god_name_spirit_7"), UILocalization.Get("god_name_spirit_8"), UILocalization.Get("god_name_spirit_9"), UILocalization.Get("god_name_spirit_10"), UILocalization.Get("god_name_spirit_11"), UILocalization.Get("god_name_spirit_12"),
            // 信息染色体（72-83）
            UILocalization.Get("god_name_info_1"), UILocalization.Get("god_name_info_2"), UILocalization.Get("god_name_info_3"), UILocalization.Get("god_name_info_4"), UILocalization.Get("god_name_info_5"), UILocalization.Get("god_name_info_6"),
            UILocalization.Get("god_name_info_7"), UILocalization.Get("god_name_info_8"), UILocalization.Get("god_name_info_9"), UILocalization.Get("god_name_info_10"), UILocalization.Get("god_name_info_11"), UILocalization.Get("god_name_info_12"),
            // 概念染色体（84-95）
            UILocalization.Get("god_name_concept_1"), UILocalization.Get("god_name_concept_2"), UILocalization.Get("god_name_concept_3"), UILocalization.Get("god_name_concept_4"), UILocalization.Get("god_name_concept_5"), UILocalization.Get("god_name_concept_6"),
            UILocalization.Get("god_name_concept_7"), UILocalization.Get("god_name_concept_8"), UILocalization.Get("god_name_concept_9"), UILocalization.Get("god_name_concept_10"), UILocalization.Get("god_name_concept_11"), UILocalization.Get("god_name_concept_12")
        };

        /// <summary>异能标签 → 神名组映射（v0.18.32：神名与异能相符，大众认知；组号 0物质/1生命/2能量/3力场/4时空/5精神/6信息/7概念）</summary>
        private static readonly Dictionary<Code.Realm.Prototype.PrototypeTag, int> TagToDivineGroup = new Dictionary<Code.Realm.Prototype.PrototypeTag, int>
        {
            // ---- 攻击标签 ----
            { Code.Realm.Prototype.PrototypeTag.Burn, 2 },      // 灼烧 → 能量（炽阳/烈焰）
            { Code.Realm.Prototype.PrototypeTag.Plasma, 2 },    // 等离子 → 能量
            { Code.Realm.Prototype.PrototypeTag.Frost, 0 },     // 寒冰 → 物质（寒冰帝君）
            { Code.Realm.Prototype.PrototypeTag.Frostbite, 0 }, // 霜冻 → 物质
            { Code.Realm.Prototype.PrototypeTag.Slow, 4 },      // 减速 → 时空（时间之主）
            { Code.Realm.Prototype.PrototypeTag.TimeSlow, 4 },  // 时缓 → 时空
            { Code.Realm.Prototype.PrototypeTag.Stun, 3 },      // 麻痹 → 力场（磁场帝君）
            { Code.Realm.Prototype.PrototypeTag.Knockback, 3 }, // 击退 → 力场（斥力）
            { Code.Realm.Prototype.PrototypeTag.Gravity, 3 },   // 重力 → 力场（引力之主）
            { Code.Realm.Prototype.PrototypeTag.Berserk, 3 },   // 狂暴 → 力场（力场编织者）
            { Code.Realm.Prototype.PrototypeTag.Crit, 3 },      // 暴击 → 力场
            { Code.Realm.Prototype.PrototypeTag.Chain, 3 },     // 溅射 → 力场（空间扩散）
            { Code.Realm.Prototype.PrototypeTag.ArmorBreak, 3 },// 破甲 → 力场（空间撕裂）
            { Code.Realm.Prototype.PrototypeTag.MultiShot, 3 }, // 多重 → 力场（爆发）
            { Code.Realm.Prototype.PrototypeTag.Poison, 7 },    // 毒蚀 → 概念（熵/混沌）
            { Code.Realm.Prototype.PrototypeTag.Curse, 7 },     // 诅咒 → 概念
            { Code.Realm.Prototype.PrototypeTag.Aging, 7 },     // 衰老 → 概念（熵增）
            { Code.Realm.Prototype.PrototypeTag.Weakness, 7 },  // 虚弱 → 概念
            { Code.Realm.Prototype.PrototypeTag.Execute, 7 },   // 致死 → 概念（虚无之神）
            { Code.Realm.Prototype.PrototypeTag.Lifesteal, 1 }, // 吸血 → 生命（血脉之源）
            { Code.Realm.Prototype.PrototypeTag.Drain, 1 },     // 汲取 → 生命
            { Code.Realm.Prototype.PrototypeTag.Blood, 1 },     // 血祭 → 生命
            { Code.Realm.Prototype.PrototypeTag.Heal, 1 },      // 治疗 → 生命（生命之母）
            { Code.Realm.Prototype.PrototypeTag.HealAlly, 1 },  // 治疗友军 → 生命
            { Code.Realm.Prototype.PrototypeTag.Regeneration, 1 }, // 再生 → 生命
            { Code.Realm.Prototype.PrototypeTag.Blessing, 1 },  // 祝福 → 生命（生机护佑）
            { Code.Realm.Prototype.PrototypeTag.Entangle, 1 },  // 束缚 → 生命（自然缠绕）
            { Code.Realm.Prototype.PrototypeTag.Soul, 5 },      // 灵魂 → 精神（灵魂之神）
            { Code.Realm.Prototype.PrototypeTag.Fear, 5 },      // 恐惧 → 精神
            { Code.Realm.Prototype.PrototypeTag.Telekinesis, 5 }, // 念力 → 精神（意念之主）
            { Code.Realm.Prototype.PrototypeTag.Illusion, 5 },  // 幻象 → 精神（梦境之王）
            { Code.Realm.Prototype.PrototypeTag.Invisibility, 5 }, // 隐身 → 精神（幻象隐遁）
            { Code.Realm.Prototype.PrototypeTag.Sleep, 5 },     // 睡眠 → 精神（梦境）
            { Code.Realm.Prototype.PrototypeTag.Clone, 5 },     // 分身 → 精神（幻象分身）
            // ---- 防守标签 ----
            { Code.Realm.Prototype.PrototypeTag.Shield, 0 },    // 护盾 → 物质（磐石/晶壁/山岳）
            { Code.Realm.Prototype.PrototypeTag.Thorns, 0 },    // 反弹 → 物质（晶壁）
            { Code.Realm.Prototype.PrototypeTag.ElementResist, 0 }, // 元素抗性 → 物质（坚固）
            { Code.Realm.Prototype.PrototypeTag.Vibration, 0 }, // 震动 → 物质（山岳）
            { Code.Realm.Prototype.PrototypeTag.Petrify, 0 },   // 石化 → 物质（磐石）
            { Code.Realm.Prototype.PrototypeTag.Dodge, 3 },     // 闪避 → 力场（虚空之神）
            { Code.Realm.Prototype.PrototypeTag.Magnetism, 3 }, // 磁力 → 力场（磁场帝君）
            { Code.Realm.Prototype.PrototypeTag.Sound, 3 },     // 声波 → 力场（音爆冲击）
            { Code.Realm.Prototype.PrototypeTag.Teleport, 3 },  // 瞬移 → 力场（空间之神）
            { Code.Realm.Prototype.PrototypeTag.Cleanse, 2 }    // 净化 → 能量（圣光/光辉）
        };

        /// <summary>按异能标签选神名组（v0.18.32：神名与异能相符；无异能/无匹配则回退基因染色体）</summary>
        private static int GetDivineNameGroupByPrototype(Actor god)
        {
            try
            {
                var def = Code.Realm.Prototype.PrototypeRegistry.GetForActor(god);
                if (def != null)
                {
                    int g;
                    if (TagToDivineGroup.TryGetValue(def.MainTag, out g)) return g;
                    if (TagToDivineGroup.TryGetValue(def.SubTag, out g)) return g;
                }
            }
            catch { }
            return GetDominantElementGroup(god);
        }

        /// <summary>按主要基因染色体获取神名（每条染色体12个，染色体索引0-7）</summary>
        private static string GetDivineNameByElementGroup(int elementGroup)
        {
            if (elementGroup < 0 || elementGroup > 7) elementGroup = UnityEngine.Random.Range(0, 8);
            int startIndex = elementGroup * 12;
            return DivineNames[startIndex + UnityEngine.Random.Range(0, 12)];
        }

        /// <summary>
        /// 分配全局唯一神名（v0.18.39 神名独属单位，跨存档不重名）
        /// 优先级：组内12名（异能相符）→ 全库96名（跨组借名，神名独属优先于同组）→ 全库耗尽随机兜底。
        /// 候选顺序洗牌，避免长期固定抽取前几名。
        /// </summary>
        private static string PickUniqueDivineName(int elementGroup)
        {
            try
            {
                // 旧档兼容：先扫描当前世界已有神名并登记，防止与旧档撞名
                DivineNameRegistry.EnsureSwept();

                // 1) 组内 12 名（随机顺序，保证神名与异能相符优先）
                int start = elementGroup * 12;
                int[] groupOrder = ShuffleOrder(12);
                for (int k = 0; k < 12; k++)
                {
                    string candidate = DivineNames[start + groupOrder[k]];
                    if (!DivineNameRegistry.IsTaken(candidate)) return candidate;
                }

                // 2) 组内全被占用 → 全库 96 名（跨组借名：独属性优先于同组相符）
                int[] allOrder = ShuffleOrder(96);
                for (int k = 0; k < 96; k++)
                {
                    string candidate = DivineNames[allOrder[k]];
                    if (!DivineNameRegistry.IsTaken(candidate)) return candidate;
                }

                // 3) 全库耗尽（96个神名全部有主，极罕见）：随机兜底并警告
                DSDebug.Warning("[GeneCultivation] 神名词库已全部被占用，回退随机分配");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 唯一神名分配异常: {e.Message}");
            }
            return GetDivineNameByElementGroup(elementGroup);
        }

        /// <summary>生成 [0, count) 的随机排列（Fisher-Yates 洗牌，避免 Linq 依赖）</summary>
        private static int[] ShuffleOrder(int count)
        {
            int[] order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }
            return order;
        }

        /// <summary>
        /// 真神诞生统一入口（突破13阶时调用）
        /// 1. 随机获得神名
        /// 2. 用神名创建氏族（真神为首领）
        /// 3. 自动选择2-4个11阶以下异能者作为神眷，加入氏族
        /// 4. 创建以真神为神明的宗教（氏族+文化模拟）
        /// </summary>
        public static void OnTrueGodAscended(Actor god)
        {
            if (god == null || !god.isAlive()) return;
            if (CultivationData.GetRealmTier(god) < 13) return;

            try
            {
                // 0. 切换到真神纪元（参考Avb虚无纪元实现，注册到era_library并切换）
                Code.Realm.RealmJudge.SwitchToTrueGodEra();

                // 1. 获得神名
                string divineName = GrantDivineName(god);
                if (string.IsNullOrEmpty(divineName))
                {
                    DSDebug.Warning("[GeneCultivation] 真神神名获取失败，跳过氏族/神眷/宗教创建");
                    return;
                }

                // 2. 创建氏族
                ClanData clan = CreateDivineClan(god, divineName);
                if (clan == null)
                {
                    DSDebug.Warning("[GeneCultivation] 真神氏族创建失败");
                }

                // 3. 自动选择神眷者（2-4个11阶以下异能者）
                int blessedCount = SelectAndBlessFollowers(god, clan);

                // 4. 宗教已在11阶登神级时创建（原版ReligionManager），13阶不重复创建

                // 神名组（与神名同一来源：异能标签优先，回退基因染色体），用于选择判词
                int elementGroup = GetDivineNameGroupByPrototype(god);

                // 使用判词系统（按神名组分类，每条染色体3套，简短有力有气势）
                string birthDesc = DSEventTexts.GetTrueGodBirthDesc(god.getName(), elementGroup);

                // 全局通知（宏大叙事风格，使用新判词）
                DSNotificationManager.NotifyMajor(DSEventTexts.GetTrueGodBirthTitle(),
                    birthDesc + string.Format(UILocalization.Get("realm_true_god_birth1"), divineName, blessedCount));
                DSEventManager.RecordEvent(
                    DSEventType.Divine,
                    DSEventTexts.GetTrueGodBirthTitle(),
                    birthDesc + string.Format(UILocalization.Get("realm_true_god_birth2"), divineName, blessedCount),
                    god.getName(),
                    god.getID(),
                    13
                );
                DSDebug.Verbose($"[GeneCultivation] 真神 {god.getName()} 立教：神名={divineName}，神眷者={blessedCount}");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 真神立教异常: {e.Message}");
            }
        }

        /// <summary>
        /// 随机获得神名（存储在 actor custom_data 中，避免重复）
        /// </summary>
                /// <summary>从已解锁基因中获取主要基因染色体</summary>
        private static int GetDominantElementGroup(Actor actor)
        {
            try
            {
                var unlocked = CultivationData.GetUnlockedElements(actor);
                if (unlocked == null || unlocked.Count == 0) return 3; // 默认体质染色体
                int[] groupCount = new int[8];
                foreach (string eid in unlocked)
                {
                    var elem = ElementDef.GetById(eid);
                    if (elem != null) groupCount[elem.Group]++;
                }
                int maxGroup = 0, maxCount = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (groupCount[i] > maxCount) { maxCount = groupCount[i]; maxGroup = i; }
                }
                return maxGroup;
            }
            catch { return 3; }
        }

private static string GrantDivineName(Actor god)
        {
            try
            {
                var data = ActorDataAccessor.GetData(god);
                // 检查是否已有神名
                string existing = null;
                if (CultivationData.GetCooldown(god, "divine_name") > 0 &&
                    data.custom_data_string != null &&
                    data.custom_data_string.TryGetValue("divine_name", out existing))
                {
                    if (!string.IsNullOrEmpty(existing)) return existing;
                }

                // 根据异能标签选择神名组（v0.18.32：神名与异能相符，如金刚=护盾→磐石/晶壁系；无匹配回退基因染色体）
                int elementGroup = GetDivineNameGroupByPrototype(god);
                // v0.18.39：神名全局唯一（跨存档独属）——组内优先（异能相符），组内耗尽跨组借名，全库耗尽才随机兜底
                string name = PickUniqueDivineName(elementGroup);

                // 存储神名
                try
                {
                    CultivationData.EnsureCustomDataInitialized(god);
                    ActorDataAccessor.GetData(god).custom_data_string["divine_name"] = name;
                    // 登记到跨存档注册表（神名独属该单位；登记失败不阻断神名授予）
                    if (!DivineNameRegistry.Claim(name, god))
                    {
                        DSDebug.Warning($"[GeneCultivation] 神名 {name} 登记失败（可能已被其他单位占用）");
                    }
                } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] GrantDivineName 异常: " + dsEx.Message); }
                CultivationData.SetCooldown(god, "divine_name", 1);

                return name;
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 神名获取异常: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 用神名创建氏族（真神作为氏族首领）
        /// 直接使用WorldBox public API
        /// </summary>
        private static ClanData CreateDivineClan(Actor god, string divineName)
        {
            try
            {
                ClanData clanData = new ClanData();
                long? nextId = Code.Core.DSReflectionHelper.GetNextId("clan");
                clanData.id = nextId ?? 0;
                clanData.name = divineName + UILocalization.Get("sect_god_cult");
                clanData.created_time = Code.Core.DSReflectionHelper.GetWorldTime();
                World.world.clans.loadObject(clanData);
                try { ActorDataAccessor.GetData(god).clan = clanData.id; } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] CreateDivineClan 异常: " + dsEx.Message); }
                DSDebug.Verbose($"[GeneCultivation] 真神 {god.getName()} 创建氏族：{divineName}神教");
                return clanData;
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 氏族创建异常: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 自动选择2-4个11阶以下的异能者作为神眷者，加入真神氏族
        /// </summary>
        private static int SelectAndBlessFollowers(Actor god, ClanData clan)
        {
            int blessedCount = 0;
            try
            {
                var candidates = new System.Collections.Generic.List<Actor>();
                foreach (var actor in World.world.units.units_only_alive)
                {
                    if (actor == null || !actor.isAlive()) continue;
                    if (actor.getID() == god.getID()) continue;
                    int tier = CultivationData.GetRealmTier(actor);
                    if (tier >= 1 && tier <= 10)
                    {
                        candidates.Add(actor);
                    }
                }

                if (candidates.Count == 0)
                {
                    DSDebug.Verbose("[GeneCultivation] 没有可点化的神眷者候选");
                    return 0;
                }

                int targetCount = UnityEngine.Random.Range(2, Mathf.Min(5, candidates.Count + 1));
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    var temp = candidates[i];
                    candidates[i] = candidates[j];
                    candidates[j] = temp;
                }

                for (int i = 0; i < targetCount && i < candidates.Count; i++)
                {
                    Actor follower = candidates[i];
                    if (GrantDivineBlessing(god, follower))
                    {
                        if (clan != null)
                        {
                            try { ActorDataAccessor.GetData(follower).clan = clan.id; } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] SelectAndBlessFollowers 异常: " + dsEx.Message); }
                        }
                        blessedCount++;
                    }
                }

                DSDebug.Verbose($"[GeneCultivation] 真神 {god.getName()} 点化 {blessedCount} 名神眷者");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 神眷选择异常: {e.Message}");
            }
            return blessedCount;
        }


    }
}





