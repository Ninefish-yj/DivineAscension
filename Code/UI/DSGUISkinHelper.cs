// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// GUI样式池（指南铁律：OnGUI 内禁止 new GUIStyle，懒加载缓存一次）。
    /// 按 (fontSize|fontStyle|wordWrap|alignment) 分档缓存共享样式。
    /// 注意：样式不带 textColor（颜色一律用 GUI.color 设置），避免共享样式颜色污染。
    /// 仅允许在 OnGUI / Draw 绘制路径内调用（new GUIStyle 读 GUI.skin）。
    /// </summary>
    public static class DSGUIStylePool
    {
        private static readonly Dictionary<string, GUIStyle> _labelPool = new Dictionary<string, GUIStyle>();
        private static readonly Dictionary<string, GUIStyle> _buttonPool = new Dictionary<string, GUIStyle>();
        private static GUIStyle _box;
        private static bool _boxCached;

        public static GUIStyle Label(int fontSize, FontStyle fontStyle = FontStyle.Normal,
            bool wordWrap = false, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            string key = fontSize + "|" + (int)fontStyle + "|" + (wordWrap ? 1 : 0) + "|" + (int)alignment;
            GUIStyle style;
            if (!_labelPool.TryGetValue(key, out style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    fontStyle = fontStyle,
                    wordWrap = wordWrap,
                    alignment = alignment
                };
                _labelPool[key] = style;
            }
            return style;
        }

        public static GUIStyle Button(int fontSize, FontStyle fontStyle = FontStyle.Normal)
        {
            string key = fontSize + "|" + (int)fontStyle;
            GUIStyle style;
            if (!_buttonPool.TryGetValue(key, out style))
            {
                style = new GUIStyle(GUI.skin.button) { fontSize = fontSize, fontStyle = fontStyle };
                _buttonPool[key] = style;
            }
            return style;
        }

        /// <summary>box 样式（皮肤默认，不改任何属性）</summary>
        public static GUIStyle Box()
        {
            GUIStyle style;
            if (!_boxCached)
            {
                style = new GUIStyle(GUI.skin.box);
                _boxCached = true;
                _box = style;
            }
            return _box;
        }
    }

    /// <summary>
    /// GUI辅助工具类
    /// 提供统一的彩色标签、标题、分隔线等绘制方法
    /// </summary>
    public static class DSGUIHelper
    {
        /// <summary>
        /// 绘制彩色标签
        /// </summary>
        /// <param name="text">文本内容</param>
        /// <param name="color">文本颜色</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="fontStyle">字体样式</param>
        /// <param name="options">布局选项</param>
        public static void ColoredLabel(string text, Color color, int fontSize = 12, FontStyle fontStyle = FontStyle.Normal, params GUILayoutOption[] options)
        {
            Color originalColor = GUI.color;
            GUI.color = color;
            GUILayout.Label(text, DSGUIStylePool.Label(fontSize, fontStyle, true), options);
            GUI.color = originalColor;
        }

        /// <summary>
        /// 绘制标题
        /// </summary>
        /// <param name="text">标题文本</param>
        /// <param name="fontSize">字体大小</param>
        public static void Title(string text, int fontSize = 16)
        {
            Color originalColor = GUI.color;
            GUI.color = DSUITheme.TitleText; // 柔和米白
            GUILayout.Label(text, DSGUIStylePool.Label(fontSize, FontStyle.Bold, false, TextAnchor.MiddleCenter), GUILayout.ExpandWidth(true));
            GUI.color = originalColor;
        }

        /// <summary>
        /// 绘制分隔线
        /// </summary>
        public static void Separator()
        {
            Color originalColor = GUI.color;
            GUI.color = DSUITheme.Separator;
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUI.color = originalColor;
        }

    }

    /// <summary>
    /// GUI皮肤注入器
    /// 用于在using块中临时修改GUI皮肤的字体大小，自动恢复
    /// </summary>
    public class DSGUISkinInjector : System.IDisposable
    {
        private readonly int _originalFontSize;
        private readonly GUIStyle _originalLabelStyle;
        private readonly GUIStyle _originalButtonStyle;
        private bool _disposed;

        private DSGUISkinInjector(int fontSize)
        {
            _originalFontSize = GUI.skin.label.fontSize;
            _originalLabelStyle = new GUIStyle(GUI.skin.label);
            _originalButtonStyle = new GUIStyle(GUI.skin.button);

            GUI.skin.label.fontSize = fontSize;
            GUI.skin.button.fontSize = fontSize;
            if (GUI.skin.box != null) GUI.skin.box.fontSize = fontSize;
            if (GUI.skin.toggle != null) GUI.skin.toggle.fontSize = fontSize;
        }

        /// <summary>
        /// 注入字体大小
        /// </summary>
        /// <param name="fontSize">临时字体大小</param>
        /// <returns>注入器对象，用于using块自动恢复</returns>
        public static DSGUISkinInjector Inject(int fontSize)
        {
            return new DSGUISkinInjector(fontSize);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GUI.skin.label = _originalLabelStyle;
            GUI.skin.button = _originalButtonStyle;
            GUI.skin.label.fontSize = _originalFontSize;
        }
    }
}
