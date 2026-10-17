// ============================================================
// 神系回归·新世界注入（v0.18.83）
// 挂 MapBox.finishMakingWorld Postfix（新建世界完成回调，读档不触发）：
// 新世界开局从回归池随机召回登神级以上单位降临（回归即出池，席位轮换）。
// 读档（loadWorld）不注入——旧世界单位原样保留。
// ============================================================

using HarmonyLib;

namespace Code.Realm
{
    /// <summary>神系回归注入补丁：新建世界完成 → 召回回归池单位降临</summary>
    [HarmonyPatch(typeof(MapBox), "finishMakingWorld")]
    public static class PantheonInjectionPatch
    {
        /// <summary>新世界开局召回数量（随机抽取，回归即出池）</summary>
        private const int INJECT_COUNT = 2;

        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                if (World.world == null) return;
                int returned = TrueGodManager.ReturnGodToWorld(INJECT_COUNT);
                if (returned > 0)
                {
                    Code.Core.DSNotificationManager.NotifyInfo("神系回归：上代强者降临新世界（共" + returned + "位）");
                }
            }
            catch (System.Exception ex)
            {
                Code.Core.DSDebug.Warning("[GeneCultivation] 神系回归注入异常: " + ex.Message);
            }
        }
    }
}
