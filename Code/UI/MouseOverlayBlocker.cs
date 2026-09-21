
using UnityEngine;
using Code.Core;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 鼠标事件拦截器
    /// 使用全屏透明Image + GraphicRaycaster拦截鼠标事件，防止穿透到游戏
    /// 当鼠标在任何模组面板区域内时，启用raycastTarget拦截事件
    /// </summary>
    public static class MouseOverlayBlocker
    {
        private static GameObject _blockerObject;
        private static Canvas _canvas;
        private static GraphicRaycaster _raycaster;
        private static Image _blockerImage;
        private static RectTransform _rectTransform;
        private static Texture2D _whiteTex;

        private static bool _initialized = false;
        private static int _blockerRequestCount = 0; // 请求拦截的面板数量
        private static bool _frameAnyHit = false;    // 本帧是否有任一面板命中鼠标
        private static Rect _frameHitRect;           // 本帧最后命中的面板矩形
        private static int _frameMark = -1;          // 帧标记

        /// <summary>初始化拦截器（创建Overlay Canvas）</summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;

            try
            {
                // 创建1x1白色纹理
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();

                // 创建GameObject
                _blockerObject = new GameObject("DengShenMouseOverlayBlocker");
                Object.DontDestroyOnLoad(_blockerObject);

                // 添加Canvas（ScreenSpaceOverlay，最高排序）
                _canvas = _blockerObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 29999; // 比诡秘之主的29990还高，确保在最上层

                // 添加GraphicRaycaster
                _raycaster = _blockerObject.AddComponent<GraphicRaycaster>();

                // 添加透明Image（覆盖全屏）
                _blockerImage = _blockerObject.AddComponent<Image>();
                _blockerImage.sprite = Sprite.Create(_whiteTex, new Rect(0, 0, 1, 1), Vector2.zero);
                _blockerImage.color = new Color(0, 0, 0, 0f); // 完全透明
                _blockerImage.raycastTarget = false; // 默认不拦截，需要时启用

                // 设置RectTransform覆盖全屏
                _rectTransform = _blockerObject.GetComponent<RectTransform>();
                _rectTransform.anchorMin = Vector2.zero;
                _rectTransform.anchorMax = Vector2.one;
                _rectTransform.offsetMin = Vector2.zero;
                _rectTransform.offsetMax = Vector2.zero;

                _initialized = true;
                DSDebug.Verbose("[DivineAscension] MouseOverlayBlocker 初始化成功");
            }
            catch (System.Exception e)
            {
                DSDebug.Warning($"[DivineAscension] MouseOverlayBlocker 初始化失败: {e.Message}");
            }
        }

        /// <summary>请求拦截鼠标事件（面板可见时调用）</summary>
        public static void RequestBlock()
        {
            EnsureInitialized();
            if (!_initialized) return;

            _blockerRequestCount++;
            UpdateBlockerState();
        }

        /// <summary>释放拦截请求（面板隐藏时调用）</summary>
        public static void ReleaseBlock()
        {
            if (!_initialized) return;

            _blockerRequestCount = Mathf.Max(0, _blockerRequestCount - 1);
            UpdateBlockerState();
        }

        /// <summary>
        /// 设置拦截区域（只拦截指定矩形区域内的鼠标事件）
        /// 传入面板窗口的Rect（屏幕坐标）
        /// </summary>
        public static void SetBlockArea(Rect area)
        {
            EnsureInitialized();
            if (!_initialized || _rectTransform == null) return;

            // 将Image的大小和位置设置为面板区域
            // 注意：Unity UI坐标原点在左下角，而IMGUI坐标原点在左上角
            float uiY = Screen.height - area.yMax;
            _rectTransform.anchorMin = new Vector2(0, 0);
            _rectTransform.anchorMax = new Vector2(0, 0);
            _rectTransform.pivot = new Vector2(0, 0);
            _rectTransform.anchoredPosition = new Vector2(area.x, uiY);
            _rectTransform.sizeDelta = new Vector2(area.width, area.height);
        }

        /// <summary>重置为全屏拦截</summary>

        /// <summary>更新拦截器状态</summary>
        private static void UpdateBlockerState()
        {
            if (!_initialized || _blockerImage == null) return;

            // 有请求时启用拦截，否则禁用
            _blockerImage.raycastTarget = (_blockerRequestCount > 0);
        }

        /// <summary>每帧更新（在面板的OnGUI中调用，同步拦截区域）</summary>
        public static void UpdateBlockArea(Rect panelRect, bool mouseOverPanel)
        {
            EnsureInitialized();
            if (!_initialized) return;

            // 帧内命中保持：同一帧内只要任一可见面板命中鼠标，就保持拦截，
            // 避免多窗口同时打开时后绘制的面板把前一个面板的拦截关掉
            if (Time.frameCount != _frameMark)
            {
                _frameMark = Time.frameCount;
                _frameAnyHit = false;
            }
            if (mouseOverPanel && _blockerRequestCount > 0)
            {
                _frameAnyHit = true;
                _frameHitRect = panelRect;
            }

            if (_frameAnyHit)
            {
                // 鼠标在某面板区域内，设置拦截区域为命中的面板大小
                SetBlockArea(_frameHitRect);
                _blockerImage.raycastTarget = true;
            }
            else
            {
                // 本帧没有面板命中鼠标，禁用拦截
                _blockerImage.raycastTarget = false;
            }
        }

        /// <summary>强制释放所有拦截（用于面板关闭时确保鼠标恢复正常）</summary>

        /// <summary>销毁拦截器</summary>
        public static void Destroy()
        {
            if (_blockerObject != null)
            {
                try { Object.Destroy(_blockerObject); } catch { }
                _blockerObject = null;
            }
            _initialized = false;
            _blockerRequestCount = 0;
        }
    }
}
