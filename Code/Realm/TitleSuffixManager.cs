// ============================================================

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Code.Data;
using Code.Core;
using Code.UI;

namespace Code.Realm
{
    /// <summary>
    /// 称号和境界后缀管理器
    /// 自动更新异能者的名称，添加称号和境界后缀
    /// </summary>
    public static class TitleSuffixManager
    {
        // ============================================================
        //  基因称号词库（48基因 × 6词：掌握基因后随机取一个，终身缓存）
        //  现代异能风格：体现能力特征，不体现高低
        // ============================================================
        private static readonly Dictionary<string, string[]> _elementTitleMap = new Dictionary<string, string[]>
        {
            {"gene_muscle_growth", new[]{"肌霸", "力场", "强化", "巨力", "刚躯", "爆发"}},
            {"gene_muscle_fiber", new[]{"速肌", "快肌", "疾肌", "猛肌", "烈肌", "暴肌"}},
            {"gene_muscle_density", new[]{"密肌", "实肌", "硬肌", "钢肌", "铁肌", "坚肌"}},
            {"gene_muscle_contraction", new[]{"强缩", "快缩", "猛缩", "烈缩", "暴缩", "急缩"}},
            {"gene_skeletal_support", new[]{"强骨", "硬骨", "坚骨", "钢骨", "铁骨", "密骨"}},
            {"gene_bone_density", new[]{"密骨", "实骨", "硬骨", "坚骨", "钢骨", "铁骨"}},
            {"gene_nerve_conduction", new[]{"速神", "快神", "疾神", "闪神", "电神", "光神"}},
            {"gene_reflex_arc", new[]{"快反", "急反", "敏反", "灵反", "捷反", "瞬反"}},
            {"gene_sense_of_balance", new[]{"平衡", "稳衡", "定衡", "平感", "稳感", "定感"}},
            {"gene_spatial_sense", new[]{"空间", "立体", "维度", "方位", "位置", "坐标"}},
            {"gene_tactile_receptor", new[]{"触觉", "触感", "摸感", "压感", "温感", "痛感"}},
            {"gene_gustatory_receptor", new[]{"味觉", "味感", "尝感", "食感", "口感", "舌感"}},
            {"gene_cell_division", new[]{"再生", "愈合", "修复", "恢复", "重生", "再造"}},
            {"gene_immune_response", new[]{"免疫", "抗毒", "抗病", "防御", "抵抗", "防护"}},
            {"gene_metabolic_rate", new[]{"代谢", "燃烧", "消耗", "转化", "能量", "活力"}},
            {"gene_skin_thickness", new[]{"厚皮", "硬皮", "坚皮", "密皮", "实皮", "糙皮"}},
            {"gene_tendon_strength", new[]{"强筋", "硬筋", "坚筋", "密筋", "实筋", "韧筋"}},
            {"gene_organ_function", new[]{"强脏", "硬脏", "坚脏", "密脏", "实脏", "健脏"}},
            {"gene_synaptic_connection", new[]{"智联", "脑联", "神联", "思联", "念联", "意联"}},
            {"gene_brain_region_development", new[]{"强脑", "大脑", "全脑", "优脑", "灵脑", "慧脑"}},
            {"gene_memory_formation", new[]{"强记", "博记", "广记", "久记", "深记", "牢记"}},
            {"gene_information_processing", new[]{"强算", "快算", "优算", "灵算", "慧算", "妙算"}},
            {"gene_logical_reasoning", new[]{"强逻", "快逻", "优逻", "灵逻", "慧逻", "妙逻"}},
            {"gene_creativity", new[]{"强创", "快创", "优创", "灵创", "慧创", "妙创"}},
            {"gene_visual_receptor", new[]{"强视", "远视", "夜视", "透视", "显微", "望远"}},
            {"gene_auditory_receptor", new[]{"强听", "远听", "细听", "辨听", "灵听", "敏听"}},
            {"gene_olfactory_receptor", new[]{"强嗅", "远嗅", "细嗅", "辨嗅", "灵嗅", "敏嗅"}},
            {"gene_pain_tolerance", new[]{"忍痛", "耐痛", "抗痛", "无痛", "强耐", "坚忍"}},
            {"gene_proprioception", new[]{"体感", "身感", "位感", "姿感", "动感", "衡感"}},
            {"gene_intuition", new[]{"直觉", "预感", "灵感", "灵觉", "妙觉", "神觉"}},
            {"gene_mental_focus", new[]{"专注", "凝神", "聚精", "会神", "入定", "冥想"}},
            {"gene_emotion_control", new[]{"控情", "制情", "忘情", "无情", "冷静", "理智"}},
            {"gene_stress_tolerance", new[]{"抗压", "耐压", "抗挫", "耐挫", "坚韧", "不屈"}},
            {"gene_concentration", new[]{"注意", "专注", "凝神", "聚精", "会神", "专心"}},
            {"gene_willpower", new[]{"意志", "信念", "决心", "毅力", "坚韧", "执着"}},
            {"gene_empathy", new[]{"共情", "共鸣", "同感", "共感", "连心", "通灵"}},
            {"gene_ability_perception", new[]{"异能", "超能", "念力", "心灵", "精神", "意识"}},
            {"gene_energy_absorption", new[]{"吸收", "汲取", "虹吸", "吞噬", "掠夺", "吸纳"}},
            {"gene_energy_conversion", new[]{"转化", "转换", "蜕变", "升华", "进化", "变异"}},
            {"gene_energy_condensation", new[]{"凝聚", "压缩", "结晶", "实体", "具象", "显化"}},
            {"gene_energy_release", new[]{"释放", "爆发", "喷涌", "放射", "炸裂", "冲击"}},
            {"gene_energy_manipulation", new[]{"操控", "支配", "掌控", "主宰", "御使", "驱使"}},
            {"gene_potential_seal", new[]{"封印", "枷锁", "桎梏", "牢笼", "禁锢", "封锁"}},
            {"gene_potential_excitation", new[]{"激发", "唤醒", "引爆", "点燃", "觉醒", "激活"}},
            {"gene_limit_break", new[]{"突破", "超越", "打破", "粉碎", "极限", "越级"}},
            {"gene_cell_dormancy", new[]{"休眠", "沉睡", "假死", "蛰伏", "冬眠", "停滞"}},
            {"gene_telomere_repair", new[]{"修复", "再生", "恢复", "长生", "不老", "永恒"}},
            {"gene_energy_supply", new[]{"供给", "供应", "源泉", "不竭", "无限", "永动"}},
            {"gene_reaction_speed", new[]{"极速", "神速", "瞬发", "秒杀", "一击", "必杀"}},
            {"gene_life_sublimation", new[]{"升华", "超脱", "进化", "蜕变", "超越", "突破"}},
            {"gene_structural_source", new[]{"本源", "根源", "本质", "结构", "形态", "构造"}},
            {"gene_neural_source", new[]{"神经", "意识", "心灵", "精神", "思维", "认知"}},
            {"gene_life_source", new[]{"生命", "生机", "活力", "存在", "本质", "本源"}},
            {"gene_intelligence_source", new[]{"智力", "智慧", "知识", "理解", "洞察", "启蒙"}},
            {"gene_perception_source", new[]{"感知", "感觉", "觉知", "意识", "认知", "领悟"}},
            {"gene_will_source", new[]{"意志", "信念", "决心", "毅力", "坚韧", "执着"}},
            {"gene_ability_source", new[]{"异能", "超能", "能力", "潜力", "天赋", "天资"}},
            {"gene_potential_source", new[]{"潜能", "潜力", "天赋", "天资", "资质", "根骨"}},
        };

        private static readonly Dictionary<string, string[]> _elementTitleMapEn = new Dictionary<string, string[]>
        {
            {"gene_muscle_growth", new[]{"Titan", "Hercules", "Colossus", "Juggernaut", "Behemoth", "Goliath"}},
            {"gene_muscle_fiber", new[]{"Berserker", "Fury", "Rage", "Wrath", "Frenzy", "Violent"}},
            {"gene_muscle_density", new[]{"Adamant", "Mithril", "Orichalcum", "Adamantine", "Impervium", "Unbreakable"}},
            {"gene_muscle_contraction", new[]{"Thunder", "Lightning", "Bolt", "Flash", "Spark", "Surge"}},
            {"gene_skeletal_support", new[]{"Pillar", "Column", "Spire", "Monolith", "Obelisk", "Tower"}},
            {"gene_bone_density", new[]{"Bone", "Skeleton", "Frame", "Structure", "Scaffold", "Ribcage"}},
            {"gene_nerve_conduction", new[]{"Swift", "Quick", "Rapid", "Fleet", "Nimble", "Agile"}},
            {"gene_reflex_arc", new[]{"Instinct", "Reflex", "Reaction", "Response", "Impulse", "Spark"}},
            {"gene_sense_of_balance", new[]{"Balance", "Equilibrium", "Poise", "Grace", "Elegance", "Harmony"}},
            {"gene_spatial_sense", new[]{"Spatial", "Dimensional", "Geometric", "Topological", "Cartesian", "Vector"}},
            {"gene_tactile_receptor", new[]{"Tactile", "Haptic", "Touch", "Feel", "Texture", "Sensation"}},
            {"gene_gustatory_receptor", new[]{"Gustatory", "Flavor", "Taste", "Savor", "Aroma", "Palate"}},
            {"gene_cell_division", new[]{"Regeneration", "Healing", "Recovery", "Restoration", "Renewal", "Revival"}},
            {"gene_immune_response", new[]{"Immunity", "Resistance", "Defense", "Shield", "Barrier", "Ward"}},
            {"gene_metabolic_rate", new[]{"Metabolism", "Energy", "Vitality", "Stamina", "Endurance", "Durability"}},
            {"gene_skin_thickness", new[]{"Armor", "Plating", "Scales", "Hide", "Pelt", "Shell"}},
            {"gene_tendon_strength", new[]{"Tendon", "Ligament", "Sinew", "Fiber", "Thread", "String"}},
            {"gene_organ_function", new[]{"Organ", "Visceral", "Internal", "Gland", "Viscera", "Bowels"}},
            {"gene_synaptic_connection", new[]{"Synapse", "Neuron", "Nerve", "Brain", "Mind", "Intellect"}},
            {"gene_brain_region_development", new[]{"Cortex", "Cerebrum", "Cerebellum", "Thalamus", "Hypothalamus", "Hippocampus"}},
            {"gene_memory_formation", new[]{"Memory", "Recall", "Remembrance", "Recollection", "Anamnesis", "Mnemosyne"}},
            {"gene_information_processing", new[]{"Processing", "Computation", "Calculation", "Analysis", "Synthesis", "Integration"}},
            {"gene_logical_reasoning", new[]{"Logic", "Reason", "Rationality", "Deduction", "Induction", "Inference"}},
            {"gene_creativity", new[]{"Creativity", "Imagination", "Inspiration", "Innovation", "Originality", "Ingenuity"}},
            {"gene_visual_receptor", new[]{"Vision", "Sight", "Perception", "Observation", "View", "Gaze"}},
            {"gene_auditory_receptor", new[]{"Hearing", "Listening", "Audition", "Sound", "Acoustic", "Audio"}},
            {"gene_olfactory_receptor", new[]{"Smell", "Olfaction", "Scent", "Aroma", "Fragrance", "Odor"}},
            {"gene_pain_tolerance", new[]{"Pain", "Suffering", "Agony", "Torment", "Misery", "Distress"}},
            {"gene_proprioception", new[]{"Proprioception", "Kinesthesia", "Body", "Position", "Location", "Orientation"}},
            {"gene_intuition", new[]{"Intuition", "Hunch", "Gut", "Instinct", "Feeling", "Sense"}},
            {"gene_mental_focus", new[]{"Focus", "Concentration", "Attention", "Mindfulness", "Awareness", "Alertness"}},
            {"gene_emotion_control", new[]{"Emotion", "Feeling", "Sentiment", "Mood", "Temper", "Disposition"}},
            {"gene_stress_tolerance", new[]{"Stress", "Pressure", "Tension", "Strain", "Burden", "Load"}},
            {"gene_concentration", new[]{"Concentration", "Focus", "Absorption", "Engrossment", "Immersion", "Rapture"}},
            {"gene_willpower", new[]{"Willpower", "Resolve", "Determination", "Tenacity", "Persistence", "Perseverance"}},
            {"gene_empathy", new[]{"Empathy", "Compassion", "Sympathy", "Understanding", "Kindness", "Warmth"}},
            {"gene_ability_perception", new[]{"Ability", "Power", "Talent", "Gift", "Capability", "Potential"}},
            {"gene_energy_absorption", new[]{"Absorption", "Assimilation", "Incorporation", "Integration", "Embodiment", "Inclusion"}},
            {"gene_energy_conversion", new[]{"Conversion", "Transformation", "Transmutation", "Alchemy", "Metamorphosis", "Change"}},
            {"gene_energy_condensation", new[]{"Condensation", "Compression", "Solidification", "Crystallization", "Materialization", "Embodiment"}},
            {"gene_energy_release", new[]{"Release", "Emission", "Discharge", "Eruption", "Explosion", "Burst"}},
            {"gene_energy_manipulation", new[]{"Manipulation", "Control", "Mastery", "Domination", "Command", "Sovereignty"}},
            {"gene_potential_seal", new[]{"Seal", "Lock", "Bind", "Chain", "Shackle", "Fetter"}},
            {"gene_potential_excitation", new[]{"Excitation", "Awakening", "Activation", "Stimulation", "Provocation", "Incitement"}},
            {"gene_limit_break", new[]{"Breakthrough", "Transcendence", "Ascension", "Elevation", "Exaltation", "Apothesis"}},
            {"gene_cell_dormancy", new[]{"Dormancy", "Hibernation", "Sleep", "Slumber", "Rest", "Repose"}},
            {"gene_telomere_repair", new[]{"Repair", "Restoration", "Renewal", "Rejuvenation", "Revitalization", "Regeneration"}},
            {"gene_energy_supply", new[]{"Supply", "Source", "Reservoir", "Pool", "Store", "Stockpile"}},
            {"gene_reaction_speed", new[]{"Speed", "Velocity", "Swiftness", "Celerity", "Rapidity", "Fleetness"}},
            {"gene_life_sublimation", new[]{"Sublimation", "Transcendence", "Ascension", "Elevation", "Exaltation", "Apothesis"}},
            {"gene_structural_source", new[]{"Structure", "Form", "Shape", "Pattern", "Design", "Blueprint"}},
            {"gene_neural_source", new[]{"Neural", "Nerve", "Brain", "Mind", "Consciousness", "Awareness"}},
            {"gene_life_source", new[]{"Life", "Vitality", "Animation", "Existence", "Being", "Essence"}},
            {"gene_intelligence_source", new[]{"Intelligence", "Wisdom", "Knowledge", "Understanding", "Insight", "Enlightenment"}},
            {"gene_perception_source", new[]{"Perception", "Sensation", "Awareness", "Consciousness", "Cognition", "Apprehension"}},
            {"gene_will_source", new[]{"Will", "Volition", "Desire", "Intention", "Purpose", "Resolve"}},
            {"gene_ability_source", new[]{"Ability", "Power", "Capacity", "Potential", "Talent", "Gift"}},
            {"gene_potential_source", new[]{"Potential", "Possibility", "Capacity", "Capability", "Potency", "Latency"}},
        };

        private const string ELEMENT_TITLE_KEY = "ds_element_title";

        // 前缀称号（基因称号）解锁门槛：高境界（8阶起）
        public const int TITLE_UNLOCK_TIER = 8;    // 高境界：8阶（干涉者）起自动解锁，8阶之前没有称号
        // ============================================================
        //  组合式称号词库：词干 × 后缀（约 56×6×30  1万种）
        //  与固定词库混合使用：50% 固定词 / 50% 组合词
        // ============================================================
        private static readonly string[] _titleSuffixPool = new[]
        {
            "操手","窃贼","银行家","观察者","修正员","漂移者","旅者","管理员","守卫","织者",
            "支配者","学徒","专家","技师","向导","猎人","使者","书记","大师","先锋",
            "分析师","工程师","协调者","见证者","收藏家","导航员","接线员","雕刻者","塑造者","仲裁者"
        };

        private static readonly string[] _titleSuffixPoolEn = new[]
        {
            "Operator","Thief","Banker","Observer","Corrector","Drifter","Traveler","Administrator","Guardian","Weaver",
            "Dominator","Apprentice","Expert","Technician","Guide","Hunter","Envoy","Secretary","Master","Pioneer",
            "Analyst","Engineer","Coordinator","Witness","Collector","Navigator","Operator","Carver","Shaper","Arbiter"
        };

        private static readonly Dictionary<string, string[]> _elementStemMap = new Dictionary<string, string[]>
        {
            {"gene_cell_division", new[]{"基因","遗传","染色体","生命码","细胞","基因组"}},
            {"gene_muscle_growth", new[]{"固相","晶体","刚体","物质","矿物","岩层"}},
            {"gene_nerve_conduction", new[]{"热浪","烈焰","温控","燃烧","熵流","高温"}},
            {"gene_mental_focus", new[]{"重力","质量","引力","反重","潮汐","引力井"}},
            {"gene_visual_receptor", new[]{"空间","相位","维度","坐标","门径","折叠"}},
            {"gene_synaptic_connection", new[]{"数据","信息","代码","算法","数字","网络"}},
            {"gene_ability_perception", new[]{"超感","感官","预读","情报","雷达","第六感"}},
            {"gene_potential_seal", new[]{"概率","几率","骰子","命运","意外","幸运"}},
            {"gene_immune_response", new[]{"细胞","微观","组织","生命","单元","再生"}},
            {"gene_muscle_fiber", new[]{"流体","水态","液界","水压","潮流","形态"}},
            {"gene_reflex_arc", new[]{"光子","光束","光速","棱镜","极光","光源"}},
            {"gene_emotion_control", new[]{"斥力","反冲","排斥","推力","碰撞","力场"}},
            {"gene_auditory_receptor", new[]{"时流","时序","秒针","停滞","历史","时间"}},
            {"gene_brain_region_development", new[]{"信号","波段","频率","电磁","频道","信息"}},
            {"gene_energy_absorption", new[]{"思维","心智","意识","脑波","思想","心灵"}},
            {"gene_limit_break", new[]{"熵增","无序","混乱","秩序","热寂","熵值"}},
            {"gene_bone_density", new[]{"进化","物种","演化","突变","适应","自然"}},
            {"gene_muscle_density", new[]{"气流","大气","风压","云层","气体","空气"}},
            {"gene_muscle_contraction", new[]{"电弧","电流","雷暴","电压","电网","电荷"}},
            {"gene_stress_tolerance", new[]{"高压","压强","压缩","压力","深潜","大气"}},
            {"gene_olfactory_receptor", new[]{"维度","高维","投影","裂缝","次元","空间"}},
            {"gene_memory_formation", new[]{"代码","程序","漏洞","防火墙","数字","算法"}},
            {"gene_concentration", new[]{"意志","铁意","精神","信念","决断","意志力"}},
            {"gene_potential_excitation", new[]{"因果","命运","事件","蝴蝶","时间线","链条"}},
            {"gene_metabolic_rate", new[]{"再生","修复","重建","伤痕","器官","恢复"}},
            {"gene_skeletal_support", new[]{"等离子","电离","电浆","星云","弧光","高温"}},
            {"gene_sense_of_balance", new[]{"磁场","极性","磁力","电磁","磁极","磁性"}},
            {"gene_information_processing", new[]{"张力","表面","应力","分子","绷紧","力平衡"}},
            {"gene_tactile_receptor", new[]{"极速","时差","瞬时","音障","光速","加速"}},
            {"gene_pain_tolerance", new[]{"知识","全知","信息","百科","通识","智慧"}},
            {"gene_energy_conversion", new[]{"情绪","心绪","情感","气氛","移情","心情"}},
            {"gene_cell_dormancy", new[]{"虚空","湮灭","空白","归零","寂静","虚无"}},
            {"gene_skin_thickness", new[]{"免疫","抗体","病原","病菌","防御","屏障"}},
            {"gene_tendon_strength", new[]{"金属","合金","钢铁","熔铸","磁化","液态金"}},
            {"gene_spatial_sense", new[]{"动能","冲量","动量","能量","冲击","守恒"}},
            {"gene_logical_reasoning", new[]{"撞击","动量","碰撞","冲击","对撞","破碎"}},
            {"gene_gustatory_receptor", new[]{"相位","频率","振动","波形","共振","振幅"}},
            {"gene_energy_release", new[]{"记忆","回溯","遗忘","经验","档案","时光"}},
            {"gene_energy_condensation", new[]{"直觉","先兆","预感","本能","危险","预读"}},
            {"gene_telomere_repair", new[]{"混沌","无序","随机","熵增","混乱","概率"}},
            {"gene_organ_function", new[]{"真神","界限","超越","形态","次元","上限"}},
            {"gene_energy_supply", new[]{"暗物质","不可见","隐形","引力","暗影","质量"}},
            {"gene_reaction_speed", new[]{"辐射","衰变","同位素","半衰期","射线","核安全"}},
            {"gene_creativity", new[]{"核力","强作用","原子核","聚变","裂变","核能"}},
            {"gene_proprioception", new[]{"折叠","重叠","连续体","拓扑","压缩","折痕"}},
            {"gene_energy_manipulation", new[]{"量子","叠加","隧穿","退相干","比特","纠缠"}},
            {"gene_willpower", new[]{"共鸣","频率","情感","共情","心网","波峰"}},
            {"gene_life_sublimation", new[]{"真理","绝对","公理","事实","真相","锚点"}},
            {"gene_life_source", new[]{"永生","不朽","恒常","不死","岁月","时光"}},
            {"gene_structural_source", new[]{"反物质","湮灭","正反","真空","镜像","对称"}},
            {"gene_neural_source", new[]{"零点","真空","基态","虚粒子","泡沫","取能"}},
            {"gene_intelligence_source", new[]{"统一场","力合","大统一","常数","法则","物理"}},
            {"gene_perception_source", new[]{"奇点","坍缩","视界","密度","时空","黑洞"}},
            {"gene_will_source", new[]{"全息","投影","光刻","复刻","光场","三维"}},
            {"gene_ability_source", new[]{"心网","意识","群体","心灵","脑联","共感"}},
            {"gene_potential_source", new[]{"绝对","终极","永恒","基准","万法","终点"}}
        };

        private static readonly Dictionary<string, string[]> _elementStemMapEn = new Dictionary<string, string[]>
        {
            {"gene_cell_division", new[]{"Gene","Heredity","Chromosome","LifeCode","Cell","Genome"}},
            {"gene_muscle_growth", new[]{"Solid","Crystal","Rigid","Matter","Mineral","Rock"}},
            {"gene_nerve_conduction", new[]{"Heat","Flame","Temp","Burn","Entropy","HighTemp"}},
            {"gene_mental_focus", new[]{"Gravity","Mass","Gravitation","AntiGrav","Tide","GravWell"}},
            {"gene_visual_receptor", new[]{"Space","Phase","Dimension","Coord","Portal","Fold"}},
            {"gene_synaptic_connection", new[]{"Data","Info","Code","Algorithm","Digital","Network"}},
            {"gene_ability_perception", new[]{"SuperSense","Sense","Prefetch","Intel","Radar","SixthSense"}},
            {"gene_potential_seal", new[]{"Probability","Chance","Dice","Fate","Accident","Luck"}},
            {"gene_immune_response", new[]{"Cell","Micro","Tissue","Life","Unit","Regen"}},
            {"gene_muscle_fiber", new[]{"Fluid","Water","Liquid","Pressure","Flow","Form"}},
            {"gene_reflex_arc", new[]{"Photon","Beam","LightSpeed","Prism","Aurora","LightSrc"}},
            {"gene_emotion_control", new[]{"Repulsion","Recoil","Repel","Thrust","Collision","ForceField"}},
            {"gene_auditory_receptor", new[]{"TimeFlow","Chronology","Second","Stasis","History","Time"}},
            {"gene_brain_region_development", new[]{"Signal","Band","Frequency","EM","Channel","Info"}},
            {"gene_energy_absorption", new[]{"Thought","Mind","Consciousness","Brainwave","Idea","Soul"}},
            {"gene_limit_break", new[]{"Entropy","Disorder","Chaos","Order","HeatDeath","EntropyVal"}},
            {"gene_bone_density", new[]{"Evolution","Species","Evolve","Mutation","Adapt","Nature"}},
            {"gene_muscle_density", new[]{"Airflow","Atmosphere","WindPressure","Cloud","Gas","Air"}},
            {"gene_muscle_contraction", new[]{"Arc","Current","Thunder","Voltage","Grid","Charge"}},
            {"gene_stress_tolerance", new[]{"HighPressure","Pressure","Compress","Force","DeepDive","Atmos"}},
            {"gene_olfactory_receptor", new[]{"Dimension","HighDim","Projection","Crack","Dim","Space"}},
            {"gene_memory_formation", new[]{"Code","Program","Bug","Firewall","Digital","Algorithm"}},
            {"gene_concentration", new[]{"Will","IronWill","Spirit","Belief","Decision","Willpower"}},
            {"gene_potential_excitation", new[]{"Causality","Fate","Event","Butterfly","Timeline","Chain"}},
            {"gene_metabolic_rate", new[]{"Regen","Repair","Rebuild","Wound","Organ","Recover"}},
            {"gene_skeletal_support", new[]{"Plasma","Ionize","ElectricPlasma","Nebula","ArcLight","HighTemp"}},
            {"gene_sense_of_balance", new[]{"MagField","Polarity","Magnetism","EM","MagPole","Magnetic"}},
            {"gene_information_processing", new[]{"Tension","Surface","Stress","Molecule","Tight","Balance"}},
            {"gene_tactile_receptor", new[]{"Speed","TimeDiff","Instant","SoundBarrier","LightSpeed","Accel"}},
            {"gene_pain_tolerance", new[]{"Knowledge","Omniscience","Info","Encyclopedia","General","Wisdom"}},
            {"gene_energy_conversion", new[]{"Emotion","Mood","Feeling","Atmosphere","Empathy","Heart"}},
            {"gene_cell_dormancy", new[]{"Void","Annihilate","Blank","Zero","Silence","Nothingness"}},
            {"gene_skin_thickness", new[]{"Immunity","Antibody","Pathogen","Virus","Defense","Barrier"}},
            {"gene_tendon_strength", new[]{"Metal","Alloy","Steel","Cast","Magnetize","LiquidMetal"}},
            {"gene_spatial_sense", new[]{"Kinetic","Impulse","Momentum","Energy","Impact","Conservation"}},
            {"gene_logical_reasoning", new[]{"Impact","Momentum","Collision","Strike","Collision","Break"}},
            {"gene_gustatory_receptor", new[]{"Phase","Frequency","Vibration","Waveform","Resonance","Amplitude"}},
            {"gene_energy_release", new[]{"Memory","Recall","Forget","Experience","Archive","Time"}},
            {"gene_energy_condensation", new[]{"Intuition","Premonition","Hunch","Instinct","Danger","Prefetch"}},
            {"gene_telomere_repair", new[]{"Chaos","Disorder","Random","Entropy","Confusion","Probability"}},
            {"gene_organ_function", new[]{"Transcend","Limit","Beyond","Form","Dimension","UpperLimit"}},
            {"gene_energy_supply", new[]{"DarkMatter","Invisible","Stealth","Gravity","Shadow","Mass"}},
            {"gene_reaction_speed", new[]{"Radiation","Decay","Isotope","HalfLife","Ray","NuclearSafety"}},
            {"gene_creativity", new[]{"Nuclear","StrongForce","Nucleus","Fusion","Fission","NuclearEnergy"}},
            {"gene_proprioception", new[]{"Fold","Overlap","Continuum","Topology","Compress","Crease"}},
            {"gene_energy_manipulation", new[]{"Quantum","Superposition","Tunneling","Decoherence","Qubit","Entanglement"}},
            {"gene_willpower", new[]{"Resonance","Frequency","Emotion","Empathy","MindWeb","WavePeak"}},
            {"gene_life_sublimation", new[]{"Truth","Absolute","Axiom","Fact","Reality","Anchor"}},
            {"gene_life_source", new[]{"Immortal","Eternal","Constant","Undying","Years","Time"}},
            {"gene_structural_source", new[]{"Antimatter","Annihilate","MatterAntimatter","Vacuum","Mirror","Symmetry"}},
            {"gene_neural_source", new[]{"ZeroPoint","Vacuum","GroundState","VirtualParticle","Foam","EnergyHarvest"}},
            {"gene_intelligence_source", new[]{"UnifiedField","ForceUnify","GrandUnify","Constant","Law","Physics"}},
            {"gene_perception_source", new[]{"Singularity","Collapse","Horizon","Density","Spacetime","BlackHole"}},
            {"gene_will_source", new[]{"Hologram","Projection","Lithography","Replica","LightField","3D"}},
            {"gene_ability_source", new[]{"MindWeb","Consciousness","Collective","Soul","BrainLink","Empathy"}},
            {"gene_potential_source", new[]{"Absolute","Ultimate","Eternal","Benchmark","AllLaws","Endpoint"}}
        };

        private const string SECT_TITLE_KEY = "ds_sect_title";
        private const string SECT_TITLE_SECT_KEY = "ds_sect_title_sect";
        private const string BASE_NAME_KEY = "ds_base_name";

        // ============================================================
        //  公共API
        // ============================================================

        /// <summary>更新单位的称号（进阶时调用，触发基因称号生成）</summary>
        /// <remarks>v0.6.1 重构：不再直接修改 actor.name，展示名通过 getName() Postfix 动态添加</remarks>
        public static void UpdateTitleSuffix(Actor actor)
        {
            if (actor == null) return;
            try
            {
                int tier = CultivationData.GetRealmTier(actor);
                if (tier <= 0) return;
                // 触发基因称号生成（满足8阶解锁条件且尚未生成时自动生成）
                GetElementTitle(actor);
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"更新称号失败: {e.Message}");
            }
        }

        /// <summary>获取带后缀的展示名（供 getName() Postfix 调用，不修改存档）</summary>
        public static string GetDisplayName(Actor actor)
        {
            if (actor == null) return "";
            var data = ActorDataAccessor.GetData(actor);
            if (data == null) return "";
            try
            {
                // 存档里的本名（旧存档可能带旧后缀，先剥掉）
                string baseName = StripOurDecorations(data.name);
                if (string.IsNullOrEmpty(baseName)) baseName = data.name;

                int tier = CultivationData.GetRealmTier(actor);
                if (tier <= 0) return baseName; // 未觉醒不添加后缀

                // 基因称号（8阶及以上解锁）
                string geneTitle = GetElementTitle(actor);
                // 境界后缀
                string realmSuffix = Code.Core.DengShenConfig.EnableTitleSuffix ? GetRealmSuffix(tier) : "";

                // 组合展示名：基因称号 - 本名-境界后缀
                string result = baseName;
                if (!string.IsNullOrEmpty(geneTitle)) result = geneTitle + " - " + result;
                if (!string.IsNullOrEmpty(realmSuffix)) result = result + "-" + realmSuffix;
                return result;
            }
            catch { return data.name; }
        }

        /// <summary>获取境界后缀</summary>
        public static string GetRealmSuffix(int tier)
        {
            return UILocalization.GetTierName(tier);
        }

        /// <summary>前缀称号解锁判定：高境界（8阶干涉者起），8阶之前没有称号</summary>
        private static bool HasElementTitleUnlocked(Actor actor)
        {
            try
            {
                int tier = CultivationData.GetRealmTier(actor);
                return tier >= TITLE_UNLOCK_TIER;
            }
            catch { return false; }
        }

        public static string GetElementTitle(Actor actor)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return "";
            try
            {
                // 即使有缓存，也要检查是否满足8阶解锁条件（防止旧缓存的称号在8阶前显示）
                if (!HasElementTitleUnlocked(actor)) return "";

                if (ActorDataAccessor.GetData(actor).custom_data_string != null &&
                    ActorDataAccessor.GetData(actor).custom_data_string.TryGetValue(ELEMENT_TITLE_KEY, out string cached) &&
                    !string.IsNullOrEmpty(cached))
                {
                    return cached; // 已获得的名号终身保留（旧存档强者不受影响）
                }

                //  前缀称号解锁门槛：高境界（8阶干涉者）才拥有名号
                if (!HasElementTitleUnlocked(actor)) return "";

                // 根据主要基因来生成称号（从已解锁基因中选择出现最多的染色体的第一个基因）
                var unlockedForTitle = CultivationData.GetUnlockedElements(actor);
                if (unlockedForTitle == null || unlockedForTitle.Count == 0)
                {
                    return "";
                }

                // 获取主要基因染色体
                int[] groupCountForTitle = new int[8];
                foreach (string eid in unlockedForTitle)
                {
                    var elem = ElementDef.GetById(eid);
                    if (elem != null) groupCountForTitle[elem.Group]++;
                }
                int dominantGroupForTitle = 0, maxCountForTitle = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (groupCountForTitle[i] > maxCountForTitle) { maxCountForTitle = groupCountForTitle[i]; dominantGroupForTitle = i; }
                }

                // 从主要基因染色体中选择第一个已解锁的基因
                string chosenId = null;
                foreach (string eid in unlockedForTitle)
                {
                    var elem = ElementDef.GetById(eid);
                    if (elem != null && elem.Group == dominantGroupForTitle)
                    {
                        chosenId = eid;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(chosenId))
                {
                    chosenId = unlockedForTitle[0]; // 回退：使用第一个已解锁基因
                }

                // 检查主要基因是否有称号词
                if (!_elementTitleMap.TryGetValue(chosenId, out var secondaryTitles) || secondaryTitles == null || secondaryTitles.Length == 0)
                {
                    return "";
                }
                string title;
                // 100% 用组合词（词干×后缀，约1万种不重复），不再使用固定词
                var stemMap = UILocalization.CurrentLanguage == "en" ? _elementStemMapEn : _elementStemMap;
                if (stemMap.TryGetValue(chosenId, out var stems) && stems.Length > 0)
                {
                    string stem = stems[UnityEngine.Random.Range(0, stems.Length)];
                    var suffixPool = UILocalization.CurrentLanguage == "en" ? _titleSuffixPoolEn : _titleSuffixPool;
                    string sfx = suffixPool[UnityEngine.Random.Range(0, suffixPool.Length)];
                    title = stem + sfx;
                }
                else
                {
                    // 回退：如果没有组合词干，使用固定词
                    var titleMap = UILocalization.CurrentLanguage == "en" ? _elementTitleMapEn : _elementTitleMap;
                    var pool = titleMap[chosenId];
                    title = pool[UnityEngine.Random.Range(0, pool.Length)];
                }
                SetElementTitle(actor, title);
                return title;
            }
            catch { return ""; }
        }

        /// <summary>缓存基因称号</summary>
        private static void SetElementTitle(Actor actor, string title)
        {
            if (actor == null || ActorDataAccessor.GetData(actor) == null) return;
            try
            {
                if (ActorDataAccessor.GetData(actor).custom_data_string == null) return;
                ActorDataAccessor.GetData(actor).custom_data_string[ELEMENT_TITLE_KEY] = title ?? "";
            }
            catch { }
        }

        /// <summary>组织职位称号词（队长及以上）</summary>

        // ============================================================
        //  内部方法
        // ============================================================

        /// <summary>剥掉我们的前缀和后缀（供 setName Prefix 调用，存档前清理）</summary>
        /// <remarks>兼容旧存档：旧存档里的名字可能带旧后缀，此方法能正确剥掉</remarks>
        public static string StripOurDecorations(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            string result = name.Trim();

            // 1. 剥掉结尾的"-境界后缀"
            int lastDash = result.LastIndexOf('-');
            if (lastDash > 0)
            {
                string possibleSuffix = result.Substring(lastDash + 1).Trim();
                // 检查是否是已知的境界后缀
                for (int t = 1; t <= 13; t++)
                {
                    if (UILocalization.GetTierName(t) == possibleSuffix)
                    {
                        result = result.Substring(0, lastDash).Trim();
                        break;
                    }
                }
            }

            // 2. 剥掉开头的"基因称号 - "（称号不含空格且长度≤10）
            int sepIndex = result.IndexOf(" - ");
            if (sepIndex > 0 && sepIndex < result.Length - 3)
            {
                string prefix = result.Substring(0, sepIndex);
                if (!string.IsNullOrEmpty(prefix) && prefix.Length <= 10 && !prefix.Contains(" "))
                {
                    result = result.Substring(sepIndex + 3).Trim();
                }
            }

            return result;
        }

        /// <summary>从名称中提取基础名称（兼容旧存档，保留用于迁移）</summary>
        public static string ExtractBaseName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;
            // 直接调用 StripOurDecorations，逻辑统一
            return StripOurDecorations(fullName);
        }
    }

    // ============================================================
    //  Harmony Patch：getName 加后缀 / setName 剥后缀
    //  参考斗破人生 DoupoLifeNameSuffix 的实现方式，简化版
    // ============================================================
    [HarmonyPatch]
    public static class TitleSuffixPatch
    {
        [ThreadStatic]
        private static bool _updating;

        /// <summary>getName Postfix：显示时动态添加称号和境界后缀（不修改存档）</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.getName))]
        static void GetNamePostfix(Actor __instance, ref string __result)
        {
            if (_updating) return; // 防递归
            if (__instance == null) return;
            if (ActorDataAccessor.GetData(__instance) == null) return;

            _updating = true;
            try
            {
                __result = TitleSuffixManager.GetDisplayName(__instance);
            }
            catch { }
            finally { _updating = false; }
        }

        /// <summary>setName Prefix：写回名字前剥掉我们的称号和后缀，只存本名（后缀不进存档）</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(NanoObject), nameof(NanoObject.setName))]
        static bool SetNamePrefix(NanoObject __instance, ref string pName)
        {
            if (_updating) return true; // 防递归
            if (string.IsNullOrEmpty(pName)) return true;

            _updating = true;
            try
            {
                pName = TitleSuffixManager.StripOurDecorations(pName);
            }
            catch { }
            finally { _updating = false; }
            return true; // 继续执行原方法
        }
    }
}








