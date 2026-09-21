// ============================================================

using UnityEngine;

namespace Code.UI
{
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
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
            style.wordWrap = true;
            GUILayout.Label(text, style, options);
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
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label(text, style, GUILayout.ExpandWidth(true));
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
