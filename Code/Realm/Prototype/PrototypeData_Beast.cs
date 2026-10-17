// ============================================================
// 异能数据 · 兽化系（v0.18.99 大众认知补齐）
// 大类：兽化系（大众认知占比最高的异能体系）
// 小类：猛兽/飞禽/爬行/虫豸/神兽
// 机制 = 主/副标签组合（复用 45 标签引擎，无新增机制）；
// 大众意象 = 名称/描述贴合对应生物的战斗特征。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Beast
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 猛兽 ----
            new PrototypeDef("beast_tiger_roar", "虎啸", "声波震慑，恐惧+眩晕目标", "兽化系", "猛兽",
                PrototypeTag.Fear, PrototypeTag.Stun, 0, 5),
            new PrototypeDef("beast_wolf_pack", "狼群撕咬", "攻击吸血，多重连击", "兽化系", "猛兽",
                PrototypeTag.Lifesteal, PrototypeTag.MultiShot, 0, 2),
            new PrototypeDef("beast_bear_strength", "熊罴之力", "狂暴强化当次攻击，震退目标", "兽化系", "猛兽",
                PrototypeTag.Berserk, PrototypeTag.Knockback, 2, 4),
            new PrototypeDef("beast_lion_awe", "狮王威压", "恐惧震慑，斩杀重伤目标", "兽化系", "猛兽",
                PrototypeTag.Fear, PrototypeTag.Execute, 0, 6),
            new PrototypeDef("beast_leopard_strike", "豹袭", "多重连击，受击闪避", "兽化系", "猛兽",
                PrototypeTag.MultiShot, PrototypeTag.Dodge, 1, 3),
            // ---- 飞禽 ----
            new PrototypeDef("beast_eagle_eye", "鹰眼锁定", "攻击暴击，破甲穿透", "兽化系", "飞禽",
                PrototypeTag.Crit, PrototypeTag.ArmorBreak, 4, 4),
            new PrototypeDef("beast_falcon_dive", "隼击", "斩杀重伤，攻击暴击", "兽化系", "飞禽",
                PrototypeTag.Execute, PrototypeTag.Crit, 1, 6),
            new PrototypeDef("beast_roc_wings", "鹏翼御风", "攻击溅射，时缓强减速", "兽化系", "飞禽",
                PrototypeTag.Chain, PrototypeTag.TimeSlow, 1, 8),
            // ---- 爬行 ----
            new PrototypeDef("beast_viper_fang", "蛇蛊毒牙", "毒蚀目标，斩杀重伤", "兽化系", "爬行",
                PrototypeTag.Poison, PrototypeTag.Execute, 2, 5),
            new PrototypeDef("beast_croc_roll", "鳄噬缠杀", "缠绕定身，血祭回生", "兽化系", "爬行",
                PrototypeTag.Entangle, PrototypeTag.Blood, 2, 6),
            new PrototypeDef("beast_lizard_regrow", "蜥蜴再生", "再生自愈，护盾减伤", "兽化系", "爬行",
                PrototypeTag.Regeneration, PrototypeTag.Shield, 2, 3),
            // ---- 虫豸 ----
            new PrototypeDef("beast_spider_web", "蛛网缚敌", "缠绕定身，减速目标", "兽化系", "虫豸",
                PrototypeTag.Entangle, PrototypeTag.Slow, 2, 3),
            new PrototypeDef("beast_scorpion_sting", "蝎尾毒针", "毒蚀目标，破甲穿透", "兽化系", "虫豸",
                PrototypeTag.Poison, PrototypeTag.ArmorBreak, 2, 5),
            new PrototypeDef("beast_bee_swarm", "蜂群蜇刺", "多重连击，毒蚀目标", "兽化系", "虫豸",
                PrototypeTag.MultiShot, PrototypeTag.Poison, 2, 2),
            // ---- 神兽 ----
            new PrototypeDef("beast_dragon_awes", "龙威浩荡", "恐惧震慑，等离子龙息灼烧", "兽化系", "神兽",
                PrototypeTag.Fear, PrototypeTag.Plasma, 0, 9),
            new PrototypeDef("beast_qilin_bless", "麒麟瑞气", "祝福庇护，治疗友军", "兽化系", "神兽",
                PrototypeTag.Blessing, PrototypeTag.HealAlly, 5, 8),
            new PrototypeDef("beast_phoenix_burn", "朱雀焚天", "灼烧目标，等离子强化燃烧", "兽化系", "神兽",
                PrototypeTag.Burn, PrototypeTag.Plasma, 0, 9),
            new PrototypeDef("beast_tortoise_guard", "玄武镇守", "护盾减伤，元素抗性", "兽化系", "神兽",
                PrototypeTag.Shield, PrototypeTag.ElementResist, 2, 8),
            new PrototypeDef("beast_white_tiger", "白虎啸天", "声波震慑，斩杀重伤", "兽化系", "神兽",
                PrototypeTag.Execute, PrototypeTag.Sound, 0, 9),
        };
    }
}
