// ============================================================
// 异能数据 · 元素系（v0.18.0）
// 大类：元素系 | 小类：火/水/风/雷/冰/土/木/光/暗/金属
// 注意：fire/water/… 10 个 id 为 v0.17.0 旧档异能，禁止改名/删（旧档 ds_prototype 兼容）。
// 描述 = 真实机制（由主/副标签组合表达），不含虚假效果。
// ============================================================

using System.Collections.Generic;

namespace Code.Realm.Prototype
{
    public static class PrototypeData_Elemental
    {
        public static readonly List<PrototypeDef> List = new List<PrototypeDef>
        {
            // ---- 火 ----
            new PrototypeDef("fire", "御火", "灼烧敌人，攻击波及周围", "元素系", "火",
                PrototypeTag.Burn, PrototypeTag.Chain, 6, 6),
            new PrototypeDef("el_fire_02", "炎爆", "强化灼烧，攻击溅射连锁", "元素系", "火",
                PrototypeTag.Plasma, PrototypeTag.Chain, 6, 2),
            new PrototypeDef("el_fire_03", "业火", "狂暴当次攻击+25%，附加灼烧", "元素系", "火",
                PrototypeTag.Berserk, PrototypeTag.Burn, 6, 5),
            new PrototypeDef("el_fire_04", "火衣", "受击概率自愈，攻击附加灼烧", "元素系", "火",
                PrototypeTag.Regeneration, PrototypeTag.Burn, 6, 8),
            new PrototypeDef("el_fire_05", "焚世", "等离子灼烧，低血量直接焚灭", "元素系", "火",
                PrototypeTag.Plasma, PrototypeTag.Execute, 6, 9),
            // ---- 水 ----
            new PrototypeDef("water", "御水", "治疗自身，护盾减伤", "元素系", "水",
                PrototypeTag.Heal, PrototypeTag.Shield, 2, 1),
            new PrototypeDef("el_water_02", "潮汐", "攻击溅射波及周围，减速目标", "元素系", "水",
                PrototypeTag.Chain, PrototypeTag.Slow, 2, 5),
            new PrototypeDef("el_water_03", "洪流", "受击回血，受击概率免伤", "元素系", "水",
                PrototypeTag.Blessing, PrototypeTag.Dodge, 2, 9),
            // ---- 风 ----
            new PrototypeDef("wind", "疾风", "身法闪避，攻击减速目标", "元素系", "风",
                PrototypeTag.Dodge, PrototypeTag.Slow, 1, 1),
            new PrototypeDef("el_wind_02", "龙卷", "攻击溅射连锁，概率震晕敌人", "元素系", "风",
                PrototypeTag.Chain, PrototypeTag.Stun, 1, 5),
            new PrototypeDef("el_wind_03", "御风", "闪避强化，受击概率免伤", "元素系", "风",
                PrototypeTag.Illusion, PrototypeTag.Dodge, 1, 9),
            // ---- 雷 ----
            new PrototypeDef("lightning", "驭雷", "攻击溅射连锁，概率麻痹眩晕", "元素系", "雷",
                PrototypeTag.Chain, PrototypeTag.Stun, 6, 3),
            new PrototypeDef("el_lightning_02", "雷锤", "攻击念力贯穿高伤，麻痹", "元素系", "雷",
                PrototypeTag.Telekinesis, PrototypeTag.Stun, 6, 6),
            new PrototypeDef("el_lightning_03", "磁场", "受击麻痹攻击者，攻击眩晕", "元素系", "雷",
                PrototypeTag.Magnetism, PrototypeTag.Stun, 6, 9),
            // ---- 冰 ----
            new PrototypeDef("ice", "霜降", "减速冻结目标，附加持续灼伤", "元素系", "冰",
                PrototypeTag.Slow, PrototypeTag.Burn, 4, 1),
            new PrototypeDef("el_ice_02", "暴风雪", "深度冰冻目标，当次攻击+5%寒霜微伤", "元素系", "冰",
                PrototypeTag.Frostbite, PrototypeTag.Frost, 4, 5),
            new PrototypeDef("el_ice_03", "冰甲", "护盾减伤，受击反击震慑攻击者", "元素系", "冰",
                PrototypeTag.Shield, PrototypeTag.Vibration, 4, 9),
            new PrototypeDef("el_ice_04", "寒霜", "减速目标，附加寒霜微伤", "元素系", "冰",
                PrototypeTag.Slow, PrototypeTag.Frost, 4, 2),
            new PrototypeDef("el_ice_05", "冰棱", "寒霜微伤，震退目标", "元素系", "冰",
                PrototypeTag.Frost, PrototypeTag.Knockback, 4, 3),
            new PrototypeDef("el_ice_06", "冻土", "深度冻结，石化定身", "元素系", "冰",
                PrototypeTag.Frostbite, PrototypeTag.Petrify, 4, 4),
            new PrototypeDef("el_ice_07", "冰封", "眩晕定身，深度冻结", "元素系", "冰",
                PrototypeTag.Stun, PrototypeTag.Frostbite, 4, 6),
            new PrototypeDef("el_ice_08", "极寒", "深度冻结，扭曲时间减速", "元素系", "冰",
                PrototypeTag.Frostbite, PrototypeTag.TimeSlow, 4, 7),
            new PrototypeDef("el_ice_09", "永冻", "石化定身，时缓强减速", "元素系", "冰",
                PrototypeTag.Petrify, PrototypeTag.TimeSlow, 4, 8),
            // ---- 土 ----
            new PrototypeDef("earth", "地裂", "护盾减伤，攻击波及周围", "元素系", "土",
                PrototypeTag.Shield, PrototypeTag.Petrify, 2, 1),
            new PrototypeDef("el_earth_02", "岩铠", "护盾减伤，元素抗性", "元素系", "土",
                PrototypeTag.Shield, PrototypeTag.ElementResist, 2, 5),
            new PrototypeDef("el_earth_03", "山崩", "攻击重力压制目标，溅射波及", "元素系", "土",
                PrototypeTag.Gravity, PrototypeTag.Chain, 2, 9),
            new PrototypeDef("el_earth_04", "沙暴", "攻击波及周围，减速目标", "元素系", "土",
                PrototypeTag.Chain, PrototypeTag.Slow, 2, 2),
            new PrototypeDef("el_earth_05", "磐石", "护盾减伤，受击反弹", "元素系", "土",
                PrototypeTag.Shield, PrototypeTag.Thorns, 2, 3),
            new PrototypeDef("el_earth_06", "地陷", "重力压制，眩晕定身", "元素系", "土",
                PrototypeTag.Gravity, PrototypeTag.Stun, 2, 4),
            new PrototypeDef("el_earth_07", "岩突", "石化定身，震退目标", "元素系", "土",
                PrototypeTag.Petrify, PrototypeTag.Knockback, 2, 6),
            new PrototypeDef("el_earth_08", "大地震击", "眩晕定身，波及周围", "元素系", "土",
                PrototypeTag.Stun, PrototypeTag.Chain, 2, 7),
            new PrototypeDef("el_earth_09", "山岳", "元素抗性，石化定身", "元素系", "土",
                PrototypeTag.ElementResist, PrototypeTag.Petrify, 2, 8),
            // ---- 木 ----
            new PrototypeDef("wood", "御木", "治疗自身，攻击减速缠绕目标", "元素系", "木",
                PrototypeTag.Heal, PrototypeTag.Slow, 2, 1),
            new PrototypeDef("el_wood_02", "森罗", "受击回血，攻击附加中毒", "元素系", "木",
                PrototypeTag.Regeneration, PrototypeTag.Poison, 2, 5),
            new PrototypeDef("el_wood_03", "缠藤", "攻击眩晕定身，减速目标", "元素系", "木",
                PrototypeTag.Stun, PrototypeTag.Slow, 2, 9),
            new PrototypeDef("el_wood_04", "萌芽", "受击自愈，净化负面", "元素系", "木",
                PrototypeTag.Regeneration, PrototypeTag.Cleanse, 2, 2),
            new PrototypeDef("el_wood_05", "荆棘", "受击反弹，减速目标", "元素系", "木",
                PrototypeTag.Thorns, PrototypeTag.Slow, 2, 3),
            new PrototypeDef("el_wood_06", "藤蔓", "缠绕定身，附加中毒", "元素系", "木",
                PrototypeTag.Entangle, PrototypeTag.Poison, 2, 4),
            new PrototypeDef("el_wood_07", "森语", "攻击回血，受击自愈", "元素系", "木",
                PrototypeTag.Heal, PrototypeTag.Regeneration, 2, 6),
            new PrototypeDef("el_wood_08", "古树守护", "护盾减伤，受击自愈", "元素系", "木",
                PrototypeTag.Shield, PrototypeTag.Regeneration, 2, 7),
            new PrototypeDef("el_wood_09", "万木逢春", "群疗友军，净化负面", "元素系", "木",
                PrototypeTag.HealAlly, PrototypeTag.Cleanse, 2, 8),
            // ---- 光 ----
            new PrototypeDef("light", "圣光", "治疗自身，护盾减伤", "光明系", "圣光",
                PrototypeTag.Heal, PrototypeTag.Shield, 3, 4),
            new PrototypeDef("el_light_02", "净化", "攻击概率回复生命，治疗强化", "光明系", "净化",
                PrototypeTag.Soul, PrototypeTag.Cleanse, 3, 2),
            new PrototypeDef("el_light_03", "光罩", "受击免伤强化，受击回血", "光明系", "圣光",
                PrototypeTag.Invisibility, PrototypeTag.Blessing, 3, 5),
            // ---- 暗 ----
            new PrototypeDef("dark", "暗影", "攻击吸血，附加暗蚀持续伤害", "黑暗系", "暗影",
                PrototypeTag.Lifesteal, PrototypeTag.Burn, 5, 7),
            new PrototypeDef("el_dark_02", "猎杀", "攻击吸血强化，概率暴击", "黑暗系", "暗影",
                PrototypeTag.Blood, PrototypeTag.Crit, 5, 8),
            new PrototypeDef("el_dark_03", "侵蚀", "攻击附加诅咒持续伤害，虚弱目标", "黑暗系", "夜幕",
                PrototypeTag.Curse, PrototypeTag.Weakness, 5, 3),
            // ---- 金属 ----
            new PrototypeDef("metal", "御金", "攻击暴击高伤，护盾硬化", "元素系", "金属",
                PrototypeTag.Crit, PrototypeTag.Shield, 0, 5),
            new PrototypeDef("el_metal_02", "钢铁洪流", "攻击念力贯穿高伤，溅射波及", "元素系", "金属",
                PrototypeTag.Telekinesis, PrototypeTag.Chain, 0, 7),
            new PrototypeDef("el_metal_03", "金身", "护盾减伤强化，受击反震眩晕攻击者", "元素系", "金属",
                PrototypeTag.Shield, PrototypeTag.Magnetism, 0, 9),
            new PrototypeDef("el_metal_04", "精钢", "护盾减伤，受击反弹伤害", "元素系", "金属",
                PrototypeTag.Shield, PrototypeTag.Thorns, 0, 6),
            new PrototypeDef("el_metal_05", "万剑归宗", "念力万剑贯穿，低血量斩杀", "元素系", "金属",
                PrototypeTag.Telekinesis, PrototypeTag.Execute, 0, 8),
        };
    }
}
