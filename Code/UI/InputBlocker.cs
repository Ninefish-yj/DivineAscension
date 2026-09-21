// ============================================================

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Code.Core;

namespace Code.UI
{
    /// <summary>
    /// 鼠标输入拦截器
    /// 使用Harmony Patch直接拦截游戏输入方法，比Input.ResetInputAxes()更精确可靠
    /// </summary>
    public static class InputBlocker
    {
        private static readonly List<Rect> _windowRects = new List<Rect>(4);
        private static bool _patched = false;
        private static int _lastFrameCount = -1;

        // 
        //  公共 API
        // 

        /// <summary>
        /// 手动清空窗口矩形列表（一般不需要调用，ReportWindowRect会自动按帧清空）。
        /// 保留此方法是为了兼容旧代码。
        /// </summary>
        public static void EndFrame()
        {
            _windowRects.Clear();
            _lastFrameCount = Time.frameCount;
        }

        /// <summary>
        /// 上报本帧窗口矩形（GUI.Window 的实际屏幕位置）。
        /// 所有窗口都应调用此方法。
        /// </summary>
        public static void ReportWindowRect(Rect windowRect)
        {
            // 新的一帧开始时自动清空上一帧的窗口矩形
            // 注意：不能在LateUpdate中清空，因为PlayerControl的方法在Update中执行，
            //       需要使用上一帧的窗口位置来判断当前帧的鼠标是否在窗口上
            if (Time.frameCount != _lastFrameCount)
            {
                _windowRects.Clear();
                _lastFrameCount = Time.frameCount;
            }
            if (windowRect.width > 0)
                _windowRects.Add(windowRect);
        }

        /// <summary>
        /// 实时检测鼠标是否位于任何模组窗口内。
        /// Input.mousePosition（左下角原点） IMGUI 坐标（左上角原点）。
        /// 关键修复：使用上一帧的窗口矩形来判断当前帧的鼠标是否在窗口上，
        ///           因为Update在OnGUI之前执行，OnGUI中ReportWindowRect更新的是当前帧的窗口位置。
        ///           如果超过1帧没有调用ReportWindowRect，说明所有窗口都关闭了，清空列表。
        /// </summary>
        public static bool IsMouseOverModWindow()
        {
            // 关键修复：如果超过1帧没有调用ReportWindowRect，说明所有窗口都关闭了
            // 注意：不能在每一帧都清空，因为Update在OnGUI之前执行，
            //       需要使用上一帧的窗口位置来判断当前帧的鼠标是否在窗口上
            if (Time.frameCount - _lastFrameCount > 1 && _windowRects.Count > 0)
            {
                _windowRects.Clear();
            }

            if (_windowRects.Count == 0) return false;

            Vector3 mousePos = Input.mousePosition;
            float guiY = Screen.height - mousePos.y;
            Vector2 guiMouse = new Vector2(mousePos.x, guiY);

            for (int i = 0; i < _windowRects.Count; i++)
            {
                if (_windowRects[i].Contains(guiMouse))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 激活Harmony Patch（只调用一次）。
        /// </summary>
        public static void Patch()
        {
            if (_patched) return;

            try
            {
                var harmony = new Harmony("DengShenChangJie.InputBlocker");

                // 拦截点击选中（PlayerControl.checkClickTouchInspectSelect）
                PatchExplicit(harmony,
                    typeof(PlayerControl), "checkClickTouchInspectSelect",
                    nameof(checkClickTouchInspectSelect_Prefix), isPostfix: false);

                // 拦截相机缩放（MoveCamera）
                PatchExplicit(harmony,
                    typeof(MoveCamera), "zoomIn",
                    nameof(MoveCamera_zoomIn_Prefix), isPostfix: false);
                PatchExplicit(harmony,
                    typeof(MoveCamera), "zoomOut",
                    nameof(MoveCamera_zoomOut_Prefix), isPostfix: false);
                PatchExplicit(harmony,
                    typeof(MoveCamera), "zoomInWheel",
                    nameof(MoveCamera_zoomInWheel_Prefix), isPostfix: false);
                PatchExplicit(harmony,
                    typeof(MoveCamera), "zoomOutWheel",
                    nameof(MoveCamera_zoomOutWheel_Prefix), isPostfix: false);

                // 拦截地图拖动镜头（窗口内按住拖动不再平移地图）
                PatchExplicit(harmony,
                    typeof(MoveCamera), "updateMouseCameraDrag",
                    nameof(MoveCamera_updateMouseCameraDrag_Prefix), isPostfix: false);

                // 拦截右键检视（窗口内右键不再打开单位/建筑检视）
                PatchExplicit(harmony,
                    typeof(PlayerControl), "canInspectWithRightClick",
                    nameof(PlayerControl_canInspectWithRightClick_Prefix), isPostfix: false);

                _patched = true;
                DSDebug.Verbose("[DivineAscension] InputBlocker 已激活（click拦截 + MoveCamera滚轮缩放拦截）");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"[DivineAscension] InputBlocker Patch失败: {e.Message}");
            }
        }

        /// <summary>
        /// 显式注册 Harmony 补丁：手动指定原始方法（类型+方法名）与补丁方法。
        /// 单个失败不影响其他（隔离注册）。
        /// </summary>
        private static void PatchExplicit(Harmony harmony, System.Type targetType, string originalMethodName,
            string patchMethodName, bool isPostfix)
        {
            try
            {
                var original = AccessTools.Method(targetType, originalMethodName);
                if (original == null)
                {
                    DSDebug.Warning($"[DivineAscension] InputBlocker: 找不到原始方法 {targetType.Name}.{originalMethodName}");
                    return;
                }
                var patchMethod = typeof(InputBlocker).GetMethod(patchMethodName,
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (patchMethod == null)
                {
                    DSDebug.Warning($"[DivineAscension] InputBlocker: 找不到补丁方法 {patchMethodName}");
                    return;
                }
                if (isPostfix)
                    harmony.Patch(original, postfix: new HarmonyMethod(patchMethod));
                else
                    harmony.Patch(original, prefix: new HarmonyMethod(patchMethod));
            }
            catch (System.Exception ex)
            {
                DSDebug.Warning($"[DivineAscension] InputBlocker: {targetType.Name}.{originalMethodName} 注册失败 ({ex.GetType().Name}: {ex.Message})，跳过");
            }
        }

        // 
        //  Harmony Patch 方法
        // 

        /// <summary>
        /// 拦截点击选中（PlayerControl.checkClickTouchInspectSelect）
        /// Prefix返回false = 不选中任何东西
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerControl), "checkClickTouchInspectSelect")]
        private static bool checkClickTouchInspectSelect_Prefix()
        {
            return !IsMouseOverModWindow();
        }

        /// <summary>
        /// 拦截相机缩放（MoveCamera）
        /// Prefix返回false = 不缩放
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MoveCamera), "zoomIn")]
        private static bool MoveCamera_zoomIn_Prefix() => !IsMouseOverModWindow();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MoveCamera), "zoomOut")]
        private static bool MoveCamera_zoomOut_Prefix() => !IsMouseOverModWindow();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MoveCamera), "zoomInWheel")]
        private static bool MoveCamera_zoomInWheel_Prefix() => !IsMouseOverModWindow();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MoveCamera), "zoomOutWheel")]
        private static bool MoveCamera_zoomOutWheel_Prefix() => !IsMouseOverModWindow();

        /// <summary>
        /// 拦截地图拖动镜头（MoveCamera.updateMouseCameraDrag）
        /// Prefix返回false = 不执行镜头拖动
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MoveCamera), "updateMouseCameraDrag")]
        private static bool MoveCamera_updateMouseCameraDrag_Prefix() => !IsMouseOverModWindow();

        /// <summary>
        /// 拦截右键检视（PlayerControl.canInspectWithRightClick）
        /// Prefix返回false = 不允许检视
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerControl), "canInspectWithRightClick")]
        private static bool PlayerControl_canInspectWithRightClick_Prefix() => !IsMouseOverModWindow();
    }
}


