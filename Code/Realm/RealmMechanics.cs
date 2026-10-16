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

        /// <summary>读取单位已存的神名（v0.18.41 神位制：东方名+西方名一一对应；v0.18.45 起同一语言内同时显示东西双名"东 · 西"；旧档单键自动兼容）</summary>
        public static string GetStoredDivineName(Actor actor)
        {
            try
            {
                if (actor == null) return "";
                var data = ActorDataAccessor.GetData(actor);
                if (data == null || data.custom_data_string == null) return "";
                string east, west;
                data.custom_data_string.TryGetValue("divine_name", out east);
                data.custom_data_string.TryGetValue("divine_name_west", out west);
                if (string.IsNullOrEmpty(east) && string.IsNullOrEmpty(west)) return "";
                if (string.IsNullOrEmpty(east)) return west;
                if (string.IsNullOrEmpty(west)) return east;
                return east + " · " + west;
            }
            catch { return ""; }
        }

        /// <summary>读取单位已存神名的完整对（东名+西名，供展示与兼容；无则空串）（v0.18.41）</summary>
        public static void GetStoredDivineNamePair(Actor actor, out string east, out string west)
        {
            east = west = "";
            try
            {
                if (actor == null) return;
                var data = ActorDataAccessor.GetData(actor);
                if (data == null || data.custom_data_string == null) return;
                data.custom_data_string.TryGetValue("divine_name", out east);
                data.custom_data_string.TryGetValue("divine_name_west", out west);
            }
            catch { }
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

        /// <summary>神名意象域（20个：v0.18.41 标签→意象域→神位制；每域6个神位，每神位=东方名+西方名一一对应）</summary>
        private static readonly string[] DivineDomains = new string[]
        {
            "frost", "stone", "bulwark", "blood", "heal", "nature",
            "flame", "light", "field", "space", "impact", "time",
            "mind", "dream", "phantom", "soul", "data", "curse", "decay", "death"
        };

        /// <summary>东方神名词库（20域×6神位，键 god_domain_{域}_{序号}；UILocalization 动态读，改 Locales 即生效）</summary>
        private static readonly string[][] DivineNamesByDomain = BuildDomainNameTable(false);

        /// <summary>西方神名词库（20域×6神位，与东方一一对应，键 god_domain_{域}_{序号}_west）</summary>
        private static readonly string[][] DivineNamesByDomainWest = BuildDomainNameTable(true);

        /// <summary>构建域词库表（域×神位）；风格键缺失时回退另一风格同名神位（v0.18.41）</summary>
        private static string[][] BuildDomainNameTable(bool west)
        {
            string[][] table = new string[DivineDomains.Length][];
            for (int d = 0; d < DivineDomains.Length; d++)
            {
                table[d] = new string[6];
                for (int i = 0; i < 6; i++)
                {
                    string key = "god_domain_" + DivineDomains[d] + "_" + (i + 1) + (west ? "_west" : "");
                    table[d][i] = UILocalization.Get(key);
                    if (string.IsNullOrEmpty(table[d][i]))
                    {
                        string fallbackKey = "god_domain_" + DivineDomains[d] + "_" + (i + 1) + (west ? "" : "_west");
                        table[d][i] = UILocalization.Get(fallbackKey);
                    }
                }
            }
            return table;
        }

        /// <summary>异能标签 → 神名意象域（v0.18.41：神名与具体异能标签相符；新异能加一行即接入，服务异能系统扩展）</summary>
        private static readonly Dictionary<Code.Realm.Prototype.PrototypeTag, int> TagToDivineDomain = new Dictionary<Code.Realm.Prototype.PrototypeTag, int>
        {
            // 冰霜域（frost）：寒冰/霜冻
            { Code.Realm.Prototype.PrototypeTag.Frost, 0 },
            { Code.Realm.Prototype.PrototypeTag.Frostbite, 0 },
            // 岩土域（stone）：石化/震动
            { Code.Realm.Prototype.PrototypeTag.Petrify, 1 },
            { Code.Realm.Prototype.PrototypeTag.Vibration, 1 },
            // 壁垒域（bulwark）：护盾/反弹/元素抗性
            { Code.Realm.Prototype.PrototypeTag.Shield, 2 },
            { Code.Realm.Prototype.PrototypeTag.Thorns, 2 },
            { Code.Realm.Prototype.PrototypeTag.ElementResist, 2 },
            // 血脉域（blood）：吸血/汲取/血祭
            { Code.Realm.Prototype.PrototypeTag.Lifesteal, 3 },
            { Code.Realm.Prototype.PrototypeTag.Drain, 3 },
            { Code.Realm.Prototype.PrototypeTag.Blood, 3 },
            // 治愈域（heal）：治疗/再生/祝福
            { Code.Realm.Prototype.PrototypeTag.Heal, 4 },
            { Code.Realm.Prototype.PrototypeTag.HealAlly, 4 },
            { Code.Realm.Prototype.PrototypeTag.Regeneration, 4 },
            { Code.Realm.Prototype.PrototypeTag.Blessing, 4 },
            // 草木域（nature）：束缚/自然
            { Code.Realm.Prototype.PrototypeTag.Entangle, 5 },
            // 火焰域（flame）：灼烧
            { Code.Realm.Prototype.PrototypeTag.Burn, 6 },
            // 圣光域（light）：等离子/净化
            { Code.Realm.Prototype.PrototypeTag.Plasma, 7 },
            { Code.Realm.Prototype.PrototypeTag.Cleanse, 7 },
            // 场域（field）：重力/磁力
            { Code.Realm.Prototype.PrototypeTag.Gravity, 8 },
            { Code.Realm.Prototype.PrototypeTag.Magnetism, 8 },
            // 空间域（space）：瞬移/闪避
            { Code.Realm.Prototype.PrototypeTag.Teleport, 9 },
            { Code.Realm.Prototype.PrototypeTag.Dodge, 9 },
            // 冲击域（impact）：麻痹/击退/狂暴/暴击/溅射/破甲/多重/声波
            { Code.Realm.Prototype.PrototypeTag.Stun, 10 },
            { Code.Realm.Prototype.PrototypeTag.Knockback, 10 },
            { Code.Realm.Prototype.PrototypeTag.Berserk, 10 },
            { Code.Realm.Prototype.PrototypeTag.Crit, 10 },
            { Code.Realm.Prototype.PrototypeTag.Chain, 10 },
            { Code.Realm.Prototype.PrototypeTag.ArmorBreak, 10 },
            { Code.Realm.Prototype.PrototypeTag.MultiShot, 10 },
            { Code.Realm.Prototype.PrototypeTag.Sound, 10 },
            // 时间域（time）：减速/时缓
            { Code.Realm.Prototype.PrototypeTag.Slow, 11 },
            { Code.Realm.Prototype.PrototypeTag.TimeSlow, 11 },
            // 心灵域（mind）：念力
            { Code.Realm.Prototype.PrototypeTag.Telekinesis, 12 },
            // 梦魇域（dream）：恐惧/睡眠
            { Code.Realm.Prototype.PrototypeTag.Fear, 13 },
            { Code.Realm.Prototype.PrototypeTag.Sleep, 13 },
            // 幻象域（phantom）：幻象/隐身/分身
            { Code.Realm.Prototype.PrototypeTag.Illusion, 14 },
            { Code.Realm.Prototype.PrototypeTag.Invisibility, 14 },
            { Code.Realm.Prototype.PrototypeTag.Clone, 14 },
            // 灵魂域（soul）：灵魂
            { Code.Realm.Prototype.PrototypeTag.Soul, 15 },
            // 诅咒域（curse）：毒蚀/诅咒
            { Code.Realm.Prototype.PrototypeTag.Poison, 17 },
            { Code.Realm.Prototype.PrototypeTag.Curse, 17 },
            // 朽灭域（decay）：衰老/虚弱
            { Code.Realm.Prototype.PrototypeTag.Aging, 18 },
            { Code.Realm.Prototype.PrototypeTag.Weakness, 18 },
            // 终焉域（death）：致死
            { Code.Realm.Prototype.PrototypeTag.Execute, 19 }
        };

        /// <summary>域 → 神名大组（0物质/1生命/2能量/3力场/4时空/5精神/6信息/7概念；判词仍按大组，此表仅供借名范围）</summary>
        private static readonly int[] DomainToGroup = new int[]
        {
            0, 0, 0,       // frost stone bulwark
            1, 1, 1,       // blood heal nature
            2, 2,          // flame light
            3, 3, 3,       // field space impact
            4,             // time
            5, 5, 5, 5,    // mind dream phantom soul
            6,             // data
            7, 7, 7        // curse decay death
        };

        /// <summary>大组 → 所含域（借名顺序；洗牌后逐域尝试）</summary>
        private static readonly int[][] GroupDomains = new int[][]
        {
            new int[] { 0, 1, 2 },           // 0 物质
            new int[] { 3, 4, 5 },           // 1 生命
            new int[] { 6, 7 },              // 2 能量
            new int[] { 8, 9, 10 },          // 3 力场
            new int[] { 11 },                // 4 时空
            new int[] { 12, 13, 14, 15 },    // 5 精神
            new int[] { 16 },                // 6 信息
            new int[] { 17, 18, 19 }         // 7 概念
        };

        /// <summary>染色体 → 默认域（无匹配标签时按基因本源意象）</summary>
        private static readonly int[] ChromosomeDefaultDomain = new int[]
        {
            1,  // 0 物质 → 岩土
            4,  // 1 生命 → 治愈
            6,  // 2 能量 → 火焰
            8,  // 3 力场 → 场域
            11, // 4 时空 → 时间
            12, // 5 精神 → 心灵
            16, // 6 信息 → 信息
            18  // 7 概念 → 朽灭
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

        /// <summary>按异能标签选神名意象域（v0.18.41：神名与具体标签相符；无匹配回退基因染色体→默认域）</summary>
        private static int GetDivineDomainIndexByPrototype(Actor god)
        {
            try
            {
                var def = Code.Realm.Prototype.PrototypeRegistry.GetForActor(god);
                if (def != null)
                {
                    int d;
                    if (TagToDivineDomain.TryGetValue(def.MainTag, out d)) return d;
                    if (TagToDivineDomain.TryGetValue(def.SubTag, out d)) return d;
                }
            }
            catch { }
            int group = GetDominantElementGroup(god);
            if (group < 0 || group > 7) group = UnityEngine.Random.Range(0, 8);
            return ChromosomeDefaultDomain[group];
        }

        /// <summary>在指定域内尝试取空闲神位（域内确定性选套；东名或西名任一被占用即视为该神位已占用）</summary>
        private static bool TryPickFromDomain(int domain, out string east, out string west, int preferredSlot = -1)
        {
            east = west = null;
            if (domain < 0 || domain >= DivineDomains.Length) return false;
            // v0.18.58：确定性选套（preferredSlot 优先，占用则顺延），不再随机洗牌
            int start = preferredSlot >= 0 ? preferredSlot : 0;
            string[] eastPool = DivineNamesByDomain[domain];
            string[] westPool = DivineNamesByDomainWest[domain];
            for (int k = 0; k < 6; k++)
            {
                int slot = (start + k) % 6;
                string e = eastPool[slot];
                string w = westPool[slot];
                if (DivineNameRegistry.IsTaken(e) || DivineNameRegistry.IsTaken(w)) continue;
                east = e; west = w;
                return true;
            }
            return false;
        }

        /// <summary>在同大组其他域尝试（洗牌逐域；神名独属优先于同组意象一致）</summary>
        private static bool TryPickFromGroup(int domain, out string east, out string west)
        {
            east = west = null;
            if (domain < 0 || domain >= DomainToGroup.Length) return false;
            int group = DomainToGroup[domain];
            int[] domOrder = ShuffleOrder(GroupDomains[group].Length);
            for (int k = 0; k < domOrder.Length; k++)
            {
                int d = GroupDomains[group][domOrder[k]];
                if (d == domain) continue;
                if (TryPickFromDomain(d, out east, out west)) return true;
            }
            return false;
        }

        /// <summary>全域120神位尝试（洗牌逐域；终极兜底前的最后一层）</summary>
        private static bool TryPickFromAll(out string east, out string west)
        {
            east = west = null;
            int[] domOrder = ShuffleOrder(DivineDomains.Length);
            for (int k = 0; k < domOrder.Length; k++)
            {
                if (TryPickFromDomain(domOrder[k], out east, out west)) return true;
            }
            return false;
        }

        /// <summary>
        /// 分配全局唯一神名对（v0.18.41 神位制：东方名+西方名一一对应，同一神明同时获得两者）
        /// 顺序：标签域内6神位 → 同大组借名 → 全域120神位 → 域内随机兜底。
        /// 唯一性按神位整体判定，跨存档由 DivineNameRegistry 保证；旧档单键神名自动纳入占用。
        /// </summary>
        /// <summary>异能专属神名映射（v0.18.58）：按异能原型ID查固定神名（一一对应，意象随异能本体，品质不参与）</summary>
        private static bool GetFixedPrototypeName(Actor god, out string east, out string west)
        {
            east = west = null;
            try
            {
                var def = Code.Realm.Prototype.PrototypeRegistry.GetForActor(god);
                if (def == null || string.IsNullOrEmpty(def.Id)) return false;
                string keyE = "god_proto_" + def.Id;
                string e = UILocalization.Get(keyE);
                if (string.IsNullOrEmpty(e) || e == keyE) return false; // key 缺失（未登记的新异能）→ 走确定性兜底
                string w = UILocalization.Get("god_proto_" + def.Id + "_west");
                if (string.IsNullOrEmpty(w) || w == "god_proto_" + def.Id + "_west") w = e;
                east = e; west = w;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>确定性套号：基于异能原型ID的稳定哈希（同异能永远同套，跨存档稳定）</summary>
        private static int StableSlotFor(Actor god)
        {
            try
            {
                var def = Code.Realm.Prototype.PrototypeRegistry.GetForActor(god);
                string seed = def != null && !string.IsNullOrEmpty(def.Id) ? def.Id : (god.getName() ?? "proto");
                int h = 0;
                for (int i = 0; i < seed.Length; i++) h = h * 31 + seed[i];
                return ((h % 6) + 6) % 6;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>世系中文序号（同异能第N位登神者，东名后缀）</summary>
        private static string GetSuffixCn(int gen)
        {
            string[] s = { "", "·贰", "·叁", "·肆", "·伍", "·陆", "·柒", "·捌", "·玖", "·拾" };
            return gen < s.Length ? s[gen] : "·" + (gen + 1);
        }

        /// <summary>世系罗马序号（同异能第N位登神者，西名后缀）</summary>
        private static string GetSuffixRoman(int gen)
        {
            string[] s = { "", "·II", "·III", "·IV", "·V", "·VI", "·VII", "·VIII", "·IX", "·X" };
            return gen < s.Length ? s[gen] : "·" + (gen + 1);
        }

        private static bool PickUniqueDivineNamePair(Actor god, out string east, out string west)
        {
            east = west = null;
            try
            {
                // v0.18.58：异能专属神名——固定映射优先（一一对应）；同异能多单位登神 → 世系后缀（神名仍独属）
                if (GetFixedPrototypeName(god, out east, out west))
                {
                    string baseEast = east, baseWest = west;
                    int gen = 0;
                    while (gen < 9 && (DivineNameRegistry.IsTaken(east) || DivineNameRegistry.IsTaken(west)))
                    {
                        gen++;
                        east = baseEast + GetSuffixCn(gen);
                        west = baseWest + GetSuffixRoman(gen);
                    }
                    return true;
                }
                // 未登记异能（用户后续新增）：域内确定性套号（ID哈希稳定），不再随机
                DivineNameRegistry.EnsureSwept();
                int domain = GetDivineDomainIndexByPrototype(god);
                if (TryPickFromDomain(domain, out east, out west, StableSlotFor(god))) return true;
                if (TryPickFromGroup(domain, out east, out west)) return true;
                if (TryPickFromAll(out east, out west)) return true;
                DSDebug.Warning("[GeneCultivation] 神名词库120神位已全部被占用，回退随机分配");
            }
            catch (Exception e)
            {
                DSDebug.Warning($"[GeneCultivation] 唯一神名对分配异常: {e.Message}");
            }
            // 兜底：域内固定套（仅全库耗尽时可能出现重名）
            int fallbackDomain = GetDivineDomainIndexByPrototype(god);
            int slot = StableSlotFor(god);
            east = DivineNamesByDomain[fallbackDomain][slot];
            west = DivineNamesByDomainWest[fallbackDomain][slot];
            return true;
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

                // v0.18.53 神名加冕记录：神名授予成功即写入历史（独立于氏族/神眷/判词链路，
                // 旧版神名记录挂在立教链末尾，链路中途异常即丢失——表现为历史面板"获得神名"文案消失）
                try
                {
                    string eastFull, westFull;
                    GetStoredDivineNamePair(god, out eastFull, out westFull);
                    string fullName = !string.IsNullOrEmpty(westFull) ? eastFull + " · " + westFull : eastFull;
                    DSEventManager.RecordEvent(
                        DSEventType.Divine,
                        UILocalization.Get("judge_true_god_name_title"),
                        string.Format(UILocalization.Get("judge_true_god_name_desc"), fullName, god.getName()),
                        god.getName(),
                        god.getID(),
                        13, god);
                }
                catch (Exception e)
                {
                    DSDebug.Warning($"[GeneCultivation] 神名加冕记录异常: {e.Message}");
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

		/// <summary>
		/// 圣所放置场景恢复神名（v0.18.51，跨存档唯一性）
		/// 只恢复神名本身：快照有神名则写回+登记（同角色跨存档复刻放行）；
		/// 旧记录无神名则补授唯一神名对（复用选名排重逻辑，绝不与已占用神名重复）。
		/// 不触发氏族/神眷/宗教/切纪元等诞生链（放置≠诞生）。
		/// </summary>
		public static void RestoreSpawnedGodDivineName(Actor god, string eastSnapshot, string westSnapshot)
		{
			if (god == null || !god.isAlive()) return;
			try
			{
				string east = eastSnapshot, west = westSnapshot;
				if (string.IsNullOrEmpty(east) || string.IsNullOrEmpty(west))
				{
					// 旧记录无神名：补授唯一神名对（只授名，不建氏族不切纪元）
					if (!PickUniqueDivineNamePair(god, out east, out west) || string.IsNullOrEmpty(east))
					{
						DSDebug.Warning("[GeneCultivation] 放置真神补授神名失败");
						return;
					}
				}

				CultivationData.EnsureCustomDataInitialized(god);
				var data = ActorDataAccessor.GetData(god);
				if (data?.custom_data_string == null) return;
				data.custom_data_string["divine_name"] = east;
				data.custom_data_string["divine_name_west"] = west;
				// 登记到跨存档注册表（东名+西名；同角色复刻幂等放行，已被其他单位占用则不覆盖）
				if (!DivineNameRegistry.Claim(east, god))
				{
					DSDebug.Warning($"[GeneCultivation] 东方神名 {east} 登记失败（可能已被其他单位占用）");
				}
				if (!DivineNameRegistry.Claim(west, god))
				{
					DSDebug.Warning($"[GeneCultivation] 西方神名 {west} 登记失败（可能已被其他单位占用）");
				}
			}
			catch (Exception dsEx)
			{
				DSDebug.Warning("[GeneCultivation] RestoreSpawnedGodDivineName 异常: " + dsEx.Message);
			}
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

                // 根据异能标签选神名意象域（v0.18.41：神名与具体异能标签相符，如金刚=护盾→壁垒域；无匹配回退基因染色体→默认域）
                // v0.18.41：神位制——同时授予东方名+西方名（一一对应），全局唯一（跨存档独属）
                string east, west;
                if (!PickUniqueDivineNamePair(god, out east, out west) || string.IsNullOrEmpty(east))
                {
                    DSDebug.Warning("[GeneCultivation] 神名对分配失败");
                    return null;
                }

                // 存储神名对（divine_name=东方名，divine_name_west=西方名；旧档单键自动兼容）
                try
                {
                    CultivationData.EnsureCustomDataInitialized(god);
                    ActorDataAccessor.GetData(god).custom_data_string["divine_name"] = east;
                    ActorDataAccessor.GetData(god).custom_data_string["divine_name_west"] = west;
                    // 登记到跨存档注册表（东名+西名分别登记；登记失败不阻断神名授予）
                    if (!DivineNameRegistry.Claim(east, god))
                    {
                        DSDebug.Warning($"[GeneCultivation] 东方神名 {east} 登记失败（可能已被其他单位占用）");
                    }
                    if (!DivineNameRegistry.Claim(west, god))
                    {
                        DSDebug.Warning($"[GeneCultivation] 西方神名 {west} 登记失败（可能已被其他单位占用）");
                    }
                } catch (System.Exception dsEx) { Code.Core.DSDebug.Warning("[GeneCultivation] GrantDivineName 异常: " + dsEx.Message); }
                CultivationData.SetCooldown(god, "divine_name", 1);

                return east;
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





