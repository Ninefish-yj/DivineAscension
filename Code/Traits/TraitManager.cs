// ============================================================
using HarmonyLib;
//   [禁令4] 不逐帧遍历  所有操作均为事件驱动/年度Tick调用
//   [禁令1] 不覆写原生底层  仅注册新ActorTrait，操作原生immortal特质
//   [禁令8] 不直接改写原生血量/攻击  仅微量倍率修正
// ============================================================

using System;
using Code.Core;
using Code.Data;
using UnityEngine;
using Code.UI;

namespace Code.Traits
{
    public static class TraitManager
    {
        // ============================================================
        //  境界Trait ID常量（两位数编号，确保排序正确：01,02...10,11,12,13）
        // ============================================================
        public const string TIER_1_AWAKENED    = "ds_tier_01_awakened";
        public const string TIER_2_INITIATE   = "ds_tier_02_initiate";
        public const string TIER_3_REFINED  = "ds_tier_03_refined";
        public const string TIER_4_TRANSFORMED    = "ds_tier_04_transformed";
        public const string TIER_5_CONTROLLER = "ds_tier_05_controller";
        public const string TIER_6_DOMAIN   = "ds_tier_06_domain";
        public const string TIER_7_MATERIALIZED = "ds_tier_07_materialized";
        public const string TIER_8_LAWBEARER  = "ds_tier_08_lawbearer";
        public const string TIER_9_ORIGINATOR  = "ds_tier_09_originator";
        public const string TIER_10_REALMBREAKER = "ds_tier_10_realmbreaker";
        public const string TIER_11_SANCTUARY  = "ds_tier_11_sanctuary";
        public const string TIER_12_ASCENDED = "ds_tier_12_ascended";
        public const string TIER_13_TRUEGOD = "ds_tier_13_truegod";

        /// <summary>被黑色方碑标记：4阶突破时获得，标记单位已受过方碑辐射影响</summary>
        
        // 纪元主宰专属唯一特质（只有当前纪元主宰才能拥有）
        public const string ERA_DOMINATOR_TRAIT = "ds_era_dominator_trait";

        private static readonly string[] _realmTraits =
        {
            TIER_1_AWAKENED,   TIER_2_INITIATE,  TIER_3_REFINED, TIER_4_TRANSFORMED,
            TIER_5_CONTROLLER,TIER_6_DOMAIN,  TIER_7_MATERIALIZED,TIER_8_LAWBEARER,
            TIER_9_ORIGINATOR, TIER_10_REALMBREAKER,TIER_11_SANCTUARY,TIER_12_ASCENDED,
            TIER_13_TRUEGOD
        };

        // ============================================================
        //  特殊状态Trait ID常量（能力失控、顿悟、能力根基受损等，增加游戏丰富度）
        //  禁令7遵守：仅作身份标识+微量属性加成，不存储结构化数据
        // ============================================================
        public const string STATUS_DEMONIC_OBSESSION   = "ds_status_demonic_obsession";
        public const string STATUS_ENLIGHTENMENT        = "ds_status_enlightenment";
        public const string STATUS_DAMAGED_FOUNDATION   = "ds_status_damaged_foundation";
        public const string STATUS_SANCTUARY_AVATAR     = "ds_status_sanctuary_avatar";
        public const string STATUS_DIVINE_PROJECTION     = "ds_status_divine_projection";
        public const string STATUS_DIVINE_BLESSING       = "ds_status_divine_blessing";
        public const string STATUS_VOID_CORRUPTION       = "ds_status_void_corruption";
        public const string STATUS_IMMORTAL_BODY         = "ds_status_immortal_body";
        public const string STATUS_DIVINE_PUNISHMENT     = "ds_status_divine_punishment";
        public const string STATUS_ENERGY_SHIELD      = "ds_status_energy_shield";
        public const string STATUS_BODY_BOOST        = "ds_status_body_boost";
        public const string STATUS_ENERGY_WEAPON    = "ds_status_energy_weapon";
        public const string STATUS_GENE_MUTATION     = "ds_status_gene_mutation";  // 4阶专属：基因突变者
        public const string STATUS_BREAKTHROUGH_MOMENTUM = "ds_status_breakthrough_momentum";  // 破境之势：挑战上位时的临时战力爆发状态

        // ============================================================
        //  异能组织Trait ID常量（组织身份）
        // ============================================================
        // 纵向等级身份（7级）
        public const string SECT_LEADER     = "ds_sect_leader";
        public const string SECT_DEPUTY     = "ds_sect_deputy";
        public const string SECT_ELDER      = "ds_sect_elder";
        public const string SECT_CADRE      = "ds_sect_cadre";
        public const string SECT_ELITE      = "ds_sect_elite";
        public const string SECT_MEMBER     = "ds_sect_member";
        public const string SECT_PROBATION  = "ds_sect_probation";

        // 横向同级/荣誉身份（4个，可与等级身份叠加）
        public const string SECT_FOUNDER    = "ds_sect_founder";
        public const string SECT_HERITAGE   = "ds_sect_heritage";
        public const string SECT_GUARDIAN   = "ds_sect_guardian";
        public const string SECT_GUEST      = "ds_sect_guest";

        private static readonly string[] _sectTraits = {
            SECT_LEADER, SECT_DEPUTY, SECT_ELDER, SECT_CADRE, SECT_ELITE, SECT_MEMBER, SECT_PROBATION,
            SECT_FOUNDER, SECT_HERITAGE, SECT_GUARDIAN, SECT_GUEST
        };

        // ============================================================

        // WorldBox原生不朽特质ID
        private const string NATIVE_IMMORTAL = "immortal";

        private static bool _registered = false;

        // ============================================================
        //  注册入口
        // ============================================================

        public static void RegisterAllTraits()
        {
            if (_registered) return;

            // 先创建特质分组（参考念力修行体系的ActorTraitGroupAsset）
            RegisterTraitGroups();

            // === 13阶异能境界（基因异能体系，每个名字对应实际机制）===
            RegisterRealmTrait(TIER_1_AWAKENED,     UILocalization.Get(TIER_1_AWAKENED),  UILocalization.Get(TIER_1_AWAKENED + "_desc"), 1);
            RegisterRealmTrait(TIER_2_INITIATE,    UILocalization.Get(TIER_2_INITIATE),  UILocalization.Get(TIER_2_INITIATE + "_desc"), 2);
            RegisterRealmTrait(TIER_3_REFINED,   UILocalization.Get(TIER_3_REFINED),  UILocalization.Get(TIER_3_REFINED + "_desc"), 3);
            RegisterRealmTrait(TIER_4_TRANSFORMED,     UILocalization.Get(TIER_4_TRANSFORMED),  UILocalization.Get(TIER_4_TRANSFORMED + "_desc"), 4);
            RegisterRealmTrait(TIER_5_CONTROLLER,  UILocalization.Get(TIER_5_CONTROLLER),  UILocalization.Get(TIER_5_CONTROLLER + "_desc"), 5);
            RegisterRealmTrait(TIER_6_DOMAIN,    UILocalization.Get(TIER_6_DOMAIN),  UILocalization.Get(TIER_6_DOMAIN + "_desc"), 6);
            RegisterRealmTrait(TIER_7_MATERIALIZED,  UILocalization.Get(TIER_7_MATERIALIZED),  UILocalization.Get(TIER_7_MATERIALIZED + "_desc"), 7);
            RegisterRealmTrait(TIER_8_LAWBEARER,   UILocalization.Get(TIER_8_LAWBEARER),  UILocalization.Get(TIER_8_LAWBEARER + "_desc"), 8);
            RegisterRealmTrait(TIER_9_ORIGINATOR,   UILocalization.Get(TIER_9_ORIGINATOR),  UILocalization.Get(TIER_9_ORIGINATOR + "_desc"), 9);
            RegisterRealmTrait(TIER_10_REALMBREAKER, UILocalization.Get(TIER_10_REALMBREAKER),  UILocalization.Get(TIER_10_REALMBREAKER + "_desc"), 10);
            RegisterRealmTrait(TIER_11_SANCTUARY,  UILocalization.Get(TIER_11_SANCTUARY),  UILocalization.Get(TIER_11_SANCTUARY + "_desc"), 11);
            RegisterRealmTrait(TIER_12_ASCENDED, UILocalization.Get(TIER_12_ASCENDED),  UILocalization.Get(TIER_12_ASCENDED + "_desc"), 12);
            RegisterRealmTrait(TIER_13_TRUEGOD, UILocalization.Get(TIER_13_TRUEGOD),  UILocalization.Get(TIER_13_TRUEGOD + "_desc"), 13);

            
            // 纪元主宰专属唯一特质（只有当前纪元主宰才能拥有，主宰变更时自动转移）
            RegisterStatusTrait(ERA_DOMINATOR_TRAIT, UILocalization.Get(ERA_DOMINATOR_TRAIT), UILocalization.Get(ERA_DOMINATOR_TRAIT + "_desc"), false);
            // === Buff技能改为被动特质（达到对应境界自动获得）===
            RegisterStatusTrait(STATUS_ENERGY_SHIELD, UILocalization.Get(STATUS_ENERGY_SHIELD), UILocalization.Get(STATUS_ENERGY_SHIELD + "_desc"), false);
            RegisterStatusTrait(STATUS_BODY_BOOST, UILocalization.Get(STATUS_BODY_BOOST), UILocalization.Get(STATUS_BODY_BOOST + "_desc"), false);
            RegisterStatusTrait(STATUS_ENERGY_WEAPON, UILocalization.Get(STATUS_ENERGY_WEAPON), UILocalization.Get(STATUS_ENERGY_WEAPON + "_desc"), false);
            RegisterStatusTrait(STATUS_GENE_MUTATION, UILocalization.Get(STATUS_GENE_MUTATION), UILocalization.Get(STATUS_GENE_MUTATION + "_desc"), false);
            // 破境之势已改为原版状态栏系统，不再作为特质
            // RegisterStatusTrait(STATUS_BREAKTHROUGH_MOMENTUM, ...);

            // === 异能状态已改为状态栏系统，不再作为特质 ===
            // 限时状态（深度觉醒、能力失控、能量过载、虚空侵蚀等）使用游戏原生 addStatusEffect API
            // 非限时状态（登神化身、维度投影、不朽之躯等）存储在元数据中
            // RegisterStatusTrait(STATUS_DEMONIC_OBSESSION, "能力失控", "...", true);
            // RegisterStatusTrait(STATUS_DAMAGED_FOUNDATION, "能力根基受损", "...", true);
            // RegisterStatusTrait(STATUS_SANCTUARY_AVATAR, "登神化身", "...", false);
            // RegisterStatusTrait(STATUS_DIVINE_PROJECTION, "维度投影", "...", false);
            // RegisterStatusTrait(STATUS_DIVINE_BLESSING, "神眷", "...", false);
            // RegisterStatusTrait(STATUS_VOID_CORRUPTION, "虚空侵蚀", "...", true);
            // RegisterStatusTrait(STATUS_ENERGY_OVERLOAD, "能量过载", "...", true);
            // RegisterStatusTrait(STATUS_IMMORTAL_BODY, "不朽之躯", "...", false);
            // RegisterStatusTrait(STATUS_DIVINE_PUNISHMENT, "神罚降临", "...", false);
            // RegisterStatusTrait(STATUS_ENLIGHTENMENT, "深度觉醒", "...", true);

            // === 异能组织特质（11个，纵向7级+横向4同级/荣誉身份）===
            // 纵向等级身份（7级）
            RegisterSectTrait(SECT_LEADER, UILocalization.Get(SECT_LEADER), UILocalization.Get(SECT_LEADER + "_desc"), false);
            RegisterSectTrait(SECT_DEPUTY, UILocalization.Get(SECT_DEPUTY), UILocalization.Get(SECT_DEPUTY + "_desc"), false);
            RegisterSectTrait(SECT_ELDER, UILocalization.Get(SECT_ELDER), UILocalization.Get(SECT_ELDER + "_desc"), false);
            RegisterSectTrait(SECT_CADRE, UILocalization.Get(SECT_CADRE), UILocalization.Get(SECT_CADRE + "_desc"), false);
            RegisterSectTrait(SECT_ELITE, UILocalization.Get(SECT_ELITE), UILocalization.Get(SECT_ELITE + "_desc"), false);
            RegisterSectTrait(SECT_MEMBER, UILocalization.Get(SECT_MEMBER), UILocalization.Get(SECT_MEMBER + "_desc"), false);
            RegisterSectTrait(SECT_PROBATION, UILocalization.Get(SECT_PROBATION), UILocalization.Get(SECT_PROBATION + "_desc"), false);
            // 横向同级/荣誉身份（4个，可与等级身份叠加）
            RegisterSectTrait(SECT_FOUNDER, UILocalization.Get(SECT_FOUNDER), UILocalization.Get(SECT_FOUNDER + "_desc"), false);
            RegisterSectTrait(SECT_HERITAGE, UILocalization.Get(SECT_HERITAGE), UILocalization.Get(SECT_HERITAGE + "_desc"), false);
            RegisterSectTrait(SECT_GUARDIAN, UILocalization.Get(SECT_GUARDIAN), UILocalization.Get(SECT_GUARDIAN + "_desc"), false);
            RegisterSectTrait(SECT_GUEST, UILocalization.Get(SECT_GUEST), UILocalization.Get(SECT_GUEST + "_desc"), false);

            _registered = true;
            DSDebug.Verbose("25个异能Trait注册完成（13境界+11组织+1纪元主宰），已创建3个特质分组");
        }

        /// <summary>创建登神长阶特质分组（境界组+异能体系组）</summary>
        private static void RegisterTraitGroups()
        {
            try
            {
                // 境界组（name用trait_group_<id>格式，游戏原生本地化系统自动查找）
                var realmGroup = new ActorTraitGroupAsset();
                realmGroup.id = "ds_realm_group";
                realmGroup.name = "trait_group_ds_realm_group";
                realmGroup.color = "#00BFFF";
                AssetManager.trait_groups.add(realmGroup);
                DSDebug.Verbose("特质分组创建成功: ds_realm_group (异能境界)");

                // 特殊状态组
                var statusGroup = new ActorTraitGroupAsset();
                statusGroup.id = "ds_status_group";
                statusGroup.name = "trait_group_ds_status_group";
                statusGroup.color = "#FF6347";
                AssetManager.trait_groups.add(statusGroup);
                DSDebug.Verbose("特质分组创建成功: ds_status_group (异能状态)");

                // 异能组织组
                var sectGroup = new ActorTraitGroupAsset();
                sectGroup.id = "ds_sect_group";
                sectGroup.name = "trait_group_ds_sect_group";
                sectGroup.color = "#32CD32";
                AssetManager.trait_groups.add(sectGroup);
                DSDebug.Verbose("特质分组创建成功: ds_sect_group (异能组织)");

            }
            catch (System.Exception e)
            {
                DSDebug.Error("[DivineAscension] 特质分组创建失败: " + e.Message + "\n" + e.StackTrace);
            }
        }

        // ============================================================
        //  内部注册方法
        // ============================================================

        private static void RegisterRealmTrait(string id, string name, string desc, int tier)
        {
            try
            {
                // 境界属性加成（参考香火神道神格数值，指数增长，高阶压倒性力量）
                var stats = new BaseStats();
                switch (tier)
                {
                    case 1: stats["damage"]=30; stats["health"]=300; stats["speed"]=0.2f; stats["mana"]=20; stats["lifespan"]=20f; break;
                    case 2: stats["damage"]=80; stats["health"]=800; stats["speed"]=0.3f; stats["mana"]=40; stats["stamina"]=20; stats["lifespan"]=40f; break;
                    case 3: stats["damage"]=200; stats["health"]=2000; stats["speed"]=0.4f; stats["mana"]=60; stats["intelligence"]=3; stats["lifespan"]=80f; break;
                    case 4: stats["damage"]=500; stats["health"]=5000; stats["speed"]=0.4f; stats["stamina"]=40; stats["scale"]=0.1f; stats["armor"]=30f; stats["lifespan"]=160f; break;
                    case 5: stats["damage"]=1200; stats["health"]=12000; stats["speed"]=0.5f; stats["mana"]=100; stats["warfare"]=3; stats["armor"]=60f; stats["lifespan"]=300f; break;
                    case 6: stats["damage"]=2800; stats["health"]=28000; stats["speed"]=0.6f; stats["range"]=2f; stats["stamina"]=60; stats["armor"]=120f; stats["attack_speed"]=2f; stats["lifespan"]=500f; break;
                    case 7: stats["damage"]=6000; stats["health"]=60000; stats["speed"]=0.6f; stats["intelligence"]=6; stats["mana"]=160; stats["attack_speed"]=4f; stats["armor"]=240f; stats["lifespan"]=800f; break;
                    case 8: stats["damage"]=13000; stats["health"]=130000; stats["speed"]=0.7f; stats["mana"]=240; stats["skill_spell"]=0.2f; stats["attack_speed"]=6f; stats["armor"]=450f; stats["lifespan"]=1200f; break;
                    case 9: stats["damage"]=30000; stats["health"]=300000; stats["speed"]=0.8f; stats["stamina"]=100; stats["warfare"]=6; stats["armor"]=750f; stats["attack_speed"]=10f; stats["range"]=2f; stats["lifespan"]=1800f; break;
                    case 10: stats["damage"]=70000; stats["health"]=700000; stats["speed"]=0.9f; stats["range"]=4f; stats["mana"]=320; stats["attack_speed"]=16f; stats["armor"]=1200f; stats["lifespan"]=3000f; break;
                    case 11: stats["damage"]=160000; stats["health"]=1600000; stats["speed"]=1.0f; stats["range"]=6f; stats["stamina"]=200; stats["attack_speed"]=24f; stats["armor"]=2100f; break;
                    case 12: stats["damage"]=350000; stats["health"]=3500000; stats["speed"]=1.2f; stats["range"]=8f; stats["mana"]=400; stats["attack_speed"]=30f; stats["armor"]=3000f; break;
                    case 13: stats["damage"]=800000; stats["health"]=8000000; stats["speed"]=1.5f; stats["range"]=10f; stats["stamina"]=400; stats["attack_speed"]=40f; stats["armor"]=5000f; break;
                }

                var trait = new ActorTrait
                {
                    id = id,
                    group_id = "ds_realm_group",
                    can_be_removed = tier < 12,
                    can_be_given = true,
                    needs_to_be_explored = false,
                    has_locales = true,  // 使用游戏本地化系统，根据特质id查找翻译
                    has_description_1 = true,
                    base_stats = stats,  // 实际属性加成
                    path_icon = "trait/" + id,  // 自定义境界图标
                };

                AssetManager.traits.add(trait);
                DSDebug.Verbose("境界特质注册成功: " + id + " tier=" + tier + " damage+" + stats["damage"]);
            }
            catch (System.Exception e)
            {
                DSDebug.Error("[DivineAscension] 境界特质注册失败: " + id + " - " + e.Message + "\n" + e.StackTrace);
            }
        }

/// <summary>注册特殊状态特质</summary>
        private static void RegisterStatusTrait(string id, string name, string desc, bool canBeRemoved)
        {
            try
            {
                // 特殊状态属性加成（每个状态都有实际效果）
                var stats = new BaseStats();
                switch (id)
                {
                    case STATUS_DEMONIC_OBSESSION:  // 能力失控：攻击大增，防御大降
                        stats["damage"] = 10; stats["health"] = -10; stats["speed"] = 0.3f;
                        stats["intelligence"] = -2; stats["stamina"] = -10; break;
                    case STATUS_ENLIGHTENMENT:  // 顿悟：智力法术大增
                        stats["intelligence"] = 5; stats["mana"] = 20; stats["skill_spell"] = 0.2f;
                        stats["speed"] = 0.2f; break;
                    case STATUS_DAMAGED_FOUNDATION:  // 能力根基受损：全属性下降
                        stats["damage"] = -5; stats["health"] = -15; stats["speed"] = -0.2f;
                        stats["mana"] = -10; stats["stamina"] = -5; break;
                    case STATUS_SANCTUARY_AVATAR:  // 登神化身：攻击生命大增
                        stats["damage"] = 15; stats["health"] = 50; stats["range"] = 0.5f;
                        stats["stamina"] = 20; break;
                    case STATUS_DIVINE_PROJECTION:  // 维度投影：全属性大增
                        stats["damage"] = 25; stats["health"] = 100; stats["mana"] = 50;
                        stats["scale"] = 0.1f; stats["speed"] = 0.3f; stats["range"] = 1f; break;
                    case STATUS_DIVINE_BLESSING:  // 神眷：全面提升
                        stats["damage"] = 10; stats["health"] = 50; stats["intelligence"] = 2;
                        stats["warfare"] = 2; stats["mana"] = 30; stats["stamina"] = 15; break;
                    case STATUS_VOID_CORRUPTION:  // 虚空侵蚀：攻击微增，防御大降
                        stats["damage"] = 5; stats["health"] = -20; stats["stamina"] = -10;
                        stats["speed"] = -0.2f; stats["intelligence"] = -1; break;
                    case STATUS_IMMORTAL_BODY:  // 不朽之躯：生命防御大增，免疫疾病
                        stats["health"] = 200; stats["armor"] = 200f; stats["stamina"] = 50;
                        stats["damage"] = 10; stats["speed"] = 0.3f; break;
                    case STATUS_DIVINE_PUNISHMENT:  // 神罚降临：全属性大增
                        stats["damage"] = 50; stats["health"] = 200; stats["range"] = 2f;
                        stats["attack_speed"] = 10f; stats["mana"] = 100; stats["intelligence"] = 5; break;
                    case STATUS_ENERGY_SHIELD:  // 能量护盾（2阶）：防御大增
                        stats["armor"] = 500f; stats["health"] = 300; stats["stamina"] = 100; break;
                    case STATUS_BODY_BOOST:  // 身体强化（3阶）：攻击速度大增
                        stats["damage"] = 200; stats["speed"] = 1.0f; stats["attack_speed"] = 8f; stats["stamina"] = 150; break;
                    case STATUS_ENERGY_WEAPON:  // 能量具象化（7阶）：攻击射程大增
                        stats["damage"] = 500; stats["range"] = 5f; stats["attack_speed"] = 15f; stats["speed"] = 0.8f; break;
                    case STATUS_GENE_MUTATION:  // 基因突变者（4阶专属）：全属性提升
                        stats["health"] = 100; stats["damage"] = 20; stats["armor"] = 30f;
                        stats["mana"] = 50; stats["stamina"] = 50; stats["speed"] = 0.3f;
                        stats["intelligence"] = 3; stats["warfare"] = 2; break;
                    case STATUS_BREAKTHROUGH_MOMENTUM:  // 破境之势：挑战上位时的临时战力爆发
                        stats["damage"] = 250000; stats["health"] = 2500000; stats["armor"] = 1500f;
                        stats["attack_speed"] = 15f; stats["speed"] = 0.5f; stats["stamina"] = 500;
                        break;
                }

                // 所有状态特质都用状态组
                string groupId = "ds_status_group";

                var trait = new ActorTrait
                {
                    id = id,
                    group_id = groupId,
                    can_be_removed = canBeRemoved,
                    can_be_given = true,
                    needs_to_be_explored = false,
                    has_locales = true,  // 使用游戏本地化系统，根据特质id查找翻译
                    has_description_1 = true,
                    base_stats = stats,
                    path_icon = "trait/" + id,  // 自定义状态图标
                };

                AssetManager.traits.add(trait);
                DSDebug.Verbose("状态特质注册成功: " + id + " (" + name + ") damage+" + stats["damage"]);
            }
            catch (System.Exception e)
            {
                DSDebug.Error("[DivineAscension] 状态特质注册失败: " + id + " - " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>
        /// 成神条件判定（纯基因系统）：
        /// - 所有基因同级，不存在顶级基因，无需"真理"基因
        /// - 觉醒时100%获得初始基因（细胞分裂），5%概率额外获得觉醒第二基因（天赋异禀）
        /// - 成神条件：深度觉醒经验>=50000（对超越的深度理解）+ 失控指数<50（精神状态稳定）+ >=12个基因
        /// </summary>
        public static bool CanAscendToGod(Actor actor)
        {
            if (actor == null) return false;

            // 获取已解锁的基因
            var unlockedElements = CultivationData.GetUnlockedElements(actor);
            if (unlockedElements == null || unlockedElements.Count == 0) return false;

            // 成神条件（纯基因系统）：
            // 条件1：深度觉醒经验 >= 50000（对超越的深度理解）
            // 条件2：失控指数 < 50（成神需要稳定的精神状态）
            // 条件3：至少拥有12个基因（基因觉醒达到一定程度）
            if (CultivationData.GetTurbulence(actor) >= 50f) return false;
            if (unlockedElements.Count < 12) return false;
            return CultivationData.GetEnlightenmentExp(actor) >= 50000f;
        }
        /// <summary>注册异能组织特质</summary>
        private static void RegisterSectTrait(string id, string name, string desc, bool canBeRemoved)
        {
            try
            {
                // 组织特质属性加成（每个身份都有实际效果）
                var stats = new BaseStats();
                switch (id)
                {
                    // 纵向等级身份
                    case SECT_LEADER:  // 领袖：管理军事外交全面提升
                        stats["diplomacy"] = 8; stats["stewardship"] = 8; stats["warfare"] = 5;
                        stats["damage"] = 15; stats["health"] = 80; stats["intelligence"] = 5; break;
                    case SECT_DEPUTY:  // 副领袖：管理外交提升
                        stats["diplomacy"] = 6; stats["stewardship"] = 6; stats["warfare"] = 3;
                        stats["damage"] = 10; stats["health"] = 60; stats["intelligence"] = 4; break;
                    case SECT_ELDER:  // 理事：外交管理修炼提升
                        stats["diplomacy"] = 5; stats["stewardship"] = 4; stats["intelligence"] = 4;
                        stats["damage"] = 5; stats["health"] = 40; stats["mana"] = 30; break;
                    case SECT_CADRE:  // 主管：管理战斗提升
                        stats["diplomacy"] = 3; stats["stewardship"] = 4; stats["warfare"] = 3;
                        stats["damage"] = 8; stats["health"] = 40; stats["intelligence"] = 2; break;
                    case SECT_ELITE:  // 精英：战斗修炼大幅提升
                        stats["damage"] = 12; stats["health"] = 30; stats["speed"] = 0.3f;
                        stats["intelligence"] = 3; stats["mana"] = 25; stats["stamina"] = 15; break;
                    case SECT_MEMBER:  // 正式成员：修炼速度提升
                        stats["damage"] = 5; stats["health"] = 15; stats["stamina"] = 8;
                        stats["mana"] = 15; break;
                    case SECT_PROBATION:  // 预备成员：少量加成
                        stats["damage"] = 2; stats["health"] = 8; stats["stamina"] = 3;
                        stats["mana"] = 5; break;
                    // 横向同级/荣誉身份
                    case SECT_FOUNDER:  // 缔造者：永久荣誉，修炼速度提升
                        stats["intelligence"] = 5; stats["diplomacy"] = 5; stats["mana"] = 40;
                        stats["damage"] = 5; stats["health"] = 50; break;
                    case SECT_HERITAGE:  // 继承者：能力图谱维护，修炼提升
                        stats["intelligence"] = 8; stats["stewardship"] = 3; stats["mana"] = 50;
                        stats["damage"] = 3; stats["health"] = 30; break;
                    case SECT_GUARDIAN:  // 捍卫者：组织防御，防御生命提升
                        stats["warfare"] = 4; stats["damage"] = 8; stats["health"] = 100;
                        stats["armor"] = 20; stats["stamina"] = 20; break;
                    case SECT_GUEST:  // 顾问：外部顾问，外交提升
                        stats["diplomacy"] = 6; stats["intelligence"] = 4; stats["mana"] = 30;
                        stats["damage"] = 5; stats["health"] = 40; break;
                }

                var trait = new ActorTrait
                {
                    id = id,
                    group_id = "ds_sect_group",
                    can_be_removed = canBeRemoved,
                    can_be_given = true,
                    needs_to_be_explored = false,
                    has_locales = true,  // 使用游戏本地化系统，根据特质id查找翻译
                    has_description_1 = true,
                    base_stats = stats,
                    path_icon = "trait/" + id,
                };

                AssetManager.traits.add(trait);
                DSDebug.Verbose("组织特质注册成功: " + id + " (" + name + ")");
            }
            catch (System.Exception e)
            {
                DSDebug.Error("[DivineAscension] 组织特质注册失败: " + id + " - " + e.Message + "\n" + e.StackTrace);
            }
        }

        // ============================================================
        //  境界Trait操作（互斥保证）
        // ============================================================

        /// <summary>
        /// 设置单位境界Trait。自动移除所有其他境界Trait（互斥）。
        /// </summary>
        public static void SetRealmTrait(Actor actor, int tier)
        {
            if (actor == null || tier < 1 || tier > 13) return;

            string target = _realmTraits[tier - 1];
            if (actor.hasTrait(target)) return;

            RemoveAllRealmTraits(actor);
            actor.addTrait(target, false); // false=不检查opposite（境界间互斥由代码控制）

            // 奇点自动绑定原生不朽特质（文档8.3节）
            if (tier >= 12)
            {
                GrantImmortal(actor);
            }
        }

        /// <summary>移除单位身上全部境界Trait</summary>
        public static void RemoveAllRealmTraits(Actor actor)
        {
            if (actor == null) return;
            foreach (string traitId in _realmTraits)
            {
                if (actor.hasTrait(traitId))
                {
                    actor.removeTrait(traitId);
                }
            }
        }

        /// <summary>
        /// 按组织职位授予/刷新组织身份特质（职位变化时先清旧再授新）。
        /// 组织系统与特质系统接轨：加入组织即获得对应职位特质，单位面板可见组织身份。
        /// 创始人额外获得 SECT_FOUNDER（与职位特质并存）。
        /// </summary>
        public static void ApplySectTrait(Actor actor, int rank, bool isFounder)
        {
            if (actor == null) return;
            // 先移除旧组织特质
            foreach (string traitId in _sectTraits)
            {
                if (actor.hasTrait(traitId)) actor.removeTrait(traitId);
            }

            string target = null;
            switch ((Code.Sect.SectRank)rank)
            {
                case Code.Sect.SectRank.Probationary: target = SECT_PROBATION; break;
                case Code.Sect.SectRank.Member: target = SECT_MEMBER; break;
                case Code.Sect.SectRank.Senior: target = SECT_ELITE; break;
                case Code.Sect.SectRank.Captain: target = SECT_CADRE; break;
                case Code.Sect.SectRank.Commander: target = SECT_ELDER; break;
                case Code.Sect.SectRank.Leader: target = SECT_LEADER; break;
                case Code.Sect.SectRank.DeputyLeader: target = SECT_DEPUTY; break;
                case Code.Sect.SectRank.Guardian: target = SECT_GUARDIAN; break;
                case Code.Sect.SectRank.Advisor: target = SECT_GUEST; break;
                case Code.Sect.SectRank.Heritor: target = SECT_HERITAGE; break;
                case Code.Sect.SectRank.Founder: target = SECT_FOUNDER; break;
            }
            if (!string.IsNullOrEmpty(target) && !actor.hasTrait(target))
                actor.addTrait(target, false);
            if (isFounder && !actor.hasTrait(SECT_FOUNDER))
                actor.addTrait(SECT_FOUNDER, false);
        }

        /// <summary>移除全部组织特质（离开组织/组织解散时）</summary>

        /// <summary>获取当前境界阶位（1~13），返回0表示未异能</summary>
        public static int GetCurrentRealmTier(Actor actor)
        {
            if (actor == null) return 0;
            for (int i = 0; i < _realmTraits.Length; i++)
            {
                if (actor.hasTrait(_realmTraits[i])) return i + 1;
            }
            return 0;
        }

        /// <summary>获取指定境界（1~13）的特质ID，供图鉴等UI读取境界属性加成</summary>
        public static string GetRealmTraitId(int tier)
        {
            if (tier < 1 || tier > 13) return null;
            return _realmTraits[tier - 1];
        }

        // ============================================================
        //  体系归属操作（元数据存储，不再作为特质）
        // ============================================================

        // ============================================================
        //  原生不朽特质操作
        // ============================================================

        public static void GrantImmortal(Actor actor)
        {
            if (actor == null) return;
            if (!actor.hasTrait(NATIVE_IMMORTAL))
                actor.addTrait(NATIVE_IMMORTAL, false);
        }

        // ============================================================
        //  工具方法
        // ============================================================

    }
}

        /// <summary>
        /// 注册"被黑色方碑标记"特质：4阶突破时获得，标记单位已受过方碑辐射影响。
        /// 效果：免疫新的突变特质（与4阶境界特质的免疫效果叠加），作为方碑进化的视觉标记。
        /// </summary>
